using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Arenas {
    class Program {
        unsafe static void Main(string[] args) {
            UnmanagedRef<Entity> entity;

            using (var arena = new Arena()) {
                entity = arena.Allocate(new Entity(1, 2, 3));
                entity.Value->Name = "John Doe";
                entity.Value->Y = 8;
                Console.WriteLine(entity);

                var bytes = arena.AllocValues<byte>(129);
                for (int i = 0; i < bytes.ElementCount; i++) {
                    *(bytes.Value + i) = (byte)i;
                }

                arena.AllocValue(new Entity(13, 12, 69));
                arena.Allocate(new Entity(8, 8, 8));

                foreach (var item in arena) {
                    Console.WriteLine(item);
                }

                arena.Free(entity);
                Console.WriteLine(entity);
            }

            Console.WriteLine($"Is entity.Value null? {entity.Value == null}");
        }
    }
}
