# DCIM Mod

A MelonLoader mod for **Data Center** that adds a full DCIM (Data Center Infrastructure Management) laptop app to the game.

## Features

- **Dashboard** — live overview of your data center
- **Floor Map** — visual rack layout with room tabs (Main Hall / DMZ), live refresh, filters, customer health outlines, and EOL color indicators
- **Batch actions** — select multiple racks and assign customers, recolor, buy racks, or apply a rack profile to all of them at once
- **Device List & Search** — find any server or switch with live EOL countdown timers
- **Device Config** — remote power, IP assignment, LACP control, and router/firewall detail views with one-click access to the native config screens
- **Router & Firewall support** — buy them from the mod shop, configure SFP modules per port, or let Auto Configure set them up with a full preview before applying
- **Customer IP View** — see all IPs assigned per customer
- **Rack Diagram** — per-rack slot view with a mini shop; devices bought on an empty slot are installed into the rack automatically
- **Rack Profiles** — capture a rack's layout and re-apply it to any empty rack (or many at once), plus one-click Clear Rack
- **Buy Configured Switches** — configure SFP modules per port before purchasing (QSFP+, SFP28, SFP+ Fiber, SFP+ RJ45)
- **Shop Cart** — queue multiple purchases (including configured SFP switches) and check out all at once
- **SFP Presets** — save and load per-switch-model port configurations for quick repeat purchases
- **Rack Colors** — HSV color picker with live hex input and 8 persistent favorites
- **3D Rack Labels** — color-coded labels visible in the world

## Requirements

- [MelonLoader](https://melonwiki.xyz) v0.7.x (0.7.2 and 0.7.3 need the one-time fix below)
- Data Center (Steam)

## Installation

### Standard
1. Install MelonLoader for Data Center
2. Download `DCIM-x.x.x.zip` from the [latest release](https://github.com/V1ndicate1/DCIM-Mod/releases/latest)
3. Extract `DCIM.dll` into your `Data Center/Mods/` folder
4. Launch the game

### MelonLoader Fix (`Duplicate type '<>O'` crash, or mods silently not loading)

MelonLoader has a [known bug](https://github.com/LavaGang/MelonLoader/issues/1142) with the game's current Unity version: on ML 0.7.2 the game crashes with a `Duplicate type '<>O'` error; on ML 0.7.3 mods simply never load (no crash, no error). This affects **all** mods for Data Center, not just DCIM. Game updates can bring the problem back — re-run the fix whenever mods stop loading after an update.

**Option A — Automatic fix (recommended, works with any MelonLoader game):**
1. Close the game
2. Download the fix from the [FixCoreModule repo](https://github.com/V1ndicate1/FixCoreModule/releases/latest) and extract it
3. Run `FixCoreModule.exe` — it auto-scans your Steam libraries for affected games
4. Launch the game — the error should be gone

The exe patches one file (`UnityEngine.CoreModule.dll`) in your local game folder. It makes no network calls and modifies nothing else. [Full source and details here](https://github.com/V1ndicate1/FixCoreModule). A Data Center-specific build is also mirrored on this repo's [melonloader-fix-1.0.2 release](https://github.com/V1ndicate1/DCIM-Mod/releases/tag/melonloader-fix-1.0.2).

**Option B — Manual fix (Data Center only, locked to game version 1.0.47.2):**
1. Close the game
2. Download `DCIM_MelonLoader_Fix_1.0.1.zip` from the [v1.0.3 release](https://github.com/V1ndicate1/DCIM-Mod/releases/tag/v1.0.3)
3. Extract the `MelonLoader` folder directly into your `Data Center` game folder (merge/replace when prompted)
4. Launch the game

The manual zip contains pre-generated MelonLoader assemblies that skip the broken generation step. These assemblies are output from [MelonLoader](https://github.com/LavaGang/MelonLoader) (Apache 2.0) and [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) (LGPL-3.0) — no game code or DCIM code is included.

## Nexus Mods

Also available on [Nexus Mods](https://www.nexusmods.com/datacenter/mods/8).

## Credits / Third-Party

| Library | License | Author |
|---------|---------|--------|
| [MelonLoader](https://github.com/LavaGang/MelonLoader) | Apache 2.0 | LavaGang |
| [HarmonyX / 0Harmony](https://github.com/BepInEx/HarmonyX) | MIT | BepInEx / pardeike |
| [Il2CppInterop](https://github.com/BepInEx/Il2CppInterop) | LGPL-3.0 | BepInEx |
| [Mono.Cecil](https://github.com/jbevain/cecil) | MIT | Jb Evain |
