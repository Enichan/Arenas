using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    unsafe readonly public struct UnmanagedRef<T> where T : unmanaged {
        private readonly T* pointer;
        private readonly RefVersion version;
        private readonly Arena arena;
        private readonly int elementCount;

        public UnmanagedRef(T* pointer, Arena arena, RefVersion version, int elementCount) {
            this.pointer = pointer;
            this.arena = arena;
            this.version = version;
            this.elementCount = elementCount;
        }

        public override string ToString() {
            var ptr = Value;
            return ptr == null ? string.Empty : (*ptr).ToString();
        }

        public static explicit operator IntPtr(UnmanagedRef<T> uref) {
            return (IntPtr)uref.pointer;
        }

        public static explicit operator UnmanagedRef(UnmanagedRef<T> uref) {
            return new UnmanagedRef((IntPtr)uref.pointer, uref.arena, uref.version, uref.elementCount);
        }

        public static explicit operator UnmanagedRef<T>(UnmanagedRef uref) {
            return new UnmanagedRef<T>((T*)(IntPtr)uref, uref.Arena, uref.Version, uref.ElementCount);
        }

        public Arena Arena { get { return arena; } }
        public T* Value { get { return !arena.VersionsMatch(version, (IntPtr)pointer) ? null : pointer; } }
        public bool HasValue { get { return arena.VersionsMatch(version, (IntPtr)pointer) && pointer != null; } }
        public RefVersion Version { get { return version; } }
        public int ElementCount { get { return elementCount; } }
    }

    unsafe readonly public struct UnmanagedRef {
        private readonly IntPtr pointer;
        private readonly RefVersion version;
        private readonly Arena arena;
        private readonly int elementCount;

        public UnmanagedRef(IntPtr pointer, Arena arena, RefVersion version, int elementCount) {
            this.pointer = pointer;
            this.arena = arena;
            this.version = version;
            this.elementCount = elementCount;
        }

        public override string ToString() {
            var ptr = Value;
            return ptr == null ? string.Empty : $"0x{ptr:x}";
        }

        public static explicit operator IntPtr(UnmanagedRef uref) {
            return uref.pointer;
        }

        public Arena Arena { get { return arena; } }
        public IntPtr Value { get { return !arena.VersionsMatch(version, pointer) ? IntPtr.Zero : pointer; } }
        public bool HasValue { get { return arena.VersionsMatch(version, pointer) && pointer != null; } }
        public RefVersion Version { get { return version; } }
        public int ElementCount { get { return elementCount; } }
    }
}
