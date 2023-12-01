using System;
using System.Text;

namespace Arenas {
    public interface IUnmanagedRef : IEquatable<UnsafeRef> {
        UnsafeRef Reference { get; }
        UnmanagedRef ToUnmanaged();
    }
}
