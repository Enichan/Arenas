﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    unsafe public class Arena : IDisposable, IEnumerable<ArenaEntry> {
        public const int PageSize = 256;

        private Dictionary<object, IntPtr> objToPtr;
        private Dictionary<IntPtr, object> ptrToObj;
        private bool disposedValue;
        private List<IntPtr> pages;
        private Dictionary<FreelistKey, IntPtr> freelists;
        private Guid ID;
        private int enumVersion;

        public Arena() {
            objToPtr = new Dictionary<object, IntPtr>(ObjectReferenceEqualityComparer.Instance);
            ptrToObj = new Dictionary<IntPtr, object>();
            pages = new List<IntPtr>();
            freelists = new Dictionary<FreelistKey, IntPtr>();

            // call clear to set up everything we need for use
            Clear(false);
        }

        private void AllocPage(int size) {
            Debug.Assert(size == Page.AlignCeil(size, PageSize), "Non page-aligned size in AllocPage");
            
            // create memory and clear
            var mem = Marshal.AllocHGlobal(size);
            ZeroMem(mem, size);

            // create page instance and clear, then add to list of pages
            var ptr = Marshal.AllocHGlobal(sizeof(Page));
            *(Page*)ptr = new Page(mem, size);
            pages.Add(ptr);
        }

        // convenience function cause .NET doesn't let you put pointers in dictionaries
        private Freelist* GetFreelist(Type type, int size) {
            IntPtr ptr;
            if (!freelists.TryGetValue(new FreelistKey(type, size), out ptr)) {
                return null;
            }
            return (Freelist*)ptr;
        }

        private IntPtr Allocate(Type type, long sizeBytes, out RefVersion version) {
            IntPtr ptr;

            // make sure size in bytes is at least one word and doesn't overflow
            if (sizeBytes < sizeof(ulong)) {
                sizeBytes = sizeof(ulong);
            }
            if (sizeBytes > int.MaxValue) {
                throw new InvalidOperationException("Arena can't allocate size that large");
            }

            var iSizeBytes = (int)sizeBytes;

            // check if there is a freelist for this type and attempt to get an item from it
            Freelist* freelist;
            if ((freelist = GetFreelist(type, iSizeBytes)) == null || (ptr = freelist->Pop()) == IntPtr.Zero) {
                // failed to get an item from freelist so push a new item onto the arena
                ptr = Push(iSizeBytes + sizeof(ItemHeader)) + sizeof(ItemHeader);

                // increment item version by 1 and set header
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = new RefVersion(prevVersion.Item + 1, Version);
                ItemHeader.SetHeader(ptr, new ItemHeader(GetTypeHandle(type), iSizeBytes, IntPtr.Zero, version)); // set header
            }
            else {
                // increment item version by 1
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = new RefVersion(prevVersion.Item + 1, Version);
                ItemHeader.SetVersion(ptr, version);
            }

            return ptr;
        }

        public UnmanagedRef<T> AllocValues<T>(int count) where T : unmanaged {
            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                throw new InvalidOperationException("Can't allocate arena items which implement IArenaContents via AllocValues, use Allocate instead");
            }
            return _AllocValues<T>(count);
        }

        public UnmanagedRef<T> _AllocValues<T>(int count) where T : unmanaged {
            enumVersion++;

            Type type = typeof(T);
            int elementSize = sizeof(T);

            long sizeBytes = elementSize * count;
            sizeBytes = NextPowerOfTwo(sizeBytes);

            // allocate items and zero memory
            RefVersion version;
            var ptr = Allocate(type, sizeBytes, out version);
            ZeroMem(ptr, (int)sizeBytes);

            // get actual allocated item count (can be bigger than requested)
            count = (int)sizeBytes / sizeof(T);

            // return pointer as an UnmanagedRef
            return new UnmanagedRef<T>((T*)ptr, this, version, count);
        }

        public UnmanagedRef<T> AllocValue<T>(T item) where T : unmanaged {
            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                throw new InvalidOperationException("Can't allocate arena item which implements IArenaContents via AllocValue, use Allocate instead");
            }
            return _AllocValue(item);
        }

        private UnmanagedRef<T> _AllocValue<T>(T item) where T : unmanaged {
            enumVersion++;

            Type type = typeof(T);
            long sizeBytes = sizeof(T);

            // allocate item
            RefVersion version;
            var ptr = Allocate(type, sizeBytes, out version);

            // initialize item value
            *(T*)ptr = item;

            // return pointer as an UnmanagedRef
            return new UnmanagedRef<T>((T*)ptr, this, version, 1);
        }

        public UnmanagedRef<T> Allocate<T>(int count) where T : unmanaged, IArenaContents {
            return _Allocate(_AllocValues<T>(count));
        }

        public UnmanagedRef<T> Allocate<T>(T item) where T : unmanaged, IArenaContents {
            return _Allocate(_AllocValue(item));
        }

        private UnmanagedRef<T> _Allocate<T>(UnmanagedRef<T> items) where T : unmanaged, IArenaContents {
            var cur = items.Value;
            for (int i = 0; i < items.ElementCount; i++, cur++) {
                // set arena ID
                cur->ArenaID = ID;
            }
            return items;
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

        public void FreeValues<T>(UnmanagedRef<T> uref) where T : unmanaged {
            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                throw new InvalidOperationException("Can't free arena items which implement IArenaContents via FreeValues, use Free instead");
            }
            if (!uref.HasValue) {
                // can't free that ya silly bugger
                return;
            }

            enumVersion++;
            _FreeValues(uref);
        }

        private void _FreeValues<T>(UnmanagedRef<T> uref) where T : unmanaged {
            var itemPtr = (IntPtr)uref.Value;
            var sizeBytes = ItemHeader.GetSize(itemPtr);
            var type = typeof(T);

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
                if ((freelist = GetFreelist(type, sizeBytes)) == null) {
                    var freelistPtr = Marshal.AllocHGlobal(sizeof(Freelist));
                    freelist = (Freelist*)freelistPtr;
                    *freelist = new Freelist();
                    freelists[new FreelistKey(type, sizeBytes)] = freelistPtr;
                }

                freelist->Push(itemPtr);
            }
        }

        internal void Free<T>(UnmanagedRef<T> uref) where T : unmanaged, IArenaContents {
            if (!uref.HasValue) {
                // can't free that ya silly bugger
                return;
            }

            enumVersion++;

            // tell the items to free anything they need to
            // usually this means setting ManagedRefs to null but it could also
            // have to free other unmanaged allocations
            var item = uref.Value;
            for (int i = 0; i < uref.ElementCount; i++, item++) {
                item->Free();
            }

            _FreeValues(uref);
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

            // handle 0 should be void type
            typeToHandle[typeof(void)] = new TypeHandle(0);
            handleToType[new TypeHandle(0)] = typeof(void); 
        }

        public static Arena Get(Guid id) {
            Arena arena;
            if (!arenas.TryGetValue(id, out arena)) {
                return null;
            }
            return arena;
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

        // http://graphics.stanford.edu/%7Eseander/bithacks.html#RoundUpPowerOf2
        private static long NextPowerOfTwo(long v) {
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

        private static void ZeroMem(IntPtr ptr, int size) {
            if (size % sizeof(ulong) != 0) {
                throw new ArgumentException("Size must be divisible by sizeof(ulong)", nameof(size));
            }

            var cur = (ulong*)ptr;
            var count = size / sizeof(ulong);

            for (int i = 0; i < count; i++, cur++) {
                *cur = 0;
            }
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

            public static void SetSize(IntPtr item, int size) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Size = size;
            }

            public static int GetSize(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->Size;
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

            public static void Invalidate(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Version = new RefVersion(header->Version.Item, 0);
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

        private readonly struct FreelistKey : IEquatable<FreelistKey> {
            public readonly Type Type;
            public readonly int Size;

            public FreelistKey(Type type, int size) {
                Type = type;
                Size = size;
            }

            public override bool Equals(object obj) {
                return obj is FreelistKey key &&
                       Type.Equals(key.Type) &&
                       Size == key.Size;
            }

            public bool Equals(FreelistKey other) {
                return
                       Type.Equals(other.Type) &&
                       Size == other.Size;
            }

            public override int GetHashCode() {
                int hashCode = 1281792895;
                hashCode = hashCode * -1521134295 + Type.GetHashCode();
                hashCode = hashCode * -1521134295 + Size.GetHashCode();
                return hashCode;
            }

            public static bool operator ==(FreelistKey left, FreelistKey right) {
                return left.Equals(right);
            }

            public static bool operator !=(FreelistKey left, FreelistKey right) {
                return !(left == right);
            }

            public override string ToString() {
                return $"Freelist(Type={Type}, Size={Size})";
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
                            offset = 0;
                            continue;
                        }

                        var ptr = curPage->Address + offset + sizeof(ItemHeader);
                        var header = ItemHeader.GetHeader(ptr);
                        offset += header.Size + sizeof(ItemHeader);

                        if (header.Type == typeof(Exception)) {
                            throw new InvalidOperationException("Enumeration found an unknown type; arena memory may be corrupted");
                        }

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
