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
                var ent2 = arena.Allocate(new Entity());

                entity = arena.Allocate(new Entity(1, 2, 3));
                entity.Value->Name = "John Doe";
                entity.Value->Y = 8;
                Console.WriteLine(entity);

                ent2.Free();
                arena.Allocate(new Entity());

                entity.Free();
                Console.WriteLine(entity);
            }

            Console.WriteLine($"Is entity.Value null? {entity.Value == null}");
        }
    }
}
