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
        }
    }
}
