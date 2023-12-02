using System;
using System.Text;

namespace Arenas {
    public interface IUnmanagedRef : IEquatable<UnmanagedRef> {
        UnmanagedRef Reference { get; }
    }
}
