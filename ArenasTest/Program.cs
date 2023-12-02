using Arenas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ArenasTest {
    class Program {
        static unsafe void Main(string[] args) {
            UnsafeRef<Person> staleRefTest;

            using (var arena = new Arena()) {
                // allocate some people in the arena
                var john = arena.Allocate(new Person());
                john.Value->FirstName = "John";
                john.Value->LastName = "Doe";

                var jack = arena.Allocate(new Person());
                jack.Value->FirstName = "Jack";
                jack.Value->LastName = "Black";

                Console.WriteLine(john);
                Console.WriteLine(jack);

                // make a list of integers in the arena
                var list = new ArenaList<int>(arena) { 1, 2, 3 };
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

                // free the rest and show that our references are stale
                arena.Clear();
                Console.WriteLine($"Does stale reference have a value? {john.HasValue}");

                // make some random bytes using a Guid
                var guid = Guid.NewGuid();
                var guidBytes = guid.ToByteArray();

                // allocate a buffer for the bytes in the arena and copy them
                var unmanagedBytes = arena.AllocCount<byte>(guidBytes.Length);
                Console.WriteLine(unmanagedBytes.ElementCount);
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
        }

        static unsafe void UnmanagedPtrList() {
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
        }
    }
}
