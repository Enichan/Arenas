# Arenas

Proof of concept for arena allocator inside C#. Use by creating a `new Arena()` and calling `Allocate` with blittable structs that implement `IArenaContents` or `AllocValue(s)` with unmanaged types that do not implement `IArenaContents`, and `Free` or `FreeValues` with instances of `UnmanagedRef<T>`. Also supports `Clear` and `Dispose`.

## Features

- Arena allocation for multiple unmanaged types
- All memory is dumped when calling `Clear` or `Dispose` on an arena instance
- Memory reuse via per-type freelists
- Managed C# code can reference unmanaged structs via the `UnmanagedRef<T>` type
- Unmanaged C# code can reference managed objects via the `ManagedRef` type
- Managed references are kept alive by the arena via reference counting
- `UnmanagedRef<T>` returns null if the reference is stale
- Can enumerate over all entries in the arena
- Allocate any count of items
- Allocate generic buffers of any size by allocating using `AllocValues<byte>(sizeInBytes)`

## Potential future work

- One freelist per size instead of one freelist per size and type
- Custom per-arena tracing GC

## Should I use this in production?

Absolutely not. Unless you really want to, I'm not your dad. I'd probably use it myself, but I'm a game developer, what do I know?
