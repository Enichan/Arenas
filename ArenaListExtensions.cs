using System;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public static class ArenaListExtensions {
        public static void Add<TItem>(this ArenaList<UnsafeRef> list, in TItem uref) where TItem : struct, IUnmanagedRef {
            list.Add(uref.Reference);
        }

        public static void Add<T>(this ArenaList<IntPtr> list, in T uref) where T : struct, IUnmanagedRef {
            list.Add(uref.Reference.RawUnsafePointer);
        }
    }
}
