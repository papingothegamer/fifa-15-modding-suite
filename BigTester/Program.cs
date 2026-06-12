using System;
using FifaLibrary;

namespace BigTester
{
    class Program
    {
        static void Main(string[] args)
        {
            string filePath = @"C:\Users\Laptop\Downloads\big-bh test\FC26 Scoreboard by Kenobodylikeu\FC26\overlays\overlay_2002.big";
            try
            {
                var bigFile = new FifaBigFile(filePath);
                bigFile.LoadArchivedFiles();
                Console.WriteLine("BigFile loaded. Files count: " + bigFile.Files.Length);

                for (int i = 0; i < bigFile.Files.Length; i++)
                {
                    var f = bigFile.Files[i];
                    if (f != null)
                    {
                        Console.WriteLine($"Index {i}: Name={f.Name}, IsDds={f.IsDds()}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: " + ex.Message);
            }
        }
    }
}
