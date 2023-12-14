using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace Arenas {
    using static Arenas.UnmanagedListTypes;
    using static UnmanagedDictTypes;

    // TODO: Custom IEqualityComparer
    [DebuggerTypeProxy(typeof(ArenaDictDebugView<,>))]
    public unsafe struct ArenaDict<TKey, TValue> : IDictionary<TKey, TValue>, IDisposable where TKey : unmanaged where TValue : unmanaged {
        private const int defaultCapacity = 4;
        private const uint fibHashMagic = 2654435769;
        private const int noneHashCode = 0;
        private const int nullOffset = 0;

        private static int RebalanceCount(int capacity) {
            return capacity * 3 / 4;
        }

        private UnmanagedRef<UnmanagedDict> info;

        private ArenaDict(UnmanagedRef<UnmanagedDict> dictData) {
            info = dictData;
        }

        public ArenaDict(Arena arena, int capacity = defaultCapacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }

            if (capacity < 0) {
                capacity = defaultCapacity;
            }

            var typeK = TypeInfo.GenerateTypeInfo<TKey>();
            var typeV = TypeInfo.GenerateTypeInfo<TValue>();

            if (typeK.IsArenaContents) {
                throw new NotSupportedException("ArenaList cannot store keys which implement IArenaContents. Please use UnmanagedRef instead.");
            }
            if (typeV.IsArenaContents) {
                throw new NotSupportedException("ArenaList cannot store values which implement IArenaContents. Please use UnmanagedRef instead.");
            }

            // first allocate our info object
            info = arena.Allocate(new UnmanagedDict());

            // must have positive power of two capacity
            ulong powTwo = (uint)capacity;
            if (!MemHelper.IsPowerOfTwo(powTwo)) {
                if (capacity <= 0) {
                    capacity = defaultCapacity;
                }

                powTwo = MemHelper.NextPowerOfTwo((ulong)capacity);
                if (powTwo > int.MaxValue) {
                    throw new ArgumentOutOfRangeException(nameof(capacity));
                }
            }
            else if (powTwo == 1) {
                // capacity 1 doesn't really work since the rebalance count is 0
                // so it'll rebalance to 2 items as soon as you use it at all
                powTwo = 2;
            }

            // get the shift amount from the capacity which is now a power of two
            // this is used to shift the hashcode into a backing array index after
            // multiplying it by our magic fibonacci number
            capacity = (int)powTwo;
            var shift = 32 - MemHelper.PowerOfTwoLeadingZeros[powTwo];

            // because .NET standard can't have composed unmanaged types we cant allocate a memory area
            // that is a series of entry structs, and instead have to interleave entry headers with key
            // and value manually. we use our type info here in order to get all the offsets and sizes
            // and such that are needed
            var entryInfo = TypeInfo.GenerateTypeInfo<UnmanagedDictEntry>();
            var entrySize = MemHelper.AlignCeil(typeK.Size + typeV.Size + entryInfo.Size, sizeof(ulong));

            // Old comment:
            // now we know entry size, calculate the actual amount of memory needed
            // this needs to be twice the amount in case of a worst case scenario of all items mapping
            // to the same hashcode, that way we know we always have enough capacity outside of the
            // backing array 

            // New comment:
            // actually we only need as much overflow storage as 75% of the capacity in order to make
            // sure that there's always enough room for items before rebalancing, so just use the
            // rebalancing amount to determine the extra overflow storage space needed. this is still
            // 1 item more than we actually need, because even in the worst case the head is always
            // stored inside the backing array, but idk, off by one errors are scary and there's no
            // need to ditch the spare bonus item just in case
            //
            // by keeping the overflow size a little smaller than the power-of-two sized backing array
            // we usually wind up with a size a little under a power of 2 in size, which is useful
            // for memory efficiency because the arena will dole out chunks of power-of-two minus the
            // size of an item header in memory, so requesting a full power of two actually doubles the
            // amount allocated but requesting slightly less means we wind up with basically exactly
            // the amount we want so overhead becomes way lower
            var size = capacity * entrySize;
            //size *= 2; // extra space for linked lists
            size += RebalanceCount(capacity) * entrySize;
            var itemsRef = arena.AllocCount<byte>(size); // alloc memory buffer

            var backingArrayLength = capacity;
            var overflowLength = itemsRef.Size / entrySize - backingArrayLength;

            // set all our props
            var self = info.Value;

            self->Shift = shift;
            self->EntrySize = entrySize;
            self->KeyOffset = entryInfo.Size; // key comes after entry struct
            self->ValueOffset = self->KeyOffset + typeK.Size; // val comes after key

            self->ItemsBuffer = (UnmanagedRef)itemsRef;
            self->BackingArrayLength = capacity;
            self->OverflowLength = itemsRef.Size / entrySize - backingArrayLength;
            //Debug.Assert(self->OverflowLength >= self->BackingArrayLength);
            Debug.Assert(self->OverflowLength >= RebalanceCount(self->BackingArrayLength) - 1);

            // position our bump allocator to the end of the backing array
            // this is used to allocate new entries when the freelist is empty
            self->Bump = self->BackingArrayLength * self->EntrySize;
        }

        // https://probablydance.com/2018/06/16/fibonacci-hashing-the-optimization-that-the-world-forgot-or-a-better-alternative-to-integer-modulo/
        private static uint HashToIndex(uint hash, int shift) {
            hash ^= hash >> shift; // this line improves distribution but can be commented out
            return (fibHashMagic * hash) >> shift;
        }

        public void Free() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Free ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Free ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            self->Version++;
            var items = self->ItemsBuffer;
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
                throw new InvalidOperationException("Cannot Clear ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Clear ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot Clear ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            self->Version++;
            self->Count = 0;
            self->Bump = self->BackingArrayLength * self->EntrySize;
            self->Head = nullOffset;

            // zero backing array memory (Bump has been set to the end of the backing array)
            // remaining storage space doesn't need to be zeroed because the bump allocator
            // zeroes out newly allocated entries
            MemHelper.ZeroMemory(items, self->Bump);
        }

        // TODO: implement
        //public void TrimExcess(int capacity = 0) {
        //}

        private void AddCapacity(UnmanagedDict* self, ref IntPtr items) {
            // copy into new dictionary
            var newDict = new ArenaDict<TKey, TValue>(info.Arena, self->BackingArrayLength * 2);

            var entryEnumerator = new FastInternalEnumerator(this);
            while (entryEnumerator.GetNextEntry(out var entry)) {
                newDict.Add(*entry.Key, *entry.Value);
            }

            // free old items buffer
            Arena.Free(items);

            // copy new dictionary's info
            *self = *newDict.info.Value;

            // free new dictionary's info
            Arena.Free(newDict.info);

            items = self->ItemsBuffer.Value;
        }

        private int GetHashCode(TKey* key) {
            var hashCode = key->GetHashCode();
            if (hashCode == noneHashCode) hashCode = 0x1234FEDC; // hashCode cannot be 0
            return hashCode;
        }

        private bool TryGetEntry(UnmanagedDict* self, IntPtr items, TKey* _key, int hashCode, out Entry entry, out Entry? previous) {
            var index = (int)HashToIndex((uint)hashCode, self->Shift);
            entry = GetIndex(self, items, index);

            var key = *_key;

            if (entry.HashCode == noneHashCode) {
                // no value at index, insert
                previous = null;
                return true;
            }
            else if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(*entry.Key, key)) {
                // found identical key at index
                previous = null;
                return true;
            }
            else {
                // bucket exists but value wasn't at head, search nodes
                var next = entry.Next;
                while (next > nullOffset) {
                    previous = entry;
                    entry = GetOffset(self, items, next);
                    if (entry.HashCode == hashCode && EqualityComparer<TKey>.Default.Equals(*entry.Key, key)) {
                        // found identical key in bucket
                        return true;
                    }
                    next = entry.Next;
                }

                previous = null;
                return false;
            }
        }

        public void Add(TKey key, TValue value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Add item to ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Add item to ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot Add item to ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            Set(self, ref items, &key, &value, false);
        }

        void ICollection<KeyValuePair<TKey, TValue>>.Add(KeyValuePair<TKey, TValue> item) {
            Add(item.Key, item.Value);
        }

        public bool TryGetValue(TKey key, out TValue value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot TryGetValue on ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot TryGetValue on ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot TryGetValue on ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            var valRef = Get(self, items, &key, false);
            if (valRef == null) {
                value = default;
                return false;
            }
            value = *valRef;
            return true;
        }

        public bool ContainsKey(TKey key) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot ContainsKey on ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot ContainsKey on ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot ContainsKey on ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            return Get(self, items, &key, false) != null;
        }

        public bool ContainsValue(TValue value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot ContainsValue on ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot ContainsValue on ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot ContainsValue on ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            var entryEnumerator = new FastInternalEnumerator(this);
            while (entryEnumerator.GetNextEntry(out var entry)) {
                if (EqualityComparer<TValue>.Default.Equals(value, *entry.Value)) return true;
            }

            return false;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Contains(KeyValuePair<TKey, TValue> item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Contains key value pair on ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Contains key value pair on ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot Contains key value pair on ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            var key = item.Key;
            var valRef = Get(self, items, &key, false);
            if (valRef == null) {
                return false;
            }

            return EqualityComparer<TValue>.Default.Equals(*valRef, item.Value);
        }

        private TValue* Get(UnmanagedDict* self, IntPtr items, TKey* key, bool throwIfNotFound) {
            var hashCode = GetHashCode(key);

            if (!TryGetEntry(self, items, key, hashCode, out var entry, out _) || entry.HashCode == noneHashCode) {
                // no key found
                if (throwIfNotFound) {
                    throw new KeyNotFoundException();
                }
                return null;
            }

            return entry.Value;
        }

        private void Set(UnmanagedDict* self, ref IntPtr items, TKey* key, in TValue* value, bool allowDuplicates) {
            var hashCode = GetHashCode(key);

            if (!TryGetEntry(self, items, key, hashCode, out var entry, out _)) {
                // if TryGetEntry fails then `entry` is the last entry it checked
                // inside the bucket and never the head entry inside the backing
                // array, so we need to add a node after it

                // no key found, add to bucket
                var head = GetHead(self, items); // get head from freelist or bump allocator

                // remove head by advancing head to the next item
                self->Head = head.Next;

                // point popped head to nothing, point entry to popped head
                head.Next = nullOffset;
                entry.Next = head.Offset;
                entry = head;
            }

            // overwrite entry with correct values
            entry.HashCode = hashCode;
            *entry.Key = *key;
            *entry.Value = *value;

            // increment count and rebalance at 75% full
            self->Version++;
            self->Count++;
            if (self->Count >= RebalanceCount(self->BackingArrayLength)) {
                AddCapacity(self, ref items);
            }
        }

        private Entry GetIndex(UnmanagedDict* self, IntPtr items, int index) {
            return GetOffset(self, items, index * self->EntrySize);
        }

        private Entry GetOffset(UnmanagedDict* self, IntPtr items, int offset) {
            return new Entry(items + offset, self);
        }

        /// <summary>
        /// Gets but does not pop the head off the overflow area's freelist. This may
        /// instead allocate a new entry using the bump allocator if the freelist is
        /// empty
        /// </summary>
        /// <returns>Entry which point to the head entry</returns>
        private Entry GetHead(UnmanagedDict* self, IntPtr items) {
            var head = self->Head;

            if (head != nullOffset) {
                return GetOffset(self, items, head);
            }

            // no entry in freelist, allocate via bump allocator
            head = self->Bump;
            self->Bump += self->EntrySize;

            // make sure the newly allocated entry is zeroed, because
            // we only zero the backing array on clear, not the additional
            // memory areas
            var headEntry = GetOffset(self, items, head);

            // don't actually need to clear, just set Next to 0
            //headEntry.Clear();
            headEntry.Next = nullOffset;

            Debug.Assert(head < self->ItemsBuffer.Size);
            Debug.Assert(headEntry.Next == nullOffset);

            return headEntry;
        }

        public bool Remove(TKey key) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Remove item from ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Remove item from ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot Remove item from ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            var hashCode = GetHashCode(&key);
            if (!TryGetEntry(self, items, &key, hashCode, out var entry, out var _prev)) {
                return false;
            }

            if (_prev.HasValue) {
                // entry is a linked list node, so we just unlink it
                UnlinkEntry(self, entry, _prev.Value);
            }
            else {
                // entry is in main backing array
                if (entry.Next != nullOffset) {
                    // entry links to another value, copy the next value into the
                    // backing array and then unlink it
                    var next = GetOffset(self, items, entry.Next);

                    // set hashcode, key, and value but leave `next` property as
                    // is so we can subsequently call UnlinkEntry
                    entry.HashCode = next.HashCode;
                    *entry.Key = *next.Key;
                    *entry.Value = *next.Value;

                    UnlinkEntry(self, next, entry);
                }
                else {
                    // entry links to no values, clear
                    entry.Clear();
                }
            }

            self->Version++;
            self->Count--;
            return true;
        }

        private void UnlinkEntry(UnmanagedDict* self, Entry entry, Entry prev) {
            // unlink node
            prev.Next = entry.Next;

            // insert new head node into freelist
            var prevHead = self->Head;
            self->Head = entry.Offset;
            entry.Next = prevHead;
        }

        bool ICollection<KeyValuePair<TKey, TValue>>.Remove(KeyValuePair<TKey, TValue> item) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot Remove key value pair from ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot Remove key value pair from ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot Remove key value pair from ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            var key = item.Key;
            var valRef = Get(self, items, &key, false);
            if (valRef != null && EqualityComparer<TValue>.Default.Equals(*valRef, item.Value)) {
                Remove(key);
                return true;
            }
            return false;
        }

        void ICollection<KeyValuePair<TKey, TValue>>.CopyTo(KeyValuePair<TKey, TValue>[] array, int index) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot CopyTo key value pairs on ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot CopyTo key value pairs on ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot CopyTo key value pairs on ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            if (array == null) {
                throw new ArgumentNullException(nameof(array));
            }
            if (array.Rank != 1) {
                throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", nameof(array));
            }
            if (array.GetLowerBound(0) != 0) {
                throw new ArgumentException("The lower bound of target array must be zero.", nameof(array));
            }
            if ((uint)index > (uint)array.Length) {
                throw new ArgumentOutOfRangeException(nameof(index), "Non-negative number required.");
            }
            if (array.Length - index < Count) {
                throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.", nameof(array));
            }

            var entryEnumerator = new FastInternalEnumerator(this);
            while (entryEnumerator.GetNextEntry(out var entry)) {
                array[index++] = new KeyValuePair<TKey, TValue>(*entry.Key, *entry.Value);
            }
        }

        public Enumerator GetEnumerator() {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
            }

            var self = info.Value;
            if (self == null) {
                throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
            }

            var items = self->ItemsBuffer.Value;
            if (items == IntPtr.Zero) {
                throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
            }

            return new Enumerator(this);
        }

        IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public UnmanagedRef<UnmanagedDict> GetUnderlyingReference() {
            return info;
        }

        public static explicit operator ArenaDict<TKey, TValue>(UnmanagedRef<UnmanagedDict> dictData) {
            return new ArenaDict<TKey, TValue>(dictData);
        }

        public TValue this[TKey key] {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
                }

                var self = info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot get item at index in ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
                }

                return *Get(self, items, &key, true);
            }
            set {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot set item at index in ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
                }

                var self = info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot set item at index in ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot set item at index in ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
                }

                Set(self, ref items, &key, &value, true);
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

        public KeyCollection Keys {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get Keys for ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
                }

                var self = info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot get Keys for ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot get Keys for ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
                }

                return new KeyCollection(this); 
            } 
        }
        public ValueCollection Values {
            get {
                if (Arena is null) {
                    throw new InvalidOperationException("Cannot get Values for ArenaDict<TKey, TValue>: dictionary has not been properly initialized with arena reference");
                }

                var self = info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot get Values for ArenaDict<TKey, TValue>: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot get Values for ArenaDict<TKey, TValue>: dictionary's backing array has previously been freed");
                }

                return new ValueCollection(this); 
            } 
        }

        public bool IsAllocated { get { return info.HasValue; } }
        public Arena Arena { get { return info.Arena; } }
        bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly { get { return false; } }

        ICollection<TKey> IDictionary<TKey, TValue>.Keys { get { return Keys; } }
        ICollection<TValue> IDictionary<TKey, TValue>.Values { get { return Values; } }

        /// <summary>
        /// Wrapper struct around a memory area representing an UnmanagedDictEntry, key, and value
        /// </summary>
        private unsafe readonly struct Entry {
            public readonly IntPtr Pointer;
            public readonly UnmanagedDict* Dict;

            public Entry(IntPtr pointer, UnmanagedDict* dict) {
                Pointer = pointer;
                Dict = dict;
            }

            public void Clear() {
                *(UnmanagedDictEntry*)Pointer = default;
                *Key = default;
                *Value = default;
            }

            public override string ToString() {
                return $"Entry({*Key}={*Value}, HashCode={HashCode}, Next={Next}, Offset={Offset})";
            }

            /// <summary>
            /// Offset of this entry from the start of the memory storage area for the dictionary's items
            /// </summary>
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
            private int headOffset;
            private int version;
            private int count;
            private KeyValuePair<TKey, TValue> current;

            internal Enumerator(ArenaDict<TKey, TValue> dict) {
                this.dict = dict;
                var dictPtr = dict.info.Value;
                index = 0;
                offset = 0;
                headOffset = 0;
                version = dictPtr->Version;
                count = dictPtr->BackingArrayLength;
                current = default;
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                var dictPtr = dict.info.Value;
                if (dictPtr == null || version != dictPtr->Version) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                var itemsPtr = dictPtr->ItemsBuffer.Value;
                if (itemsPtr == IntPtr.Zero) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                while ((uint)index < (uint)count) {
                    // get current entry
                    var entry = dict.GetOffset(dictPtr, itemsPtr, offset);

                    // move to next entry associated with the current index
                    offset = entry.Next;
                    if (offset == nullOffset) {
                        // if the position of the next entry is zero then there are no further
                        // entries at this index, increment the index and set the new offset to
                        // the head of the list (the entry in the backing array)
                        headOffset += dictPtr->EntrySize;
                        offset = headOffset;
                        index++;
                    }

                    // only entries with a hashcode which isn't zero are valid entries
                    if (entry.HashCode != noneHashCode) {
                        current = new KeyValuePair<TKey, TValue>(*entry.Key, *entry.Value);
                        return true;
                    }
                }

                index = count + 1;
                current = default;
                return false;
            }

            public KeyValuePair<TKey, TValue> Current {
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
                var dictPtr = dict.info.Value;
                if (dictPtr == null || version != dictPtr->Version || !dictPtr->ItemsBuffer.HasValue) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                index = 0;
                offset = 0;
                headOffset = 0;
                current = default;
            }
        }

        /// <summary>
        /// Used in place of `foreach (var kvp in this)` internally since it's more efficient
        /// </summary>
        private struct FastInternalEnumerator {
            private ArenaDict<TKey, TValue> dict;
            private int index;
            private int offset;
            private int headOffset;
            private int count;
            private UnmanagedDict* dictPtr;
            private IntPtr itemsPtr;

            internal FastInternalEnumerator(ArenaDict<TKey, TValue> dict) {
                this.dict = dict;
                dictPtr = dict.info.Value;
                index = 0;
                offset = 0;
                headOffset = 0;
                count = dictPtr->BackingArrayLength;
                itemsPtr = dictPtr->ItemsBuffer.Value;
            }

            public bool GetNextEntry(out Entry result) {
                while ((uint)index < (uint)count) {
                    // get current entry
                    var entry = dict.GetOffset(dictPtr, itemsPtr, offset);

                    // move to next entry associated with the current index
                    offset = entry.Next;
                    if (offset == nullOffset) {
                        // if the position of the next entry is zero then there are no further
                        // entries at this index, increment the index and set the new offset to
                        // the head of the list (the entry in the backing array)
                        headOffset += dictPtr->EntrySize;
                        offset = headOffset;
                        index++;
                    }

                    // only entries with a hashcode which isn't zero are valid entries
                    if (entry.HashCode != noneHashCode) {
                        result = entry;
                        return true;
                    }
                }

                index = count + 1;
                result = default;
                return false;
            }
        }

        #region Key and value collections
        public readonly struct KeyCollection : ICollection<TKey>, IEnumerable<TKey>, IReadOnlyCollection<TKey> {
            private readonly ArenaDict<TKey, TValue> dict;

            public KeyCollection(ArenaDict<TKey, TValue> dict) {
                this.dict = dict;
            }

            public Enumerator GetEnumerator() {
                if (dict.Arena is null) {
                    throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>.KeyCollection: dictionary has not been properly initialized with arena reference");
                }

                var self = dict.info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>.KeyCollection: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>.KeyCollection: dictionary's backing array has previously been freed");
                }

                return new Enumerator(dict);
            }

            public void CopyTo(TKey[] array, int index) {
                if (dict.Arena is null) {
                    throw new InvalidOperationException("Cannot CopyTo on ArenaDict<TKey, TValue>.KeyCollection: dictionary has not been properly initialized with arena reference");
                }

                var self = dict.info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot CopyTo on ArenaDict<TKey, TValue>.KeyCollection: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot CopyTo on ArenaDict<TKey, TValue>.KeyCollection: dictionary's backing array has previously been freed");
                }

                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }
                if (array.Rank != 1) {
                    throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", nameof(array));
                }
                if (array.GetLowerBound(0) != 0) {
                    throw new ArgumentException("The lower bound of target array must be zero.", nameof(array));
                }
                if ((uint)index > (uint)array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), "Non-negative number required.");
                }
                if (array.Length - index < Count) {
                    throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.", nameof(array));
                }

                var entryEnumerator = new FastInternalEnumerator(dict);
                while (entryEnumerator.GetNextEntry(out var entry)) {
                    array[index++] = *entry.Key;
                }
            }

            public int Count => dict.Count;

            bool ICollection<TKey>.IsReadOnly => true;

            void ICollection<TKey>.Add(TKey item) =>
                throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");

            bool ICollection<TKey>.Remove(TKey item) =>
                throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");

            void ICollection<TKey>.Clear() =>
                throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");

            bool ICollection<TKey>.Contains(TKey item) => dict.ContainsKey(item);

            IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<TKey>, System.Collections.IEnumerator {
                private ArenaDict<TKey, TValue> dict;
                private int index;
                private int offset;
                private int headOffset;
                private int version;
                private int count;
                private TKey currentKey;

                internal Enumerator(ArenaDict<TKey, TValue> dict) {
                    this.dict = dict;
                    var dictPtr = dict.info.Value;
                    index = 0;
                    offset = 0;
                    headOffset = 0;
                    version = dictPtr->Version;
                    count = dictPtr->BackingArrayLength;
                    currentKey = default;
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    var dictPtr = dict.info.Value;
                    if (dictPtr == null || version != dictPtr->Version) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    var itemsPtr = dictPtr->ItemsBuffer.Value;
                    if (itemsPtr == IntPtr.Zero) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    while ((uint)index < (uint)count) {
                        // get current entry
                        var entry = dict.GetOffset(dictPtr, itemsPtr, offset);

                        // move to next entry associated with the current index
                        offset = entry.Next;
                        if (offset == nullOffset) {
                            // if the position of the next entry is zero then there are no further
                            // entries at this index, increment the index and set the new offset to
                            // the head of the list (the entry in the backing array)
                            headOffset += dictPtr->EntrySize;
                            offset = headOffset;
                            index++;
                        }

                        // only entries with a hashcode which isn't zero are valid entries
                        if (entry.HashCode != noneHashCode) {
                            currentKey = *entry.Key;
                            return true;
                        }
                    }

                    index = count + 1;
                    currentKey = default;
                    return false;
                }

                public TKey Current {
                    get {
                        return currentKey;
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
                    var dictPtr = dict.info.Value;
                    if (dictPtr == null || version != dictPtr->Version || !dictPtr->ItemsBuffer.HasValue) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    index = 0;
                    offset = 0;
                    headOffset = 0;
                    currentKey = default;
                }
            }
        }

        public readonly struct ValueCollection : ICollection<TValue>, IEnumerable<TValue>, IReadOnlyCollection<TValue> {
            private readonly ArenaDict<TKey, TValue> dict;

            public ValueCollection(ArenaDict<TKey, TValue> dict) {
                this.dict = dict;
            }

            public Enumerator GetEnumerator() {
                if (dict.Arena is null) {
                    throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>.ValueCollection: dictionary has not been properly initialized with arena reference");
                }

                var self = dict.info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>.ValueCollection: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot GetEnumerator for ArenaDict<TKey, TValue>.ValueCollection: dictionary's backing array has previously been freed");
                }

                return new Enumerator(dict);
            }

            public void CopyTo(TValue[] array, int index) {
                if (dict.Arena is null) {
                    throw new InvalidOperationException("Cannot CopyTo on ArenaDict<TKey, TValue>.ValueCollection: dictionary has not been properly initialized with arena reference");
                }

                var self = dict.info.Value;
                if (self == null) {
                    throw new InvalidOperationException("Cannot CopyTo on ArenaDict<TKey, TValue>.ValueCollection: dictionary memory has previously been freed");
                }

                var items = self->ItemsBuffer.Value;
                if (items == IntPtr.Zero) {
                    throw new InvalidOperationException("Cannot CopyTo on ArenaDict<TKey, TValue>.ValueCollection: dictionary's backing array has previously been freed");
                }

                if (array == null) {
                    throw new ArgumentNullException(nameof(array));
                }
                if (array.Rank != 1) {
                    throw new ArgumentException("Only single dimensional arrays are supported for the requested action.", nameof(array));
                }
                if (array.GetLowerBound(0) != 0) {
                    throw new ArgumentException("The lower bound of target array must be zero.", nameof(array));
                }
                if ((uint)index > (uint)array.Length) {
                    throw new ArgumentOutOfRangeException(nameof(index), "Non-negative number required.");
                }
                if (array.Length - index < Count) {
                    throw new ArgumentException("Destination array is not long enough to copy all the items in the collection. Check array index and length.", nameof(array));
                }

                var entryEnumerator = new FastInternalEnumerator(dict);
                while (entryEnumerator.GetNextEntry(out var entry)) {
                    array[index++] = *entry.Value;
                }
            }

            public int Count => dict.Count;

            bool ICollection<TValue>.IsReadOnly => true;

            void ICollection<TValue>.Add(TValue item) =>
                throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");

            bool ICollection<TValue>.Remove(TValue item) =>
                throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");

            void ICollection<TValue>.Clear() =>
                throw new NotSupportedException("Mutating a value collection derived from a dictionary is not allowed.");

            bool ICollection<TValue>.Contains(TValue item) => dict.ContainsValue(item);

            IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public struct Enumerator : IEnumerator<TValue>, System.Collections.IEnumerator {
                private ArenaDict<TKey, TValue> dict;
                private int index;
                private int offset;
                private int headOffset;
                private int version;
                private int count;
                private TValue currentValue;

                internal Enumerator(ArenaDict<TKey, TValue> dict) {
                    this.dict = dict;
                    var dictPtr = dict.info.Value;
                    index = 0;
                    offset = 0;
                    headOffset = 0;
                    version = dictPtr->Version;
                    count = dictPtr->BackingArrayLength;
                    currentValue = default;
                }

                public void Dispose() {
                }

                public bool MoveNext() {
                    var dictPtr = dict.info.Value;
                    if (dictPtr == null || version != dictPtr->Version) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    var itemsPtr = dictPtr->ItemsBuffer.Value;
                    if (itemsPtr == IntPtr.Zero) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    while ((uint)index < (uint)count) {
                        // get current entry
                        var entry = dict.GetOffset(dictPtr, itemsPtr, offset);

                        // move to next entry associated with the current index
                        offset = entry.Next;
                        if (offset == nullOffset) {
                            // if the position of the next entry is zero then there are no further
                            // entries at this index, increment the index and set the new offset to
                            // the head of the list (the entry in the backing array)
                            headOffset += dictPtr->EntrySize;
                            offset = headOffset;
                            index++;
                        }

                        // only entries with a hashcode which isn't zero are valid entries
                        if (entry.HashCode != noneHashCode) {
                            currentValue = *entry.Value;
                            return true;
                        }
                    }

                    index = count + 1;
                    currentValue = default;
                    return false;
                }

                public TValue Current {
                    get {
                        return currentValue;
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
                    var dictPtr = dict.info.Value;
                    if (dictPtr == null || version != dictPtr->Version || !dictPtr->ItemsBuffer.HasValue) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }

                    index = 0;
                    offset = 0;
                    headOffset = 0;
                    currentValue = default;
                }
            }
        }
        #endregion
    }

    public static class UnmanagedDictTypes {
        [StructLayout(LayoutKind.Sequential)]
        public struct UnmanagedDict {
            /// <summary>
            /// Raw memory buffer that contains dictionary entries
            /// </summary>
            public UnmanagedRef ItemsBuffer;
            /// <summary>
            /// Length in number of items of the backing array that holds the linked list head entries,
            /// which should always be a power of two
            /// </summary>
            public int BackingArrayLength;
            /// <summary>
            /// Length in number of items that the remaining raw memory buffer can store,
            /// which should always be greater than or equal to the backing array length
            /// </summary>
            public int OverflowLength;
            /// <summary>
            /// Number of items in the dictionary
            /// </summary>
            public int Count;
            /// <summary>
            /// Number of bits to shift to the right to get a valid backing array index
            /// after applying fibonacci hasing (see HashToIndex)
            /// </summary>
            public int Shift;
            /// <summary>
            /// Version number, increased with every mutation of the contained data
            /// </summary>
            public int Version;
            /// <summary>
            /// Size of each dictionary entry in bytes
            /// </summary>
            public int EntrySize;
            /// <summary>
            /// Byte offset from the start of an entry (which contains an UnmanagedDictEntry)
            /// to the entry's key
            /// </summary>
            public int KeyOffset;
            /// <summary>
            /// Byte offset from the start of an entry (which contains an UnmanagedDictEntry)
            /// to the entry's value
            /// </summary>
            public int ValueOffset;
            /// <summary>
            /// The head of the freelist of entries removed from the dictionary which resided
            /// outside of the backing array, which is used to allocate entries if not zero.
            /// This is the offset in bytes inside the raw memory buffer to get to the entry
            /// </summary>
            public int Head;
            /// <summary>
            /// Position of the bump allocator, used to allocate entries when the freelist is
            /// empty. This is the offset in bytes inside the raw memory buffer to get to the entry
            /// </summary>
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
                    ((ICollection<KeyValuePair<TKey, TValue>>)dict).CopyTo(items, 0);
                    return items;
                }
            }

            public int Count { get { return dict.Count; } }
            public Arena Arena { get { return dict.Arena; } }
            public bool IsAllocated { get { return dict.IsAllocated; } }
        }
    }
}
