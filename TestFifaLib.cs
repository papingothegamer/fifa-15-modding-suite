using System;
using System.Reflection;
using System.Linq;

class Program
{
    static void Main()
    {
        try
        {
            var asm = Assembly.LoadFile(@"C:\Users\Laptop\Downloads\15 mods\FIFLibrary_25.05.02\Release\FIFALibrary.dll");
            var dbFile = asm.GetType("FifaLibrary.DbFile");
            if (dbFile != null)
            {
                Console.WriteLine("Found DbFile!");
                foreach(var m in dbFile.GetMethods().Where(x => x.IsPublic))
                {
                    Console.WriteLine("Method: " + m.Name);
                }
            }

            var fifaEnv = asm.GetType("FifaLibrary.FifaEnvironment");
            if (fifaEnv != null)
            {
                Console.WriteLine("Found FifaEnvironment!");
                foreach(var p in fifaEnv.GetProperties())
                {
                    Console.WriteLine("Property: " + p.Name);
                }
            }
        }
        catch(Exception e)
        {
            Console.WriteLine(e.ToString());
        }
    }
}
