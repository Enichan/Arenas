using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ItemVersion : IEquatable<ItemVersion> {
        private readonly int rawValue;

        public ItemVersion(int version, bool valid) {
            Debug.Assert((version & -2147483648) == 0);
            if (!valid) {
                version |= -2147483648;
            }
            rawValue = version;
        }

        public ItemVersion Invalidate() {
            return new ItemVersion(Version, false);
        }

        public ItemVersion Increment(bool valid) {
            var newVersion = Version + 1;
            newVersion &= 0x7FFFFFFF;
            if (newVersion == 0) {
                newVersion = 1;
            }
            return new ItemVersion(newVersion, valid);
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
            return !Valid ? $"{Version:x} (invalid)" : $"{Version}";
        }

        public int Version { get { return rawValue & 0x7FFFFFFF; } }
        public bool Valid { get { return (rawValue & -2147483648) == 0; } }
    }
}
