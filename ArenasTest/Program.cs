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
            UnmanagedRef<Entity> entity;

            using (var arena = new Arena()) {
                entity = arena.Allocate(new Entity(1, 2, 3));
                entity.Value->Name = "John Doe";
                entity.Value->Y = 8;
                Console.WriteLine(entity);

                var typelessRef = (UnmanagedRef)entity;
                var typelessArr = typelessRef.ToArray<object>();
                arena.Free(typelessRef.Value);
                Console.WriteLine(typelessRef);
                Console.WriteLine(Marshal.SizeOf(typelessRef));

                var bytes = arena.AllocCount<byte>(129);
                for (int i = 0; i < bytes.ElementCount; i++) {
                    bytes.Value[i] = (byte)(bytes.ElementCount - 1 - i);
                }

                arena.Allocate(new Entity(13, 12, 69));

                arena.Free(bytes);
                arena.AllocCount<Entity>(3);

                arena.Allocate(new Entity(8, 8, 8));

                foreach (var item in arena) {
                    Console.WriteLine($"{item}, {item.ElementCount} elements");
                }

                arena.Free(entity);
                Console.WriteLine(entity);

                var list = new ArenaList<int>(arena, 6);
                list.Add(5);
                list.Add(8);
                list.Add(1);

                for (int i = 0; i < 8; i++) {
                    list.Add(i + 10);
                }

                Console.WriteLine(list.IndexOf(10));
                Console.WriteLine(list.IndexOf(666));
                Console.WriteLine(list.Contains(10));
                Console.WriteLine(list.Contains(666));

                foreach (var val in list) {
                    Console.WriteLine(val);
                }

                foreach (var item in arena) {
                    Console.WriteLine($"{item}, {item.ElementCount} elements");
                }

                Console.WriteLine("Freeing list");
                list.Free();
                Console.WriteLine($"List is allocated: {list.IsAllocated}");

                foreach (var item in arena) {
                    Console.WriteLine($"{item}, {item.ElementCount} elements");
                }
            }

            Console.WriteLine($"Is entity.Value null? {entity.Value == null}");
        }
    }
}
