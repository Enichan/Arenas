using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    public unsafe readonly struct ArenaString {
        private const int minCapacity = (16 - sizeof(int)) / sizeof(char);
        private const int contentsOffset = 2;
        private const int contentsOffsetBytes = 2 * sizeof(char);

        static ArenaString() {
            Debug.Assert(sizeof(int) == sizeof(char) * 2);
        }

        private readonly UnmanagedRef<char> contents;

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
            end -= sourceLength;

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
        #region (Last)IndexOf char[]
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
        private void Trim<T>(in T trimProvider, bool trimStart, bool trimEnd, Arena arena = null, char* contents = null) where T : struct, IIsTrimChar {
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

        private ArenaString TrimCopy<T>(in T trimProvider, bool trimStart, bool trimEnd, Arena arena) where T : struct, IIsTrimChar {
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

        [ThreadStatic]
        private static List<int> _replaceList;
        private static List<int> replaceList { get { return _replaceList ?? (_replaceList = new List<int>()); } }

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
            var replaceList = ArenaString.replaceList;
            replaceList.Clear();

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
            var newArenaString = new ArenaString(arena, newString);
            var newContents = newArenaString.Contents;
            var replaceLen = newString.Length;
            var replaceLenBytes = replaceLen * sizeof(char);

            foreach (var pos in replaceList) {
                var length = pos - sourceIndex;
                if (length > 0) {
                    if (length > 64) {
                        var lengthBytes = length * sizeof(char);
                        Buffer.MemoryCopy(contents + sourceIndex, dest, lengthBytes, lengthBytes);
                        dest += length;
                    }
                    else {
                        int count = length;
                        while (count > 0) {
                            *(dest++) = contents[sourceIndex++];
                            count--;
                        }
                    }
                }

                if (replaceLen > 64) {
                    Buffer.MemoryCopy(newContents, dest, replaceLenBytes, replaceLenBytes);
                    dest += replaceLen;
                }
                else {
                    for (int i = 0; i < replaceLen; i++) {
                        *(dest++) = newContents[i];
                    }
                }

                sourceIndex += oldString.Length;
            }

            {
                var length = selfLength - sourceIndex;
                if (length > 0) {
                    if (length > 64) {
                        var lengthBytes = length * sizeof(char);
                        Buffer.MemoryCopy(contents + sourceIndex, dest, lengthBytes, lengthBytes);
                        dest += length;
                    }
                    else {
                        int count = length;
                        while (count > 0) {
                            *(dest++) = contents[sourceIndex++];
                            count--;
                        }
                    }
                }
            }

            newArenaString.Free();

            result.Length = (int)(((ulong)dest - (ulong)resultContents) / sizeof(char));
            return result;
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

        // doable in place
        // enumerator
        // hashcode and equality

        // doable with reallocations
        // Insert
        // PadLeft / PadRight
        // Split
        // Substring

        public void Free() {
            var arena = contents.Arena;
            if (!(arena is null)) {
                arena.Free(in contents);
            }
        }

        public override string ToString() {
            var contents = Contents;
            if (contents == null) {
                return string.Empty;
            }
            return new string(contents, 0, Length);
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

        public bool IsAllocated { get { return contents.HasValue; } }
        public Arena Arena { get { return contents.Arena; } }
    }
}
