# Built-In Mod System (Shop Items & Static Assets)

Use this system for: custom shop items, custom models, textures, static world objects.
For logic/code mods use MelonLoader instead.

## Folder Structure

```
Data Center_Data/StreamingAssets/Mods/
  MyMod/
    config.json
    model.obj
    texture.png
    icon.png
    MyPlugin.dll    (optional — for code entry point)
```

## config.json Schema

```json
{
  "modName": "My Mod Name",
  "shopItems":   [ <ShopItemConfig> ],
  "staticItems": [ <StaticItemConfig> ],
  "dlls":        [ <DllEntry> ]
}
```

## ShopItemConfig

```json
{
  "price": 2500,
  "xpToUnlock": 0,
  "sizeInU": 1,
  "mass": 8.0,
  "modelScale": 1.0,
  "colliderSize":   { "x": 0.45, "y": 0.044, "z": 0.45 },
  "colliderCenter": { "x": 0.0,  "y": 0.022, "z": 0.0  },
  "modelFile":   "model.obj",
  "textureFile": "texture.png",
  "iconFile":    "icon.png"
}
```

| Field | Notes |
|---|---|
| `price` | Cost in $ |
| `xpToUnlock` | 0 = always available |
| `sizeInU` | Rack units tall (1U, 2U, 4U, etc.) |
| `modelScale` | 1.0 = true size |
| `iconFile` | PNG, 256×256 recommended |

## StaticItemConfig

```json
{
  "isKinematic": true,
  "modelFile":   "model.obj",
  "textureFile": "texture.png"
}
```

## DllEntry

```json
{
  "entryClass": "MyNamespace.MyPlugin"
}
```

Note: `fileName` field is NOT required — only `entryClass` is needed.

## IModPlugin Interface

```csharp
public interface IModPlugin
{
    void OnModLoad(string modFolderPath);   // modFolderPath is a PARAMETER, not a property
    void OnModUnload();
}

public class Plugin : IModPlugin
{
    public void OnModLoad(string modFolderPath)
    {
        UnityEngine.Debug.Log("[MyMod] Loaded from: " + modFolderPath);
    }
    public void OnModUnload() { }
}
```

## Collider Size Reference

Rack unit height = `0.044m` per U.

| Size | `sizeInU` | `colliderSize.y` | `colliderCenter.y` |
|---|---|---|---|
| 1U | 1 | 0.044 | 0.022 |
| 2U | 2 | 0.088 | 0.044 |
| 3U | 3 | 0.132 | 0.066 |
| 4U | 4 | 0.176 | 0.088 |
| 6U | 6 | 0.264 | 0.132 |

Formula: `colliderSize.y = sizeInU × 0.044` / `colliderCenter.y = sizeInU × 0.022`

## Model Files

- Format: Wavefront OBJ, 1 unit = 1 metre
- 1U server: ~0.45m wide × 0.044m tall × 0.45m deep
- Blender export: Scale 1.0, Up Y, Forward -Z, disable "Write Materials"

## Debug Log Messages (Player.log)

| Message | Meaning |
|---|---|
| `[ModLoader] Loading mod pack: X` | Mod folder found |
| `[ModLoader] No config.json found in X` | config.json missing |
| `[ModLoader] Failed to parse config in X` | JSON syntax error |
| `[ModLoader] Shop item loaded: X` | Item loaded ✓ |
| `[ModLoader] Static item loaded: X` | Static item loaded ✓ |
| `[ModLoader] Failed to load model: X` | OBJ missing or corrupt |
| `[ModLoader] DLL plugin loaded: X` | DLL loaded ✓ |
| `[ModLoader] Failed to load DLL X` | DLL not found |
| `[ModLoader] Entry class 'X' does not implement IModPlugin` | Wrong interface |
