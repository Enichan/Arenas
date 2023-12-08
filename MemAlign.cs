using System;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public static class MemAlign {
        public static int Floor(int addr, int size) {
            return addr & (~(size - 1));
        }

        public static int Ceil(int addr, int size) {
            return (addr + (size - 1)) & (~(size - 1));
        }

        public static IntPtr Floor(IntPtr addr, int size) {
            return (IntPtr)Floor((ulong)addr, size);
        }

        public static IntPtr Ceil(IntPtr addr, int size) {
            return (IntPtr)Ceil((ulong)addr, size);
        }

        public static ulong Floor(ulong addr, int size) {
            var sizel = (ulong)size;
            return addr & (~(sizel - 1));
        }

        public static ulong Ceil(ulong addr, int size) {
            var sizel = (ulong)size;
            return (addr + (sizel - 1)) & (~(sizel - 1));
        }
    }
}
