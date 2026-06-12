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
            Console.WriteLine(""BigFile loaded. Files count: "" + (bigFile.Files != null ? bigFile.Files.Length.ToString() : ""null""));
        }
        catch (Exception ex)
        {
            Console.WriteLine(""Exception: "" + ex.Message + ""\n"" + ex.StackTrace);
        }
    }
}
