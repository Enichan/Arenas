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

        public Arena Arena { get { return arena; } }
        public T* Value { get { return !arena.VersionsMatch(version, (IntPtr)pointer) ? null : pointer; } }
        public bool HasValue { get { return arena.VersionsMatch(version, (IntPtr)pointer) && pointer != null; } }
        public RefVersion Version { get { return version; } }
        public int ElementCount { get { return elementCount; } }
    }
}
