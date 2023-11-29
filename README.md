# Arenas: Arena allocators for C#/CSharp

This is a .NET Standard 2.0 library that provides access to arena allocators along with the ability to use unmanaged references with natural C# syntax and safe-guards, as well as the ability for arena-allocated items to reference managed C# objects.

Use by creating a `new Arena()` and calling `Allocate` with blittable structs or `AllocCount` to allocate arrays of items. Types that implement `IArenaContents` will automatically have `Free` and `SetArenaID` called where appropriate (without boxing.)

## Features

- Arena allocation for multiple unmanaged types
- All memory is dumped when calling `Clear` or `Dispose` on an arena instance
- Memory reuse via per-size freelists
- Managed C# code can reference unmanaged structs via the `UnmanagedRef<T>` and `UnmanagedRef` types
- Unmanaged C# code can reference managed objects via the `ManagedRef` type
- Managed references are kept alive by the arena via reference counting
- `UnmanagedRef` types returns null/IntPtr.Zero if the reference is stale
- Can enumerate over all entries in the arena
- Allocate any count of items
- Allocate generic buffers of any size by allocating using `AllocCount<byte>(sizeInBytes)`
- Debug view will show list of items for `UnmanagedRef` types (handy when inspecting multiple elements)
- Copy `UnmanagedRef` types to arrays via `ToArray` and `CopyTo`
- Generic `ArenaList<T>` type for storing lists inside an arena instance
- Ability to free items via IntPtr.

## Potential future work

- Custom per-arena tracing GC?

## Should I use this in production?

Probably not. Unless you really want to, I'm not your dad. I'd probably use it myself, but I'm a game developer, what do I know?
