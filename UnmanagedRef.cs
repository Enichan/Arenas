using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    unsafe public struct UnmanagedRef<T> where T : unmanaged, IArenaContents {
        private readonly T* pointer;
        private readonly int version;

        public UnmanagedRef(T* pointer, Arena arena) {
            this.pointer = pointer;
            Arena = arena;
            version = Arena.Version;
        }

        public override string ToString() {
            var ptr = Value;
            return ptr == null ? string.Empty : (*ptr).ToString();
        }

        public Arena Arena { get; private set; }
        public T* Value { get { return Arena.IsDisposed || Arena.Version != version ? null : pointer; } }
        public bool HasValue { get { return !Arena.IsDisposed && pointer != null; } }
    }
}
