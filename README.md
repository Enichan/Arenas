# Arenas

Proof of concept for arena allocator inside C#. Use by creating a `new Arena()` and calling `Allocate` with blittable structs that implement `IArenaContents`, and `Free` with instances of `UnmanagedRef<T>`. Also supports `Clear` and `Dispose`.

## Features

- Arena allocation for multiple unmanaged types
- All memory is dumped when calling `Clear` or `Dispose` on an arena instance
- Memory reuse via per-type freelists
- Managed C# code can reference unmanaged structs via the `UnmanagedRef<T>` type
- Unmanaged C# code can reference managed structs via the `ManagedRef` type
- Managed references are kept alive by the arena via reference counting

## Potential future work

- Custom per-arena tracing GC
- Freestyle allocations of a certain size
