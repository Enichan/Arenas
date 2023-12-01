using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    public unsafe readonly struct ArenaString {
        static ArenaString() {
            Debug.Assert(sizeof(int) == sizeof(char) * 2);
        }

        private readonly UnsafeRef<char> contents;

        public ArenaString(Arena arena, string source) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }

            var capacity = source.Length;
            contents = (UnsafeRef<char>)arena.AllocCount<char>(capacity + 2);
            CopyFrom(source);
        }

        public ArenaString(Arena arena, int capacity) {
            if (arena is null) {
                throw new ArgumentNullException(nameof(arena));
            }

            contents = (UnsafeRef<char>)arena.AllocCount<char>(capacity + 2);
        }

        private char* InitCopy(out int capacity) {
            var arena = contents.Arena;
            if (arena is null) {
                capacity = 0;
                return null;
            }

            var ptr = (IntPtr)contents;
            if (ptr == IntPtr.Zero) {
                capacity = 0;
                return null;
            }

            var uref = arena.UnmanagedRefFromPtr<char>(ptr);
            capacity = uref.ElementCount - 2;
            return (char*)(IntPtr)uref;
        }

        public bool TryCopyFrom(string s) {
            int capacity;
            var cur = InitCopy(out capacity);

            if (cur == null || s.Length > capacity) {
                return false;
            }

            for (int i = 0; i < s.Length; i++) {
                *(cur++) = s[i];
            }

            Length = s.Length;
            return true;
        }

        public void CopyFrom(string s) {
            if (!TryCopyFrom(s)) {
                throw new InvalidOperationException("Cannot CopyFrom string in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(char[] chars) {
            int capacity;
            var cur = InitCopy(out capacity);

            if (cur == null || chars.Length > capacity) {
                return false;
            }

            Marshal.Copy(chars, 0, (IntPtr)cur, chars.Length);
            Length = chars.Length;
            return true;
        }

        public void CopyFrom(char[] chars) {
            if (!TryCopyFrom(chars)) {
                throw new InvalidOperationException("Cannot CopyFrom char[] in ArenaString: insufficient capacity");
            }
        }

        public bool TryCopyFrom(char* ptr, int length) {
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            if (ptr == null) {
                throw new ArgumentNullException(nameof(ptr));
            }

            int capacity;
            var dest = InitCopy(out capacity);

            if (dest == null || length > capacity) {
                return false;
            }

            Buffer.MemoryCopy(ptr, dest, capacity * sizeof(char), length * sizeof(char));
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

        public bool TryCopyFrom(List<char> list) {
            int capacity;
            var cur = InitCopy(out capacity);

            if (cur == null || list.Count > capacity) {
                return false;
            }

            for (int i = 0; i < list.Count; i++) {
                *(cur++) = list[i];
            }

            Length = list.Count;
            return true;
        }

        public void CopyFrom(List<char> list) {
            if (!TryCopyFrom(list)) {
                throw new InvalidOperationException("Cannot CopyFrom List<char> in ArenaString: insufficient capacity");
            }
        }

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
