using System;
using System.Linq;
using System.Reflection;
using RimWorld;

public class Test
{
    public static void Main()
    {
        var type = typeof(FloatMenuMakerMap);
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
        {
            Console.WriteLine(method.Name);
        }
    }
}
