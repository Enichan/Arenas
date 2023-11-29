using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    internal static class CollectionExtensions {
        public static void SetLast<T>(this IList<T> list, T item) {
            if (list is null) {
                throw new ArgumentNullException(nameof(list));
            }
            if (list.Count == 0) {
                throw new ArgumentOutOfRangeException("List in SetLast was empty");
            }
            list[list.Count - 1] = item;
        }

        public static void SetLast<T>(this IList<T> list, ref T item) {
            if (list is null) {
                throw new ArgumentNullException(nameof(list));
            }
            if (list.Count == 0) {
                throw new ArgumentOutOfRangeException("List in SetLast was empty");
            }
            list[list.Count - 1] = item;
        }
    }
}
