using System;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public static class ArenaListExtensions {
        public static void Add<T>(this ArenaList<UnsafeRef> list, UnmanagedRef<T> uref) where T : unmanaged {
            list.Add((UnsafeRef)uref);
        }
        public static void Add<T>(this ArenaList<IntPtr> list, UnmanagedRef<T> uref) where T : unmanaged {
            list.Add((IntPtr)uref);
        }
    }
}
