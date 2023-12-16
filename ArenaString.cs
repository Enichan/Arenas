using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace Arenas {
    // TODO: to int and to double
    // TODO: from int and from double
    // TODO: IFormattable
    // TODO: IComparable
    // TODO: switch to `using` blocks for temporary instances of ArenaString and ArenaList
    public unsafe readonly struct ArenaString : IEnumerable<char>, IEquatable<ArenaString>, IEquatable<string>, IDisposable {
        private const int minCapacity = (16 - sizeof(int)) / sizeof(char);
        private const int contentsOffset = 2;
        private const int contentsOffsetBytes = 2 * sizeof(char);

        static ArenaString() {
            Debug.Assert(sizeof(int) == sizeof(char) * 2);
        }

        private readonly UnmanagedRef<char> contents;

        private ArenaString(UnmanagedRef<char> contents) {
            this.contents = contents;
        }

        public ArenaString(Arena arena, string source) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            if (source is null) {
                throw new ArgumentNullException(nameof(source));
            }

            var capacity = source.Length;
            contents = arena.AllocCount<char>(capacity + contentsOffset);
            CopyFrom(source);
        }

        public ArenaString(Arena arena, int capacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            if (capacity < minCapacity) {
                capacity = minCapacity;
            }

            contents = arena.AllocCount<char>(capacity + contentsOffset);
        }

        public ArenaString(Arena arena, ArenaString source) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }

            var sourceArena = source.Arena;
            if (sourceArena is null) {
                throw new InvalidOperationException("Cannot create new ArenaString from ArenaString: source string has not been properly initialized with arena reference");
            }

            var sourceContents = source.Contents;
            if (sourceContents == null) {
                throw new InvalidOperationException("Cannot create new ArenaString from ArenaString: source string memory has previously been freed");
            }

            var sourceLength = source.Length;
            var capacity = sourceLength;
            contents = arena.AllocCount<char>(capacity + contentsOffset);

            if (!TryCopyFrom(source, 0, sourceLength, arena, null, capacity, sourceArena, sourceContents, sourceLength)) {
                throw new InvalidOperationException("ArenaString copy failed unexpectedly");
            }
        }

        #region CopyFrom
        private char* InitCopy(out int capacity) {
            var arena = contents.Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot CopyFrom in ArenaString: string has not been properly initialized with arena reference");
            }

            var ptr = contents.Value;
            if (ptr == null) {
                throw new InvalidOperationException("Cannot CopyFrom in ArenaString: string memory has previously been freed");
            }

            capacity = contents.ElementCount - contentsOffset;
            return ptr + contentsOffset;
        }

        #region Copy from string
        public bool TryCopyFrom(string str, int index, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (str is null) {
                throw new ArgumentNullException(nameof(str));
            }
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > str.Length) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int capacity;
            var cur = InitCopy(out capacity);

            if (length > capacity) {
                return false;
            }

            var end = index + length;
            for (int i = index; i < end; i++) {
                *(cur++) = str[i];
            }

            Length = length;
            return true;
        }

        public void CopyFrom(string str, int index, int length) {
            if (!TryCopyFrom(str)) {
                throw new InvalidOperationException("Cannot CopyFrom string in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(string str, int index) {
            return TryCopyFrom(str, index, str.Length - index);
        }

        public bool TryCopyFrom(string str) {
            return TryCopyFrom(str, 0, str.Length);
        }

        public void CopyFrom(string str, int index) {
            CopyFrom(str, index, str.Length - index);
        }

        public void CopyFrom(string str) {
            CopyFrom(str, 0, str.Length);
        }
        #endregion

        #region Copy from StringBuilder
        public bool TryCopyFrom(StringBuilder str, int index, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (str is null) {
                throw new ArgumentNullException(nameof(str));
            }
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > str.Length) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int capacity;
            var cur = InitCopy(out capacity);

            if (length > capacity) {
                return false;
            }

            var end = index + length;
            for (int i = index; i < end; i++) {
                *(cur++) = str[i];
            }

            Length = length;
            return true;
        }

        public void CopyFrom(StringBuilder str, int index, int length) {
            if (!TryCopyFrom(str)) {
                throw new InvalidOperationException("Cannot CopyFrom StringBuilder in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(StringBuilder str, int index) {
            return TryCopyFrom(str, index, str.Length - index);
        }

        public bool TryCopyFrom(StringBuilder str) {
            return TryCopyFrom(str, 0, str.Length);
        }

        public void CopyFrom(StringBuilder str, int index) {
            CopyFrom(str, index, str.Length - index);
        }

        public void CopyFrom(StringBuilder str) {
            CopyFrom(str, 0, str.Length);
        }
        #endregion

        #region Copy from char[]
        public bool TryCopyFrom(char[] chars, int index, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (chars is null) {
                throw new ArgumentNullException(nameof(chars));
            }
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > chars.Length) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int capacity;
            var cur = InitCopy(out capacity);

            if (length > capacity) {
                return false;
            }

            Marshal.Copy(chars, index, (IntPtr)cur, length);
            Length = length;
            return true;
        }

        public void CopyFrom(char[] chars, int index, int length) {
            if (!TryCopyFrom(chars, index, length)) {
                throw new InvalidOperationException("Cannot CopyFrom char[] in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(char[] chars, int index) {
            return TryCopyFrom(chars, index, chars.Length - index);
        }

        public bool TryCopyFrom(char[] chars) {
            return TryCopyFrom(chars, 0, chars.Length);
        }

        public void CopyFrom(char[] chars, int index) {
            CopyFrom(chars, index, chars.Length - index);
        }

        public void CopyFrom(char[] chars) {
            CopyFrom(chars, 0, chars.Length);
        }
        #endregion

        #region Copy from char*
        private bool TryCopyFrom(char* charPtr, int length, int capacity, char* dest) {
            if (length > capacity) {
                return false;
            }

            Buffer.MemoryCopy(charPtr, dest, capacity * sizeof(char), length * sizeof(char));
            Length = length;
            return true;
        }

        public bool TryCopyFrom(char* charPtr, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (charPtr == null) {
                throw new ArgumentNullException(nameof(charPtr));
            }
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            int capacity;
            var dest = InitCopy(out capacity);
            return TryCopyFrom(charPtr, length, capacity, dest);
        }

        public void CopyFrom(char* ptr, int length) {
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (ptr == null) {
                throw new ArgumentNullException(nameof(ptr));
            }
            if (!TryCopyFrom(ptr, length)) {
                throw new InvalidOperationException("Cannot CopyFrom char* in ArenaString: insufficient capacity");
            }
        }
        #endregion

        #region Copy from ArenaString
        private bool TryCopyFrom(ArenaString source, int index, int length, Arena selfArena = null, char* selfContents = null, int selfCapacity = -1, Arena sourceArena = null, char* sourceContents = null, int sourceLength = -1) {
            if (length == 0) {
                Length = 0;
                return true;
            }

            if (selfArena == null) {
                selfArena = Arena;
                if (selfArena == null) {
                    throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: destination string has not been properly initialized with arena reference");
                }
            }

            if (selfContents == null) {
                selfContents = Contents;
                if (selfContents == null) {
                    throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: destination string memory has previously been freed");
                }
            }

            if (selfCapacity < 0) {
                selfCapacity = Capacity;
            }

            if (sourceArena == null) {
                sourceArena = source.Arena;
                if (sourceArena == null) {
                    throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string has not been properly initialized with arena reference");
                }
            }

            if (sourceContents == null) {
                sourceContents = source.Contents;
                if (sourceContents == null) {
                    throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string memory has previously been freed");
                }
            }

            if (sourceLength < 0) {
                sourceLength = source.Length;
            }

            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > sourceLength) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return TryCopyFrom(sourceContents + index, length, selfCapacity, selfContents);
        }

        public bool TryCopyFrom(ArenaString source, int index, int length) {
            return TryCopyFrom(source, index, length);
        }

        public bool TryCopyFrom(ArenaString source, int index) {
            var sourceArena = source.Arena;
            if (sourceArena == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string has not been properly initialized with arena reference");
            }

            var sourceContents = source.Contents;
            if (sourceContents == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string memory has previously been freed");
            }

            var sourceLength = source.Length;
            return TryCopyFrom(source, index, sourceLength - index, sourceArena: sourceArena, sourceContents: sourceContents, sourceLength: sourceLength);
        }

        public bool TryCopyFrom(ArenaString source) {
            var sourceArena = source.Arena;
            if (sourceArena == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string has not been properly initialized with arena reference");
            }

            var sourceContents = source.Contents;
            if (sourceContents == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string memory has previously been freed");
            }

            var sourceLength = source.Length;
            return TryCopyFrom(source, 0, sourceLength, sourceArena: sourceArena, sourceContents: sourceContents, sourceLength: sourceLength);
        }

        public void CopyFrom(ArenaString source, int index, int length) {
            if (!TryCopyFrom(source, index, length)) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: insufficient capacity");
            }
        }

        public void CopyFrom(ArenaString source, int index) {
            var sourceArena = source.Arena;
            if (sourceArena == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string has not been properly initialized with arena reference");
            }

            var sourceContents = source.Contents;
            if (sourceContents == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string memory has previously been freed");
            }

            var sourceLength = source.Length;
            if (!TryCopyFrom(source, index, sourceLength - index, sourceArena: sourceArena, sourceContents: sourceContents, sourceLength: sourceLength)) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: insufficient capacity");
            }
        }

        public void CopyFrom(ArenaString source) {
            var sourceArena = source.Arena;
            if (sourceArena == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string has not been properly initialized with arena reference");
            }

            var sourceContents = source.Contents;
            if (sourceContents == null) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: source string memory has previously been freed");
            }

            var sourceLength = source.Length;
            if (!TryCopyFrom(source, 0, sourceLength, sourceArena: sourceArena, sourceContents: sourceContents, sourceLength: sourceLength)) {
                throw new InvalidOperationException("Cannot CopyFrom ArenaString in ArenaString: insufficient capacity");
            }
        }
        #endregion

        #region Copy from List<char>
        public bool TryCopyFrom(List<char> list, int index, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (list is null) {
                throw new ArgumentNullException(nameof(list));
            }
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > list.Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int capacity;
            var cur = InitCopy(out capacity);

            if (length > capacity) {
                return false;
            }

            var end = index + length;
            for (int i = index; i < end; i++) {
                *(cur++) = list[i];
            }

            Length = length;
            return true;
        }

        public void CopyFrom(List<char> list, int index, int length) {
            if (!TryCopyFrom(list, index, length)) {
                throw new InvalidOperationException("Cannot CopyFrom List<char> in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(List<char> list, int index) {
            return TryCopyFrom(list, index, list.Count - index);
        }

        public bool TryCopyFrom(List<char> list) {
            return TryCopyFrom(list, 0, list.Count);
        }

        public void CopyFrom(List<char> list, int index) {
            CopyFrom(list, index, list.Count - index);
        }

        public void CopyFrom(List<char> list) {
            CopyFrom(list, 0, list.Count);
        }
        #endregion

        #region Copy from IList<char>
        public bool TryCopyFrom(IList<char> list, int index, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (list is null) {
                throw new ArgumentNullException(nameof(list));
            }
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > list.Count) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            int capacity;
            var cur = InitCopy(out capacity);

            if (length > capacity) {
                return false;
            }

            var end = index + length;
            for (int i = index; i < end; i++) {
                *(cur++) = list[i];
            }

            Length = length;
            return true;
        }

        public void CopyFrom(IList<char> list, int index, int length) {
            if (!TryCopyFrom(list, index, length)) {
                throw new InvalidOperationException("Cannot CopyFrom List<char> in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(IList<char> list, int index) {
            return TryCopyFrom(list, index, list.Count - index);
        }

        public bool TryCopyFrom(IList<char> list) {
            return TryCopyFrom(list, 0, list.Count);
        }

        public void CopyFrom(IList<char> list, int index) {
            CopyFrom(list, index, list.Count - index);
        }

        public void CopyFrom(IList<char> list) {
            CopyFrom(list, 0, list.Count);
        }
        #endregion

        #region Copy from ICollection<char>
        public bool TryCopyFrom(ICollection<char> col, int length) {
            if (length == 0) {
                Length = 0;
                return true;
            }
            if (col is null) {
                throw new ArgumentNullException(nameof(col));
            }
            if (length < 0 || length > col.Count) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            int capacity;
            var cur = InitCopy(out capacity);

            if (length > capacity) {
                return false;
            }

            foreach (var c in col) {
                *(cur++) = c;
            }

            Length = length;
            return true;
        }

        public void CopyFrom(ICollection<char> col, int length) {
            if (!TryCopyFrom(col, length)) {
                throw new InvalidOperationException("Cannot CopyFrom ICollection<char> in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(ICollection<char> col) {
            return TryCopyFrom(col, col.Count);
        }

        public void CopyFrom(ICollection<char> col) {
            CopyFrom(col, col.Count);
        }
        #endregion
        #endregion

        #region (Last)IndexOf
        #region (Last)IndexOf char
        private int IndexOf(char c, int index, int length, int selfLength, bool reverse, bool checkArena = true, char* contents = null) {
            if (index < 0 || index > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (length < 0 || index + length > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            if (checkArena && Arena is null) {
                throw new InvalidOperationException("Cannot IndexOf in ArenaString: string has not been properly initialized with arena reference");
            }

            if (contents == null) {
                contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot IndexOf in ArenaString: string memory has previously been freed");
                }
            }

            var end = index + length;
            if (reverse) {
                for (int i = end - 1; i >= index; i--) {
                    if (contents[i] == c) {
                        return i;
                    }
                }
            }
            else {
                for (int i = index; i < end; i++) {
                    if (contents[i] == c) {
                        return i;
                    }
                }
            }
            return -1;
        }

        public int IndexOf(char c, int index, int length) {
            return IndexOf(c, index, length, Length, false);
        }

        public int IndexOf(char c, int index) {
            var selfLength = Length;
            return IndexOf(c, index, selfLength - index, selfLength, false);
        }

        public int IndexOf(char c) {
            var selfLength = Length;
            return IndexOf(c, 0, selfLength, selfLength, false);
        }

        public int LastIndexOf(char c, int index, int length) {
            return IndexOf(c, index, length, Length, true);
        }

        public int LastIndexOf(char c, int index) {
            var selfLength = Length;
            return IndexOf(c, index, selfLength - index, selfLength, true);
        }

        public int LastIndexOf(char c) {
            var selfLength = Length;
            return IndexOf(c, 0, selfLength, selfLength, true);
        }
        #endregion

        #region (Last)IndexOf string
        private int IndexOf(string source, int searchIndex, int searchLength, int sourceIndex, int sourceLength, int selfLength, bool reverse, bool checkArena = true, char* contents = null) {
            if (source is null) {
                throw new ArgumentNullException(nameof(source));
            }
            if (searchIndex < 0 || searchIndex > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(searchIndex));
            }
            if (searchLength < 0 || searchIndex + searchLength > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(searchLength));
            }
            if (sourceIndex < 0 || sourceIndex > source.Length) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }
            if (sourceIndex < 0 || sourceIndex + sourceLength > source.Length) {
                throw new ArgumentOutOfRangeException(nameof(sourceLength));
            }
            if (source == "" || sourceLength == 0) {
                return 0;
            }

            if (checkArena && Arena is null) {
                throw new InvalidOperationException("Cannot IndexOf in ArenaString: string has not been properly initialized with arena reference");
            }

            if (contents == null) {
                contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot IndexOf in ArenaString: string memory has previously been freed");
                }
            }

            var end = searchIndex + searchLength;
            end -= sourceLength - 1;

            var sourceEnd = sourceIndex + sourceLength;

            if (reverse) {
                for (int i = end - 1; i >= searchIndex; i--) {
                    var cmpi = i;
                    for (int j = sourceIndex; j < sourceEnd; j++, cmpi++) {
                        if (contents[cmpi] != source[j]) {
                            cmpi = -1;
                            break;
                        }
                    }
                    if (cmpi > -1) {
                        return i;
                    }
                }
            }
            else {
                for (int i = searchIndex; i < end; i++) {
                    var cmpi = i;
                    for (int j = sourceIndex; j < sourceEnd; j++, cmpi++) {
                        if (contents[cmpi] != source[j]) {
                            cmpi = -1;
                            break;
                        }
                    }
                    if (cmpi > -1) {
                        return i;
                    }
                }
            }

            return -1;
        }

        public int IndexOf(string source, int searchIndex, int searchLength, int sourceIndex, int sourceLength) {
            return IndexOf(source, searchIndex, searchLength, sourceIndex, sourceLength, Length, false);
        }

        public int IndexOf(string str, int index, int length) {
            return IndexOf(str, index, length, 0, str.Length, Length, false);
        }

        public int IndexOf(string str, int index) {
            var selfLength = Length;
            return IndexOf(str, index, selfLength - index, 0, str.Length, selfLength, false);
        }

        public int IndexOf(string str) {
            var selfLength = Length;
            return IndexOf(str, 0, selfLength, 0, str.Length, selfLength, false);
        }

        public int LastIndexOf(string source, int searchIndex, int searchLength, int sourceIndex, int sourceLength) {
            return IndexOf(source, searchIndex, searchLength, sourceIndex, sourceLength, Length, true);
        }

        public int LastIndexOf(string str, int index, int length) {
            return IndexOf(str, index, length, 0, str.Length, Length, true);
        }

        public int LastIndexOf(string str, int index) {
            var selfLength = Length;
            return IndexOf(str, index, selfLength - index, 0, str.Length, selfLength, true);
        }

        public int LastIndexOf(string str) {
            var selfLength = Length;
            return IndexOf(str, 0, selfLength, 0, str.Length, selfLength, true);
        }
        #endregion
        #endregion

        #region (Last)IndexOfAny
        private int IndexOfAny(char[] chars, int index, int length, int selfLength, bool reverse) {
            if (chars is null) {
                throw new ArgumentNullException(nameof(chars));
            }
            if (index < 0 || index > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (length < 0 || index + length > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (chars.Length == 0) {
                return 0;
            }

            if (Arena is null) {
                throw new InvalidOperationException("Cannot IndexOfAny in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot IndexOfAny in ArenaString: string memory has previously been freed");
            }

            var end = index + length;

            if (reverse) {
                for (int i = end - 1; i >= index; i--) {
                    var c = contents[i];
                    for (int j = 0; j < chars.Length; j++) {
                        if (c == chars[j]) {
                            return i;
                        }
                    }
                }
            }
            else {
                for (int i = index; i < end; i++) {
                    var c = contents[i];
                    for (int j = 0; j < chars.Length; j++) {
                        if (c == chars[j]) {
                            return i;
                        }
                    }
                }
            }

            return -1;
        }

        public int IndexOfAny(char[] chars, int index, int length) {
            return IndexOfAny(chars, index, length, Length, false);
        }

        public int IndexOfAny(char[] chars, int index) {
            var selfLength = Length;
            return IndexOfAny(chars, index, selfLength - index, selfLength, false);
        }

        public int IndexOfAny(char[] chars) {
            var selfLength = Length;
            return IndexOfAny(chars, 0, selfLength, selfLength, false);
        }

        public int LastIndexOfAny(char[] chars, int index, int length) {
            return IndexOfAny(chars, index, length, Length, true);
        }

        public int LastIndexOfAny(char[] chars, int index) {
            var selfLength = Length;
            return IndexOfAny(chars, index, selfLength - index, selfLength, true);
        }

        public int LastIndexOfAny(char[] chars) {
            var selfLength = Length;
            return IndexOfAny(chars, 0, selfLength, selfLength, true);
        }
        #endregion

        #region Contains
        public bool Contains(char c) {
            return IndexOf(c) >= 0;
        }

        public bool Contains(string str) {
            return IndexOf(str) >= 0;
        }
        #endregion

        #region CopyTo
        public void CopyTo(int sourceIndex, char* destination, int count) {
            var selfLength = Length;
            if (destination == null) {
                throw new ArgumentNullException(nameof(destination));
            }
            if (sourceIndex < 0 || sourceIndex > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(sourceIndex));
            }
            if (count < 0 || sourceIndex + count > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            if (count == 0) {
                return;
            }

            if (Arena is null) {
                throw new InvalidOperationException("Cannot CopyTo in ArenaString: string has not been properly initialized with arena reference");
            }

            var src = Contents;
            if (src == null) {
                throw new InvalidOperationException("Cannot CopyTo in ArenaString: string memory has previously been freed");
            }

            src += sourceIndex;
            var bytes = count * sizeof(char);
            Buffer.MemoryCopy(src, destination, bytes, bytes);
        }

        public void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
            if (destination is null) {
                throw new ArgumentNullException(nameof(destination));
            }
            if (destinationIndex < 0 || destinationIndex > destination.Length) {
                throw new ArgumentOutOfRangeException(nameof(destinationIndex));
            }
            if (destinationIndex + count > destination.Length) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
            fixed (char* charPtr = destination) {
                CopyTo(sourceIndex, charPtr, count);
            }
        }
        #endregion

        #region EndsWith
        public bool EndsWith(char value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot EndsWith in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot EndsWith in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            if (selfLength < 1) {
                return false;
            }

            return IndexOf(value, selfLength - 1, 1, selfLength, false, false, contents) > -1;
        }

        public bool EndsWith(string source, int sourceIndex, int sourceLength) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot EndsWith in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot EndsWith in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            if (selfLength < sourceLength) {
                return false;
            }

            return IndexOf(source, selfLength - sourceLength, 1, sourceIndex, sourceLength, selfLength, false, false, contents) > -1;
        }

        public bool EndsWith(string source, int sourceIndex) {
            return EndsWith(source, sourceIndex, source.Length - sourceIndex);
        }

        public bool EndsWith(string source) {
            return EndsWith(source, 0, source.Length);
        }
        #endregion

        #region StartsWith
        public bool StartsWith(char value) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot StartsWith in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot StartsWith in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            if (selfLength < 1) {
                return false;
            }

            return IndexOf(value, 0, 1, selfLength, false, false, contents) > -1;
        }

        public bool StartsWith(string source, int sourceIndex, int sourceLength) {
            if (Arena is null) {
                throw new InvalidOperationException("Cannot StartsWith in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot StartsWith in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            if (selfLength < sourceLength) {
                return false;
            }

            return IndexOf(source, 0, 1, sourceIndex, sourceLength, selfLength, false, false, contents) > -1;
        }

        public bool StartsWith(string source, int sourceIndex) {
            return StartsWith(source, sourceIndex, source.Length - sourceIndex);
        }

        public bool StartsWith(string source) {
            return StartsWith(source, 0, source.Length);
        }
        #endregion

        #region Trim(End/Start)
        private void Trim<T>(in T trimProvider, bool trimStart, bool trimEnd, Arena arena = null, char* contents = null) where T : IIsTrimChar {
            if (arena == null) {
                arena = Arena;
                if (arena == null) {
                    throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
                }
            }

            if (contents == null) {
                contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot Trim in ArenaString: string memory has previously been freed");
                }
            }

            var selfLength = Length;
            var start = 0;
            var end = selfLength - 1;

            if (trimStart) {
                while (start < selfLength && trimProvider.Trim(contents[start])) {
                    start++;
                }

                if (start >= selfLength) {
                    Length = 0;
                    return;
                }
            }

            if (trimEnd) {
                while (trimProvider.Trim(contents[end])) {
                    end--;
                }
                end++;

                if (end == 0) {
                    Length = 0;
                    return;
                }
            }

            if (start == 0) {
                Length = end;
            }
            else {
                var newLength = end - start;
                var source = contents + start;
                var capacity = Capacity;
                Buffer.MemoryCopy(source, contents, capacity * sizeof(char), (capacity - start) * sizeof(char));
                Length = newLength;
            }
        }

        public void TrimInPlace() {
            Trim(new TrimIsWhiteSpace(), true, true);
        }

        public void TrimInPlace(char[] chars) {
            Trim(new TrimIsCharArray(chars), true, true);
        }

        public void TrimInPlace(char chr) {
            Trim(new TrimIsChar(chr), true, true);
        }

        public void TrimStartInPlace() {
            Trim(new TrimIsWhiteSpace(), true, false);
        }

        public void TrimStartInPlace(char[] chars) {
            Trim(new TrimIsCharArray(chars), true, false);
        }

        public void TrimStartInPlace(char chr) {
            Trim(new TrimIsChar(chr), true, false);
        }

        public void TrimEndInPlace() {
            Trim(new TrimIsWhiteSpace(), false, true);
        }

        public void TrimEndInPlace(char[] chars) {
            Trim(new TrimIsCharArray(chars), false, true);
        }

        public void TrimEndInPlace(char chr) {
            Trim(new TrimIsChar(chr), false, true);
        }

        private ArenaString TrimCopy<T>(in T trimProvider, bool trimStart, bool trimEnd, Arena arena) where T : IIsTrimChar {
            var s = new ArenaString(arena, this);
            s.Trim(trimProvider, trimStart, trimEnd, arena);
            return s;
        }

        public ArenaString Trim() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsWhiteSpace(), true, true, arena);
        }

        public ArenaString Trim(char[] chars) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsCharArray(chars), true, true, arena);
        }

        public ArenaString Trim(char chr) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsChar(chr), true, true, arena);
        }

        public ArenaString TrimStart() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsWhiteSpace(), true, false, arena);
        }

        public ArenaString TrimStart(char[] chars) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsCharArray(chars), true, false, arena);
        }

        public ArenaString TrimStart(char chr) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsChar(chr), true, false, arena);
        }

        public ArenaString TrimEnd() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsWhiteSpace(), false, true, arena);
        }

        public ArenaString TrimEnd(char[] chars) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsCharArray(chars), false, true, arena);
        }

        public ArenaString TrimEnd(char chr) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Trim in ArenaString: string has not been properly initialized with arena reference");
            }
            return TrimCopy(new TrimIsChar(chr), false, true, arena);
        }

        private interface IIsTrimChar { bool Trim(char c); }

        private readonly struct TrimIsWhiteSpace : IIsTrimChar {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Trim(char c) { return char.IsWhiteSpace(c); }
        }
        private readonly struct TrimIsChar : IIsTrimChar {
            private readonly char chr;
            public TrimIsChar(char chr) { this.chr = chr; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Trim(char c) { return c == chr; }
        }
        private readonly struct TrimIsCharArray : IIsTrimChar {
            private readonly char[] chars;
            public TrimIsCharArray(char[] chars) { this.chars = chars; }
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Trim(char c) {
                for (int i = 0; i < chars.Length; i++) {
                    if (chars[i] == c) {
                        return true;
                    }
                }
                return false;
            }
        }
        #endregion

        #region ToUpper(Invariant) / ToLower(Invariant)
        private void ChangeCase(bool toUpper, CultureInfo culture, Arena arena = null, char* contents = null) {
            if (arena == null) {
                arena = Arena;
                if (arena == null) {
                    throw new InvalidOperationException("Cannot ToUpper/Lower in ArenaString: string has not been properly initialized with arena reference");
                }
            }

            if (contents == null) {
                contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot ToUpper/Lower in ArenaString: string memory has previously been freed");
                }
            }

            var end = contents + Length;
            var cur = contents;

            if (toUpper) {
                while (cur < end) {
                    *(cur++) = char.ToUpper(*cur, culture);
                }
            }
            else {
                while (cur < end) {
                    *(cur++) = char.ToLower(*cur, culture);
                }
            }
        }

        public void ToLowerInPlace(CultureInfo culture) {
            ChangeCase(false, culture);
        }

        public void ToLowerInvariantInPlace() {
            ChangeCase(false, CultureInfo.InvariantCulture);
        }

        public void ToUpperInPlace(CultureInfo culture) {
            ChangeCase(true, culture);
        }

        public void ToUpperInvariantInPlace() {
            ChangeCase(true, CultureInfo.InvariantCulture);
        }

        private ArenaString ChangeCase(bool toUpper, CultureInfo culture, Arena arena) {
            var s = new ArenaString(arena, this);
            s.ChangeCase(toUpper, culture, arena);
            return s;
        }

        public ArenaString ToLower(CultureInfo culture) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot ToLower in ArenaString: string has not been properly initialized with arena reference");
            }
            return ChangeCase(false, culture, arena);
        }

        public ArenaString ToLowerInvariant() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot ToLowerInvariant in ArenaString: string has not been properly initialized with arena reference");
            }
            return ChangeCase(false, CultureInfo.InvariantCulture, arena);
        }

        public ArenaString ToUpper(CultureInfo culture) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot ToUpper in ArenaString: string has not been properly initialized with arena reference");
            }
            return ChangeCase(false, culture, arena);
        }

        public ArenaString ToUpperInvariant() {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot ToUpperInvariant in ArenaString: string has not been properly initialized with arena reference");
            }
            return ChangeCase(false, CultureInfo.InvariantCulture, arena);
        }
        #endregion

        #region Replace
        private void Replace(char oldChar, char newChar, Arena arena = null, char* contents = null) {
            if (arena == null) {
                arena = Arena;
                if (arena == null) {
                    throw new InvalidOperationException("Cannot Replace in ArenaString: string has not been properly initialized with arena reference");
                }
            }

            if (contents == null) {
                contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot Replace in ArenaString: string memory has previously been freed");
                }
            }

            var end = contents + Length;
            var cur = contents;

            while (cur < end) {
                if (*cur == oldChar) {
                    *cur = newChar;
                }
                cur++;
            }
        }

        public void ReplaceInPlace(char oldChar, char newChar) {
            Replace(oldChar, newChar);
        }

        public ArenaString Replace(char oldChar, char newChar) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Replace in ArenaString: string has not been properly initialized with arena reference");
            }

            var s = new ArenaString(arena, this);
            s.Replace(oldChar, newChar, arena);
            return s;
        }

        public ArenaString Replace(string oldString, string newString) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot Replace in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot Replace in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            var replaceList = new ArenaList<int>(arena);
            ArenaString tmpString = default;

            try {
                int searchIndex = 0;
                int resultIndex;
                while (searchIndex <= selfLength && (resultIndex = IndexOf(oldString, searchIndex, selfLength - searchIndex, 0, oldString.Length, selfLength, false, checkArena: false, contents: contents)) > 0) {
                    replaceList.Add(resultIndex);
                    searchIndex = resultIndex + 1;
                }

                var sizeDifPerInstance = newString.Length - oldString.Length;
                var sizeDif = sizeDifPerInstance * replaceList.Count;

                var result = new ArenaString(arena, selfLength + sizeDif);
                var sourceIndex = 0;
                var resultContents = result.Contents;
                var dest = resultContents;

                // copy new string into ArenaString to make copying it into destination easier
                tmpString = new ArenaString(arena, newString);
                var newContents = tmpString.Contents;
                var replaceLen = newString.Length;
                var replaceLenBytes = replaceLen * sizeof(char);

                foreach (var pos in replaceList) {
                    var length = pos - sourceIndex;

                    CharCopy(contents + sourceIndex, dest, length);
                    sourceIndex += length;
                    dest += length;

                    CharCopy(newContents, dest, replaceLen);
                    dest += replaceLen;

                    sourceIndex += oldString.Length;
                }

                {
                    var length = selfLength - sourceIndex;
                    CharCopy(contents + sourceIndex, dest, length);
                    dest += length;
                }

                result.Length = (int)(((ulong)dest - (ulong)resultContents) / sizeof(char));
                return result;
            }
            finally {
                tmpString.Free();
                replaceList.Free();
            }
        }
        #endregion

        #region Remove
        private void Remove(int index, int count, Arena arena = null, char* contents = null, int selfLength = -1) {
            if (arena == null) {
                arena = Arena;
                if (arena == null) {
                    throw new InvalidOperationException("Cannot Remove in ArenaString: string has not been properly initialized with arena reference");
                }
            }

            if (contents == null) {
                contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot Remove in ArenaString: string memory has previously been freed");
                }
            }

            if (selfLength < 0) {
                selfLength = Length;
            }

            if (index < 0 || index > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (count < 0 || index + count > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0) {
                return;
            }
            if (index == 0 && count == selfLength) {
                Length = 0;
                return;
            }

            if (index + count == selfLength) {
                Length = selfLength - count;
                return;
            }

            var capacity = Capacity;
            Buffer.MemoryCopy(contents + index + count, contents + index, (capacity - index) * sizeof(char), (capacity - index - count) * sizeof(char));
        }

        public void RemoveInPlace(int index, int count) {
            Remove(index, count);
        }

        public void RemoveInPlace(int index) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot RemoveInPlace in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot RemoveInPlace in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            Remove(index, selfLength - index, arena, contents, selfLength);
        }

        public ArenaString Remove(int index, int count) {
            var arena = Arena;
            if (arena == null) {
                throw new InvalidOperationException("Cannot Remove in ArenaString: string has not been properly initialized with arena reference");
            }

            var s = new ArenaString(arena, this);
            s.Remove(index, count, arena);
            return s;
        }

        public ArenaString Remove(int index) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot RemoveInPlace in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot RemoveInPlace in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;

            var s = new ArenaString(arena, this);
            s.Remove(index, selfLength - index, arena, contents, selfLength);
            return s;
        }
        #endregion

        #region Insert
        public ArenaString Insert(int index, char* chars, int length) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;

            if (index < 0 || index > selfLength) {
                throw new ArgumentOutOfRangeException("index");
            }

            var s = new ArenaString(arena, selfLength + length);
            var dest = s.Contents;

            var preLength = index;
            CharCopy(contents, dest, preLength);
            dest += preLength;

            CharCopy(chars, dest, length);
            dest += length;

            var postLength = selfLength - preLength;
            CharCopy(contents + preLength, dest, postLength);

            s.Length = selfLength + length;
            return s;
        }

        private ArenaString Insert(int index, ArenaString str, Arena strArena = null) {
            if (strArena is null) {
                strArena = str.Arena;
                if (strArena is null) {
                    throw new InvalidOperationException("Cannot Insert in ArenaString: source string has not been properly initialized with arena reference");
                }
            }

            var strContents = str.Contents;
            if (strContents == null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: source string memory has previously been freed");
            }

            return Insert(index, strContents, str.Length);
        }

        public ArenaString Insert(int index, ArenaString str) {
            var strArena = str.Arena;
            if (strArena is null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: source string has not been properly initialized with arena reference");
            }
            return Insert(index, str, strArena);
        }

        public ArenaString Insert(int index, string str) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: string has not been properly initialized with arena reference");
            }

            var tmp = new ArenaString(arena, str);
            try {
                var ret = Insert(index, tmp, arena);
                return ret;
            }
            finally {
                tmp.Free();
            }
        }
        #endregion

        #region PadLeft/PadRight
        private ArenaString Pad(int count, char c, bool left, bool right) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot Insert in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;

            var s = new ArenaString(arena, selfLength + count);
            var dest = s.Contents;
            var extraLength = 0;

            if (left) {
                for (int i = 0; i < count; i++) {
                    *(dest++) = c;
                }
                extraLength += count;
            }

            CharCopy(contents, dest, selfLength);
            dest += selfLength;

            if (right) {
                for (int i = 0; i < count; i++) {
                    *(dest++) = c;
                }
                extraLength += count;
            }

            s.Length = selfLength + extraLength;
            return s;
        }

        public ArenaString Pad(int count, char c) {
            return Pad(count, c, true, true);
        }

        public ArenaString Pad(int count) {
            return Pad(count, ' ');
        }

        public ArenaString PadLeft(int count, char c) {
            return Pad(count, c, true, false);
        }

        public ArenaString PadLeft(int count) {
            return PadLeft(count, ' ');
        }

        public ArenaString PadRight(int count, char c) {
            return Pad(count, c, false, true);
        }

        public ArenaString PadRight(int count) {
            return PadRight(count, ' ');
        }
        #endregion

        #region Split
        private static StringSplitResults Split<T>(Arena arena, T separators, int count, StringSplitOptions options, char* str, int strLength) where T : ISeparatorProvider {
            if (str == null) {
                return default;
            }

            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            if (count == 0) {
                return new StringSplitResults(arena, 0);
            }

            var splitSpans = new ArenaList<SplitSpan>(arena);

            try {
                var index = 0;
                var fromIndex = index;

                while (index < strLength) {
                    var split = false;

                    for (int i = 0; i < separators.Count; i++) {
                        if (separators.IsNullOrEmpty(i)) { continue; }

                        int sepLength = separators.LengthOf(i);
                        if (index + sepLength > strLength) { continue; }

                        if (separators.IndexOf(i, index, strLength, str) > -1) {
                            var span = new SplitSpan(fromIndex, index - fromIndex);
                            index += sepLength;
                            fromIndex = index;
                            split = true;

                            if (span.Count > 0 || options != StringSplitOptions.RemoveEmptyEntries) {
                                splitSpans.Add(span);
                            }
                            break;
                        }
                    }

                    if (splitSpans.Count >= count - 1) {
                        if (options == StringSplitOptions.None || !split) {
                            break;
                        }
                    }

                    if (!split) {
                        index++;
                    }
                }

                {
                    var span = new SplitSpan(fromIndex, strLength - fromIndex);
                    if (span.Count > 0 || options != StringSplitOptions.RemoveEmptyEntries) {
                        splitSpans.Add(span);
                    }
                }

                var results = new StringSplitResults(arena, splitSpans.Count);
                for (int i = 0; i < splitSpans.Count; i++) {
                    var span = splitSpans[i];
                    if (span.Count == 0 && options == StringSplitOptions.RemoveEmptyEntries) { continue; }

                    var s = new ArenaString(arena, span.Count);
                    CharCopy(str + span.Start, s.Contents, span.Count);
                    s.Length = span.Count;

                    results[i] = s;
                }

                return results;
            }
            finally {
                splitSpans.Free();
            }
        }

        private StringSplitResults Split<T>(T separators, int count, StringSplitOptions options) where T : ISeparatorProvider {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot Split in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot Split in ArenaString: string memory has previously been freed");
            }

            return Split(arena, separators, count, options, contents, Length);
        }

        [StructLayout(LayoutKind.Sequential)]
        private readonly struct SplitSpan {
            public readonly int Start;
            public readonly int Count;

            public SplitSpan(int start, int count) {
                Start = start;
                Count = count;
            }

            public override string ToString() {
                return $"{Start}-{Start + Count}";
            }
        }

        private interface ISeparatorProvider {
            int Count { get; }
            bool IsNullOrEmpty(int index);
            int LengthOf(int index);
            int IndexOf(int index, int searchIndex, int selfLength, char* contents);
        }

        private readonly struct StringArraySeparator : ISeparatorProvider {
            private readonly string[] arr;
            private readonly ArenaString str;

            public StringArraySeparator(string[] arr, ArenaString str) {
                this.arr = arr;
                this.str = str;
            }

            public int Count { get => arr.Length; }
            public bool IsNullOrEmpty(int index) => arr[index] == null || arr[index].Length == 0;
            public int LengthOf(int index) => arr[index].Length;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => str.IndexOf(arr[index], searchIndex, arr[index].Length, 0, arr[index].Length, selfLength, false, false, contents);
        }

        public StringSplitResults Split(string[] separators, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(count, options);
            }
            return Split(new StringArraySeparator(separators, this), count, options);
        }

        public StringSplitResults Split(string[] separators, StringSplitOptions options) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(int.MaxValue, options);
            }
            return Split(new StringArraySeparator(separators, this), int.MaxValue, options);
        }

        private readonly struct StringSeparator : ISeparatorProvider {
            private readonly string sep;
            private readonly ArenaString str;

            public StringSeparator(string sep, ArenaString str) {
                this.sep = sep;
                this.str = str;
            }

            public int Count { get => 1; }
            public bool IsNullOrEmpty(int index) => sep == null || sep.Length == 0;
            public int LengthOf(int index) => sep.Length;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => str.IndexOf(sep, searchIndex, sep.Length, 0, sep.Length, selfLength, false, false, contents);
        }

        public StringSplitResults Split(string separator, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            return Split(new StringSeparator(separator, this), count, options);
        }

        public StringSplitResults Split(string separator, StringSplitOptions options) {
            return Split(new StringSeparator(separator, this), int.MaxValue, options);
        }

        private readonly struct CharArraySeparator : ISeparatorProvider {
            private readonly char[] arr;
            private readonly ArenaString str;

            public CharArraySeparator(char[] arr, ArenaString str) {
                this.arr = arr;
                this.str = str;
            }

            public int Count { get => arr.Length; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => contents[searchIndex] == arr[index] ? searchIndex : -1;
        }

        public StringSplitResults Split(char[] separators, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(count, options);
            }
            return Split(new CharArraySeparator(separators, this), count, options);
        }

        public StringSplitResults Split(char[] separators, StringSplitOptions options) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(int.MaxValue, options);
            }
            return Split(new CharArraySeparator(separators, this), int.MaxValue, options);
        }

        private readonly struct CharSeparator : ISeparatorProvider {
            private readonly char chr;
            private readonly ArenaString str;

            public CharSeparator(char chr, ArenaString str) {
                this.chr = chr;
                this.str = str;
            }

            public int Count { get => 1; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => contents[searchIndex] == chr ? searchIndex : -1;
        }

        public StringSplitResults Split(char separator, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            return Split(new CharSeparator(separator, this), count, options);
        }

        public StringSplitResults Split(char separator, StringSplitOptions options) {
            return Split(new CharSeparator(separator, this), int.MaxValue, options);
        }

        private readonly struct WhiteSpaceSeparator : ISeparatorProvider {
            public int Count { get => 1; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => char.IsWhiteSpace(contents[searchIndex]) ? searchIndex : -1;
        }

        public StringSplitResults SplitWhiteSpace(int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            return Split(new WhiteSpaceSeparator(), count, options);
        }

        public StringSplitResults SplitWhiteSpace(StringSplitOptions options) {
            return Split(new WhiteSpaceSeparator(), int.MaxValue, options);
        }
        #endregion

        #region Substring
        private ArenaString _Substring(int index, int length = -1) {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot Substring in ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot Substring in ArenaString: string memory has previously been freed");
            }

            var selfLength = Length;
            if (length < 0) {
                length = selfLength - index;
            }

            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (index < 0 || index + length > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            if (length == 0) {
                return new ArenaString(arena, 0);
            }
            if (length == selfLength) {
                return new ArenaString(arena, this);
            }

            var s = new ArenaString(arena, length);
            CharCopy(contents + index, s.Contents, length);
            s.Length = length;
            return s;
        }

        public ArenaString Substring(int index, int length) {
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            return _Substring(index, length);
        }

        public ArenaString Substring(int index) {
            return _Substring(index);
        }
        #endregion

        #region Concat
        /// <summary>
        /// Concatenates this string in this string's arena with another string.
        /// </summary>
        public ArenaString Concat(ArenaString other) {
            return Concat(Arena, this, other);
        }
        #endregion

        #region Concat/Join static
        public static ArenaString Join(Arena arena, char* sep, int sepLength, char* lhs, int lhsLength, char* rhs, int rhsLength) {
            if (sepLength < 0) {
                throw new ArgumentOutOfRangeException(nameof(sepLength));
            }
            else if (sepLength > 0 && sep == null) {
                throw new ArgumentNullException(nameof(sep));
            }

            if (lhs == null) {
                throw new ArgumentNullException(nameof(lhs));
            }
            if (rhs == null) {
                throw new ArgumentNullException(nameof(rhs));
            }

            if (lhsLength < 0) {
                throw new ArgumentOutOfRangeException(nameof(lhsLength));
            }
            if (rhsLength < 0) {
                throw new ArgumentOutOfRangeException(nameof(rhsLength));
            }

            var result = new ArenaString(arena, lhsLength + rhsLength + sepLength);
            var contents = result.Contents;

            CharCopy(lhs, contents, lhsLength);
            var length = lhsLength;

            if (sep != null && sepLength > 0) {
                CharCopy(sep, contents + length, sepLength);
                length += sepLength;
            }

            CharCopy(rhs, contents + length, rhsLength);
            length += rhsLength;

            result.Length = length;
            return result;
        }

        public static ArenaString Concat(Arena arena, char* lhs, int lhsLength, char* rhs, int rhsLength) {
            return Join(arena, null, 0, lhs, lhsLength, rhs, rhsLength);
        }

        public static ArenaString Join(Arena arena, ArenaString sep, ArenaString lhs, ArenaString rhs) {
            var lhsArena = lhs.Arena;
            if (lhsArena is null) {
                throw new InvalidOperationException("Cannot concatenate ArenaStrings: string has not been properly initialized with arena reference");
            }
            var lhsContents = lhs.Contents;
            if (lhsContents == null) {
                throw new InvalidOperationException("Cannot concatenate ArenaStrings: string memory has previously been freed");
            }
            var lhsLength = lhs.Length;

            var rhsArena = rhs.Arena;
            if (rhsArena is null) {
                throw new InvalidOperationException("Cannot concatenate ArenaStrings: string has not been properly initialized with arena reference");
            }
            var rhsContents = rhs.Contents;
            if (rhsContents == null) {
                throw new InvalidOperationException("Cannot concatenate ArenaStrings: string memory has previously been freed");
            }
            var rhsLength = rhs.Length;

            if (arena is null) {
                if (lhsArena != rhsArena) {
                    throw new InvalidOperationException("Cannot concatenate ArenaStrings: both strings do not belong to the same arena. Please specify an arena when using the Concat method.");
                }
                arena = lhsArena;
            }

            int sepLength = 0;
            char* sepContents = sep.contents.Value;

            if (sepContents != null) {
                sepContents += contentsOffset;
                sepLength = sep.Length;
            }

            return Join(arena, sepContents, sepLength, lhsContents, lhsLength, rhsContents, rhsLength);
        }

        public static ArenaString Join(Arena arena, ArenaString sep, ArenaString s0, ArenaString s1, ArenaString s2) {
            return Join(arena, sep, Join(arena, sep, s0, s1), s2);
        }

        public static ArenaString Join(Arena arena, ArenaString sep, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3) {
            return Join(arena, sep, Join(arena, sep, Join(arena, sep, s0, s1), s2), s3);
        }

        public static ArenaString Join(Arena arena, ArenaString sep, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3, ArenaString s4) {
            return Join(arena, sep, Join(arena, sep, Join(arena, sep, Join(arena, sep, s0, s1), s2), s3), s4);
        }

        public static ArenaString Join(Arena arena, ArenaString sep, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3, ArenaString s4, ArenaString s5) {
            return Join(arena, sep, Join(arena, sep, Join(arena, sep, Join(arena, sep, Join(arena, sep, s0, s1), s2), s3), s4), s5);
        }

        public static ArenaString Concat(Arena arena, ArenaString lhs, ArenaString rhs) {
            return Join(arena, default(ArenaString), lhs, rhs);
        }

        public static ArenaString Concat(Arena arena, ArenaString s0, ArenaString s1, ArenaString s2) {
            return Join(arena, default(ArenaString), Join(arena, default(ArenaString), s0, s1), s2);
        }

        public static ArenaString Concat(Arena arena, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3) {
            return Join(arena, default(ArenaString), Join(arena, default(ArenaString), Join(arena, default(ArenaString), s0, s1), s2), s3);
        }

        public static ArenaString Concat(Arena arena, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3, ArenaString s4, ArenaString s5) {
            return Join(arena, default(ArenaString), Join(arena, default(ArenaString), Join(arena, default(ArenaString), Join(arena, default(ArenaString), Join(arena, default(ArenaString), s0, s1), s2), s3), s4), s5);
        }

        public static ArenaString Join(Arena arena, string separator, ArenaString lhs, ArenaString rhs) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, lhs, rhs);
            }
            finally {
                sep.Free();
            }
        }

        public static ArenaString Join(Arena arena, string separator, ArenaString s0, ArenaString s1, ArenaString s2) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, Join(arena, sep, s0, s1), s2);
            }
            finally {
                sep.Free();
            }
        }

        public static ArenaString Join(Arena arena, string separator, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, Join(arena, sep, Join(arena, sep, s0, s1), s2), s3);
            }
            finally {
                sep.Free();
            }
        }

        public static ArenaString Join(Arena arena, string separator, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3, ArenaString s4) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, Join(arena, sep, Join(arena, sep, Join(arena, sep, s0, s1), s2), s3), s4);
            }
            finally {
                sep.Free();
            }
        }

        public static ArenaString Join(Arena arena, string separator, ArenaString s0, ArenaString s1, ArenaString s2, ArenaString s3, ArenaString s4, ArenaString s5) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, Join(arena, sep, Join(arena, sep, Join(arena, sep, Join(arena, sep, s0, s1), s2), s3), s4), s5);
            }
            finally {
                sep.Free();
            }
        }

        /// <summary>
        /// Non-boxing version of Join when the enumerator is a struct
        /// </summary>
        public static ArenaString Join<TEnum>(Arena arena, ArenaString sep, ref TEnum enumerator) where TEnum : IEnumerator<ArenaString> {
            var s = new ArenaString(arena, 0);
            while (enumerator.MoveNext()) {
                var result = Join(arena, sep, s, enumerator.Current);
                s.Free();
                s = result;
            }
            return s;
        }

        /// <summary>
        /// Non-boxing version of Concat when the enumerator is a struct
        /// </summary>
        public static ArenaString Concat<TEnum>(Arena arena, ref TEnum enumerator) where TEnum : IEnumerator<ArenaString> {
            return Join(arena, default(ArenaString), ref enumerator);
        }

        /// <summary>
        /// Non-boxing version of Join when the enumerator is a struct
        /// </summary>
        public static ArenaString Join<TEnum>(Arena arena, string separator, ref TEnum enumerator) where TEnum : IEnumerator<ArenaString> {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, ref enumerator);
            }
            finally {
                sep.Free();
            }
        }

        public static ArenaString Join(Arena arena, ArenaString sep, IEnumerable<ArenaString> values) {
            var s = new ArenaString(arena, 0);
            foreach (var v in values) {
                var result = Join(arena, sep, s, v);
                s.Free();
                s = result;
            }
            return s;
        }

        public static ArenaString Concat(Arena arena, IEnumerable<ArenaString> values) {
            return Join(arena, default(ArenaString), values);
        }

        public static ArenaString Join(Arena arena, string separator, IEnumerable<ArenaString> values) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, values);
            }
            finally {
                sep.Free();
            }
        }

        public static ArenaString Join(Arena arena, ArenaString sep, IEnumerable<object> values) {
            var s = new ArenaString(arena, 0);
            foreach (var v in values) {
                if (v is null) {
                    continue;
                }

                if (v is ArenaString) {
                    var result = Join(arena, sep, s, (ArenaString)v);
                    s.Free();
                    s = result;
                }
                else {
                    var tmp = new ArenaString(arena, v.ToString());
                    try {
                        var result = Join(arena, sep, s, tmp);
                        s.Free();
                        s = result;
                    }
                    finally {
                        tmp.Free();
                    }
                }
            }
            return s;
        }

        public static ArenaString Concat(Arena arena, IEnumerable<object> values) {
            return Join(arena, default(ArenaString), values);
        }


        public static ArenaString Join(Arena arena, string separator, IEnumerable<object> values) {
            var sep = new ArenaString(arena, separator);
            try {
                return Join(arena, sep, values);
            }
            finally {
                sep.Free();
            }
        }

        /// <summary>
        /// Non-boxing version of Join when the enumerator is a struct
        /// </summary>
        public static ArenaString Join<TEnum, TVal>(Arena arena, ArenaString sep, ref TEnum enumerator) where TEnum : IEnumerator<TVal> {
            var s = new ArenaString(arena, 0);
            while (enumerator.MoveNext()) {
                var v = enumerator.Current;
                var tmp = new ArenaString(arena, v.ToString());
                try {
                    var result = Join(arena, sep, s, tmp);
                    s.Free();
                    s = result;
                }
                finally {
                    tmp.Free();
                }
            }
            return s;
        }

        /// <summary>
        /// Non-boxing version of Concat when the enumerator is a struct
        /// </summary>
        public static ArenaString Concat<TEnum, TVal>(Arena arena, ref TEnum enumerator) where TEnum : IEnumerator<TVal> {
            return Join<TEnum, TVal>(arena, default(ArenaString), ref enumerator);
        }

        /// <summary>
        /// Non-boxing version of Join when the enumerator is a struct
        /// </summary>
        public static ArenaString Join<TEnum, TVal>(Arena arena, string separator, ref TEnum enumerator) where TEnum : IEnumerator<TVal> {
            var sep = new ArenaString(arena, separator);
            try {
                return Join<TEnum, TVal>(arena, sep, ref enumerator);
            }
            finally {
                sep.Free();
            }
        }
        #endregion

        #region Split static
        public static StringSplitResults SplitWhiteSpace(Arena arena, string str, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            fixed (char* chars = str) {
                return Split(arena, new WhiteSpaceSeparator(), count, options, chars, str.Length);
            }
        }

        public static StringSplitResults SplitWhiteSpace(Arena arena, string str, StringSplitOptions options) {
            fixed (char* chars = str) {
                return Split(arena, new WhiteSpaceSeparator(), int.MaxValue, options, chars, str.Length);
            }
        }

        private readonly struct DotNetStringArraySeparator : ISeparatorProvider {
            private readonly string[] arr;
            private readonly string str;

            public DotNetStringArraySeparator(string[] arr, string str) {
                this.arr = arr;
                this.str = str;
            }

            public int Count { get => arr.Length; }
            public bool IsNullOrEmpty(int index) => arr[index] == null || arr[index].Length == 0;
            public int LengthOf(int index) => arr[index].Length;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => str.IndexOf(arr[index], searchIndex, 1);
        }

        public static StringSplitResults Split(Arena arena, string str, string[] separators, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(arena, str, count, options);
            }
            fixed (char* chars = str) {
                return Split(arena, new DotNetStringArraySeparator(separators, str), count, options, chars, str.Length);
            }
        }

        public static StringSplitResults Split(Arena arena, string str, string[] separators, StringSplitOptions options) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(arena, str, int.MaxValue, options);
            }
            return Split(arena, str, separators, int.MaxValue, options);
        }

        private readonly struct DotNetStringSeparator : ISeparatorProvider {
            private readonly string sep;
            private readonly string str;

            public DotNetStringSeparator(string sep, string str) {
                this.sep = sep;
                this.str = str;
            }

            public int Count { get => 1; }
            public bool IsNullOrEmpty(int index) => sep == null || sep.Length == 0;
            public int LengthOf(int index) => sep.Length;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => str.IndexOf(sep, searchIndex, 1);
        }

        public static StringSplitResults Split(Arena arena, string str, string separator, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            fixed (char* chars = str) {
                return Split(arena, new DotNetStringSeparator(separator, str), count, options, chars, str.Length);
            }
        }

        public static StringSplitResults Split(Arena arena, string str, string separator, StringSplitOptions options) {
            return Split(arena, str, separator, int.MaxValue, options);
        }

        private readonly struct DotNetStringCharArraySeparator : ISeparatorProvider {
            private readonly char[] arr;

            public DotNetStringCharArraySeparator(char[] arr) {
                this.arr = arr;
            }

            public int Count { get => arr.Length; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => contents[searchIndex] == arr[index] ? searchIndex : -1;
        }

        public static StringSplitResults Split(Arena arena, string str, char[] separators, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(arena, str, count, options);
            }
            fixed (char* chars = str) {
                return Split(arena, new DotNetStringCharArraySeparator(separators), count, options, chars, str.Length);
            }
        }

        public static StringSplitResults Split(Arena arena, string str, char[] separators, StringSplitOptions options) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(arena, str, int.MaxValue, options);
            }
            fixed (char* chars = str) {
                return Split(arena, new DotNetStringCharArraySeparator(separators), int.MaxValue, options, chars, str.Length);
            }
        }

        private readonly struct DotNetStringCharSeparator : ISeparatorProvider {
            private readonly char chr;

            public DotNetStringCharSeparator(char chr) {
                this.chr = chr;
            }

            public int Count { get => 1; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => contents[searchIndex] == chr ? searchIndex : -1;
        }

        public static StringSplitResults Split(Arena arena, string str, char separator, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            fixed (char* chars = str) {
                return Split(arena, new DotNetStringCharSeparator(separator), count, options, chars, str.Length);
            }
        }

        public static StringSplitResults Split(Arena arena, string str, char separator, StringSplitOptions options) {
            fixed (char* chars = str) {
                return Split(arena, new DotNetStringCharSeparator(separator), int.MaxValue, options, chars, str.Length);
            }
        }

        public static StringSplitResults SplitWhiteSpace(Arena arena, char* str, int strLength, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            return Split(arena, new WhiteSpaceSeparator(), count, options, str, strLength);
        }

        public static StringSplitResults SplitWhiteSpace(Arena arena, char* str, int strLength, StringSplitOptions options) {
            return Split(arena, new WhiteSpaceSeparator(), int.MaxValue, options, str, strLength);
        }

        private readonly struct CharPtrCharArraySeparator : ISeparatorProvider {
            private readonly char[] arr;

            public CharPtrCharArraySeparator(char[] arr) {
                this.arr = arr;
            }

            public int Count { get => arr.Length; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => contents[searchIndex] == arr[index] ? searchIndex : -1;
        }

        public static StringSplitResults Split(Arena arena, char* str, int strLength, char[] separators, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(arena, str, strLength, count, options);
            }
            return Split(arena, new CharPtrCharArraySeparator(separators), count, options, str, strLength);
        }

        public static StringSplitResults Split(Arena arena, char* str, int strLength, char[] separators, StringSplitOptions options) {
            if (separators == null || separators.Length == 0) {
                return SplitWhiteSpace(arena, str, int.MaxValue, options);
            }
            return Split(arena, new CharPtrCharArraySeparator(separators), int.MaxValue, options, str, strLength);
        }

        private readonly struct CharPtrCharSeparator : ISeparatorProvider {
            private readonly char chr;

            public CharPtrCharSeparator(char chr) {
                this.chr = chr;
            }

            public int Count { get => 1; }
            public bool IsNullOrEmpty(int index) => false;
            public int LengthOf(int index) => 1;
            public int IndexOf(int index, int searchIndex, int selfLength, char* contents)
                => contents[searchIndex] == chr ? searchIndex : -1;
        }

        public static StringSplitResults Split(Arena arena, char* str, int strLength, char separator, int count = int.MaxValue, StringSplitOptions options = StringSplitOptions.None) {
            return Split(arena, new CharPtrCharSeparator(separator), count, options, str, strLength);
        }

        public static StringSplitResults Split(Arena arena, char* str, int strLength, char separator, StringSplitOptions options) {
            return Split(arena, new CharPtrCharSeparator(separator), int.MaxValue, options, str, strLength);
        }
        #endregion

        private static void CharCopy(char* source, char* dest, int length) {
            if (length > 0) {
                if (length > 64) {
                    var lengthBytes = length * sizeof(char);
                    Buffer.MemoryCopy(source, dest, lengthBytes, lengthBytes);
                    dest += length;
                }
                else {
                    int count = length;
                    while (count > 0) {
                        *(dest++) = *(source++);
                        count--;
                    }
                }
            }
        }

        public UnmanagedRef<char> GetUnderlyingReference() {
            return contents;
        }

        public void Free() {
            var arena = contents.Arena;
            if (!(arena is null)) {
                arena.Free(in contents);
            }
        }

        public void Dispose() {
            if (!IsAllocated) {
                return;
            }
            Free();
        }

        public override string ToString() {
            var contents = Contents;
            if (contents == null) {
                return string.Empty;
            }
            return new string(contents, 0, Length);
        }

        public Enumerator GetEnumerator() {
            var arena = Arena;
            if (arena is null) {
                throw new InvalidOperationException("Cannot enumerate ArenaString: string has not been properly initialized with arena reference");
            }

            var contents = Contents;
            if (contents == null) {
                throw new InvalidOperationException("Cannot enumerate ArenaString: string memory has previously been freed");
            }

            return new Enumerator(this);
        }

        IEnumerator<char> IEnumerable<char>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is ArenaString @string &&
                   Equals(@string);
        }

        private bool Equals(char* selfContents, int selfLength, char* otherContents, int otherLength) {
            if (selfLength != otherLength) {
                return false;
            }
            var length = selfLength;

            if (length < 32) {
                for (int i = 0; i < length; i++) {
                    if (selfContents[i] != otherContents[i]) {
                        return false;
                    }
                }
                return true;
            }

            var selfAlign = (int)(((ulong)selfContents) & 0b111);
            var otherAlign = (int)(((ulong)otherContents) & 0b111);

            if (selfAlign == otherAlign) {
                // compare to word alignment
                var byteptrA = (byte*)selfContents;
                var byteptrB = (byte*)otherContents;

                var bytes = selfAlign;
                for (int i = 0; i < bytes; i++) {
                    if (*(byteptrA++) != *(byteptrB++)) {
                        return false;
                    }
                }

                length -= bytes;

                // compare words
                var count = length / sizeof(ulong);
                var longptrA = (ulong*)byteptrA;
                var longptrB = (ulong*)byteptrB;

                for (int i = 0; i < count; i++) {
                    if (*(longptrA++) != *(longptrB++)) {
                        return false;
                    }
                }

                length -= count * sizeof(ulong);

                // compare remaining bytes
                byteptrA = (byte*)longptrA;
                byteptrB = (byte*)longptrB;
                bytes = length;
                for (int i = 0; i < bytes; i++) {
                    if (*(longptrA++) != *(longptrB++)) {
                        return false;
                    }
                }
            }
            else {
                selfAlign = (int)(((ulong)selfContents) & 0b11);
                otherAlign = (int)(((ulong)otherContents) & 0b11);

                if (selfAlign == otherAlign) {
                    // compare to word alignment
                    var byteptrA = (byte*)selfContents;
                    var byteptrB = (byte*)otherContents;

                    var bytes = selfAlign;
                    for (int i = 0; i < bytes; i++) {
                        if (*(byteptrA++) != *(byteptrB++)) {
                            return false;
                        }
                    }

                    length -= bytes;

                    // compare words
                    var count = length / sizeof(uint);
                    var intptrA = (uint*)byteptrA;
                    var intptrB = (uint*)byteptrB;

                    for (int i = 0; i < count; i++) {
                        if (*(intptrA++) != *(intptrB++)) {
                            return false;
                        }
                    }

                    length -= count * sizeof(uint);

                    // compare remaining bytes
                    byteptrA = (byte*)intptrA;
                    byteptrB = (byte*)intptrB;
                    bytes = length;
                    for (int i = 0; i < bytes; i++) {
                        if (*(intptrA++) != *(intptrB++)) {
                            return false;
                        }
                    }
                }
                else {
                    for (int i = 0; i < length; i++) {
                        if (selfContents[i] != otherContents[i]) {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        public bool Equals(ArenaString other) {
            var selfContents = Contents;
            var selfLength = Length;
            var otherContents = other.Contents;
            var otherLength = other.Length;

            if (selfContents == otherContents) {
                return true;
            }

            return Equals(selfContents, selfLength, otherContents, otherLength);
        }

        public bool Equals(string other) {
            var selfContents = Contents;
            if (other is null) {
                return selfContents == null;
            }

            var selfLength = Length;
            if (other.Length != selfLength) {
                return false;
            }

            fixed (char* strPtr = other) {
                return Equals(selfContents, selfLength, strPtr, other.Length);
            }
        }

        // TODO: (Core) use string.GetHashCode(ReadOnlySpan<char> value)
        public override int GetHashCode() {
            var contents = Contents;
            if (contents == null) {
                return 0x0BADBA5E;
            }

            // from https://stackoverflow.com/a/36846609
            unchecked {
                int hash1 = 5381;
                int hash2 = hash1;

                var length = Length;

                for (int i = 0; i < length && contents[i] != '\0'; i += 2) {
                    hash1 = ((hash1 << 5) + hash1) ^ contents[i];
                    if (i == length - 1 || contents[i + 1] == '\0')
                        break;
                    hash2 = ((hash2 << 5) + hash2) ^ contents[i + 1];
                }

                return hash1 + (hash2 * 1566083941);
            }
        }

        public static bool operator ==(ArenaString left, ArenaString right) {
            return left.Equals(right);
        }

        public static bool operator !=(ArenaString left, ArenaString right) {
            return !(left == right);
        }

        public static bool operator ==(string left, ArenaString right) {
            return right.Equals(left);
        }

        public static bool operator !=(string left, ArenaString right) {
            return !(left == right);
        }

        public static bool operator ==(ArenaString left, string right) {
            return left.Equals(right);
        }

        public static bool operator !=(ArenaString left, string right) {
            return !(left == right);
        }
        #endregion

        public static ArenaString operator +(ArenaString lhs, ArenaString rhs) {
            return Concat(null, lhs, rhs); 
        }

        public static explicit operator ArenaString(UnmanagedRef<char> strData) {
            return new ArenaString(strData);
        }

        public char* Contents { 
            get {
                var ptr = contents.Value;
                if (ptr == null) {
                    return null;
                }
                return ptr + contentsOffset; 
            } 
        }

        public int Length {
            get {
                var ptr = contents.Value;
                if (ptr == null) {
                    return 0;
                }
                return *(int*)ptr;
            }
            private set {
                var ptr = contents.Value;
                if (ptr == null) {
                    throw new InvalidOperationException("Cannot set Length in ArenaString: contents was null");
                }
                *(int*)contents.Value = value;
            }
        }

        public int Capacity {
            get {
                return contents.ElementCount;
            }
        }

        public char this[int index] {
            get {
                var arena = Arena;
                if (arena is null) {
                    throw new InvalidOperationException("Cannot index ArenaString: string has not been properly initialized with arena reference");
                }

                var contents = Contents;
                if (contents == null) {
                    throw new InvalidOperationException("Cannot index ArenaString: string memory has previously been freed");
                }

                if (index < 0 || index >= Length) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return contents[index];
            }
        }

        public bool IsAllocated { get { return contents.HasValue; } }
        public Arena Arena { get { return contents.Arena; } }

        [Serializable]
        public struct Enumerator : IEnumerator<char>, System.Collections.IEnumerator {
            private ArenaString str;
            private int index;
            private int length;
            private char current;

            internal Enumerator(ArenaString str) {
                this.str = str;
                index = 0;
                length = this.str.Length;
                current = default;
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                var contents = str.Contents;

                if (contents != null && length == str.Length && ((uint)index < (uint)length)) {
                    current = contents[index];
                    index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare() {
                var contents = str.Contents;
                if (contents == null || length != str.Length) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                index = length + 1;
                current = default;
                return false;
            }

            public char Current {
                get {
                    return current;
                }
            }

            object IEnumerator.Current {
                get {
                    if (index == 0 || index == length + 1) {
                        throw new InvalidOperationException("Enumeration has either not started or has already finished.");
                    }
                    return Current;
                }
            }

            void IEnumerator.Reset() {
                var contents = str.Contents;
                if (contents == null || length != str.Length) {
                    throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                }

                index = 0;
                current = default;
            }
        }
    }

    [DebuggerTypeProxy(typeof(StringSplitResultsDebugView))]
    public unsafe readonly struct StringSplitResults : IDisposable, IEnumerable<ArenaString> {
        private readonly UnmanagedRef<UnmanagedRef> tokens;
        public readonly int Count;

        public StringSplitResults(Arena arena, int count) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }
            if (count < 0) {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Count = count;
            if (Count > 0) {
                tokens = arena.AllocCount<UnmanagedRef>(count);
            }
            else {
                tokens = default;
            }
        }

        public void Free() {
            if (Count == 0) { return; }

            var arena = tokens.Arena;
            if (!(arena is null)) {
                arena.Free(in tokens);
            }
        }

        public void Dispose() {
            Free();
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(this);
        }

        IEnumerator<ArenaString> IEnumerable<ArenaString>.GetEnumerator() {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }

        public ArenaString this[int index] {
            get {
                if (index < 0 || index >= Count) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (tokens.Arena is null) {
                    throw new InvalidOperationException("Cannot get item in StringSplitResults: results instance has not been properly initialized with arena reference");
                }

                var items = tokens.Value;
                if (items == null) {
                    throw new InvalidOperationException("Cannot get item in StringSplitResults: results instance memory has previously been freed");
                }

                var ptr = (UnmanagedRef<char>)items[index];
                if (ptr == null) {
                    throw new InvalidOperationException("Cannot get item in StringSplitResults: item has previously been freed");
                }

                return (ArenaString)ptr;
            }
            set {
                if (index < 0 || index >= Count) {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                if (tokens.Arena is null) {
                    throw new InvalidOperationException("Cannot set item in StringSplitResults: results instance has not been properly initialized with arena reference");
                }

                var items = tokens.Value;
                if (items == null) {
                    throw new InvalidOperationException("Cannot set item in StringSplitResults: results instance memory has previously been freed");
                }

                items[index] = (UnmanagedRef)value.GetUnderlyingReference();
            }
        }

        [Serializable]
        public struct Enumerator : IEnumerator<ArenaString>, System.Collections.IEnumerator {
            private StringSplitResults results;
            private int index;
            private int count;
            private ArenaString current;

            internal Enumerator(StringSplitResults results) {
                this.results = results;
                index = 0;
                count = results.Count;
                current = default;
            }

            public void Dispose() {
            }

            public bool MoveNext() {
                if (count == 0) { return MoveNextRare(); }

                var tokensPtr = results.tokens.Value;

                if (tokensPtr != null && ((uint)index < (uint)count)) {
                    current = results[index];
                    index++;
                    return true;
                }

                return MoveNextRare();
            }

            private bool MoveNextRare() {
                if (count > 0) {
                    var tokensPtr = results.tokens.Value;
                    if (tokensPtr == null) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }
                }

                index = count + 1;
                current = default;
                return false;
            }

            public ArenaString Current {
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
                if (count > 0) {
                    var tokensPtr = results.tokens.Value;
                    if (tokensPtr == null) {
                        throw new InvalidOperationException("Collection was modified; enumeration operation may not execute.");
                    }
                }

                index = 0;
                current = default;
            }
        }
    }

    internal unsafe readonly struct StringSplitResultsDebugView {
        private readonly StringSplitResults results;

        public StringSplitResultsDebugView(StringSplitResults results) {
            this.results = results;
        }

        public ArenaString[] Items {
            get {
                var items = new ArenaString[results.Count];
                for (int i = 0; i < results.Count; i++) {
                    items[i] = results[i];
                }
                return items;
            }
        }

        public int Count { get { return results.Count; } }
    }
}
