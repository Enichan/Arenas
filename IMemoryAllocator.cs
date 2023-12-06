using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Arenas {
    public interface IMemoryAllocator {
        MemoryAllocation Allocate(int sizeBytes);
        void Free(IntPtr ptr);
    }

    public readonly struct MemoryAllocation {
        public readonly IntPtr Pointer;
        public readonly int SizeBytes;

        public MemoryAllocation(IntPtr pointer, int sizeBytes) {
            Pointer = pointer;
            SizeBytes = sizeBytes;
        }

        public override string ToString() {
            return $"Allocation(0x{(ulong)Pointer:x}, {SizeBytes} bytes)";
        }
    }
}
