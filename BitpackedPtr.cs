using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Arenas {
    internal readonly struct BitpackedPtr : IEquatable<BitpackedPtr> {
        internal const int LowerMask = 0b111;
        internal const ulong PointerMask = ~0b111UL;

        private readonly ulong value;

        public BitpackedPtr(IntPtr ptr, int packedValue) {
            if (packedValue < 0 || packedValue > LowerMask) {
                throw new ArgumentOutOfRangeException(nameof(packedValue));
            }
            Debug.Assert(((ulong)ptr & LowerMask) == 0);
            value = (ulong)ptr | (uint)packedValue;
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is BitpackedPtr ptr &&
                   value == ptr.value;
        }

        public bool Equals(BitpackedPtr other) {
            return value == other.value;
        }

        public override int GetHashCode() {
            return 731850958 + value.GetHashCode();
        }

        public static bool operator ==(BitpackedPtr left, BitpackedPtr right) {
            return left.Equals(right);
        }

        public static bool operator !=(BitpackedPtr left, BitpackedPtr right) {
            return !(left == right);
        }
        #endregion

        public override string ToString() {
            return $"{Value}:{PackedValue}";
        }

        public IntPtr Value { get { return (IntPtr)(value & PointerMask); } }
        public int PackedValue { get { return (int)value & LowerMask; } }
    }
}
