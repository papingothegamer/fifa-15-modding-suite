using System;
using System.Reflection;
using FifaLibrary;

class Program
{
    static void Main()
    {
        var type = typeof(DdsFile);
        foreach (var method in type.GetMethods())
        {
            Console.WriteLine(method.Name);
        }
    }
}
