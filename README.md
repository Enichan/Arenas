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

## Sample

```csharp
using (var arena = new Arena()) {
    // allocate some people in the arena
    var john = arena.Allocate(new Person());
    john.Value->FirstName = "John";
    john.Value->LastName = "Doe";

    var jack = arena.Allocate(new Person());
    john.Value->FirstName = "Jack";
    john.Value->LastName = "Black";

    // make a list of integers in the arena
    var list = new ArenaList<int>(arena);
    for (int i = 10; i < 22; i++) {
        list.Add(i);
    }

    Console.WriteLine("Items in arena:");
    foreach (var item in arena) {
        Console.WriteLine(item);
    }

    Console.WriteLine("Values in list:");
    foreach (var i in list) {
        Console.WriteLine(i);
    }

    // free an item
    arena.Free(jack);

    Console.WriteLine("Items in arena after freeing:");
    foreach (var item in arena) {
        Console.WriteLine(item);
    }

    // free the rest
    arena.Clear();

    // make some random bytes using a Guid
    var guid = Guid.NewGuid();
    var guidBytes = guid.ToByteArray();

    // allocate a buffer for the bytes in the arena and copy them
    var unmanagedBytes = arena.AllocCount<byte>(guidBytes.Length);
    Marshal.Copy(guidBytes, 0, (IntPtr)unmanagedBytes.Value, guidBytes.Length);

    // check if the bytes are the same
    var isSame = true;
    for (int i = 0; i < guidBytes.Length; i++) {
        if (guidBytes[i] != *unmanagedBytes[i]) {
            isSame = false;
            break;
        }
    }
    Console.WriteLine(isSame ? "Guid bytes match" : "Guid bytes don't match");
}
```

```csharp
[StructLayout(LayoutKind.Sequential)]
unsafe public struct Person : IArenaContents {
    private Guid arenaID;
    private ManagedRef firstName;
    private ManagedRef lastName;

    void IArenaContents.Free() {
        // free managed references by setting to null
        FirstName = null; 
        LastName = null;
    }

    public override string ToString() {
        return $"{FirstName} {LastName}";
    }

    public string FirstName {
        get { return firstName.Get<string>(); }
        set { firstName = firstName.Set(Arena.Get(arenaID), value); }
    }
    public string LastName {
        get { return lastName.Get<string>(); }
        set { lastName = lastName.Set(Arena.Get(arenaID), value); }
    }

    void IArenaContents.SetArenaID(Guid value) { arenaID = value; }
}
```

## Potential future work

- Custom per-arena tracing GC?

## Should I use this in production?

Probably not. Unless you really want to, I'm not your dad. I'd probably use it myself, but I'm a game developer, what do I know?
