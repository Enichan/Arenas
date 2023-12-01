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
        public readonly ItemVersion Item;
        [FieldOffset(sizeof(int))]
        public readonly ArenaID Arena;

        public RefVersion(ItemVersion item, ArenaID arena) {
            Value = 0;
            Item = item;
            Arena = arena;
        }

        public RefVersion IncrementItemVersion(bool valid) {
            return new RefVersion(Item.Increment(valid), Arena);
        }

        public RefVersion SetArenaID(ArenaID id) {
            return new RefVersion(Item, id);
        }

        public RefVersion Invalidate() {
            return new RefVersion(Item.Invalidate(), ArenaID.Empty);
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is RefVersion version && Value == version.Value;
        }

        public bool Equals(RefVersion other) {
            return Value == other.Value;
        }

        public override int GetHashCode() {
            return 1688058797 + Value.GetHashCode();
        }

        public static bool operator ==(RefVersion left, RefVersion right) {
            return left.Equals(right);
        }

        public static bool operator !=(RefVersion left, RefVersion right) {
            return !(left == right);
        }
        #endregion

        public override string ToString() {
            return $"RefVersion(Arena={Arena}, Item={Item})";
        }

        public bool Valid { get { return Arena.Value != 0 && Item.Valid; } }
    }
}
