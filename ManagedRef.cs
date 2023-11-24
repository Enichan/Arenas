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
            if (arena == null) {
                throw new InvalidOperationException("Arena cannot be null in ManagedRef.Set");
            }
            return new ManagedRef(arena.SetOutsidePtr(value, handle));
        }

        public T Get<T>(Arena arena) where T : class {
            if (arena == null) {
                throw new InvalidOperationException("Arena cannot be null in ManagedRef.Get");
            }
            return arena.GetOutsidePtr<T>(handle);
        }

        public override string ToString() {
            return $"ManagedRef({handle})";
        }

        public static explicit operator IntPtr(ManagedRef ptr) {
            return ptr.handle;
        }
    }
}
