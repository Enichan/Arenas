using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    public readonly struct ArenaEntry : IEquatable<ArenaEntry> {
        public readonly Type Type;
        public readonly IntPtr Ptr;
        public readonly int Size;

        public ArenaEntry(Type type, IntPtr ptr, int size) {
            Type = type;
            Ptr = ptr;
            Size = size;
        }

        public override bool Equals(object obj) {
            return obj is ArenaEntry item &&
                   EqualityComparer<Type>.Default.Equals(Type, item.Type) &&
                   EqualityComparer<IntPtr>.Default.Equals(Ptr, item.Ptr) &&
                   Size == item.Size;
        }

        public bool Equals(ArenaEntry other) {
            return 
                   EqualityComparer<Type>.Default.Equals(Type, other.Type) &&
                   EqualityComparer<IntPtr>.Default.Equals(Ptr, other.Ptr) &&
                   Size == other.Size;
        }

        public override int GetHashCode() {
            int hashCode = -720610513;
            hashCode = hashCode * -1521134295 + EqualityComparer<Type>.Default.GetHashCode(Type);
            hashCode = hashCode * -1521134295 + Ptr.GetHashCode();
            hashCode = hashCode * -1521134295 + Size.GetHashCode();
            return hashCode;
        }

        public static bool operator ==(ArenaEntry left, ArenaEntry right) {
            return left.Equals(right);
        }

        public static bool operator !=(ArenaEntry left, ArenaEntry right) {
            return !(left == right);
        }

        public override string ToString() {
            return $"ArenaEntry(Type={Type}, Ptr=0x{Ptr.ToInt64().ToString("x")}, Size={Size})";
        }
    }
}
