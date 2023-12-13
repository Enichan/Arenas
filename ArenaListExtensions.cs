using System;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public static class ArenaListExtensions {
        public static void Add<T>(this ArenaList<UnmanagedRef> list, in T uref) where T : IUnmanagedRef {
            list.Add(uref.Reference);
        }

        public static void Add<T>(this ArenaList<IntPtr> list, in T uref) where T : IUnmanagedRef {
            var pointer = uref.Reference.Value;
            if (pointer == IntPtr.Zero) {
                throw new ArgumentNullException(nameof(uref));
            }
            list.Add(pointer);
        }
    }
}
