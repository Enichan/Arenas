using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace Arenas {
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct ArenaID : IEquatable<ArenaID> {
        public readonly int Value;

        public ArenaID(int value) {
            Value = value;
        }

        #region Equality
        public override bool Equals(object obj) {
            return obj is ArenaID iD &&
                   Value == iD.Value;
        }

        public bool Equals(ArenaID other) {
            return other.Value == Value;
        }

        public override int GetHashCode() {
            return 710438321 + Value.GetHashCode();
        }

        public static bool operator ==(ArenaID left, ArenaID right) {
            return left.Equals(right);
        }

        public static bool operator !=(ArenaID left, ArenaID right) {
            return !(left == right);
        }
        #endregion

        public override string ToString() {
            return Value.ToString("x", CultureInfo.InvariantCulture);
        }

        public static ArenaID NewID() {
            return new ArenaID((int)LFSR.Shift());
        }

        private static readonly ArenaID empty = new ArenaID(0);
        public static ArenaID Empty { get { return empty; } }

        /// <summary>
        /// A maximum length 32-bit linear feedback shift register which produces ID numbers
        /// </summary>
        private static class LFSR {
            /// <summary>
            /// 32-bit term that produces a maximum-length LFSR
            /// </summary>
            private const uint feedbackTerm = 0x80000EA6;

            private static uint value;
            private static object lfsrLock = new object();

            static LFSR() {
                value = 0x0BADCAFE;
            }

            public static uint Shift() {
                uint ret;
                lock (lfsrLock) {
                    if (value == feedbackTerm) {
                        ret = value = 0;
                    }
                    else {
                        if (value == 0) {
                            ret = value = feedbackTerm;
                        }

                        if ((value & 1) == 1) {
                            ret = value = (value >> 1) ^ feedbackTerm;
                        }
                        else {
                            ret = value = value >> 1;
                        }
                    }
                }
                if (ret == 0) {
                    return Shift();
                }
                return ret;
            }
        }
    }
}
