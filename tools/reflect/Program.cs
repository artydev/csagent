using System.Reflection;
using Photino.NET;

var t = typeof(PhotinoWindow);
foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
{
    if (m.Name.StartsWith("Set") || m.Name.StartsWith("Register") || m.Name.StartsWith("Load") ||
        m.Name.StartsWith("SendWebMessage") || m.Name.StartsWith("Center") || m.Name.StartsWith("WaitForClose"))
    {
        Console.WriteLine(m.ToString());
    }
}
