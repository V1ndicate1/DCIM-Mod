# DCIM Mod

A MelonLoader mod for **Data Center** that adds a full DCIM (Data Center Infrastructure Management) laptop app to the game.

## Features

- **Dashboard** — live overview of your data center
- **Floor Map** — visual rack layout with live refresh, filters, multi-select, customer health outlines, and EOL color indicators
- **Device List & Search** — find any server or switch with live EOL countdown timers
- **Device Config** — remote power, IP assignment, and LACP control
- **Customer IP View** — see all IPs assigned per customer
- **Rack Diagram** — per-rack slot view with mini shop to buy and install devices directly
- **Buy Configured Switches** — configure SFP modules per port before purchasing (QSFP+, SFP28, SFP+ Fiber, SFP+ RJ45)
- **Shop Cart** — queue multiple purchases (including configured SFP switches) and check out all at once
- **SFP Presets** — save and load per-switch-model port configurations for quick repeat purchases
- **Rack Colors** — HSV color picker with live hex input and 8 persistent favorites
- **3D Rack Labels** — color-coded labels visible in the world
- **Warning Sign Suppression** — toggle to hide warning signs globally

## Requirements

- [MelonLoader](https://melonwiki.xyz) v0.6+
- Data Center (Steam)

## Installation

### Standard
1. Install MelonLoader for Data Center
2. Download `DCIM-1.0.3.zip` from the [latest release](https://github.com/V1ndicate1/DCIM-Mod/releases/latest)
3. Extract `DCIM.dll` into your `Data Center/Mods/` folder
4. Launch the game

### MelonLoader Fix (if you get a `Duplicate type '<>O>'` error)
1. Download `DCIM_MelonLoader_Fix_1.0.2.zip`
2. Follow the included instructions to patch `UnityEngine.CoreModule.dll`
3. Rebuild/reinstall the mod

## Nexus Mods

Also available on [Nexus Mods](https://www.nexusmods.com/datacenter/mods/8).

## MelonLoader Fix — Source & Build

The fix utility that patches `UnityEngine.CoreModule.dll` is open-source.
The main DCIM mod is closed-source; only the fix utility source is published here.

**Source location:** `/src/FixCoreModule/`

**Prerequisites:** [.NET 6 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)

**Build:**
```
dotnet build -c Release
```

**Expected output:** `FixCoreModule.exe`

The utility makes no network calls. It only reads and rewrites
`UnityEngine.CoreModule.dll` inside your local game folder — nothing else
is modified and no data leaves your machine.

## Credits / Third-Party

| Library | License | Author |
|---------|---------|--------|
| [MelonLoader](https://github.com/LavaGang/MelonLoader) | Apache 2.0 | LavaGang |
| [HarmonyX / 0Harmony](https://github.com/BepInEx/HarmonyX) | MIT | BepInEx / pardeike |
| [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) | LGPL-3.0 | BepInEx |
| [Mono.Cecil](https://github.com/jbevain/cecil) | MIT | Jb Evain |
