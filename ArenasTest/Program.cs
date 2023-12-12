using Arenas;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ArenasTest {
    class Program {
        static unsafe void Main(string[] args) {
            UnmanagedRef<Person> staleRefTest;

            using (var arena = new Arena()) {
                var buffer = new string(' ', 64);
                fixed (char* bs = buffer) {
                    bs[0] = '3';
                    bs[1] = '.';
                    bs[2] = '1';
                    bs[3] = '4';
                }
                Console.WriteLine(double.Parse(buffer));
                
                var s = new ArenaString(arena, "Hello world!");
                Console.WriteLine(s);

                Console.WriteLine(s.Substring(2, 5));

                using (var tokens = s.Split(new string[] { "ello", "lo", "rl", "!" }, int.MaxValue, StringSplitOptions.None)) {
                    foreach (var token in tokens) {
                        Console.WriteLine(token);
                    }
                }

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

                // final stale reference test for disposal
                staleRefTest = arena.Allocate(new Person());
                staleRefTest.Value->FirstName = "Stale";
                staleRefTest.Value->LastName = "Reference";
            }

            Console.WriteLine($"Does stale reference have a value after disposal? {staleRefTest.HasValue}");

            Console.WriteLine();
            Console.WriteLine("Running unmanaged list of pointers code");
            UnmanagedPtrList();

            Console.WriteLine();
            Console.WriteLine("Running arenas in arenas");
            ArenaArenas();
        }

        static unsafe void UnmanagedPtrList() {
            using (var arena = new Arena()) {
                // allocate a list
                var people = new ArenaList<UnmanagedRef>(arena);

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

                people.Free();
            }
        }

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
    }
}
