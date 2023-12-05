using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using static Arenas.TypeHandle;
using static Arenas.TypeInfo;

namespace Arenas {
    public delegate IntPtr AllocMemoryDelegate(int sizeBytes);
    public delegate void FreeMemoryDelegate(IntPtr ptr);

    // NOTE: all items allocated to the arena will be aligned to a 64-bit word boundary and their size will be a multiple of 64-bits
    public unsafe class Arena : IDisposable, IEnumerable<UnmanagedRef> {
        public const int PageSize = 4096;

        private Dictionary<object, ObjectEntry> objToPtr;
        private bool disposedValue;
        private List<Page> pages;
        private Dictionary<int, Freelist> freelists;
        private ArenaID id;
        private int enumVersion;
        private bool initialized;

        public Arena() {
            objToPtr = new Dictionary<object, ObjectEntry>(ObjectReferenceEqualityComparer.Instance);
            pages = new List<Page>();
            freelists = new Dictionary<int, Freelist>();

            AllocMemory = DefaultAllocMemory;
            FreeMemory = DefaultFreeMemory;

            // call clear to set up everything we need for use
            Clear(false);
        }

        public UnmanagedRef<T> UnmanagedRefFromPtr<T>(T* ptr) where T : unmanaged {
            return UnmanagedRefFromPtr<T>((IntPtr)ptr);
        }

        public UnmanagedRef<T> UnmanagedRefFromPtr<T>(IntPtr ptr) where T : unmanaged {
            if (ptr == IntPtr.Zero) {
                throw new ArgumentNullException(nameof(ptr));
            }

            var header = ItemHeader.GetHeader(ptr);

            Type type;
            if (!TryGetTypeFromHandle(header.TypeHandle, out type)) {
                throw new InvalidOperationException("Type mismatch in header for pointer in UnmanagedRefFromPtr<T>(IntPtr), address may be invalid.");
            }
            if (type != typeof(T)) {
                throw new InvalidOperationException($"Type mismatch in header for pointer in UnmanagedRefFromPtr<T>(IntPtr), types do not match: type {typeof(T)} expected, but item was of type {type}.");
            }

            var version = header.Version.SetArenaID(id);
            if (!version.Valid) {
                throw new InvalidOperationException("Pointer in UnmanagedRefFromPtr<T>(IntPtr) did not point to a valid item.");
            }

            return new UnmanagedRef<T>((T*)ptr, version, header.Size / sizeof(T));
        }

        public UnmanagedRef UnmanagedRefFromPtr(IntPtr ptr) {
            if (ptr == IntPtr.Zero) {
                throw new ArgumentNullException(nameof(ptr));
            }

            var header = ItemHeader.GetHeader(ptr);

            TypeInfo info;
            if (!TryGetTypeInfo(header.TypeHandle, out info)) {
                throw new InvalidOperationException("Invalid type in header for pointer in UnmanagedRefFromPtr(IntPtr), address may be invalid.");
            }

            var version = header.Version.SetArenaID(id);
            if (!version.Valid) {
                throw new InvalidOperationException("Pointer in UnmanagedRefFromPtr(IntPtr) did not point to a valid item.");
            }

            return new UnmanagedRef(ptr, version, header.Size / info.Size);
        }

        private Page AllocPage(int size) {
            // add one 64-bit word in size to make sure this can always be 64-bit aligned
            // while keeping the original size (unused space is available if already aligned)
            size += sizeof(ulong);
            size = Page.AlignCeil(size, PageSize);

            // allocate pointer, potentially unaligned to 64-bit word
            var mem = (AllocMemory ?? DefaultAllocMemory)(size);
            ZeroMemory(mem, (UIntPtr)size);

            // store the original pointer for freeing, and find 64-bit word align position
            var freePtr = mem;
            var aligned = Page.AlignCeil(mem, sizeof(ulong));

            // page memory must be 64-bit word aligned to keep 64-bit word alignment for all items
            // allocated to the arena
            if (mem != aligned) {
                // memory is misaligned, realign and reduce size accordingly
                var sizeDifference = (int)((ulong)aligned - (ulong)mem);
                mem = aligned;
                size -= sizeDifference;
            }

            // create page
            var page = new Page(freePtr, mem, size);
            pages.Add(page);

            Debug.Assert(((ulong)page.Address % sizeof(ulong)) == 0);
            return page;
        }

        private IntPtr Allocate(Type type, int elementSize, ulong sizeBytes, out RefVersion version) {
            IntPtr ptr;

            Debug.Assert(sizeBytes >= sizeof(ulong));
            Debug.Assert((sizeBytes % sizeof(ulong)) == 0);
            Debug.Assert(sizeBytes <= int.MaxValue);

            var iSizeBytes = (int)sizeBytes;
            var elementCount = iSizeBytes / elementSize;

            // check if there is a freelist for this type and attempt to get an item from it
            Freelist freelist;
            if (!freelists.TryGetValue(iSizeBytes, out freelist) || (ptr = freelist.Pop()) == IntPtr.Zero) {
                // failed to get an item from freelist so push a new item onto the arena
                ptr = Push(iSizeBytes + itemHeaderSize) + itemHeaderSize;

                // increment item version by 1 and set header
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = prevVersion.IncrementItemVersion(true, elementCount).SetArenaID(id);
                ItemHeader.SetHeader(ptr, new ItemHeader(GetTypeHandle(type), iSizeBytes, IntPtr.Zero, version)); // set header
            }
            else {
                freelists[iSizeBytes] = freelist;

                // increment item version by 1
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = prevVersion.IncrementItemVersion(true, elementCount).SetArenaID(id);
                ItemHeader.SetVersion(ptr, version);
                ItemHeader.SetTypeHandle(ptr, GetTypeHandle(type));
            }

            return ptr;
        }

        public UnmanagedRef<T> Allocate<T>(T item) where T : unmanaged {
            var info = GenerateTypeInfo<T>();
            var items = AllocCount<T>(1);
            items.Value[0] = item;
            info.TrySetArenaID((IntPtr)items.Value, id);
            return items;
        }

        public UnmanagedRef<T> Allocate<T>(ref T item) where T : unmanaged {
            var info = GenerateTypeInfo<T>();
            var items = AllocCount<T>(1);
            items.Value[0] = item;
            info.TrySetArenaID((IntPtr)items.Value, id);
            return items;
        }

        public UnmanagedRef<T> AllocCount<T>(int count) where T : unmanaged {
            if (count <= 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var info = GenerateTypeInfo<T>();
            var items = _AllocValues<T>(count);

            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                var cur = items.Value;
                var elementCount = items.ElementCount;

                for (int i = 0; i < elementCount; i++, cur++) {
                    // set arena ID
                    info.TrySetArenaID((IntPtr)cur, id);
                }
            }

            return items;
        }

        public UnmanagedRef<T> _AllocValues<T>(int count) where T : unmanaged {
            enumVersion++;

            Type type = typeof(T);
            int elementSize = sizeof(T);

            ulong sizeBytes = (ulong)elementSize * (ulong)count;

            // make sure size in bytes is at least one 64-bit word, is 64-bit word
            // aligned, is a power of 2 for multiple elements and doesn't overflow
            // item size must be 64-bit word aligned to keep 64-bit word alignment
            // for all items allocated to the arena
            if (sizeBytes < sizeof(ulong)) {
                sizeBytes = sizeof(ulong);
            }

            sizeBytes = Page.AlignCeil(sizeBytes, sizeof(ulong));

            if (count > 1) {
                sizeBytes = NextPowerOfTwo(sizeBytes);
            }

            if (sizeBytes > int.MaxValue - sizeof(ulong)) {
                throw new InvalidOperationException("Arena can't allocate size that large");
            }

            Debug.Assert((sizeBytes % sizeof(ulong)) == 0);

            // allocate items and zero memory
            RefVersion version;
            var ptr = Allocate(type, sizeof(T), sizeBytes, out version);
            ZeroMemory(ptr, (UIntPtr)sizeBytes);

            Debug.Assert(((ulong)ptr % sizeof(ulong)) == 0);

            // get actual allocated item count (can be bigger than requested)
            count = (int)sizeBytes / sizeof(T);

            // return pointer as an UnmanagedRef
            return new UnmanagedRef<T>((T*)ptr, version, count);
        }

        private IntPtr Push(int size) {
            IntPtr ptr;
            var page = pages.Last();

            Debug.Assert((size % sizeof(ulong)) == 0);

            // try to claim size bytes in current page, will return null if out of space
            if ((ptr = page.Push(size)) == IntPtr.Zero) {
                // out of space, allocate new page, rounding size up to nearest multiple of PageSize
                page = AllocPage(size);

                // claim size bytes in current page
                // this will always work because we just made sure the new page fits the requested size
                ptr = page.Push(size);
                Debug.Assert(ptr != IntPtr.Zero);
            }

            pages.SetLast(page);

            Debug.Assert(((ulong)ptr % sizeof(ulong)) == 0);
            return ptr;
        }

        public void Free(IntPtr ptr) {
            var uref = UnmanagedRefFromPtr(ptr);
            Free(uref);
        }

        public void Free<T>(in T items) where T : struct, IUnmanagedRef {
            Free(items.Reference);
        }

        public void Free(in UnmanagedRef items) {
            IntPtr cur;
            if (!items.TryGetValue(out cur)) {
                // can't free that ya silly bugger
                return;
            }

            enumVersion++;

            var type = items.Type;
            var info = GetTypeInfo(type);
            var elementSize = info.Size;

            if (typeof(IArenaContents).IsAssignableFrom(type)) {
                var elementCount = items.ElementCount;
                for (int i = 0; i < elementCount; i++) {
                    // free contents
                    info.TryFree(cur);
                    cur += elementSize;
                }
            }

            _FreeValues(items.Value);
        }

        public void Free<T>(in UnmanagedRef<T> items) where T : unmanaged {
            T* cur;
            if (!items.TryGetValue(out cur)) {
                // can't free that ya silly bugger
                return;
            }

            enumVersion++;
            var info = GetTypeInfo(typeof(T));

            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                var elementCount = items.ElementCount;
                for (int i = 0; i < elementCount; i++, cur++) {
                    // free contents
                    info.TryFree((IntPtr)cur);
                }
            }

            _FreeValues((IntPtr)items.Value);
        }

        private void _FreeValues(IntPtr itemPtr) {
            var sizeBytes = ItemHeader.GetSize(itemPtr);

            // set version to indicate item is not valid
            ItemHeader.Invalidate(itemPtr);

            var page = pages.Last();
            if (page.IsTop(itemPtr + sizeBytes)) {
                // if the item as at the top of the current page then simply pop it off
                page.Pop(sizeBytes + itemHeaderSize);
                pages.SetLast(page);
            }
            else {
                // otherwise ensure a freelist for the type exists and push the item's location onto it
                // for reuse
                Freelist freelist;
                if (!freelists.TryGetValue(sizeBytes, out freelist)) {
                    freelist = new Freelist();
                }

                freelist.Push(itemPtr);
                freelists[sizeBytes] = freelist;
            }
        }

        internal IntPtr SetOutsidePtr<T>(T value, IntPtr currentHandlePtr) where T : class {
            if (!(value is object) && currentHandlePtr == IntPtr.Zero) {
                // both null, do nothing
                return IntPtr.Zero;
            }

            var managedEntry = default(ObjectEntry);

            if (value is object) {
                // value is not null. get object handle, or create one if none exist
                if (!objToPtr.TryGetValue(value, out managedEntry)) {
                    // allocate object handle
                    managedEntry.Handle = GCHandle.Alloc(value, GCHandleType.Weak);

                    // add handle to lookup tables
                    objToPtr[value] = managedEntry;
                }
            }

            if (managedEntry.Handle.IsAllocated) {
                var managedHandlePtr = GCHandle.ToIntPtr(managedEntry.Handle);
                if (managedHandlePtr == currentHandlePtr) {
                    // same value, do nothing
                    return managedHandlePtr;
                }
            }

            if (currentHandlePtr != IntPtr.Zero) {
                var currentHandle = GCHandle.FromIntPtr(currentHandlePtr);
                var currentTarget = currentHandle.Target;
                Debug.Assert(!(currentTarget is null));

                ObjectEntry currentManagedEntry;
                if (!objToPtr.TryGetValue(currentTarget, out currentManagedEntry)) {
                    throw new InvalidOperationException("Object handle is allocated but not in lookup tables");
                }

                // current value of field being set isn't null so decrease refcount and clean up if needed
                currentManagedEntry.RefCount--;

                // can clean up here because we've already established the value isn't the same on both sides
                if (currentManagedEntry.RefCount <= 0) {
                    // free object handle and remove from lookup tables so .NET's tracing GC can (theoretically)
                    // collect the object being referenced now that no references to it from within this arena exist
                    currentHandle.Free();
                    objToPtr.Remove(currentTarget);
                }
                else {
                    objToPtr[currentTarget] = currentManagedEntry; // update entry in lookup tables
                }
            }

            if (managedEntry.Handle.IsAllocated) {
                // increase object handle reference count
                managedEntry.RefCount++;
                objToPtr[value] = managedEntry; // update entry in lookup tables

                // return new object handle
                return GCHandle.ToIntPtr(managedEntry.Handle);
            }

            return IntPtr.Zero;
        }

        public bool VersionsMatch(RefVersion version, IntPtr item) {
            return version.Arena == id && version == ItemHeader.GetVersion(item);
        }

        public void Clear() {
            Clear(false);
        }

        private void Clear(bool disposing, bool fromFinalizer = false) {
            enumVersion++;

            // free GCHandles
            foreach (var entry in objToPtr.Values) {
                var gcHandle = entry.Handle;
                if (gcHandle.IsAllocated) {
                    gcHandle.Free();
                }
            }

            // free page memory
            var freeMem = FreeMemory ?? DefaultFreeMemory;
            foreach (var page in pages) {
                page.Free(freeMem);
            }

            pages.Clear();
            freelists.Clear();
            objToPtr.Clear();

            if (disposing) {
                Remove(id, fromFinalizer);
                id = ArenaID.Empty;
            }
            else {
                if (!initialized) {
                    // get an ID and ID->Arena mapping entry
                    initialized = true;
                    Add(this);
                }
                else {
                    // get a new ID to invalidate any stale references
                    ChangeID(this);
                }

                // allocate one page to start
                AllocPage(PageSize - sizeof(ulong));
            }
        }

        #region IEnumerable
        public Enumerator GetEnumerator() {
            return new Enumerator(this);
        }

        IEnumerator<UnmanagedRef> IEnumerable<UnmanagedRef>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        #endregion

        #region IDisposable
        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // dispose managed state (managed objects)
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                Clear(true, !disposing);

                pages = null;
                freelists = null;
                objToPtr = null;

                disposedValue = true;
            }
        }

        ~Arena() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose() {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

        public bool IsDisposed { get { return disposedValue; } }
        public ArenaID ID { get { return id; } }
        public AllocMemoryDelegate AllocMemory { get; set; }
        public FreeMemoryDelegate FreeMemory { get; set; }

        #region Static
        private const int MaxFinalizedRemovalsPerAdd = 8;

        [DllImport("kernel32.dll")]
        private static extern void RtlZeroMemory(IntPtr dst, UIntPtr length);
        private delegate void ZeroMemoryDelegate(IntPtr dst, UIntPtr length);

        private static ConcurrentQueue<ArenaID> finalizedRemovals;
        private static Dictionary<ArenaID, WeakReference<Arena>> arenas;
        private static object arenasLock;
        private static ZeroMemoryDelegate ZeroMemory;
        private static readonly int itemHeaderSize;

        public static AllocMemoryDelegate DefaultAllocMemory { get; set; }
        public static FreeMemoryDelegate DefaultFreeMemory { get; set; }

        static Arena() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                ZeroMemory = RtlZeroMemory;
            }
            else {
                ZeroMemory = ZeroMemPlatformIndependent;
            }
            
            // item header size must be 64-bit word aligned to keep 64-bit word alignment for all items
            // allocated to the arena
            itemHeaderSize = Page.AlignCeil(sizeof(ItemHeader), sizeof(ulong));
            Debug.Assert((itemHeaderSize % sizeof(ulong)) == 0);

            finalizedRemovals = new ConcurrentQueue<ArenaID>();
            arenas = new Dictionary<ArenaID, WeakReference<Arena>>();
            arenasLock = new object();

            DefaultAllocMemory = Marshal.AllocHGlobal;
            DefaultFreeMemory = Marshal.FreeHGlobal;
        }

        private static void Add(Arena arena) {
            Add(arena, false);
        }

        private static void Add(Arena arena, bool hasPreviousEntry) {
            bool doRemovals = true;

            while (true) {
                var id = ArenaID.NewID();
                Debug.Assert(id.Value != 0);

                lock (arenasLock) {
                    if (doRemovals && !finalizedRemovals.IsEmpty) {
                        doRemovals = false;

                        // if there are pending removals via finalizer, remove them now
                        // but only remove a limited number as to not block for too long
                        for (int i = 0; i < MaxFinalizedRemovalsPerAdd; i++) {
                            ArenaID removeID;
                            if (finalizedRemovals.TryDequeue(out removeID)) {
                                arenas.Remove(removeID);
                            }
                            else {
                                break;
                            }
                        }
                    }

                    if (arenas.ContainsKey(id)) {
                        continue;
                    }

                    if (hasPreviousEntry) {
                        var removed = arenas.Remove(arena.id);
                        Debug.Assert(removed);
                    }
                    else {
                        Debug.Assert(!arenas.ContainsKey(arena.id));
                    }

                    arenas[id] = new WeakReference<Arena>(arena);
                    arena.id = id;
                    break;
                }
            }
        }

        private static void ChangeID(Arena arena) {
            Add(arena, true);
        }

        private static void Remove(ArenaID id, bool fromFinalizer) {
            if (fromFinalizer) {
                // don't lock in code called from finalizer, instead
                // add to removals queue which is emptied during Add
                finalizedRemovals.Enqueue(id);
            }
            else {
                // removal from Dispose (or Clear) method, remove now
                lock (arenasLock) {
                    arenas.Remove(id);
                }
            }
        }

        public static Arena Get(ArenaID id) {
            if (id.Value == 0) {
                return null;
            }

            WeakReference<Arena> aref;
            lock (arenasLock) {
                if (!arenas.TryGetValue(id, out aref)) {
                    return null;
                }

                Arena arena;
                if (!aref.TryGetTarget(out arena)) {
                    return null;
                }

                return arena;
            }
        }

        // http://graphics.stanford.edu/%7Eseander/bithacks.html#RoundUpPowerOf2
        private static ulong NextPowerOfTwo(ulong v) {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            v++;
            return v;
        }

        private static void ZeroMemPlatformIndependent(IntPtr ptr, UIntPtr length) {
            ulong size = (ulong)length;

            // clear to word alignment
            var byteptr = (byte*)ptr;
            var bytes = (int)((ulong)byteptr & 0b111);
            for (int i = 0; i < bytes; i++, byteptr++) {
                *byteptr = 0;
            }

            size -= (ulong)bytes;

            // clear words
            var count = size / sizeof(ulong);
            var longptr = (ulong*)byteptr;

            for (ulong i = 0; i < count; i++, longptr++) {
                *longptr = 0;
            }

            size -= count * sizeof(ulong);

            // clear remaining bytes
            byteptr = (byte*)longptr;
            bytes = (int)size;
            for (int i = 0; i < bytes; i++, byteptr++) {
                *byteptr = 0;
            }
        }
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct ObjectEntry {
            public GCHandle Handle;
            public int RefCount;

            public override string ToString() {
                return $"ObjectEntry(Handle=0x{GCHandle.ToIntPtr(Handle).ToInt64():x}, RefCount={RefCount})";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Page {
            public IntPtr Address;
            /// <summary>
            /// Size in bytes
            /// </summary>
            public int Size;
            /// <summary>
            /// Offset in bytes
            /// </summary>
            public int Offset;

            private IntPtr freePtr;

            public Page(IntPtr freePtr, IntPtr address, int size) {
                this.freePtr = freePtr;
                Address = address;
                Size = size;
                Offset = 0;
            }

            public void Free(FreeMemoryDelegate freeMem) {
                if (freePtr == IntPtr.Zero) {
                    return;
                }
                freeMem(freePtr);
                freePtr = IntPtr.Zero;
            }

            public IntPtr Push(int size) {
                var end = (long)Offset + size;
                if (end > Size) {
                    return IntPtr.Zero;
                }
                var ret = Address + Offset;
                Offset += size;
                return ret;
            }

            public void Pop(int size) {
                Debug.Assert(Offset - size >= 0, "Bad pop size");
                Offset -= size;
            }

            public bool IsTop(IntPtr ptr) {
                return ptr == Address + Offset;
            }

            public override string ToString() {
                return $"Page(Address=0x{Address.ToInt64():x}, Size={Size}, Offset={Offset})";
            }

            public static int AlignFloor(int addr, int size) {
                return addr & (~(size - 1));
            }

            public static int AlignCeil(int addr, int size) {
                return (addr + (size - 1)) & (~(size - 1));
            }

            public static IntPtr AlignFloor(IntPtr addr, int size) {
                return (IntPtr)AlignFloor((ulong)addr, size);
            }

            public static IntPtr AlignCeil(IntPtr addr, int size) {
                return (IntPtr)AlignCeil((ulong)addr, size);
            }

            public static ulong AlignFloor(ulong addr, int size) {
                var sizel = (ulong)size;
                return addr & (~(sizel - 1));
            }

            public static ulong AlignCeil(ulong addr, int size) {
                var sizel = (ulong)size;
                return (addr + (sizel - 1)) & (~(sizel - 1));
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Freelist {
            public IntPtr Head;

            public IntPtr Pop() {
                if (Head == IntPtr.Zero) {
                    return IntPtr.Zero;
                }

                var item = Head;
                Head = ItemHeader.GetNextFree(Head);

                return item;
            }

            public void Push(IntPtr item) {
                var next = Head;
                Head = item;
                ItemHeader.SetNextFree(Head, next);
            }

            public override string ToString() {
                return $"Freelist(Head=0x{Head.ToInt64():x})";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct ItemHeader {
            public TypeHandle TypeHandle;
            public int Size;
            public IntPtr NextFree;
            public RefVersion Version;

            public ItemHeader(TypeHandle typeHandle, int size, IntPtr next, RefVersion version) {
                TypeHandle = typeHandle;
                Size = size;
                NextFree = next;
                Version = version;
            }

            public override string ToString() {
                return $"ItemHeader(Type={GetTypeFromHandle(TypeHandle).FullName}, Size={Size}, Next=0x{NextFree.ToInt64():x}, Version=({Version}))";
            }

            // helper functions for manipulating an item header, which is always located
            // in memory right before where the item is allocated
            public static void SetVersion(IntPtr item, RefVersion version) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Version = version;
            }

            public static RefVersion GetVersion(IntPtr item) {
                if (item == IntPtr.Zero) {
                    return default(RefVersion).Invalidate();
                }
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->Version;
            }

            public static void SetSize(IntPtr item, int size) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Size = size;
            }

            public static int GetSize(IntPtr item) {
                if (item == IntPtr.Zero) {
                    return 0;
                }
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->Size;
            }

            public static void SetNextFree(IntPtr item, IntPtr next) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->NextFree = next;
            }

            public static IntPtr GetNextFree(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->NextFree;
            }

            public static void SetTypeHandle(IntPtr item, TypeHandle handle) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->TypeHandle = handle;
            }

            public static TypeHandle GetTypeHandle(IntPtr item) {
                if (item == IntPtr.Zero) {
                    return new TypeHandle(0);
                }
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->TypeHandle;
            }

            public static void SetHeader(IntPtr item, ItemHeader itemHeader) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                *header = itemHeader;
            }

            public static ItemHeader GetHeader(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return *header;
            }

            public static void Invalidate(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Version = header->Version.Invalidate();
            }

            public static ArenaID GetArenaID(IntPtr item) {
                if (item == IntPtr.Zero) {
                    return ArenaID.Empty;
                }
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->Version.Arena;
            }
        }

        [Serializable]
        public struct Enumerator : IEnumerator<UnmanagedRef>, IEnumerator {
            private Arena arena;
            private int pageIndex;
            private int offset;
            private int version;
            private UnmanagedRef current;

            internal Enumerator(Arena arena) {
                this.arena = arena;
                pageIndex = 0;
                offset = 0;
                version = arena.enumVersion;
                current = default(UnmanagedRef);
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                Arena localArena = arena;

                if (version == localArena.enumVersion && pageIndex < localArena.pages.Count) {
                    while (pageIndex < localArena.pages.Count) {
                        Page curPage = localArena.pages[pageIndex];

                        if (offset >= curPage.Offset) {
                            pageIndex++;
                            offset = 0;
                            continue;
                        }

                        var ptr = curPage.Address + offset + itemHeaderSize;
                        var header = ItemHeader.GetHeader(ptr);
                        offset += header.Size + itemHeaderSize;

                        Type type;
                        if (!TryGetTypeFromHandle(header.TypeHandle, out type) || header.Size < 0 || offset < 0 || offset > curPage.Offset) {
                            throw new InvalidOperationException("Enumeration encountered an error; arena memory may be corrupted");
                        }

                        if (header.Version.Valid) {
                            current = localArena.UnmanagedRefFromPtr(ptr);
                            return true;
                        }
                    }
                }

                return MoveNextRare();
            }

            private bool MoveNextRare() {
                if (version != arena.enumVersion) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                pageIndex = arena.pages.Count + 1;
                offset = 0;
                current = default(UnmanagedRef);
                return false;
            }

            public UnmanagedRef Current {
                get {
                    return current;
                }
            }

            object IEnumerator.Current {
                get {
                    if ((pageIndex == 0 && offset == 0) || pageIndex == arena.pages.Count + 1) {
                        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    }
                    return Current;
                }
            }

            void IEnumerator.Reset() {
                if (version != arena.enumVersion) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                pageIndex = 0;
                offset = 0;
                current = default(UnmanagedRef);
            }
        }

        private class ObjectReferenceEqualityComparer : IEqualityComparer<object> {
            public static readonly ObjectReferenceEqualityComparer Instance = new ObjectReferenceEqualityComparer();

            public new bool Equals(object x, object y) {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(object obj) {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
