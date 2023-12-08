using Arenas;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Arenas {
    [DebuggerTypeProxy(typeof(ArenaDictDebugView<,>))]
    public unsafe struct ArenaDict<TKey, TValue> /*: IDictionary<TKey, TValue>*/ where TKey : unmanaged where TValue : unmanaged{
        private const int defaultCapacity = 8;
        private const uint fibHashMagic = 2654435769;

        private UnmanagedRef<UnmanagedDict> info;

        public ArenaDict(Arena arena, int capacity = defaultCapacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            info = arena.Allocate(new UnmanagedDict());

            if (capacity <= 0) {
                capacity = defaultCapacity;
            }

            var powTwo = MemHelper.NextPowerOfTwo((ulong)capacity);
            if (powTwo > int.MaxValue) {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            capacity = (int)powTwo;
            var shift = 32 - MemHelper.PowerOfTwoLeadingZeros[powTwo];

            var typeK = TypeInfo.GenerateTypeInfo<TKey>();
            var typeV = TypeInfo.GenerateTypeInfo<TValue>();
            var entryInfo = TypeInfo.GenerateTypeInfo<UnmanagedDictEntry>();
            var entrySize = MemHelper.AlignCeil(typeK.Size + typeV.Size + entryInfo.Size, sizeof(ulong));

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

            info.Value->Bump = info.Value->BufferLength * info.Value->EntrySize;
            info.Value->Shift = shift;
        }

        // https://probablydance.com/2018/06/16/fibonacci-hashing-the-optimization-that-the-world-forgot-or-a-better-alternative-to-integer-modulo/
        private static uint HashToIndex(uint hash, int shift) {
            hash ^= hash >> shift; // this line improved distribution but can be commented out
            return (fibHashMagic * hash) >> shift;
        }

        public void Free() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Free UnmanagedDict<TKey, TValue>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Free UnmanagedDict<TKey, TValue>: list memory has previously been freed");
            }

            var items = info.Value->ItemsBuffer;
            Arena.Free(items);
            Arena.Free(info);
            info = default;
        }

        public void Clear() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Clear UnmanagedDict<TKey, TValue>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Clear UnmanagedDict<TKey, TValue>: list memory has previously been freed");
            }

            info.Value->Version++;
            info.Value->Count = 0;
            info.Value->Bump = info.Value->BufferLength * info.Value->EntrySize;
            info.Value->Head = 0;
            MemHelper.ZeroMemory(info.Value->ItemsBuffer.Value, info.Value->Bump);
        }

        //private void Copy(int sourceIndex, int destIndex, int count) {
        //    Debug.Assert(destIndex + count <= info.Value->Capacity, "Bad ArenaList copy");
        //    var items = (T*)info.Value->Items.RawUnsafePointer;
        //    var source = items + sourceIndex;
        //    var dest = items + destIndex;
        //    var destSize = (info.Value->Capacity - destIndex) * sizeof(T);
        //    var bytesToCopy = (info.Value->Count - sourceIndex) * sizeof(T);
        //    Buffer.MemoryCopy(source, dest, destSize, bytesToCopy);
        //}

        private void AddCapacity() {
            // copy into new dictionary
            var newDict = new ArenaDict<TKey, TValue>(info.Arena, info.Value->BufferLength * 2);
            foreach (var kvp in this) {
                newDict.Add(kvp.Key, kvp.Value);
            }

            // free old items buffer
            var items = info.Value->ItemsBuffer;
            Arena.Free(items);

            // copy new dictionary's info
            *info.Value = *newDict.info.Value;

            // free new dictionary's info
            Arena.Free(newDict.info);
        }

        private int GetHashCode(TKey* key) {
            var hashCode = key->GetHashCode();
            if (hashCode == 0) hashCode = 0x1234ABCD;
            return hashCode;
        }

        private bool TryGetEntry(TKey* key, int hashCode, out Entry entry) {
            var index = (int)HashToIndex((uint)hashCode, info.Value->Shift);
            entry = GetIndex(index);

            if (entry.HashCode == 0) {
                // no value at index, insert
                return true;
            }
            else if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(*entry.Key, *key)) {
                // found identical key at index
                return true;
            }
            else {
                var next = entry.Next;
                while (next > 0) {
                    entry = GetOffset(next);
                    if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(*entry.Key, *key)) {
                        // found identical key in bucket
                        return true;
                    }
                    next = entry.Next;
                }
                return false;
            }
        }

        public void Add(TKey key, TValue value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Add item to UnmanagedDict<TKey, TValue>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot Add item to UnmanagedDict<TKey, TValue>: list memory has previously been freed");
            }
            Set(&key, &value, false);
        }

        private TValue* Get(TKey* key, bool throwIfNotFound) {
            var hashCode = GetHashCode(key);

            if ((!TryGetEntry(key, hashCode, out var entry) || entry.HashCode == 0) && throwIfNotFound) {
                // no key found, add to bucket
                throw new KeyNotFoundException();
            }

            return entry.Value;
        }

        private void Set(TKey* key, in TValue* value, bool allowDuplicates) {
            var hashCode = GetHashCode(key);

            if (!TryGetEntry(key, hashCode, out var entry)) {
                // no key found, add to bucket
                var head = GetHead();
                info.Value->Head = head.Next;
                head.Next = 0;
                entry.Next = head.Offset;
                entry = head;
            }

            Insert(entry, hashCode, key, value);
        }

        private void Insert(Entry entry, int hashCode, TKey* key, TValue* value) {
            entry.HashCode = hashCode;
            *entry.Key = *key;
            *entry.Value = *value;

            info.Value->Count++;
            if (info.Value->Count >= info.Value->BufferLength * 3 / 4) {
                AddCapacity();
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

        public Enumerator GetEnumerator() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot GetEnumerator for UnmanagedDict<TKey, TValue>: list has not been properly initialized with arena reference");
            }
            if (!info.HasValue) {
                throw new InvalidOperationException("Cannot GetEnumerator for UnmanagedDict<TKey, TValue>: list memory has previously been freed");
            }
            return new Enumerator(this);
        }

        //IEnumerator<TKey, TValue> IEnumerable<TKey, TValue>.GetEnumerator() {
        //    return GetEnumerator();
        //}

        //IEnumerator IEnumerable.GetEnumerator() {
        //    return GetEnumerator();
        //}

        public TValue this[TKey key] {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get item at index in UnmanagedDict<TKey, TValue>: list has not been properly initialized with arena reference");
                }
                if (!info.HasValue) {
                    throw new InvalidOperationException("Cannot get item at index in UnmanagedDict<TKey, TValue>: list memory has previously been freed");
                }
                return *Get(&key, true);
            }
            set {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot set item at index in UnmanagedDict<TKey, TValue>: list has not been properly initialized with arena reference");
                }
                if (!info.HasValue) {
                    throw new InvalidOperationException("Cannot set item at index in UnmanagedDict<TKey, TValue>: list memory has previously been freed");
                }
                Set(&key, &value, true);
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

        [Serializable]
        public struct Enumerator : IEnumerator<KeyValuePair<TKey, TValue>>, System.Collections.IEnumerator {
            private ArenaDict<TKey, TValue> dict;
            private int index;
            private int offset;
            private int version;
            private KeyValuePair<TKey, TValue> currentKey;

            internal Enumerator(ArenaDict<TKey, TValue> dict) {
                this.dict = dict;
                index = 0;
                offset = 0;
                version = dict.info.Value->Version;
                currentKey = default;
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                if (version != dict.info.Value->Version) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                while ((uint)index < (uint)dict.info.Value->BufferLength) {
                    var entry = dict.GetOffset(offset);

                    offset = entry.Next;
                    if (offset == 0) {
                        offset = ++index * dict.info.Value->EntrySize;
                    }

                    if (entry.HashCode > 0) {
                        currentKey = new KeyValuePair<TKey, TValue>(*entry.Key, *entry.Value);
                        return true;
                    }
                }

                index = dict.info.Value->BufferLength + 1;
                currentKey = default;
                return false;
            }

            public KeyValuePair<TKey, TValue> Current {
                get {
                    return currentKey;
                }
            }

            object System.Collections.IEnumerator.Current {
                get {
                    if (index == 0 || index == dict.info.Value->BufferLength + 1) {
                        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    }
                    return Current;
                }
            }

            void System.Collections.IEnumerator.Reset() {
                if (version != dict.info.Value->Version) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                index = 0;
                currentKey = default;
            }
        }
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

    internal unsafe readonly struct ArenaDictDebugView<TKey, TValue> where TKey : unmanaged where TValue : unmanaged {
        private readonly ArenaDict<TKey, TValue> dict;

        public ArenaDictDebugView(ArenaDict<TKey, TValue> dict) {
            this.dict = dict;
        }

        public KeyValuePair<TKey, TValue>[] Items {
            get {
                var items = new KeyValuePair<TKey, TValue>[dict.Count];
                int index = 0;
                foreach (var kvp in dict) {
                    items[index] = kvp;
                    index++;
                }
                return items;
            }
        }

        public int Count { get { return dict.Count; } }
        public Arena Arena { get { return dict.Arena; } }
        public bool IsAllocated { get { return dict.IsAllocated; } }
    }
}
