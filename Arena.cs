﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Arenas {
    unsafe public class Arena : IDisposable, IEnumerable<ArenaEntry> {
        public const int PageSize = 4096;

        private Dictionary<object, ObjectEntry> objToPtr;
        private bool disposedValue;
        private List<Page> pages;
        private Dictionary<int, Freelist> freelists;
        private Guid id;
        private int enumVersion;

        public Arena() {
            objToPtr = new Dictionary<object, ObjectEntry>(ObjectReferenceEqualityComparer.Instance);
            pages = new List<Page>();
            freelists = new Dictionary<int, Freelist>();

            // call clear to set up everything we need for use
            Clear(false);
        }

        public UnmanagedRef<T> UnmanagedRefFromPtr<T>(IntPtr ptr) where T : unmanaged {
            if (ptr == IntPtr.Zero) {
                throw new ArgumentNullException(nameof(ptr));
            }
            var header = ItemHeader.GetHeader(ptr);
            if (header.Type != typeof(T)) {
                throw new InvalidOperationException("Type mismatch in header for pointer in UnmanagedRefFromPtr<T>(IntPtr), types do not match or address may be invalid.");
            }
            return new UnmanagedRef<T>((T*)ptr, this, header.Version, header.Size / sizeof(T));
        }

        public UnmanagedRef UnmanagedRefFromPtr(IntPtr ptr) {
            if (ptr == IntPtr.Zero) {
                throw new ArgumentNullException(nameof(ptr));
            }
            var header = ItemHeader.GetHeader(ptr);
            if (header.Type == typeof(Exception)) {
                throw new InvalidOperationException("Invalid type in header for pointer in UnmanagedRefFromPtr(IntPtr), address may be invalid.");
            }
            return new UnmanagedRef(header.Type, ptr, this, header.Version, header.Size / Marshal.SizeOf(header.Type));
        }

        private Page AllocPage(int size) {
            Debug.Assert(size == Page.AlignCeil(size, PageSize), "Non page-aligned size in AllocPage");
            
            var mem = Marshal.AllocHGlobal(size);
            ZeroMemory(mem, (UIntPtr)size);

            var page = new Page(mem, size);
            pages.Add(new Page(mem, size));
            return page;
        }

        private IntPtr Allocate(Type type, ulong sizeBytes, out RefVersion version) {
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
            Freelist freelist;
            if (!freelists.TryGetValue(iSizeBytes, out freelist) || (ptr = freelist.Pop()) == IntPtr.Zero) {
                // failed to get an item from freelist so push a new item onto the arena
                ptr = Push(iSizeBytes + sizeof(ItemHeader)) + sizeof(ItemHeader);

                // increment item version by 1 and set header
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = new RefVersion(prevVersion.Item + 1, Version);
                ItemHeader.SetHeader(ptr, new ItemHeader(GetTypeHandle(type), iSizeBytes, IntPtr.Zero, version)); // set header
            }
            else {
                freelists[iSizeBytes] = freelist;

                // increment item version by 1
                var prevVersion = ItemHeader.GetVersion(ptr);
                version = new RefVersion(prevVersion.Item + 1, Version);
                ItemHeader.SetVersion(ptr, version);
                ItemHeader.SetTypeHandle(ptr, GetTypeHandle(type));
            }

            return ptr;
        }

        public UnmanagedRef<T> Allocate<T>(T item) where T : unmanaged {
            var items = AllocCount<T>(1);
            items.Value[0] = item;
            ArenaContentsHelper.SetArenaID(items.Value, id);
            return items;
        }

        public UnmanagedRef<T> Allocate<T>(ref T item) where T : unmanaged {
            var items = AllocCount<T>(1);
            items.Value[0] = item;
            ArenaContentsHelper.SetArenaID(items.Value, id);
            return items;
        }

        public UnmanagedRef<T> AllocCount<T>(int count) where T : unmanaged {
            if (count <= 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            var items = _AllocValues<T>(count);

            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                var cur = items.Value;
                for (int i = 0; i < items.ElementCount; i++, cur++) {
                    // set arena ID
                    ArenaContentsHelper.SetArenaID(cur, id);
                }
            }

            return items;
        }

        public UnmanagedRef<T> _AllocValues<T>(int count) where T : unmanaged {
            enumVersion++;

            Type type = typeof(T);
            int elementSize = sizeof(T);

            ulong sizeBytes = (uint)elementSize * (uint)count;
            if (count > 1) {
                sizeBytes = NextPowerOfTwo(sizeBytes);
            }

            // allocate items and zero memory
            RefVersion version;
            var ptr = Allocate(type, sizeBytes, out version);
            ZeroMemory(ptr, (UIntPtr)sizeBytes);

            // get actual allocated item count (can be bigger than requested)
            count = (int)sizeBytes / sizeof(T);

            // return pointer as an UnmanagedRef
            return new UnmanagedRef<T>((T*)ptr, this, version, count);
        }

        private IntPtr Push(int size) {
            IntPtr ptr;
            var page = pages.Last();

            // try to claim size bytes in current page, will return null if out of space
            if ((ptr = page.Push(size)) == IntPtr.Zero) {
                // out of space, allocate new page, rounding size up to nearest multiple of PageSize
                page = AllocPage(Page.AlignCeil(size, PageSize));

                // claim size bytes in current page
                // this will always work because we just made sure the new page fits the requested size
                ptr = page.Push(size);
            }

            pages.SetLast(page);
            return ptr;
        }

        public void Free<T>(in UnmanagedRef<T> items) where T : unmanaged {
            if (!items.HasValue) {
                // can't free that ya silly bugger
                return;
            }

            enumVersion++;

            if (typeof(IArenaContents).IsAssignableFrom(typeof(T))) {
                var cur = items.Value;
                for (int i = 0; i < items.ElementCount; i++, cur++) {
                    // free contents
                    ArenaContentsHelper.Free(cur);
                }
            }

            _FreeValues((IntPtr)items.Value);
        }

        public void Free(in UnmanagedRef items) {
            if (!items.HasValue) {
                // can't free that ya silly bugger
                return;
            }

            enumVersion++;

            var free = ArenaContentsHelper.GetFreeDelegate(items.Type);
            var elementSize = Marshal.SizeOf(items.Type);

            if (typeof(IArenaContents).IsAssignableFrom(items.Type)) {
                var cur = items.Value;
                for (int i = 0; i < items.ElementCount; i++) {
                    // free contents
                    free(cur);
                    cur += elementSize;
                }
            }

            _FreeValues(items.Value);
        }

        public void Free(IntPtr ptr) {
            var uref = UnmanagedRefFromPtr(ptr);
            Free(in uref);
        }

        private void _FreeValues(IntPtr itemPtr) {
            var sizeBytes = ItemHeader.GetSize(itemPtr);

            // set version to indicate item is not valid
            ItemHeader.Invalidate(itemPtr);

            var page = pages.Last();
            if (page.IsTop(itemPtr + sizeBytes)) {
                // if the item as at the top of the current page then simply pop it off
                page.Pop(sizeBytes + sizeof(ItemHeader));
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
                Debug.Assert(currentTarget != null);

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

            // free GCHandles
            foreach (var entry in objToPtr.Values) {
                var gcHandle = entry.Handle;
                if (gcHandle.IsAllocated) {
                    gcHandle.Free();
                }
            }

            pages.Clear();
            freelists.Clear();
            objToPtr.Clear();

            if (id != Guid.Empty) {
                arenas.Remove(id);
            }

            if (!disposing) {
                // allocate one page to start and then try and register a unique ID
                AllocPage(PageSize);

                id = Guid.NewGuid();
                while (arenas.ContainsKey(id)) {
                    id = Guid.NewGuid();
                }
                arenas[id] = this;
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

        public bool IsDisposed { get { return disposedValue; } }
        public int Version { get; private set; }

        #region Static
        [DllImport("kernel32.dll")]
        private static extern void RtlZeroMemory(IntPtr dst, UIntPtr length);
        private delegate void ZeroMemoryDelegate(IntPtr dst, UIntPtr length);

        private static Dictionary<Guid, Arena> arenas;
        private static Dictionary<Type, TypeHandle> typeToHandle;
        private static Dictionary<TypeHandle, Type> handleToType;
        private static ZeroMemoryDelegate ZeroMemory;

        static Arena() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                ZeroMemory = RtlZeroMemory;
            }
            else {
                ZeroMemory = ZeroMemPlatformIndependent;
            }

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
            if (size % sizeof(ulong) != 0) {
                throw new ArgumentException("Size must be divisible by sizeof(ulong)", nameof(length));
            }
            
            var cur = (ulong*)ptr;
            var count = size / sizeof(ulong);

            for (ulong i = 0; i < count; i++, cur++) {
                *cur = 0;
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
                return $"Page(Address=0x{Address.ToInt64():x}, Size={Size}, Offset={Offset})";
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
                return $"Freelist(Head=0x{Head.ToInt64():x})";
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
                return $"ItemHeader(Type={GetTypeFromHandle(TypeHandle).FullName}, Size={Size}, Next=0x{NextFree.ToInt64():x}, Version=({Version.Arena},{Version.Item}))";
            }

            public Type Type {
                get {
                    return GetTypeFromHandle(TypeHandle);
                }
            }

            // helper functions for manipulating an item header, which is always located
            // in memory right before where the item is allocated
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

            public static IntPtr GetNextFree(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->NextFree;
            }

            public static void SetTypeHandle(IntPtr item, TypeHandle handle) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->TypeHandle = handle;
            }

            public static TypeHandle GetTypeHandle(IntPtr item) {
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
                        Page curPage = localArena.pages[pageIndex];

                        if (offset >= curPage.Offset) {
                            pageIndex++;
                            offset = 0;
                            continue;
                        }

                        var ptr = curPage.Address + offset + sizeof(ItemHeader);
                        var header = ItemHeader.GetHeader(ptr);
                        offset += header.Size + sizeof(ItemHeader);

                        if (header.Type == typeof(Exception) || header.Size < 0 || offset < 0 || offset > curPage.Offset) {
                            throw new InvalidOperationException("Enumeration encountered an error; arena memory may be corrupted");
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
