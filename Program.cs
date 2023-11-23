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

                entity.Free();
            }

            entity.Free();
            Console.WriteLine($"Is entity.Value null? {entity.Value == null}");
        }
    }
}
