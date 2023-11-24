using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    unsafe public class Arena : IDisposable, IEnumerable<ArenaEntry> {
        public const int PageSize = 4096;

        private Dictionary<object, IntPtr> objToPtr;
        private Dictionary<IntPtr, object> ptrToObj;
        private bool disposedValue;
        private List<IntPtr> pages;
        private Dictionary<Type, IntPtr> freelists;
        private Guid ID;
        private int enumVersion;

        public Arena() {
            objToPtr = new Dictionary<object, IntPtr>();
            ptrToObj = new Dictionary<IntPtr, object>();
            pages = new List<IntPtr>();
            freelists = new Dictionary<Type, IntPtr>();

            // call clear to set up everything we need for use
            Clear(false);
        }

        private void AllocPage(int size) {
            // create memory and clear
            Debug.Assert(size == Page.AlignCeil(size, PageSize), "Non page-aligned size in AllocPage");
            var mem = Marshal.AllocHGlobal(size);
            
            var cur = (ulong*)mem;
            var count = size / sizeof(ulong);

            for (int i = 0; i < count; i++, cur++) {
                *cur = 0;
            }

            // create page instance and clear, then add to list of pages
            var ptr = Marshal.AllocHGlobal(sizeof(Page));
            *(Page*)ptr = new Page(mem, size);
            pages.Add(ptr);
        }

        // convenience function cause .NET doesn't let you put pointers in dictionaries
        private Freelist* GetFreelist(Type type) {
            IntPtr ptr;
            if (!freelists.TryGetValue(type, out ptr)) {
                return null;
            }
            return (Freelist*)ptr;
        }

        public UnmanagedRef<T> Allocate<T>(T item) where T : unmanaged, IArenaContents {
            IntPtr ptr;
            RefVersion version;

            enumVersion++;

            // check if there is a freelist for this type and attempt to get an item from it
            Freelist* freelist;
            if ((freelist = GetFreelist(typeof(T))) == null || (ptr = freelist->Pop()) == IntPtr.Zero) {
                // failed to get an item from freelist so push a new item onto the arena
                ptr = Push(sizeof(T) + sizeof(ItemHeader)) + sizeof(ItemHeader);

                // increment item version by 1 and set header
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = new RefVersion(prevVersion.Item + 1, Version);
                ItemHeader.SetHeader(ptr, new ItemHeader(GetTypeHandle(typeof(T)), sizeof(T), IntPtr.Zero, version)); // set header
            }
            else {
                // increment item version by 1
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = new RefVersion(prevVersion.Item + 1, Version);
                ItemHeader.SetVersion(ptr, version);
            }

            // set item's arena reference and assign to pointer to copy into our item's memory
            item.ArenaID = ID;
            *(T*)ptr = item;

            // return pointer as an UnmanagedRef
            return new UnmanagedRef<T>((T*)ptr, this, version);
        }

        private IntPtr Push(int size) {
            IntPtr ptr;
            var page = currentPage;

            // try to claim size bytes in current page, will return null if out of space
            if ((ptr = page->Push(size)) == IntPtr.Zero) {
                // out of space, allocate new page, rounding size up to nearest multiple of PageSize
                AllocPage(Page.AlignCeil(size, PageSize));
                page = currentPage;

                // claim size bytes in current page
                // this will always work because we just made sure the new page fits the requested size
                ptr = page->Push(size);
            }

            return ptr;
        }

        internal void Free<T>(UnmanagedRef<T> item) where T : unmanaged, IArenaContents {
            if (!item.HasValue) {
                // can't free null ya silly bugger
                return;
            }

            enumVersion++;
            var itemPtr = (IntPtr)item.Value;

            // tell the item to free anything it needs to
            // usually this means setting ManagedRefs to null but it could also
            // have to free other unmanaged allocations
            item.Value->Free();

            // set version to indicate item is not valid
            ItemHeader.Invalidate(itemPtr);

            var page = currentPage;
            if (page->IsTop(itemPtr + sizeof(T))) {
                // if the item as at the top of the current page then simply pop it off
                page->Pop(sizeof(T) + sizeof(ItemHeader));
            }
            else {
                // otherwise ensure a freelist for the type exists and push the item's location onto it
                // for reuse
                Freelist* freelist;
                if ((freelist = GetFreelist(typeof(T))) == null) {
                    var freelistPtr = Marshal.AllocHGlobal(sizeof(Freelist));
                    freelist = (Freelist*)freelistPtr;
                    *freelist = new Freelist();
                    freelists[typeof(T)] = freelistPtr;
                }

                freelist->Push(itemPtr);
            }
        }

        public IntPtr SetOutsidePtr<T>(T value, IntPtr currentValue) where T : class {
            if (!(value is object) && currentValue == IntPtr.Zero) {
                // both null, do nothing
                return IntPtr.Zero;
            }

            IntPtr managedPtrBase = IntPtr.Zero;

            if (value is object) {
                // value is not null. get object handle, or create one if none exist
                if (!objToPtr.TryGetValue(value, out managedPtrBase)) {
                    // heap allocate object handle and clear to zero
                    managedPtrBase = Marshal.AllocHGlobal(sizeof(ObjectHandle));
                    *(ObjectHandle*)managedPtrBase = new ObjectHandle();

                    // add handle to lookup tables
                    objToPtr[value] = managedPtrBase;
                    ptrToObj[managedPtrBase] = value;
                }
            }

            if (managedPtrBase == currentValue) {
                // same value, do nothing
                return managedPtrBase;
            }

            var managedPtr = (ObjectHandle*)managedPtrBase;
            var currentValuePtr = (ObjectHandle*)currentValue;

            if (currentValuePtr != null) {
                // current value of field being set isn't null so decrease refcount and clean up if needed
                currentValuePtr->RefCount--;

                // can clean up here because we've already established the value isn't the same on both sides
                if (currentValuePtr->RefCount <= 0) {
                    // free object handle and remove from lookup tables so .NET's tracing GC can (theoretically)
                    // collect the object being referenced now that no references to it from within this arena exist
                    Marshal.FreeHGlobal(currentValue);
                    objToPtr.Remove(ptrToObj[currentValue]);
                    ptrToObj.Remove(currentValue);
                }
            }

            if (managedPtr != null) {
                // increase object handle reference count
                managedPtr->RefCount++;
            }

            // return new object handle
            return managedPtrBase;
        }

        public T GetOutsidePtr<T>(IntPtr value) where T : class {
            if (value == IntPtr.Zero) {
                return null;
            }
            return (T)ptrToObj[value];
        }

        public bool VersionsMatch(RefVersion version, IntPtr item) {
            return version == ItemHeader.GetVersion(item);
        }

        public void Clear() {
            Clear(false);
        }

        private void Clear(bool disposing) {
            enumVersion++;

            if (disposing) {
                Version = 0;
            }
            else {
                Version = (Version + 1) & 0x7FFFFFFF; // invalidate old UnmanagedRefs

                // overflow protection
                if (Version == 0) {
                    Version = 1;
                }
            }

            // free pages
            foreach (var ptr in pages) {
                Marshal.FreeHGlobal(ptr);
            }

            // free freelists
            foreach (var ptr in freelists.Values) {
                Marshal.FreeHGlobal(ptr);
            }

            // free object handles
            foreach (var ptr in ptrToObj.Keys) {
                Marshal.FreeHGlobal(ptr);
            }

            pages.Clear();
            freelists.Clear();
            objToPtr.Clear();
            ptrToObj.Clear();

            if (ID != Guid.Empty) {
                arenas.Remove(ID);
            }

            if (!disposing) {
                // allocate one page to start and then try and register a unique ID
                AllocPage(PageSize);

                ID = Guid.NewGuid();
                while (arenas.ContainsKey(ID)) {
                    ID = Guid.NewGuid();
                }
                arenas[ID] = this;
            }
        }

        #region IDisposable
        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                if (disposing) {
                    // dispose managed state (managed objects)
                }

                // free unmanaged resources (unmanaged objects) and override finalizer
                // set large fields to null
                Clear(true);

                pages = null;
                freelists = null;
                objToPtr = null;
                ptrToObj = null;

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

        #region IEnumerable
        public Enumerator GetEnumerator() {
            return new Enumerator(this);
        }

        IEnumerator<ArenaEntry> IEnumerable<ArenaEntry>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
        #endregion

        private Page* currentPage { get { return (Page*)pages[pages.Count - 1]; } }
        public bool IsDisposed { get { return disposedValue; } }
        public int Version { get; private set; }

        #region Static
        private static Dictionary<Guid, Arena> arenas;
        private static Dictionary<Type, TypeHandle> typeToHandle;
        private static Dictionary<TypeHandle, Type> handleToType;

        static Arena() {
            arenas = new Dictionary<Guid, Arena>();
            typeToHandle = new Dictionary<Type, TypeHandle>();
            handleToType = new Dictionary<TypeHandle, Type>();

            // handle 0 should be void* type
            typeToHandle[typeof(void*)] = new TypeHandle(0);
            handleToType[new TypeHandle(0)] = typeof(void*); 
        }

        public static Arena Get(Guid id) {
            return arenas[id];
        }

        private static TypeHandle GetTypeHandle(Type type) {
            TypeHandle handle;
            if (!typeToHandle.TryGetValue(type, out handle)) {
                typeToHandle[type] = handle = new TypeHandle(typeToHandle.Count);
                if (handle.Value == 0) {
                    throw new OverflowException("Arena.TypeHandle value overflow: too many types");
                }
                handleToType[handle] = type;
            }
            return handle;
        }

        private static Type GetTypeFromHandle(TypeHandle handle) {
            Type type;
            if (!handleToType.TryGetValue(handle, out type)) {
                return typeof(Exception);
            }
            return type;
        }
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct ObjectHandle {
            public int RefCount;

            public override string ToString() {
                return $"ObjectHandle(RefCount={RefCount})";
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

            public Page(IntPtr address, int size) {
                Address = address;
                Size = size;
                Offset = 0;
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
                return $"Page(Address=0x{Address.ToInt64().ToString("x")}, Size={Size}, Offset={Offset})";
            }

            public static int AlignFloor(int addr, int size) {
                return addr & (~(size - 1));
            }

            public static int AlignCeil(int addr, int size) {
                return (addr + (size - 1)) & (~(size - 1));
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
                return $"Freelist(Head=0x{Head.ToInt64().ToString("x")})";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ItemHeader {
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
                return $"ItemHeader(Type={GetTypeFromHandle(TypeHandle).FullName}, Size={Size}, Next=0x{NextFree.ToInt64().ToString("x")}, Version=({Version.Arena},{Version.Item}))";
            }

            public Type Type {
                get {
                    return GetTypeFromHandle(TypeHandle);
                }
            }

            // helper functions for manipulating an item header, which is always located
            // in memory right before where the item is allocated
            public static IntPtr GetNextFree(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->NextFree;
            }

            public static void SetVersion(IntPtr item, RefVersion version) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Version = version;
            }

            public static RefVersion GetVersion(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->Version;
            }

            public static void Invalidate(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Version = new RefVersion(header->Version.Item, 0);
            }

            public static void SetNextFree(IntPtr item, IntPtr next) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->NextFree = next;
            }

            public static void SetHeader(IntPtr item, ItemHeader itemHeader) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                *header = itemHeader;
            }

            public static ItemHeader GetHeader(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return *header;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct TypeHandle : IEquatable<TypeHandle> {
            public readonly int Value;

            public TypeHandle(int value) {
                Value = value;
            }

            public override bool Equals(object obj) {
                return obj is TypeHandle handle &&
                       Value == handle.Value;
            }

            public bool Equals(TypeHandle other) {
                return Value == other.Value;
            }

            public override int GetHashCode() {
                return Value.GetHashCode();
            }

            public static bool operator ==(TypeHandle left, TypeHandle right) {
                return left.Equals(right);
            }

            public static bool operator !=(TypeHandle left, TypeHandle right) {
                return !(left == right);
            }

            public override string ToString() {
                return GetTypeFromHandle(this).ToString();
            }
        }

        [Serializable]
        public struct Enumerator : IEnumerator<ArenaEntry>, IEnumerator {
            private Arena arena;
            private int pageIndex;
            private int offset;
            private int version;
            private ArenaEntry current;

            internal Enumerator(Arena arena) {
                this.arena = arena;
                pageIndex = 0;
                offset = 0;
                version = arena.enumVersion;
                current = default(ArenaEntry);
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                Arena localArena = arena;

                if (version == localArena.enumVersion && pageIndex < localArena.pages.Count) {
                    while (pageIndex < localArena.pages.Count) {
                        Page* curPage = (Page*)localArena.pages[pageIndex];

                        if (offset >= curPage->Offset) {
                            pageIndex++;
                            continue;
                        }

                        var ptr = curPage->Address + offset + sizeof(ItemHeader);
                        var header = ItemHeader.GetHeader(ptr);
                        offset += header.Size + sizeof(ItemHeader);

                        if (header.Version.IsValid) {
                            current = new ArenaEntry(header.Type, ptr, header.Size);
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
                current = default(ArenaEntry);
                return false;
            }

            public ArenaEntry Current {
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
                current = default(ArenaEntry);
            }
        }
    }
}
