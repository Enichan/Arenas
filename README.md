# Arenas: Arena allocators for C#/CSharp

This is a .NET Standard 2.0 library that provides access to arena allocators along with the ability to use unmanaged references with natural C# syntax and safe-guards, as well as the ability for arena-allocated items to reference managed C# objects. It is NativeAOT compatible.

Use by creating a `new Arena()` and calling `Allocate` with blittable structs or `AllocCount` to allocate arrays of items. Types that implement `IArenaContents` will automatically have `Free` called and `ArenaID` set where appropriate (without boxing.)

## Features

- Arena allocation for multiple unmanaged types
- All memory is dumped when calling `Clear` or `Dispose` on an arena instance
- Memory reuse via per-size freelists
- Managed C# code can reference unmanaged structs via the `UnmanagedRef<T>` and `UnmanagedRef` types
- Unmanaged C# code can reference managed objects via the `ManagedRef` type
- Managed references are kept alive by the arena via reference counting
- `UnmanagedRef` types return `null`/`IntPtr.Zero` if the reference is stale
- Can enumerate over all entries in the arena
- Allocate any count of items
- Allocate generic buffers of any size by allocating using `AllocCount<byte>(sizeInBytes)`
- Arena object pooling via ArenaPool. Use `ArenaPool.Default.Get()` with `ArenaPool.Default.Return` or automatically return arenas at the end of a `using` block with `using (ArenaPool.Default.Borrow(out var arena))`
- Optimal allocations for buffers where the size doesn't matter to the caller through the `AllocRoughly` method (size may be smaller than requested)
- Debug view will show list of items for `UnmanagedRef` types (handy when inspecting multiple elements)
- Copy `UnmanagedRef` types to arrays via `ToArray` and `CopyTo`
- Generic collections `ArenaList<T>` and `ArenaDict<TKey, TValue>` for storing collections of unmanaged items inside an arena instance
- `ArenaString` type for working with string data in arenas
- Static methods on `ArenaString` for splitting standard C# strings and `char*` pointers into ArenaStrings
- Ability to free items via `IntPtr`
- `UnmanagedRef` is a lightweight struct (only 16 bytes in size) but will cache element counts (always for 7 or fewer elements, and for 32k or fewer elements until item versions exceed 32k)
- `UnmanagedRef` is blittable and can itself be stored inside arenas (see samples)
- Ability to use custom memory allocator for allocating page memory, as well as custom page size

## Samples

Do some stuff with arenas:

```csharp
UnmanagedRef<Person> staleRefTest;

using (var arena = new Arena()) {
    // allocate some people in the arena
    var john = arena.Allocate(new Person());
    john.Value->FirstName = "John";
    john.Value->LastName = "Doe";

    var jack = arena.Allocate(new Person());
    jack.Value->FirstName = "Jack";
    jack.Value->LastName = "Black";

    Console.WriteLine($"Size of UnmanagedRef: {sizeof(UnmanagedRef)}");

    Console.WriteLine(john);
    Console.WriteLine(jack);

    // make a list of integers in the arena
    var list = new ArenaList<int>(arena) { 1, 2, 3 };
    for (int i = 10; i < 22; i++) {
        list.Add(i);
    }

    Console.WriteLine("Values in list:");
    foreach (var i in list) {
        Console.WriteLine(i);
    }

    // make a dictionary of integers in the arena
    var dict = new ArenaDict<int, int>(arena);
    var random = new Random(12345);
    for (int i = 0; i < 20; i++) {
        dict[random.Next(1000)] = random.Next(1000);
    }

    Console.WriteLine("Values in dictionary (sorted by key ascending):");
    foreach (var kvp in from entry in dict orderby entry.Key ascending select entry) {
        Console.WriteLine(kvp);
    }

    Console.WriteLine("Items in arena:");
    foreach (var item in arena) {
        Console.WriteLine(item);
    }

    // free an item
    arena.Free(jack);
    
    Console.WriteLine("Items in arena after freeing:");
    foreach (var item in arena) {
        Console.WriteLine(item);
    }

    // free the rest and show that our references are stale
    arena.Clear();
    Console.WriteLine($"Does stale reference have a value? {john.HasValue}");

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
    Console.WriteLine(isSame ? "ArenaID bytes match" : "ArenaID bytes don't match");

    // split a string into a bunch of ArenaString instances
    using (var splitResults = ArenaString.Split(arena, "Lorem ipsum dolor sit amet", ' ')) {
        for (int i = 0; i < splitResults.Count; i++) {
            var str = splitResults[i];
            Console.WriteLine(str);
            str.Free();
        }
    }

    // final stale reference test for disposal
    staleRefTest = arena.Allocate(new Person());
    staleRefTest.Value->FirstName = "Stale";
    staleRefTest.Value->LastName = "Reference";
}

Console.WriteLine($"Does stale reference have a value after disposal? {staleRefTest.HasValue}");
```

Create a blittable struct with managed references:

```csharp
[StructLayout(LayoutKind.Sequential)]
unsafe public struct Person : IArenaContents {
    // these two lines are boilerplate for IArenaContents structs
    ArenaID IArenaContents.ArenaID { get; set; }
    IArenaMethods IArenaContents.ArenaMethods { get => ArenaMethods<Person>.Instance; }

    private ManagedRef firstName;
    private ManagedRef lastName;

    public override string ToString() {
        return $"{FirstName} {LastName}";
    }

    public void Free() {
        // free managed references by setting to null
        FirstName = null;
        LastName = null;
    }

    public string FirstName {
        get { return firstName.Get<string>(); }
        set { firstName = firstName.Set(ref this, value); }
    }
    public string LastName {
        get { return lastName.Get<string>(); }
        set { lastName = lastName.Set(ref this, value); }
    }
}
```

Store references to items in arena in ArenaList:

```csharp
using (var arena = new Arena()) {
    // allocate a list
    var people = new ArenaList<UnsafeRef>(arena);

    // allocate some people references
    var john = arena.Allocate(new Person());
    john.Value->FirstName = "John";
    john.Value->LastName = "Doe";

    var jack = arena.Allocate(new Person());
    jack.Value->FirstName = "Jack";
    jack.Value->LastName = "Black";

    // store references inside the list
    people.Add(john);
    people.Add(jack);

    // iterate over unmanaged list and write out all the people
    foreach (var item in people) {
        var person = item.As<Person>();
        Console.WriteLine(*person);
    }
}
```

Zero-allocation string splitting via arenas (requires .NET Core):

**This sample was made before the ArenaString type existed as an example of interaction between arenas and the .NET Core Span type. For zero-allocation string splitting please use ArenaString.Split**

```csharp
class Program {
    unsafe static void Main(string[] args) {
        using (var arena = new Arena()) {
            Console.WriteLine($"Original string: {sourceText}");

            // contrived example to split a string into words using an arena
            // in order to avoid allocations
            var words = new ArenaList<Word>(arena);

            var index = 0;
            var startIndex = 0;

            void addWord() {
                var length = index - startIndex;
                if (length > 0) {
                    var chars = arena.AllocCount<char>(length);
                    var source = sourceText.AsSpan(startIndex, length);
                    var dest = new Span<char>(chars.Value, length);
                    source.CopyTo(dest);
                    words.Add(new Word(length, chars.Value));
                }

                startIndex = index + 1;
            };

            while (index < sourceText.Length) {
                var c = sourceText[index];
                if (c == ' ') {
                    addWord();
                }
                index++;
            }

            addWord();

            Console.Write("Split string: ");
            foreach (var word in words) {
                var s = new Span<char>(word.Data, word.Length);
                foreach (var c in s) {
                    Console.Write(c);
                }
                Console.Write(' ');
            }
            Console.WriteLine();

            Console.WriteLine("Arena contents after splitting:");
            foreach (var item in arena) {
                Console.WriteLine($"0x{item.Value:x16}: {item}");
            }
        }
    }

    private static string sourceText = @"Lorem ipsum dolor sit amet, consectetur adipiscing elit. Aliquam sodales elit rutrum iaculis dictum.";

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct Word {
        public int Length;
        public char* Data;

        public Word(int length, char* data) {
            Length = length;
            Data = data;
        }

        public override string ToString() => Data is null || Length <= 0 ? "" : new string(Data, 0, Length);
    }
}
```

"yo dawg i herd u liek arena allocators so i put some arena allocators in ur arena allocators":

```csharp
private static Arena parentArena = new Arena();

private unsafe class ArenaAllocator : IMemoryAllocator {
    public MemoryAllocation Allocate(int sizeBytes) {
        var alloc = parentArena.AllocCount<byte>(sizeBytes);
        return new MemoryAllocation((IntPtr)alloc.Value, alloc.Size);
    }

    public void Free(IntPtr ptr) => parentArena.Free(ptr);
}

static unsafe void ArenaArenas() {
    // by using a page size of 2048 we're actually guaranteeing this allocator
    // will use pages of ~4k, because the size is rounded to the next power of
    // two after adding the item header size
    using (var childArena = new Arena(new ArenaAllocator(), 2048)) {
        var john = childArena.Allocate(new Person());
        john.Value->FirstName = "John";
        john.Value->LastName = "Doe";

        var jack = childArena.Allocate(new Person());
        jack.Value->FirstName = "Jack";
        jack.Value->LastName = "Black";

        Console.WriteLine("Child arena:");
        foreach (var item in childArena) Console.WriteLine(item);
        Console.WriteLine("Parent arena:");
        foreach (var item in parentArena) Console.WriteLine(item);
    }
}
```

## Potential future work

- More arena-specific generic collections like HashSet/Stack/Queue/LinkedList
- More ArenaString refinements
- ManagedObject struct which exists purely to store references to managed objects in arenas?
- Custom per-arena tracing GC?

## Should I use this in production?

Eh, maybe? I feel like the library is pretty mature, so for small projects I think it'd be okay. Probably not for big dang enterprise stuff though. Unless you really want to, I'm not your dad. I'd definitely use it myself at this point, but I'm a game developer, what do I know?
