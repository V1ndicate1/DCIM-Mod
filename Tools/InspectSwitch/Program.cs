using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

var depDir = @"D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies";
var asmPath = Path.Combine(depDir, "Assembly-CSharp.dll");

AssemblyLoadContext.Default.Resolving += (ctx, name) =>
{
    var candidate = Path.Combine(depDir, name.Name + ".dll");
    if (File.Exists(candidate)) try { return ctx.LoadFromAssemblyPath(candidate); } catch { }
    var candidate2 = Path.Combine(@"D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\net6", name.Name + ".dll");
    if (File.Exists(candidate2)) try { return ctx.LoadFromAssemblyPath(candidate2); } catch { }
    return null;
};

var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(asmPath);
Type[] types;
try { types = asm.GetTypes(); }
catch (ReflectionTypeLoadException ex) { types = ex.Types!; }

Console.WriteLine($"Types loaded: {types.Length}");
var serverType = Array.Find(types, t => t?.FullName == "Il2Cpp.Server");
if (serverType != null)
{
    Console.WriteLine("=== Il2Cpp.Server Properties ===");
    foreach (var p in serverType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Console.WriteLine($"  {p.Name} : {p.PropertyType.Name}");
}
else
    Console.WriteLine("Il2Cpp.Server not found");
