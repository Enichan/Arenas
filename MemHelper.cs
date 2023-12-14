using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    public static class MemHelper {
        [DllImport("kernel32.dll")]
        private static extern void RtlZeroMemory(IntPtr dst, UIntPtr length);

        private delegate void ZeroMemoryDelegate(IntPtr dst, UIntPtr length);
        private static readonly ZeroMemoryDelegate zeroMemory;

        static MemHelper() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) {
                zeroMemory = RtlZeroMemory;
            }
            else {
                zeroMemory = ZeroMemPlatformIndependent;
            }
        }

        public static void ZeroMemory(IntPtr dst, UIntPtr length) {
            zeroMemory(dst, length);
        }

        public static void ZeroMemory(IntPtr dst, int length) {
            if (length < 0) {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            zeroMemory(dst, (UIntPtr)length);
        }

        private static readonly Dictionary<ulong, int> powerOfTwoLeadingZeros = new Dictionary<ulong, int>() {
            { 1 << 0, 0 }, { 1 << 1, 1 }, { 1 << 2, 2 }, { 1 << 3, 3 },
            { 1 << 4, 4 }, { 1 << 5, 5 }, { 1 << 6, 6 }, { 1 << 7, 7 },
            { 1 << 8, 8 }, { 1 << 9, 9 }, { 1 << 10, 10 }, { 1 << 11, 11 },
            { 1 << 12, 12 }, { 1 << 13, 13 }, { 1 << 14, 14 }, { 1 << 15, 15 },
            { 1 << 16, 16 }, { 1 << 17, 17 }, { 1 << 18, 18 }, { 1 << 19, 19 },
            { 1 << 20, 20 }, { 1 << 21, 21 }, { 1 << 22, 22 }, { 1 << 23, 23 },
            { 1 << 24, 24 }, { 1 << 25, 25 }, { 1 << 26, 26 }, { 1 << 27, 27 },
            { 1 << 28, 28 }, { 1 << 29, 29 }, { 1 << 30, 30 }, { 1UL << 31, 31 },
            { 1UL << 32, 32 }, { 1UL << 33, 33 }, { 1UL << 34, 34 }, { 1UL << 35, 35 },
            { 1UL << 36, 36 }, { 1UL << 37, 37 }, { 1UL << 38, 38 }, { 1UL << 39, 39 },
            { 1UL << 40, 40 }, { 1UL << 41, 41 }, { 1UL << 42, 42 }, { 1UL << 43, 43 },
            { 1UL << 44, 44 }, { 1UL << 45, 45 }, { 1UL << 46, 46 }, { 1UL << 47, 47 },
            { 1UL << 48, 48 }, { 1UL << 49, 49 }, { 1UL << 50, 50 }, { 1UL << 51, 51 },
            { 1UL << 52, 52 }, { 1UL << 53, 53 }, { 1UL << 54, 54 }, { 1UL << 55, 55 },
            { 1UL << 56, 56 }, { 1UL << 57, 57 }, { 1UL << 58, 58 }, { 1UL << 59, 59 },
            { 1UL << 60, 60 }, { 1UL << 61, 61 }, { 1UL << 62, 62 }, { 1UL << 63, 63 },
        };
        public static IReadOnlyDictionary<ulong, int> PowerOfTwoLeadingZeros { get { return powerOfTwoLeadingZeros; } }

        // http://graphics.stanford.edu/%7Eseander/bithacks.html#RoundUpPowerOf2
        public static ulong NextPowerOfTwo(ulong v) {
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v |= v >> 32;
            v++;
            return v;
        }

        public static bool IsPowerOfTwo(ulong v) {
            return powerOfTwoLeadingZeros.ContainsKey(v);
        }

        public static int AlignFloor(int addr, int size) {
            return addr & (~(size - 1));
        }

        public static int AlignCeil(int addr, int size) {
            return (addr + (size - 1)) & (~(size - 1));
        }

        public static IntPtr AlignFloor(IntPtr addr, int size) {
            return (IntPtr)AlignFloor((ulong)addr, size);
        }

        public static IntPtr AlignCeil(IntPtr addr, int size) {
            return (IntPtr)AlignCeil((ulong)addr, size);
        }

        public static ulong AlignFloor(ulong addr, int size) {
            var sizel = (ulong)size;
            return addr & (~(sizel - 1));
        }

        public static ulong AlignCeil(ulong addr, int size) {
            var sizel = (ulong)size;
            return (addr + (sizel - 1)) & (~(sizel - 1));
        }

        private unsafe static void ZeroMemPlatformIndependent(IntPtr ptr, UIntPtr length) {
            ulong size = (ulong)length;

            // clear to word alignment
            var byteptr = (byte*)ptr;
            var bytes = (int)((ulong)byteptr & 0b111);
            for (int i = 0; i < bytes; i++, byteptr++) {
                *byteptr = 0;
            }

            size -= (ulong)bytes;

            // clear words
            var count = size / sizeof(ulong);
            var longptr = (ulong*)byteptr;

            for (ulong i = 0; i < count; i++, longptr++) {
                *longptr = 0;
            }

            size -= count * sizeof(ulong);

            // clear remaining bytes
            byteptr = (byte*)longptr;
            bytes = (int)size;
            for (int i = 0; i < bytes; i++, byteptr++) {
                *byteptr = 0;
            }
        }
    }
}
