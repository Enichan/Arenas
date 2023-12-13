using System;
using System.Collections.Generic;
using System.Text;

namespace Arenas {
    public static class ArenaStringExtensions {
        public static bool IsNullOrEmpty(this ArenaString value) {
            return value.Length == 0;
        }

        public static bool IsNullOrWhiteSpace(this ArenaString value) {
            if (!value.IsAllocated) {
                return true;
            }
            var trimmed = value.Trim();
            try {
                return trimmed.Length == 0;
            }
            finally {
                trimmed.Free();
            }
        }
    }
}
