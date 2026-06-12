using System;
using System.IO;
using FifaLibrary;

namespace BigTest
{
    class Program
    {
        static void Main(string[] args)
        {
            string workingPath = @"C:\Users\Laptop\Downloads\big-bh test\backup\overlays\overlay_9002.big";
            string faultyPath = @"C:\Users\Laptop\Downloads\big-bh test\FC26 Scoreboard by Kenobodylikeu\FC26\output-test\overlay_9002.big";

            Console.WriteLine("--- WORKING FIFA 15 ORIGINAL ---");
            try {
                var workingBig = new FifaBigFile(workingPath);
                workingBig.LoadArchivedFiles();
                for(int i=0; i<workingBig.Files.Length; i++) {
                    var f = workingBig.Files[i];
                    Console.WriteLine($"{i}: {f.Name} (Size: {f.UncompressedSize}, Comp: {f.CompressedSize})");
                }
            } catch(Exception e) { Console.WriteLine(e.Message); }

            Console.WriteLine("\n--- FAULTY FIFA 14 PORTED ---");
            try {
                var faultyBig = new FifaBigFile(faultyPath);
                faultyBig.LoadArchivedFiles();
                for(int i=0; i<faultyBig.Files.Length; i++) {
                    var f = faultyBig.Files[i];
                    Console.WriteLine($"{i}: {f.Name} (Size: {f.UncompressedSize}, Comp: {f.CompressedSize})");
                }
            } catch(Exception e) { Console.WriteLine(e.Message); }
        }
    }
}
