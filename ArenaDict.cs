using Arenas;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    //[DebuggerTypeProxy(typeof(ArenaListDebugView<>))]
    public unsafe struct ArenaDict<TKey, TValue> /*: IDictionary<TKey, TValue>*/ where TKey : unmanaged where TValue : unmanaged{
        private const int defaultCapacity = 8;
        private const int defaultShiftAmt = 32 - 3;
        private const uint fibHashMagic = 2654435769;

        private UnmanagedRef<UnmanagedDict> info;

        public ArenaDict(Arena arena) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            info = arena.Allocate(new UnmanagedDict());

            var typeK = TypeInfo.GenerateTypeInfo<TKey>();
            var typeV = TypeInfo.GenerateTypeInfo<TValue>();
            var entryInfo = TypeInfo.GenerateTypeInfo<UnmanagedDictEntry>();
            var entrySize = MemAlign.Ceil(typeK.Size + typeV.Size + entryInfo.Size, sizeof(ulong));

            var capacity = defaultCapacity;
            var size = capacity * entrySize;
            size *= 2; // extra space for linked lists
            var itemsRef = arena.AllocCount<byte>(size);

            var bufferLength = capacity;
            var overflowLength = itemsRef.Size / entrySize - bufferLength;

            info.Value->EntrySize = entrySize;
            info.Value->KeyOffset = entryInfo.Size;
            info.Value->ValueOffset = info.Value->KeyOffset + typeK.Size;

            info.Value->ItemsBuffer = (UnmanagedRef)itemsRef;
            info.Value->BufferLength = capacity;
            info.Value->OverflowLength = itemsRef.Size / entrySize - bufferLength;
            Debug.Assert(info.Value->OverflowLength >= info.Value->BufferLength);

            info.Value->Bump = itemsRef.Size - entrySize * info.Value->OverflowLength;
            info.Value->Shift = defaultShiftAmt;
        }

        // https://probablydance.com/2018/06/16/fibonacci-hashing-the-optimization-that-the-world-forgot-or-a-better-alternative-to-integer-modulo/
        private static uint HashToIndex(uint hash, int shift) {
            hash ^= hash >> shift; // this line improved distribution but can be commented out
            return (fibHashMagic * hash) >> shift;
        }

        //public void Free() {
        //    if (Arena is null) {
        //        throw new InvalidOperationException("Cannot Free UnmanagedDict<K, V>: list has not been properly initialized with arena reference");
        //    }
        //    if (!info.HasValue) {
        //        throw new InvalidOperationException("Cannot Free UnmanagedDict<K, V>: list memory has previously been freed");
        //    }

        //    var items = info.Value->Items;
        //    Arena.Free(items);
        //    Arena.Free(info);
        //}

        //public void Clear() {
        //    if (Arena is null) {
        //        throw new InvalidOperationException("Cannot Clear UnmanagedDict<K, V>: list has not been properly initialized with arena reference");
        //    }
        //    if (!info.HasValue) {
        //        throw new InvalidOperationException("Cannot Clear UnmanagedDict<K, V>: list memory has previously been freed");
        //    }
        //    info.Value->Version++;
        //    info.Value->Count = 0;
        //}

        //private void Copy(int sourceIndex, int destIndex, int count) {
        //    Debug.Assert(destIndex + count <= info.Value->Capacity, "Bad ArenaList copy");
        //    var items = (T*)info.Value->Items.RawUnsafePointer;
        //    var source = items + sourceIndex;
        //    var dest = items + destIndex;
        //    var destSize = (info.Value->Capacity - destIndex) * sizeof(T);
        //    var bytesToCopy = (info.Value->Count - sourceIndex) * sizeof(T);
        //    Buffer.MemoryCopy(source, dest, destSize, bytesToCopy);
        //}

        //private void AddCapacity() {
        //    if (info.Value->Count < info.Value->Capacity) {
        //        return;
        //    }

        //    var newMinCapacity = info.Value->Capacity * 2;

        //    var items = info.Value->Items;
        //    var newItems = Arena.AllocCount<K, V>(newMinCapacity);
        //    info.Value->Capacity = newItems.ElementCount; // we might get more capacity than requested

        //    var newSize = newItems.Size;
        //    Buffer.MemoryCopy((void*)items.Value, newItems.Value, newSize, newSize);
        //    Arena.Free(items);

        //    info.Value->Items = (UnmanagedRef)newItems;
        //}

        public void Add(TKey key, TValue value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Add item to UnmanagedDict<K, V>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Add item to UnmanagedDict<K, V>: list memory has previously been freed");
            }

            var hashcode = 4297586;// key.GetHashCode();
            if (hashcode == 0) hashcode = 1;

            var index = (int)HashToIndex((uint)hashcode, info.Value->Shift);
            var entry = GetIndex(index);

            if (entry.HashCode == 0) {
                // no value at index, insert
                entry.HashCode = hashcode;
                *entry.Key = key;
                *entry.Value = value;
            }
            else if (entry.HashCode == hashcode && EqualityComparer<TKey>.Default.Equals(*entry.Key, key)) {
                // duplicate key at index
                throw new ArgumentException("An element with that key already exists");
            }
            else {
                var next = entry.Next;
                while (next > 0) {
                    entry = GetOffset(next);
                    if (entry.HashCode == hashcode && EqualityComparer<TKey>.Default.Equals(*entry.Key, key)) {
                        // duplicate key in bucket
                        throw new ArgumentException("An element with that key already exists");
                    }
                    next = entry.Next;
                }

                // no key found, add to bucket
                var head = GetHead();
                info.Value->Head = head.Next;
                entry.Next = head.Offset;

                head.HashCode = hashcode;
                head.Next = 0;
                *head.Key = key;
                *head.Value = value;
            }
        }

        private Entry GetIndex(int index) {
            return GetOffset(index * info.Value->EntrySize);
        }

        private Entry GetOffset(int offset) {
            return new Entry(info.Value->ItemsBuffer.Value + offset, info.Value);
        }

        private Entry GetHead() {
            var head = info.Value->Head;
            if (head == 0) {
                head = info.Value->Bump;
                info.Value->Bump += info.Value->EntrySize;
                Debug.Assert(head < info.Value->ItemsBuffer.Size);
            }
            return GetOffset(head);
        }

        //public Enumerator GetEnumerator() {
        //    if (Arena is null) {
        //        throw new InvalidOperationException("Cannot GetEnumerator for UnmanagedDict<K, V>: list has not been properly initialized with arena reference");
        //    }
        //    if (!info.HasValue) {
        //        throw new InvalidOperationException("Cannot GetEnumerator for UnmanagedDict<K, V>: list memory has previously been freed");
        //    }
        //    return new Enumerator(this);
        //}

        //IEnumerator<K, V> IEnumerable<K, V>.GetEnumerator() {
        //    return GetEnumerator();
        //}

        //IEnumerator IEnumerable.GetEnumerator() {
        //    return GetEnumerator();
        //}

        public TValue this[TKey key] {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get item at index in UnmanagedDict<K, V>: list has not been properly initialized with arena reference");
                }
                if (!info.HasValue) {
                    throw new InvalidOperationException("Cannot get item at index in UnmanagedDict<K, V>: list memory has previously been freed");
                }
                throw new NotImplementedException();
            }
            set {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot set item at index in UnmanagedDict<K, V>: list has not been properly initialized with arena reference");
                }
                if (!info.HasValue) {
                    throw new InvalidOperationException("Cannot set item at index in UnmanagedDict<K, V>: list memory has previously been freed");
                }
                throw new NotImplementedException();
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
        //bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly { get { return false; } }

        private unsafe readonly struct Entry {
            public readonly IntPtr Pointer;
            public readonly UnmanagedDict* Dict;

            public Entry(IntPtr pointer, UnmanagedDict* dict) {
                Pointer = pointer;
                Dict = dict;
            }

            public override string ToString() {
                return $"Entry({*Key}={*Value}, HashCode={HashCode}, Next={Next}, Offset={Offset})";
            }

            public int Offset { get { return (int)((ulong)Pointer - (ulong)Dict->ItemsBuffer.Value); } }
            public TKey* Key { get { return (TKey*)(Pointer + Dict->KeyOffset); } }
            public TValue* Value { get { return (TValue*)(Pointer + Dict->ValueOffset); } }
            public int HashCode { get { return ((UnmanagedDictEntry*)Pointer)->HashCode; } set { ((UnmanagedDictEntry*)Pointer)->HashCode = value; } }
            public int Next { get { return ((UnmanagedDictEntry*)Pointer)->Next; } set { ((UnmanagedDictEntry*)Pointer)->Next = value; } }
        }

        //[Serializable]
        //public struct Enumerator : IEnumerator<K, V>, System.Collections.IEnumerator {
        //    private ArenaList<K, V> list;
        //    private int index;
        //    private int version;
        //    private T current;

        //    internal Enumerator(ArenaList<K, V> list) {
        //        this.list = list;
        //        index = 0;
        //        version = list.info.Value->Version;
        //        current = default;
        //    }

        //    public void Dispose() {
        //    }

        //    public bool MoveNext() {
        //        ArenaList<K, V> localList = list;

        //        if (version == localList.info.Value->Version && ((uint)index < (uint)localList.info.Value->Count)) {
        //            current = localList[index];
        //            index++;
        //            return true;
        //        }
        //        return MoveNextRare();
        //    }

        //    private bool MoveNextRare() {
        //        if (version != list.info.Value->Version) {
        //            throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
        //        }

        //        index = list.info.Value->Count + 1;
        //        current = default;
        //        return false;
        //    }

        //    public T Current {
        //        get {
        //            return current;
        //        }
        //    }

        //    object System.Collections.IEnumerator.Current {
        //        get {
        //            if (index == 0 || index == list.info.Value->Count + 1) {
        //                throw new InvalidOperationException("Enumeration has either not started or has already finished.");
        //            }
        //            return Current;
        //        }
        //    }

        //    void System.Collections.IEnumerator.Reset() {
        //        if (version != list.info.Value->Version) {
        //            throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
        //        }

        //        index = 0;
        //        current = default;
        //    }
        //}
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UnmanagedDict {
        public UnmanagedRef ItemsBuffer;
        public int BufferLength;
        public int OverflowLength;
        public int Count;
        public int Shift;
        public int Version;
        public int EntrySize;
        public int KeyOffset;
        public int ValueOffset;
        public int Head;
        public int Bump;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct UnmanagedDictEntry {
        public int HashCode;
        public int Next;

        public UnmanagedDictEntry(int hashCode, int next) {
            HashCode = hashCode;
            Next = next;
        }
    }

    //internal unsafe readonly struct ArenaListDebugView<K, V> where T : unmanaged {
    //    private readonly ArenaList<K, V> list;

    //    public ArenaListDebugView(ArenaList<K, V> list) {
    //        this.list = list;
    //    }

    //    public T[] Items {
    //        get {
    //            var items = new T[list.Count];
    //            list.CopyTo(items, 0);
    //            return items;
    //        }
    //    }

    //    public int Count { get { return list.Count; } }
    //    public Arena Arena { get { return list.Arena; } }
    //    public bool IsAllocated { get { return list.IsAllocated; } }
    //}
}
