using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    [StructLayout(LayoutKind.Explicit)]
    public readonly struct RefVersion : IEquatable<RefVersion> {
        [FieldOffset(0)]
        public readonly ulong Value;

        [FieldOffset(0)]
        public readonly int Item;
        [FieldOffset(sizeof(int))]
        public readonly int Arena;

        public RefVersion(int item, int arena) {
            Value = 0;
            Item = item;
            Arena = arena;
        }

        public override bool Equals(object obj) {
            return obj is RefVersion version && Value == version.Value;
        }

        public bool Equals(RefVersion other) {
            return Value == other.Value;
        }

        public override int GetHashCode() {
            return Value.GetHashCode();
        }

        public static bool operator ==(RefVersion left, RefVersion right) {
            return left.Equals(right);
        }

        public static bool operator !=(RefVersion left, RefVersion right) {
            return !(left == right);
        }

        public override string ToString() {
            return $"RefVersion(Arena={Arena}, Item={Item})";
        }

        public bool IsValid { get { return (Value & 0xFFFFFFFF00000000UL) != 0; } }
    }
}
