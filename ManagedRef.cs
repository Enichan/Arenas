using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    public struct ManagedRef {
        private readonly IntPtr handle;

        public ManagedRef(IntPtr handle) {
            this.handle = handle;
        }

        public ManagedRef Set<T>(Arena arena, T value) where T : class {
            return new ManagedRef(arena.SetOutsidePtr(value, handle));
        }

        public T Get<T>(Arena arena) where T : class {
            return arena.GetOutsidePtr<T>(handle);
        }

        public override string ToString() {
            return $"{base.ToString()}({handle})";
        }

        public static explicit operator IntPtr(ManagedRef ptr) {
            return ptr.handle;
        }
    }
}
