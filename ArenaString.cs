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

        #region CopyFrom
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
        // Substring

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
