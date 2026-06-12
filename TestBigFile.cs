using System;
using System.IO;
using FifaLibrary;

namespace TestBig
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                var bigPath = @"C:\Users\Laptop\Documents\Projects\fifa-15-modding-suite\Files\globalcomponents\overlaycomponents_9\overlaycomponents_15.big";
                var big = new FifaBigFile(bigPath);
                big.LoadArchivedFiles();
                
                var files = big.Files;
                Console.WriteLine(string.Format("Total files in .big: {0}", files.Length));
                
                for (int i = 0; i < files.Length; i++)
                {
                    var f = files[i];
                    Console.WriteLine(string.Format("File {0}: IsDds={1}, Name={2}, Compressed={3}, Uncompressed={4}", i, f.IsDds(), f.Name, f.CompressedSize, f.UncompressedSize));
                    
                    if (f.IsDds())
                    {
                        var dds = new DdsFile();
                        dds.Load(f);
                        var bmp = dds.GetBitmap();
                        Console.WriteLine(string.Format("  -> Bitmap: {0}", bmp != null ? string.Format("{0}x{1}", bmp.Width, bmp.Height) : "NULL"));
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
