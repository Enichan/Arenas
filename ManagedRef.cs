﻿using System;
using System.Runtime.InteropServices;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ManagedRef {
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

        public T Get<T>() where T : class {
            if (handle == IntPtr.Zero) {
                return null;
            }
            GCHandle gcHandle = GCHandle.FromIntPtr(handle);
            return (T)gcHandle.Target;
        }

        public override string ToString() {
            return $"ManagedRef({handle:x})";
        }

        public static explicit operator IntPtr(ManagedRef ptr) {
            return ptr.handle;
        }
    }
}
