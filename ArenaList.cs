using Arenas;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    using static UnmanagedListTypes;

    [DebuggerTypeProxy(typeof(ArenaListDebugView<>))]
    public unsafe struct ArenaList<T> : IList<T>, IDisposable where T : unmanaged {
        private const int defaultCapacity = 4;

        private UnmanagedRef<UnmanagedList> info;

        private ArenaList(UnmanagedRef<UnmanagedList> listData) {
            info = listData;
        }

        public ArenaList(Arena arena, int capacity = defaultCapacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            if (TypeInfo.GenerateTypeInfo<T>().IsArenaContents) {
                throw new NotSupportedException("ArenaList cannot store items which implement IArenaContents. Please use UnmanagedRef instead.");
            }

            info = arena.Allocate(new UnmanagedList());
            var self = info.Value;

            var minCapacity = Math.Max(capacity, defaultCapacity);
            var itemsRef = arena.AllocCount<T>(minCapacity);

            self->Items = (UnmanagedRef)itemsRef;
            self->Capacity = itemsRef.ElementCount; // we might get more capacity than requested
        }

        public void Free() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Free ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Free ArenaList<T>: list memory has previously been freed");
            }

            self->Version++;
            var items = self->Items;
            Arena.Free(items);
            Arena.Free(info);
            info = default;
        }

        public void Dispose() {
            if (!IsAllocated) {
                return;
            }
            Free();
        }

        public void Clear() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Clear ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Clear ArenaList<T>: list memory has previously been freed");
            }

            self->Version++;
            self->Count = 0;
        }

        private void Copy(UnmanagedList* self, T* items, int sourceIndex, int destIndex, int count) {
            Debug.Assert(destIndex + count <= self->Capacity, "Bad ArenaList copy");
            var source = items + sourceIndex;
            var dest = items + destIndex;
            var destSize = (self->Capacity - destIndex) * sizeof(T);
            var bytesToCopy = (self->Count - sourceIndex) * sizeof(T);
            Buffer.MemoryCopy(source, dest, destSize, bytesToCopy);
        }

        private void AddCapacity(UnmanagedList* self, ref T* items) {
            if (self->Count < self->Capacity) {
                return;
            }

            var newMinCapacity = self->Capacity * 2;

            var newItems = Arena.AllocCount<T>(newMinCapacity);
            self->Capacity = newItems.ElementCount; // we might get more capacity than requested

            var newSize = newItems.Size;
            var newItemsPtr = newItems.Value;

            Buffer.MemoryCopy(items, newItemsPtr, newSize, newSize);
            
            Arena.Free(self->Items);
            self->Items = (UnmanagedRef)newItems;

            items = newItemsPtr;
        }

        public void Add(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Add item to ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Add item to ArenaList<T>: list memory has previously been freed");
            }

            var items = (T*)self->Items.Value;
            if (items == null) {
                throw new InvalidOperationException("Cannot Add item to ArenaList<T>: list's backing array has previously been freed");
            }

            self->Version++;
            AddCapacity(self, ref items);
            items[self->Count] = item;
            self->Count++;
        }

        public void Insert(int index, T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Insert item into ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Insert item into ArenaList<T>: list memory has previously been freed");
            }

            var items = (T*)self->Items.Value;
            if (items == null) {
                throw new InvalidOperationException("Cannot Insert item into ArenaList<T>: list's backing array has previously been freed");
            }

            if (index < 0 || index >= Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            self->Version++;
            AddCapacity(self, ref items);

            if (index == Count) {
                items[self->Count] = item;
                self->Count++;
                return;
            }

            Copy(self, items, index, index + 1, Count - index);
            items[index] = item;
            self->Count++;
        }

        public void RemoveAt(int index) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot RemoveAt in ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot RemoveAt in ArenaList<T>: list memory has previously been freed");
            }

            var items = (T*)self->Items.Value;
            if (items == null) {
                throw new InvalidOperationException("Cannot RemoveAt in ArenaList<T>: list's backing array has previously been freed");
            }

            RemoveAt(self, items, index);
        }

        private void RemoveAt(UnmanagedList* self, T* items, int index) {
            if (index < 0 || index >= Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            self->Version++;

            if (index == Count - 1) {
                self->Count--;
                return;
            }

            self->Count--;
            Copy(self, items, index + 1, index, Count - index);
        }

        public bool Remove(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Remove item from ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Remove item from ArenaList<T>: list memory has previously been freed");
            }

            var items = (T*)self->Items.Value;
            if (items == null) {
                throw new InvalidOperationException("Cannot Remove item from ArenaList<T>: list's backing array has previously been freed");
            }

            var index = IndexOf(self, items, item);
            if (index < 0) {
                return false;
            }

            RemoveAt(self, items, index);
            return true;
        }

        public int IndexOf(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot get IndexOf item in ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot get IndexOf item in ArenaList<T>: list memory has previously been freed");
            }

            var items = (T*)self->Items.Value;
            if (items == null) {
                throw new InvalidOperationException("Cannot get IndexOf item in ArenaList<T>: list's backing array has previously been freed");
            }

            return IndexOf(self, items, item);
        }

        private int IndexOf(UnmanagedList* self, T* items, T item) {
            var count = Count;
            var cur = items;

            for (int i = 0; i < count; i++) {
                if (EqualityComparer<T>.Default.Equals(*(cur++), item)) {
                    return i;
                }
            }

            return -1;
        }

        public bool Contains(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot check if ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot check if ArenaList<T> Contains item: list memory has previously been freed");
            }

            var items = (T*)self->Items.Value;
            if (items == null) {
                throw new InvalidOperationException("Cannot check if ArenaList<T> Contains item: list's backing array has previously been freed");
            }

            return IndexOf(self, items, item) >= 0;
        }

        public void CopyTo(T[] dest) {
            CopyTo(0, dest, 0, Count);
        }

        public void CopyTo(T[] dest, int destIndex) {
            CopyTo(0, dest, destIndex, Count);
        }

        public void CopyTo(int sourceIndex, T[] dest, int destIndex, int count) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot CopyTo array from ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot CopyTo array from ArenaList<T>: list memory has previously been freed");
            }

            var items = self->Items;
            if (!items.HasValue) {
                throw new InvalidOperationException("Cannot CopyTo array from ArenaList<T>: list's backing array has previously been freed");
            }

            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (destIndex < 0 || destIndex + count > dest.Length) {
                throw new ArgumentOutOfRangeException(nameof(destIndex));
            }
            if (sourceIndex < 0 || sourceIndex + count > Count) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }

            items.CopyTo(dest, destIndex, sourceIndex, count);
        }

        public Enumerator GetEnumerator() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot GetEnumerator for ArenaList<T>: list has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot GetEnumerator for ArenaList<T>: list memory has previously been freed");
            }

            var items = self->Items;
            if (!items.HasValue) {
                throw new InvalidOperationException("Cannot GetEnumerator for ArenaList<T>: list's backing array has previously been freed");
            }

            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public UnmanagedRef<UnmanagedList> GetUnderlyingReference() {
            return info;
        }

        public static explicit operator ArenaList<T>(UnmanagedRef<UnmanagedList> listData) {
            return new ArenaList<T>(listData);
        }

        public T this[int index] {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaList<T>: list has not been properly initialized with arena reference");
                }

                var self = info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaList<T>: list memory has previously been freed");
                }

                var items = (T*)self->Items.Value;
                if (items == null) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaList<T>: list's backing array has previously been freed");
                }

                if (index < 0 || index >= self->Count) {
                    throw new IndexOutOfRangeException();
                }

                return items[index];
            }
            set {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot set item at index in ArenaList<T>: list has not been properly initialized with arena reference");
                }

                var self = info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot set item at index in ArenaList<T>: list memory has previously been freed");
                }

                var items = (T*)self->Items.Value;
                if (items == null) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaList<T>: list's backing array has previously been freed");
                }

                if (index < 0 || index >= self->Count) {
                    throw new IndexOutOfRangeException();
                }

                self->Version++;
                items[index] = value;
            }
        }

        public int Count {
            get {
                var self = info.Value;
                if (self == null) {
                    return 0;
                }
                return self->Count;
            }
        }

        public bool IsAllocated { get { return info.HasValue; } }
        public Arena Arena { get { return info.Arena; } }
        bool ICollection<T>.IsReadOnly { get { return false; } }

        [Serializable]
        public struct Enumerator : IEnumerator<T>, System.Collections.IEnumerator {
            private ArenaList<T> list;
            private int index;
            private int version;
            private int count;
            private T current;

            internal Enumerator(ArenaList<T> list) {
                this.list = list;
                var listPtr = list.info.Value;
                index = 0;
                count = listPtr->Count;
                version = listPtr->Version;
                current = default;
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                var listPtr = list.info.Value;

                if (listPtr != null && version == listPtr->Version && ((uint)index < (uint)count)) {
                    var items = (T*)listPtr->Items.Value;
                    if (items != null) {
                        current = items[index];
                        index++;
                        return true;
                    }
                }
                return MoveNextRare();
            }

            private bool MoveNextRare() {
                var listPtr = list.info.Value;
                if (listPtr == null || version != listPtr->Version || !listPtr->Items.HasValue) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                index = count + 1;
                current = default;
                return false;
            }

            public T Current {
                get {
                    return current;
                }
            }

            object IEnumerator.Current {
                get {
                    if (index == 0 || index == count + 1) {
                        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    }
                    return Current;
                }
            }

            void IEnumerator.Reset() {
                var listPtr = list.info.Value;
                if (listPtr == null || version != listPtr->Version || !listPtr->Items.HasValue) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                index = 0;
                current = default;
            }
        }
    }

    public static class UnmanagedListTypes {
        [StructLayout(LayoutKind.Sequential)]
        public struct UnmanagedList {
            public UnmanagedRef Items;
            public int Count;
            public int Capacity;
            public int Version;
        }

        internal unsafe readonly struct ArenaListDebugView<T> where T : unmanaged {
            private readonly ArenaList<T> list;

            public ArenaListDebugView(ArenaList<T> list) {
                this.list = list;
            }

            public T[] Items {
                get {
                    var items = new T[list.Count];
                    list.CopyTo(items, 0);
                    return items;
                }
            }

            public int Count { get { return list.Count; } }
            public Arena Arena { get { return list.Arena; } }
            public bool IsAllocated { get { return list.IsAllocated; } }
        }
    }
}
