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

                arena.Allocate(new Entity(13, 12, 69));
                arena.Allocate(new Entity(8, 8, 8));

                foreach (var item in arena) {
                    Console.WriteLine(item);
                }

                entity.Free();
                Console.WriteLine(entity);
            }

            Console.WriteLine($"Is entity.Value null? {entity.Value == null}");
        }
    }
}
