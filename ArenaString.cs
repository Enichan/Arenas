using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    public unsafe readonly struct ArenaString {
        static ArenaString() {
            Debug.Assert(sizeof(int) == sizeof(char) * 2);
        }

        private readonly UnmanagedRef<char> contents;

        public ArenaString(Arena arena, string source) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }

            var capacity = source.Length;
            contents = arena.AllocCount<char>(capacity + 2);
            CopyFrom(source);
        }

        public ArenaString(Arena arena, int capacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }

            contents = arena.AllocCount<char>(capacity + 2);
        }

        // TODO: Contains via IndexOf

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

            capacity = contents.ElementCount - 2;
            return ptr;
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

            if (length > capacity) {
                return false;
            }

            Buffer.MemoryCopy(charPtr, dest, capacity * sizeof(char), length * sizeof(char));
            Length = length;
            return true;
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

        public void CopyFrom(List<char> list) {
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
        private int IndexOf(char c, int index, int length, int selfLength, bool reverse) {
            if (index < 0 || index > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (length < 0 || index + length > selfLength) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }

            var contents = Contents;
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
        private int IndexOf(string source, int searchIndex, int searchLength, int sourceIndex, int sourceLength, int selfLength, bool reverse) {
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

            var contents = Contents;
            var end = searchIndex + searchLength;
            end -= sourceLength;

            var sourceEnd = sourceIndex + sourceLength;

            if (reverse) {
                for (int i = end - 1; i >= searchIndex; i--) {
                    var cmpi = i;
                    for (int j = sourceIndex; i < sourceEnd; i++, cmpi++) {
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
                    for (int j = sourceIndex; i < sourceEnd; i++, cmpi++) {
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

            var contents = Contents;
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

        // doable in place
        // Contains
        // CopyTo
        // EndsWith
        // Trim / TrimEnd / TrimStart
        // enumerator
        // hashcode and equality
        // IndexOf / IndexOfAny
        // LastIndexOf / LastIndexOfAny
        // StartsWith
        // ToLower(Invariant)
        // ToUpper(Invariant)
        // Remove
        // Replace (char)

        // doable with reallocations
        // Insert
        // PadLeft / PadRight
        // Replace (string)
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
            return new string(Contents, 0, Length);
        }

        public char* Contents { 
            get {
                var ptr = contents.Value;
                if (ptr == null) {
                    return null;
                }
                return ptr + 2; 
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
                int capacity;
                InitCopy(out capacity);
                return capacity;
            }
        }
    }
}
