using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    internal unsafe readonly struct UnmanagedRefDebugView<T> where T : unmanaged {
        private readonly UnmanagedRef<T> uref;

        public UnmanagedRefDebugView(UnmanagedRef<T> uref) {
            this.uref = uref;
        }

        public T[] Contents {
            get {
                return uref.ToArray();
            }
        }

        public Arena Arena { get { return uref.Arena; } }
        public T* Value { get { return uref.Value; } }
        public bool HasValue { get { return uref.HasValue; } }
        public RefVersion Version { get { return uref.Version; } }
        public int ElementCount { get { return uref.ElementCount; } }
        public int Size { get { return uref.Size; } }
    }

    internal readonly struct UnmanagedRefDebugView {
        private readonly UnmanagedRef uref;

        public UnmanagedRefDebugView(UnmanagedRef uref) {
            this.uref = uref;
        }

        public object[] Contents {
            get {
                return uref.ToArray();
            }
        }

        public Type Type { get { return uref.Type; } }
        public Arena Arena { get { return uref.Arena; } }
        public IntPtr Value { get { return uref.Value; } }
        public bool HasValue { get { return uref.HasValue; } }
        public RefVersion Version { get { return uref.Version; } }
        public int ElementCount { get { return uref.ElementCount; } }
        public int Size { get { return uref.Size; } }
    }
}
