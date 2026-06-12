using System;
using System.Reflection;
using System.Linq;

namespace Reflector
{
    class Program
    {
        static void Main(string[] args)
        {
            var assembly = Assembly.LoadFile(@"C:\Program Files (x86)\Fifa Master\DB Master\FifaLibrary14.dll");
            var t = assembly.GetTypes().FirstOrDefault(type => type.Name == "FifaBigFile");
            if (t != null)
            {
                Console.WriteLine("====== " + t.Name + " ======");
                foreach (var c in t.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
                    Console.WriteLine("Ctor: " + string.Join(", ", c.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)));
            }
        }
    }
}
