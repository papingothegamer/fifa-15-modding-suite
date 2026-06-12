using System;
using FifaLibrary;

class Program
{
    static void Main()
    {
        string filePath = @""C:\Users\Laptop\Downloads\big-bh test\FC26 Scoreboard by Kenobodylikeu\FC26\overlays\overlay_2002.big"";
        try
        {
            var bigFile = new FifaBigFile(filePath);
            Console.WriteLine(""BigFile loaded. Files count: "" + bigFile.Files.Length);
            
            for (int i = 0; i < bigFile.Files.Length; i++)
            {
                var f = bigFile.Files[i];
                if (f != null && f.IsDds())
                {
                    Console.WriteLine(""Found DDS at index "" + i);
                    var ddsFile = bigFile.GetArchivedFile(i);
                    var dds = new DdsFile();
                    dds.Load(ddsFile);
                    var bmp = dds.GetBitmap();
                    Console.WriteLine(""Bitmap generated: "" + (bmp != null));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(""Exception: "" + ex.Message);
        }
    }
}
