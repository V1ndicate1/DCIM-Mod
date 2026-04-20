using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using Microsoft.Win32;

const string RELATIVE_PATH = @"MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll";
const string GAME_FOLDER   = "Data Center";

string dllPath = FindDllPath();

if (dllPath == null)
{
    Console.WriteLine("Could not locate the game automatically.");
    Console.WriteLine(@"Please enter the full path to your Data Center game folder:");
    Console.WriteLine(@"  e.g.  C:\Program Files (x86)\Steam\steamapps\common\Data Center");
    Console.Write("> ");
    string input = Console.ReadLine()?.Trim().Trim('"') ?? "";
    string candidate = Path.Combine(input, RELATIVE_PATH);
    if (File.Exists(candidate))
        dllPath = candidate;
    else
    {
        Console.WriteLine($"File not found at: {candidate}");
        Console.WriteLine("Press any key to exit.");
        Console.ReadKey();
        return;
    }
}

Console.WriteLine($"Found: {dllPath}");
Console.WriteLine();

var backupPath = dllPath + ".bak";
var tempPath   = dllPath + ".tmp";

byte[] bytes = File.ReadAllBytes(dllPath);

bool hasDuplicates = ScanForDuplicates(bytes, verbose: true);
Console.WriteLine();

if (!hasDuplicates)
    Console.WriteLine("DLL is already clean. Running Cecil round-trip to ensure clean PE...");
else
    Console.WriteLine("Duplicates found — running Cecil round-trip to fix...");

CecilFix.Run(dllPath, tempPath);
File.Copy(dllPath, backupPath, overwrite: true);
File.Delete(dllPath);
File.Move(tempPath, dllPath);
Console.WriteLine($"Original backed up to: {backupPath}");

bool stillDirty = ScanForDuplicates(File.ReadAllBytes(dllPath), verbose: false);
Console.WriteLine(stillDirty
    ? "WARNING: duplicates still present after Cecil pass!"
    : "UnityEngine.CoreModule.dll rewritten cleanly.");

Console.WriteLine();
Console.WriteLine("Done! Press any key to exit.");
Console.ReadKey();

// ── Path discovery ────────────────────────────────────────────────────────────

static string FindDllPath()
{
    // 1. Common Steam library locations to probe
    var candidates = new List<string>
    {
        @"C:\Program Files (x86)\Steam\steamapps\common",
        @"C:\Program Files\Steam\steamapps\common",
        @"D:\Steam\steamapps\common",
        @"D:\SteamLibrary\steamapps\common",
        @"E:\Steam\steamapps\common",
        @"E:\SteamLibrary\steamapps\common",
    };

    // 2. Read Steam library folders from registry
    try
    {
        string steamPath = (string)Registry.GetValue(
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null)
            ?? (string)Registry.GetValue(
            @"HKEY_CURRENT_USER\SOFTWARE\Valve\Steam", "SteamPath", null);

        if (steamPath != null)
        {
            candidates.Insert(0, Path.Combine(steamPath, "steamapps", "common"));

            // Parse libraryfolders.vdf for additional library roots
            string vdf = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (string line in File.ReadAllLines(vdf))
                {
                    string trimmed = line.Trim();
                    if (trimmed.StartsWith("\"path\""))
                    {
                        string libPath = trimmed.Split('"')[3].Replace(@"\\", @"\");
                        candidates.Add(Path.Combine(libPath, "steamapps", "common"));
                    }
                }
            }
        }
    }
    catch { /* registry unavailable — continue */ }

    foreach (string root in candidates)
    {
        string candidate = Path.Combine(root, GAME_FOLDER, RELATIVE_PATH);
        if (File.Exists(candidate)) return candidate;
    }

    return null;
}

// ── Duplicate scanner ─────────────────────────────────────────────────────────

static bool ScanForDuplicates(byte[] data, bool verbose)
{
    bool found = false;
    using var ms   = new MemoryStream(data);
    using var pe   = new PEReader(ms);
    var meta = pe.GetMetadataReader(MetadataReaderOptions.None);

    var topSeen = new HashSet<string>();
    foreach (var h in meta.TypeDefinitions)
    {
        var td = meta.GetTypeDefinition(h);
        if (!td.GetDeclaringType().IsNil) continue;
        string fqn = $"{meta.GetString(td.Namespace)}.{meta.GetString(td.Name)}".TrimStart('.');
        if (!topSeen.Add(fqn))
        {
            if (verbose) Console.WriteLine($"  Top-level duplicate: {fqn}");
            found = true;
        }
    }

    var outerToNames = new Dictionary<int, HashSet<string>>();
    foreach (var h in meta.TypeDefinitions)
    {
        var td   = meta.GetTypeDefinition(h);
        var decl = td.GetDeclaringType();
        if (decl.IsNil) continue;
        int encRid = meta.GetRowNumber(decl);
        string name = meta.GetString(td.Name);
        if (!outerToNames.ContainsKey(encRid)) outerToNames[encRid] = new HashSet<string>();
        if (!outerToNames[encRid].Add(name))
        {
            if (verbose)
            {
                var outer = meta.GetTypeDefinition(MetadataTokens.TypeDefinitionHandle(encRid));
                Console.WriteLine($"  Nested duplicate '{name}' in '{meta.GetString(outer.Name)}'");
            }
            found = true;
        }
    }
    return found;
}
