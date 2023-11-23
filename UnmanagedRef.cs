using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    unsafe readonly public struct UnmanagedRef<T> where T : unmanaged, IArenaContents {
        private readonly T* pointer;
        private readonly int version;
        private readonly Arena arena;

        public UnmanagedRef(T* pointer, Arena arena) {
            this.pointer = pointer;
            this.arena = arena;
            version = this.arena.Version;
        }

        public void Free() {
            if (arena.IsDisposed) {
                return;
            }
            arena.Free(this);
        }

        public override string ToString() {
            var ptr = Value;
            return ptr == null ? string.Empty : (*ptr).ToString();
        }

        public Arena Arena { get { return arena; } }
        public T* Value { get { return arena.IsDisposed || arena.Version != version ? null : pointer; } }
        public bool HasValue { get { return !arena.IsDisposed && pointer != null; } }
        public int Version { get { return version; } }
    }
}
