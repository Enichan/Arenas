using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    internal readonly struct UnmanagedRefDebugView<T> where T : unmanaged {
        private readonly UnmanagedRef<T> uref;

        public UnmanagedRefDebugView(UnmanagedRef<T> uref) {
            this.uref = uref;
        }

        public T[] Items {
            get {
                return uref.ToArray();
            }
        }
    }
}
