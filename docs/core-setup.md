# Core Setup — MelonLoader Mod for Data Center

## .csproj Template

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Library</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <Nullable>disable</Nullable>  <!-- REQUIRED — IL2CPP assemblies lack NullableAttribute -->
    <AssemblySearchPaths>{CandidateAssemblyFiles};{HintPathFromItem};{TargetFrameworkDirectory};{RawFileName};D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\net6;D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies</AssemblySearchPaths>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="MelonLoader">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\net6\MelonLoader.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\net6\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2CppInterop.Runtime">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\net6\Il2CppInterop.Runtime.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Assembly-CSharp">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Il2Cppmscorlib">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\Il2Cppmscorlib.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UI">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\UnityEngine.UI.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.UIModule">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\UnityEngine.UIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.TextRenderingModule">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\UnityEngine.TextRenderingModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="Unity.TextMeshPro">
      <HintPath>D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Il2CppAssemblies\Unity.TextMeshPro.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <!-- Add for InputSystem: Unity.InputSystem.dll -->
    <!-- Add for InputLegacy: UnityEngine.InputLegacyModule.dll -->
  </ItemGroup>
</Project>
```

**Note:** All paths above assume `D:\SteamLibrary\...` — adjust to match your Steam install location.

## Mod Entry Point

```csharp
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;

[assembly: MelonInfo(typeof(MyNamespace.MyMod), "ModName", "1.0.0", "Author")]
[assembly: MelonGame("Waseku", "Data Center")]

namespace MyNamespace
{
    public class MyMod : MelonMod
    {
        public override void OnInitializeMelon() { }        // register prefs, tasks, hooks
        public override void OnLateInitializeMelon() { }   // after all mods init
        public override void OnDeinitializeMelon() { }     // on unload
        public override void OnUpdate() { }                // every frame
        public override void OnLateUpdate() { }
        public override void OnFixedUpdate() { }
        public override void OnSceneWasLoaded(int buildIndex, string sceneName) { }
        public override void OnSceneWasInitialized(int buildIndex, string sceneName) { }
        public override void OnApplicationQuit() { }
        public override void OnPreferencesSaved() { }
        public override void OnPreferencesLoaded() { }
        public override void OnSceneWasUnloaded(int buildIndex, string sceneName) { }
    }
}
```

## ⚠️ IL2CPP Namespace Rules — CRITICAL

All game classes live in `Il2Cpp` namespace in the interop assemblies.

| dc_decompiled shows | Use in mod code |
|---|---|
| `Server` | `Il2Cpp.Server` (or `using Il2Cpp;`) |
| `NetworkSwitch` | `Il2Cpp.NetworkSwitch` |
| `AssetManagement` | `Il2Cpp.AssetManagement` |
| `TechnicianManager` | `Il2Cpp.TechnicianManager` |
| `PlayerManager` | `Il2Cpp.PlayerManager` |
| `StaticUIElements` | `Il2Cpp.StaticUIElements` |
| `HRSystem` | `Il2Cpp.HRSystem` |
| `ComputerShop` | `Il2Cpp.ComputerShop` |
| `NetworkMap` | `Il2Cpp.NetworkMap` |
| `TimeController` | `Il2Cpp.TimeController` |
| `Objectives` | `Il2Cpp.Objectives` |
| `TMPro.*` | `Il2CppTMPro.*` |
| `PolyAndCode.UI.*` | `Il2CppPolyAndCode.UI.*` |

**UnityEngine types keep their original namespaces** — `UnityEngine.UI.Button`, `UnityEngine.UI.Image`, etc. unchanged.

**Harmony patch typeof must use Il2Cpp namespace:**
```csharp
[HarmonyPatch(typeof(Server), "ItIsBroken")]   // works with `using Il2Cpp;`
```

## ⚠️ Il2CppSystem Collections — foreach Broken

```csharp
// WRONG — foreach fails on Il2CppSystem collections
foreach (var tech in tm.technicians) { }

// CORRECT — index loop
for (int i = 0; i < tm.technicians.Count; i++) { }

// CORRECT — FindObjectsOfType returns Il2CppArrayBase, use indexed for-loop (NOT foreach)
var servers = Object.FindObjectsOfType<Server>();
for (int i = 0; i < servers.Length; i++) { var s = servers[i]; }
```

## ⚠️ IL2CPP Unity Type Quirks

**RectOffset — no 4-arg constructor:**
```csharp
var p = new RectOffset();
p.left = 6; p.right = 6; p.top = 2; p.bottom = 2;
```

**Button listener:**
```csharp
btn.onClick.AddListener(new System.Action(() => { /* handler */ }));
```

**MelonPreferences CreateEntry is NOT idempotent — throws if key exists:**
```csharp
// CORRECT — always use GetOrCreate pattern
private static MelonPreferences_Entry<T> GetOrCreate<T>(string key, T def, string label)
    => _cat.GetEntry<T>(key) ?? _cat.CreateEntry<T>(key, def, label);
```

## Harmony Patching

```csharp
using Il2Cpp;

[HarmonyPatch(typeof(Server), "ItIsBroken")]
public class MyPatch
{
    [HarmonyPostfix]
    public static void Postfix(Server __instance) { }

    [HarmonyPrefix]
    public static bool Prefix(Server __instance) { return true; } // false = skip original
}
```

Harmony patches auto-apply with MelonLoader — no manual `harmony.PatchAll()` needed.

## MelonPreferences

```csharp
var cat   = MelonPreferences.CreateCategory("MyMod", "My Mod Settings");
var entry = cat.CreateEntry<bool>("Enabled", false, "Enabled");

bool val  = entry.Value;
entry.Value = true;
MelonPreferences.Save();
```

## Coroutines

```csharp
object token = MelonCoroutines.Start(MyCoroutine());
MelonCoroutines.Stop(token);

private System.Collections.IEnumerator MyCoroutine()
{
    yield return new WaitForSeconds(1f);
    // ...
}
```

## Logging

```csharp
MelonLogger.Msg("message");
MelonLogger.Warning("warning");
MelonLogger.Error("error");
```

## Log File Locations

```
MelonLoader log: D:\SteamLibrary\steamapps\common\Data Center\MelonLoader\Latest.log
Player log:      C:\Users\Jacob\AppData\LocalLow\WASEKU\Data Center\Player.log
```

`MelonLogger.Msg()` → MelonLoader log. `UnityEngine.Debug.Log()` → Player log.

## Input System (Legacy Disabled)

`UnityEngine.Input` throws every frame. Use new Input System. Add `Unity.InputSystem.dll` reference.

```csharp
using UnityEngine.InputSystem;

bool leftClick  = Mouse.current.leftButton.wasPressedThisFrame;
Vector2 mousePos = Mouse.current.position.ReadValue();
bool tabPressed = Keyboard.current.tabKey.wasPressedThisFrame;
```

## Scene Names

- `MainMenu` — main menu
- `BaseScene` — the data center game scene

## Polling Strategy

| Detect... | Use |
|---|---|
| Device breaking | Harmony postfix on `Server.ItIsBroken()` / `NetworkSwitch.ItIsBroken()` |
| Device repaired | Harmony postfix on `Server.RepairDevice()` / `NetworkSwitch.RepairDevice()` |
| Tech hired/fired | Harmony postfix on `HRSystem.ButtonConfirmHire()` / `ButtonConfirmFireEmployee()` |
| Tech finishes job | Harmony prefix on `TechnicianManager.RequestNextJob(Technician)` |
| Periodic sync | `OnUpdate()` timer — 2s sufficient for non-critical, 0.5s for UI reinject |
