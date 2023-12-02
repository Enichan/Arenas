using Arenas;
using System;
using System.Runtime.InteropServices;

namespace ArenasTestCore {
    class Program {
        unsafe static void Main(string[] args) {
            using (var arena = new Arena()) {
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
}
