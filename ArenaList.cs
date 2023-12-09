﻿using Arenas;
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
    public unsafe struct ArenaList<T> : IList<T> where T : unmanaged {
        private const int defaultCapacity = 4;

        private UnmanagedRef<UnmanagedList> info;

        public ArenaList(Arena arena, int capacity = defaultCapacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            if (TypeInfo.GenerateTypeInfo<T>().IsArenaContents) {
                throw new NotSupportedException("ArenaList cannot store items which implement IArenaContents. Please use UnmanagedRef instead.");
            }

            info = arena.Allocate(new UnmanagedList());

            var minCapacity = Math.Max(capacity, defaultCapacity);
            var itemsRef = arena.AllocCount<T>(minCapacity);

            info.Value->Items = (UnmanagedRef)itemsRef;
            info.Value->Capacity = itemsRef.ElementCount; // we might get more capacity than requested
        }

        public void Free() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Free UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Free UnmanagedList<T>: list memory has previously been freed");
            }

            info.Value->Version++;
            var items = info.Value->Items;
            Arena.Free(items);
            Arena.Free(info);
            info = default;
        }

        public void Clear() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Clear UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Clear UnmanagedList<T>: list memory has previously been freed");
            }
            info.Value->Version++;
            info.Value->Count = 0;
        }

        private void Copy(int sourceIndex, int destIndex, int count) {
            Debug.Assert(destIndex + count <= info.Value->Capacity, "Bad ArenaList copy");
            var items = (T*)info.Value->Items.RawUnsafePointer;
            var source = items + sourceIndex;
            var dest = items + destIndex;
            var destSize = (info.Value->Capacity - destIndex) * sizeof(T);
            var bytesToCopy = (info.Value->Count - sourceIndex) * sizeof(T);
            Buffer.MemoryCopy(source, dest, destSize, bytesToCopy);
        }

        private void AddCapacity() {
            if (info.Value->Count < info.Value->Capacity) {
                return;
            }

            var newMinCapacity = info.Value->Capacity * 2;

            var items = info.Value->Items;
            var newItems = Arena.AllocCount<T>(newMinCapacity);
            info.Value->Capacity = newItems.ElementCount; // we might get more capacity than requested

            var newSize = newItems.Size;
            Buffer.MemoryCopy((void*)items.Value, newItems.Value, newSize, newSize);
            Arena.Free(items);

            info.Value->Items = (UnmanagedRef)newItems;
        }

        public void Add(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Add item to UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Add item to UnmanagedList<T>: list memory has previously been freed");
            }
            info.Value->Version++;
            AddCapacity();
            ((T*)info.Value->Items.RawUnsafePointer)[info.Value->Count] = item;
            info.Value->Count++;
        }

        public void Insert(int index, T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Insert item into UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Insert item into UnmanagedList<T>: list memory has previously been freed");
            }
            if (index < 0 || index >= Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            info.Value->Version++;
            AddCapacity();

            var items = (T*)info.Value->Items.RawUnsafePointer;

            if (index == Count) {
                items[info.Value->Count] = item;
                info.Value->Count++;
                return;
            }

            Copy(index, index + 1, Count - index);
            items[index] = item;
            info.Value->Count++;
        }

        public void RemoveAt(int index) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot RemoveAt in UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot RemoveAt in UnmanagedList<T>: list memory has previously been freed");
            }
            if (index < 0 || index >= Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            info.Value->Version++;

            if (index == Count - 1) {
                info.Value->Count--;
                return;
            }

            info.Value->Count--;
            Copy(index + 1, index, Count - index);
        }

        public bool Remove(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Remove item from UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Remove item from UnmanagedList<T>: list memory has previously been freed");
            }
            var index = IndexOf(item);
            if (index < 0) {
                return false;
            }
            RemoveAt(index);
            return true;
        }

        public int IndexOf(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot get IndexOf item in UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot get IndexOf item in UnmanagedList<T>: list memory has previously been freed");
            }
            var count = Count;
            var cur = (T*)info.Value->Items.RawUnsafePointer;

            for (int i = 0; i < count; i++) {
                if (EqualityComparer<T>.Default.Equals(*(cur++), item)) {
                    return i;
                }
            }

            return -1;
        }

        public bool Contains(T item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot check if UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot check if UnmanagedList<T> Contains item: list memory has previously been freed");
            }
            return IndexOf(item) >= 0;
        }

        public void CopyTo(T[] dest) {
            CopyTo(0, dest, 0, Count);
        }

        public void CopyTo(T[] dest, int destIndex) {
            CopyTo(0, dest, destIndex, Count);
        }

        public void CopyTo(int sourceIndex, T[] dest, int destIndex, int count) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot CopyTo array from UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot CopyTo array from UnmanagedList<T>: list memory has previously been freed");
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
            var items = info.Value->Items;
            items.CopyTo(dest, destIndex, sourceIndex, count);
        }

        public Enumerator GetEnumerator() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot GetEnumerator for UnmanagedList<T>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot GetEnumerator for UnmanagedList<T>: list memory has previously been freed");
            }
            return new Enumerator(this);
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public T this[int index] {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get item at index in UnmanagedList<T>: list has not been properly initialized with arena reference");
                }
                if (!info.HasValue) {
                    throw new InvalidOperationException("Cannot get item at index in UnmanagedList<T>: list memory has previously been freed");
                }
                if (index < 0 || index >= info.Value->Count) {
                    throw new IndexOutOfRangeException();
                }
                return ((T*)info.Value->Items.RawUnsafePointer)[index];
            }
            set {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot set item at index in UnmanagedList<T>: list has not been properly initialized with arena reference");
                }
                if (!info.HasValue) {
                    throw new InvalidOperationException("Cannot set item at index in UnmanagedList<T>: list memory has previously been freed");
                }
                if (index < 0 || index >= info.Value->Count) {
                    throw new IndexOutOfRangeException();
                }
                info.Value->Version++;
                ((T*)info.Value->Items.RawUnsafePointer)[index] = value;
            }
        }

        public int Count {
            get {
                if (!info.HasValue) {
                    return 0;
                }
                return info.Value->Count;
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
                index = 0;
                count = list.info.Value->Count;
                version = list.info.Value->Version;
                current = default;
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                ArenaList<T> localList = list;

                if (list.info.HasValue && version == localList.info.Value->Version && ((uint)index < (uint)count)) {
                    current = localList[index];
                    index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare() {
                if (!list.info.HasValue || version != list.info.Value->Version) {
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

            object System.Collections.IEnumerator.Current {
                get {
                    if (index == 0 || index == count + 1) {
                        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    }
                    return Current;
                }
            }

            void System.Collections.IEnumerator.Reset() {
                if (!list.info.HasValue || version != list.info.Value->Version) {
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
