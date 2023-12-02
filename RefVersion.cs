using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    // Versions contain information about the arena ID, item version number, and optional
    // element count. This allows UnmanagedRef to store the element count for items with low
    // version numbers (up to 32,767) to store element counts up to 32,767. The valid bit
    // indicates that an item is allocated in the arena if set, and indicates free space
    // if unset.
    // 
    // Bit index:  3333333333333333 2222222222222222 1111111111111111 0000000000000000
    //             FEDCBA9876543210 FEDCBA9876543210 FEDCBA9876543210 FEDCBA9876543210
    // 
    // Bit layout: AAAAAAAAAAAAAAAA AAAAAAAAAAAAAAAA VEEEEEEEEEEEEEEE IIIIIIIIIIIIIIIS
    //             AAAAAAAAAAAAAAAA AAAAAAAAAAAAAAAA VIIIIIIIIIIIIIII IIIIIIIIIIIIIIIL
    //         
    // A = arena ID (32 bits)
    // V = item version valid bit (valid if set)
    // E = element count (0 bits long, 15 bits short)
    // I = item version (30 bits long, 15 bits short)
    // S = short version (lowest bit unset)
    // L = long version (lowest bit set)
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

        public RefVersion IncrementItemVersion(bool valid, int elementCount) {
            return new RefVersion(Item.Increment(valid, elementCount), Arena);
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
