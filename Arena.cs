using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    unsafe public class Arena : IDisposable {
        public const int PageSize = 4096;

        private Dictionary<object, IntPtr> objToPtr;
        private Dictionary<IntPtr, object> ptrToObj;
        private bool disposedValue;
        private List<IntPtr> pages;
        private Dictionary<Type, IntPtr> freelists;
        private Guid ID;

        public Arena() {
            objToPtr = new Dictionary<object, IntPtr>();
            ptrToObj = new Dictionary<IntPtr, object>();

            pages = new List<IntPtr>();
            AllocPage(PageSize);

            freelists = new Dictionary<Type, IntPtr>();

            ID = Guid.NewGuid();
            while (arenas.ContainsKey(ID)) {
                ID = Guid.NewGuid();
            }
            arenas[ID] = this;
        }

        private void AllocPage(int size) {
            var mem = Marshal.AllocHGlobal(size);
            var ptr = Marshal.AllocHGlobal(sizeof(Page));
            *(Page*)ptr = new Page(mem, size);
            pages.Add(ptr);
        }

        private Freelist* GetFreelist(Type type) {
            IntPtr ptr;
            if (!freelists.TryGetValue(type, out ptr)) {
                return null;
            }
            return (Freelist*)ptr;
        }

        public UnmanagedRef<T> Allocate<T>(T item) where T : unmanaged, IArenaContents {
            IntPtr ptr;

            Freelist* freelist;
            if ((freelist = GetFreelist(typeof(T))) == null || (ptr = freelist->Pop()) == IntPtr.Zero) {
                ptr = Push(sizeof(T) + sizeof(ItemHeader)) + sizeof(ItemHeader);
                Freelist.SetHeader(ptr, new ItemHeader(typeof(T).TypeHandle, IntPtr.Zero));
            }

            item.ArenaID = ID;
            *(T*)ptr = item;

            return new UnmanagedRef<T>((T*)ptr, this);
        }

        private IntPtr Push(int size) {
            IntPtr ptr;
            var page = currentPage;

            if ((ptr = page->Push(size)) == IntPtr.Zero) {
                AllocPage(Page.AlignCeil(size, PageSize));
                page = currentPage;
                ptr = page->Push(size);
            }

            return ptr;
        }

        public void Free<T>(UnmanagedRef<T> item) where T : unmanaged, IArenaContents {
            if (item.HasValue) {
                item.Value->Free();

                var page = currentPage;
                if (page->IsTop((IntPtr)item.Value + sizeof(T))) {
                    page->Pop(sizeof(T) + sizeof(ItemHeader));
                }
                else {
                    Freelist* freelist;
                    if ((freelist = GetFreelist(typeof(T))) == null) {
                        var ptr = Marshal.AllocHGlobal(sizeof(Freelist));
                        freelist = (Freelist*)ptr;
                        *freelist = new Freelist();
                        freelists[typeof(T)] = ptr;
                    }

                    freelist->Push((IntPtr)item.Value);
                }
            }
        }

        public IntPtr SetOutsidePtr<T>(T value, IntPtr currentValue) where T : class {
            if (!(value is object) && currentValue == IntPtr.Zero) {
                // both null, do nothing
                return IntPtr.Zero;
            }

            IntPtr managedPtrBase = IntPtr.Zero;
            if (value is object) {
                if (!objToPtr.TryGetValue(value, out managedPtrBase)) {
                    managedPtrBase = Marshal.AllocHGlobal(sizeof(ObjectHandle));
                    *(ObjectHandle*)managedPtrBase = new ObjectHandle();

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
                currentValuePtr->RefCount--;

                // can do this here because we've already established the value isn't the same on both sides
                if (currentValuePtr->RefCount <= 0) {
                    Marshal.FreeHGlobal(currentValue);
                    objToPtr.Remove(ptrToObj[currentValue]);
                    ptrToObj.Remove(currentValue);
                }
            }

            if (managedPtr != null) {
                managedPtr->RefCount++;
            }
            return managedPtrBase;
        }

        public T GetOutsidePtr<T>(IntPtr value) where T : class {
            if (value == IntPtr.Zero) {
                return null;
            }
            return (T)ptrToObj[value];
        }

        public void Clear() {
            Clear(false);
        }

        private void Clear(bool disposing) {
            Version++;

            foreach (var ptr in pages) {
                Marshal.FreeHGlobal(ptr);
            }
            foreach (var ptr in freelists.Values) {
                Marshal.FreeHGlobal(ptr);
            }
            foreach (var ptr in ptrToObj.Keys) {
                Marshal.FreeHGlobal(ptr);
            }

            pages.Clear();
            freelists.Clear();
            objToPtr.Clear();
            ptrToObj.Clear();

            arenas.Remove(ID);

            if (!disposing) {
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

        private Page* currentPage { get { return (Page*)pages[pages.Count - 1]; } }
        public bool IsDisposed { get { return disposedValue; } }
        public int Version { get; private set; }

        #region Static
        private static Dictionary<Guid, Arena> arenas;

        static Arena() {
            arenas = new Dictionary<Guid, Arena>();
        }

        public static Arena Get(Guid id) {
            return arenas[id];
        }
        #endregion

        [StructLayout(LayoutKind.Sequential)]
        private struct ObjectHandle {
            public int RefCount;

            public override string ToString() {
                return $"ObjectHandler(RefCount={RefCount})";
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct Page {
            public IntPtr Address;
            /// <summary>
            /// Size in bytes
            /// </summary>
            public int Size;
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
                Head = GetNext(Head);

                return item;
            }

            public void Push(IntPtr item) {
                var next = Head;
                Head = item;
                SetNext(Head, next);
            }

            public override string ToString() {
                return $"Freelist(Head=0x{Head.ToInt64().ToString("x")})";
            }

            public static IntPtr GetNext(IntPtr item) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                return header->Next;
            }

            public static void SetNext(IntPtr item, IntPtr next) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                header->Next = next;
            }

            public static void SetHeader(IntPtr item, ItemHeader itemHeader) {
                var header = (ItemHeader*)(item - sizeof(ItemHeader));
                *header = itemHeader;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ItemHeader {
            public RuntimeTypeHandle TypeHandle;
            public IntPtr Next;

            public ItemHeader(RuntimeTypeHandle typeHandle, IntPtr next) {
                TypeHandle = typeHandle;
                Next = next;
            }

            public override string ToString() {
                return $"ItemHeader(Type={(TypeHandle.Value == IntPtr.Zero ? "void" : Type.GetTypeFromHandle(TypeHandle).FullName)}, Next=0x{Next.ToInt64().ToString("x")})";
            }
        }
    }
}
