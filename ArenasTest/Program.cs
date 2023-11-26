using Arenas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArenasTest {
    class Program {
        static unsafe void Main(string[] args) {
            UnmanagedRef<Entity> entity;

            using (var arena = new Arena()) {
                entity = arena.Allocate(new Entity(1, 2, 3));
                entity.Value->Name = "John Doe";
                entity.Value->Y = 8;
                Console.WriteLine(entity);

                var bytes = arena.AllocValues<byte>(129);
                for (int i = 0; i < bytes.ElementCount; i++) {
                    bytes.Value[i] = (byte)(bytes.ElementCount - 1 - i);
                }

                arena.Allocate(new Entity(13, 12, 69));

                arena.FreeValues(bytes);
                arena.Allocate<Entity>(3);

                arena.Allocate(new Entity(8, 8, 8));

                foreach (var item in arena) {
                    Console.WriteLine($"{item}, {item.ElementCount} elements");
                }

                arena.Free(entity);
                Console.WriteLine(entity);
            }

            Console.WriteLine($"Is entity.Value null? {entity.Value == null}");
        }
    }
}
