using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    // Bit packing layout. For more information see RefVersion.cs
    // 
    // Bit index:  1111111111111111 0000000000000000
    //             FEDCBA9876543210 FEDCBA9876543210
    // 
    // Bit layout: VEEEEEEEEEEEEEEE IIIIIIIIIIIIIIIS
    //             VIIIIIIIIIIIIIII IIIIIIIIIIIIIIIL
    //         
    // V = item version valid bit (valid if set)
    // E = element count (0 bits long, 15 bits short)
    // I = item version (30 bits long, 15 bits short)
    // S = short version (lowest bit unset)
    // L = long version (lowest bit set)
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ItemVersion : IEquatable<ItemVersion> {
        private const int validBit = unchecked((int)0x80000000); // -2147483648
        private const int versionBitIndex = 1;
        private const int maxShortVersion = 0x7FFF;
        private const int shortVersionMask = maxShortVersion << versionBitIndex;
        private const int maxVersion = 0x3FFFFFFF;
        private const int longVersionMask = maxVersion << versionBitIndex;
        private const int maxElementCount = 0x7FFF;
        private const int elementCountBitIndex = versionBitIndex + 15;
        private const int elementCountMask = maxElementCount << elementCountBitIndex;

        private readonly int rawValue;

        public ItemVersion(int rawValue) {
            this.rawValue = rawValue;
        }

        public ItemVersion(bool isShort, int version, int elementCount, bool valid) {
            int value;

            if (isShort) {
                Debug.Assert((version & (~maxShortVersion)) == 0);
                Debug.Assert((elementCount & (~maxElementCount)) == 0);
                value = (version << versionBitIndex) | (elementCount << elementCountBitIndex);
            }
            else {
                Debug.Assert((version & (~maxVersion)) == 0);
                value = (version << versionBitIndex) | 1;
            }

            if (valid) {
                value |= validBit;
            }

            rawValue = value;
        }

        public ItemVersion Invalidate() {
            return new ItemVersion(IsShortVersion, Version, ElementCount, false);
        }

        public ItemVersion Increment(bool valid, int elementCount) {
            int newVersion = Version + 1;
            bool isShort = IsShortVersion && newVersion <= maxShortVersion;

            newVersion &= maxVersion;
            if (newVersion == 0) {
                newVersion = 1;
            }

            if (!isShort || elementCount < 0 || elementCount > maxElementCount) {
                elementCount = 0;
            }

            return new ItemVersion(isShort, newVersion, elementCount, valid);
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is ItemVersion version &&
                   rawValue == version.rawValue;
        }

        public bool Equals(ItemVersion other) {
            throw new NotImplementedException();
        }

        public override int GetHashCode() {
            return -8906994 + rawValue.GetHashCode();
        }

        public static bool operator ==(ItemVersion left, ItemVersion right) {
            return left.Equals(right);
        }

        public static bool operator !=(ItemVersion left, ItemVersion right) {
            return !(left == right);
        }
        #endregion

        public override string ToString() {
            var v = Version;
            return !Valid ? $"{v} (invalid)" : $"{v}";
        }

        public int ElementCount { get { return IsShortVersion ? (rawValue & elementCountMask) >> elementCountBitIndex : 0; } }
        public int Version { get { return (IsShortVersion ? rawValue & shortVersionMask : rawValue & longVersionMask) >> versionBitIndex; } }
        public bool Valid { get { return (rawValue & validBit) != 0; } }
        public bool HasElementCount { get { return IsShortVersion && (rawValue & elementCountMask) != 0; } }
        public bool IsShortVersion { get { return (rawValue & 1) == 0; } }
    }
}
