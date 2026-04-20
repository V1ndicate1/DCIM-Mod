# AutoDispatcher — Master Plan

**Status: RETIRED (2026-04-15)**
**Last version:** v1.1.0
**Author:** V1ndicate1

Retired because the game's built-in Command Center (Asset Management → Technician Job Queue → Auto Repair) now covers broken and EOL device dispatch natively and better. The Warn toggle was migrated into DCIM v0.9.1. Running AutoDispatcher alongside Command Center causes double dispatch and double money deduction.

---

## Changelog

### v1.1.0 (2026-04-14) — Stuck tech recovery + Nexus polish

**Stuck tech detection and recovery (new in v1.1.0):**
- Added `CheckStuckTechs()` — detects idle techs holding device models and techs frozen busy > 120s
- Added `UnstickTech()` — stops coroutines, destroys held device model, resets state/IK, navigates to idle
- Added position-based `_frozenSince` tracking (replaced flat `_busySince` elapsed timer that caused false mass-resets)
- Added retry/cooldown system: first stuck = free retry, second+ stuck = 5-minute cooldown via `_stuckCooldowns`
- Added `UnityEngine.AIModule` and `Unity.Animation.Rigging` assembly references for NavMesh/IK type resolution
- All private field access wrapped in try/catch for IL2CPP safety
- `_lastPosition`, `_frozenSince`, `_stuckCooldowns`, `_stuckCount` all cleared on save load

**Nexus release polish (v1.1.0 release cleanup):**
- `MelonInfo` version corrected from `"1.0.0"` to `"1.1.0"`
- Removed entire post-load diagnostic block (~50 lines dumping all tech/job states to log on every save load)
- Removed load cooldown countdown log (fired 5× per save load — one per 2s poll tick during 10s cooldown)
- Removed per-dispatch logs: `"Deducted $X for {id}"` and `"Dispatched {id} via SendTechnician"` (fired on every dispatch)
- Removed 4 `"already in active/queued jobs — skipping"` dedup logs
- Fixed `"Mainframe 3U 5000 IOPs"` / `"Mainframe 7U 12000 IOPs"` → `"IOPS"` typo in fallback price table

### v1.0.0 (2026-04-13) — Initial release

First public release. Core dispatch loop, EOL dispatch, three AM screen toggles, cost deduction, watchdog, warning suppression, save/load recovery.

---

## What This Mod Does

Automatically dispatches technicians to broken and EOL devices without the player doing anything manually. Deducts device cost from player money on dispatch.

- **Broken devices** — detected instantly via Harmony patch, dispatched to the first free tech immediately
- **EOL devices** — orange devices (`eolTime <= 0`) dispatched every 2s, only if no broken devices are pending and EOL toggle is ON
- **Broken always has priority** over EOL
- **Cost deduction** — deducts device base price (`item.price`) before dispatching. Modules are NOT included (matches game behavior). Skips dispatch if player can't afford it.
- **Three toggles** in the Asset Management screen: **Auto: ON/OFF** (master), **EOL: ON/OFF**, and **Warn: ON/OFF**
- **Warning suppression** — when Warn is ON, yellow warning icons and broken error signs are cleared every 2s (poll interval, not every frame)

No per-tech settings. No queues. No HR screen injection. No task filtering.

---

## Architecture

### Files

| File | Purpose |
|---|---|
| `AutoDispatcherMod.cs` | MelonLoader entry point. Prefs, 2s poll timer, save/load hook |
| `DispatchController.cs` | Core dispatch logic. Poll, dedup, cost deduction, SendTechnician, watchdog, warning suppression, load cooldown, stuck tech detection |
| `AssetManagementUI.cs` | AM screen toggle injection + Harmony patch on `AssetManagement.OnEnable` |
| `Patches/BreakPatch.cs` | Patches `Server.ItIsBroken`, `NetworkSwitch.ItIsBroken`, `Server.RepairDevice`, `NetworkSwitch.RepairDevice` — failure-only logging |

---

## Dispatch Logic

### Poll cycle (every 2 seconds)

1. **Load cooldown check** — if a save was just loaded, skip polling for 10 seconds to let the game restore state. After cooldown, skip one more cycle before dispatching.
2. Refresh `_serverCache` and `_switchCache` via `FindObjectsOfType` — once per cycle
3. **Broken pass** — for every broken server/switch not in `_assignedDevices`, call `TryDispatch`. No tech reserve — uses all free techs.
4. **EOL pass** — only runs if `!anyBrokenPending && IsEolEnabled`. Dispatches orange devices (`eolTime <= 0`).
5. **Warning suppression** — if `IsWarnEnabled`, clears warning/error signs on all cached devices. Runs every 2s (poll interval).
6. **Watchdog** — every 30 seconds, cross-check `_assignedDevices` against `tm.GetActiveJobs()` and `tm.GetQueuedJobs()`. Clear orphans so they re-enter the pool. **Skips cleanup if null-ref jobs are detected** (post-load stale data from game save/load).
7. **Stuck tech detection** — every 30 seconds (runs with watchdog), checks for two stuck conditions and auto-recovers:
   - **Idle but holding device** — `!isBusy` but `deviceInHand != null`. Detects the visual bug where a tech's job ends/cancels but the carried device model stays attached.
   - **Busy timeout** — `isBusy` for > 120 seconds (real time). Detects stalled coroutines, e.g. when game pauses tech movement while the player is far away or not looking.
   - After recovery, the device is handled by the **retry/cooldown system** (see below) instead of being immediately re-dispatched by the normal poll.

### TryDispatch

1. **Stuck cooldown check** — if device is in `_stuckCooldowns` and cooldown hasn't expired, return false immediately. If expired, remove from both `_stuckCooldowns` and `_stuckCount` (fresh start).
2. Count real (non-null) techs and free techs — `tm.technicians` list can contain stale objects after reload
3. If no free techs — return false (do NOT mark as assigned, retry next poll)
4. Check `tm.GetActiveJobs()` by device ID — skip if device already has an active job
5. Check `tm.GetQueuedJobs()` by device ID — skip if device already queued
6. Deduct cost via `PlayerManager.instance.playerClass.UpdateCoin(-cost, true)` — checks balance first, skips if can't afford
7. Call `tm.SendTechnician(sw, server)` — uses the game's proper dispatch API (handles tech assignment and queueing internally)
8. Add device to `_assignedDevices`, return true

**Important:** We use `TechnicianManager.SendTechnician()`, NOT `Technician.AssignJob()`. Direct `AssignJob` calls caused duplicate queue entries on save/load because the game saved both the job queue entry and the tech assignment separately.

### Cost Deduction

- Server cost: `server.item.price` (live from ShopItemSO)
- Switch cost: `sw.item.price` (live from ShopItemSO)
- **Fallback prices** — if `item` is null, looks up device type name via `MainGameManager.ReturnServerNameFromType()`/`ReturnSwitchNameFromType()` and matches against hardcoded price table:
  - Servers: System X 3U=$400, System X 7U=$1600, RISC 3U=$450, RISC 7U=$1750, Mainframe 3U=$850, Mainframe 7U=$2000, GPU 3U=$550, GPU 7U=$2200
  - Switches: 16x10Gbps RJ45=$250, 4xSFP+/SFP28=$400, 32xQSFP+=$3800, 4xQSFP+16xSFP+/SFP28=$3500
  - If name lookup also fails, cost defaults to 0 (free dispatch)
- Modules (QSFP+, SFP, etc.) are NOT included — the game itself only charges the base device price when manually dispatching
- Money deducted via `Player.UpdateCoin(-cost, true)` (`withoutSound: true` to avoid audio spam)
- If player can't afford it, dispatch is skipped and logged
- Cost is deducted AFTER confirming a free tech exists (no charge if nobody available)

### OnDeviceBroken (instant)

Fires from Harmony patch on `Server.ItIsBroken` / `NetworkSwitch.ItIsBroken`. Dispatches immediately — no 2s delay. Suppressed during load cooldown and post-load diagnostic phase.

### OnDeviceRepaired

Fires from Harmony patch on `Server.RepairDevice` / `NetworkSwitch.RepairDevice`. Removes device from `_assignedDevices`, `_stuckCount`, and `_stuckCooldowns` — clears all stuck history so the device gets a clean slate if it breaks again later.

### Warning Suppression (every 2 seconds)

When `IsWarnEnabled`, `SuppressDeviceWarnings()` runs every poll tick (2s) calling `ClearWarningSign(true)` on all devices where `isWarningCleared == false`, and `ClearErrorSign()` on broken devices.

### Stuck Tech Detection & Recovery (every 30 seconds)

Runs alongside the watchdog. Two detection modes:

1. **Idle + holding device** — `tech.isBusy == false` but `tech.deviceInHand != null`. This is a visual/state bug where the game silently cancels a job (especially EOL) without cleaning up the carried device model. More frequent with AutoDispatcher because the mod dispatches far more aggressively than a human player.

2. **Frozen in place** — `tech.isBusy == true` but the tech's world position hasn't changed by more than `0.1` units across two consecutive watchdog ticks, for a cumulative frozen duration > 120 seconds. Only a tech that is genuinely motionless is flagged — a tech actively walking to or from a device is never reset. Tracked via `_lastPosition` (last known position) and `_frozenSince` (timestamp when movement stopped). Moving resets the frozen timer.

**Recovery procedure (`UnstickTech`):**

1. `tech.StopAllCoroutines()` — kill stalled movement/carry/IK coroutines
2. Destroy `tech.deviceInHand` + null the field (prevents re-detection next cycle)
3. Set `tech.isBusy = false`, `tech.currentState = 0` (Idle)
4. Null out `tech.server` and `tech.networkSwitch` (stale job target refs)
5. Reset `leftHandIK.weight` and `rightHandIK.weight` to 0 (arms back to normal)
6. Navigate tech to idle position via `tech.characterControl.SetTarget(tech.transformIdle.position)`

All private field access is wrapped in try/catch for IL2CPP safety. `_lastPosition`, `_frozenSince`, `_stuckCooldowns`, and `_stuckCount` are all cleared on save load alongside `_assignedDevices`.

**Note:** `RequestNextJob` was intentionally removed from step 7. It conflicted with the retry/cooldown system — calling it would let the game assign whatever was next in queue (possibly the same stuck device), bypassing retry logic entirely. Re-dispatch is now handled explicitly by `RetryDispatch` or the normal 2s poll.

**Retry / Cooldown system (`CheckStuckTechs` + `RetryDispatch`):**

After calling `UnstickTech`, the device ID is captured from `tech.server`/`tech.networkSwitch` (read before those refs are cleared) and `_stuckCount` is incremented:

- **First stuck (count = 1):** Calls `RetryDispatch` — sends the tech (or any free tech) back to the same device immediately, **no cost deducted**. Handles transient pathfinding glitches where the coroutine stalled but the device itself is repairable.
- **Second+ stuck (count ≥ 2):** Applies a 5-minute cooldown via `_stuckCooldowns`. `TryDispatch` skips the device until the cooldown expires. Handles devices whose repair coroutine is fundamentally broken (game bug). After 5 minutes, both cooldown and count are cleared for a fresh attempt.

`RetryDispatch` checks for at least one free tech before calling `SendTechnician`. If no free tech is available, it logs a skip and the normal poll will handle re-dispatch next cycle (at full cost).

**New .csproj references added:** `UnityEngine.AIModule` (for `NavMeshAgent` type resolution via `AICharacterControl`) and `Unity.Animation.Rigging` (for `TwoBoneIKConstraint.weight`).

---

### Save/Load Handling

`RebuildFromGameState` hooks `SaveSystem.onLoadingDataLater`. On load:

1. Ignores repeat calls while cooldown is already active (game fires this event repeatedly)
2. Clears `_assignedDevices`, `_lastPosition`, `_frozenSince`, `_stuckCooldowns`, and `_stuckCount`
3. Sets 10-second cooldown — all polling and patch-based dispatching is suppressed
4. After cooldown: skips one poll cycle to let game state settle
5. Resumes normal dispatching — dedup checks catch devices already in the game's queue

**Known IL2CPP limitations after load:**
- `Technician.CurrentJob` throws `NullReferenceException` for techs loaded from save — wrapped in try/catch
- `GetActiveJobs()` returns jobs with null server/switch refs for save-restored entries — dedup can only match jobs with valid refs
- `tm.technicians.Count` can double after reload (stale + new objects) — code counts only non-null techs
- The game creates ghost "Technician:" entries in the queue UI from save-restored jobs — these are cosmetic and don't affect dispatch behavior

---

## UI — Asset Management Screen

Injected via `AssetManagement.OnEnable` Harmony patch. Three buttons anchored above the filter row:

- **Auto: ON / OFF** — master switch. Green when on, gray when off. Controls broken dispatch.
- **EOL: ON / OFF** — EOL dispatch. Green when on, gray when off.
- **Warn: ON / OFF** — Warning/error sign suppression. Green when on, gray when off.

No panel background (avoids covering the laptop back button). Panel uses `ignoreLayout = true` with explicit `sizeDelta` to bypass the VL's 100px VerticalLayoutGroup. Destroyed and recreated on every `OnEnable`.

---

## MelonPreferences

| Key | Type | Default | Description |
|---|---|---|---|
| `AutoDispatcher.Enabled` | bool | false | Master on/off switch |
| `AutoDispatcher.EolEnabled` | bool | false | EOL dispatch toggle |
| `AutoDispatcher.WarnEnabled` | bool | false | Warning/error sign suppression toggle |

---

## Known Behaviors

- **No free tech** — job skipped (not marked as assigned), retried next 2s poll.
- **All techs busy on EOL, device breaks** — `OnDeviceBroken` finds no free tech, retries every 2s until one finishes. Worst case: ~2s delay.
- **EOL dispatch rejection** — game occasionally silently cancels EOL dispatches. Watchdog clears orphan within 30s and device re-enters pool.
- **Auto is master switch** — EOL and Warn toggles only have effect when Auto is also ON.
- **Warn toggle is independent** — can suppress warnings without EOL dispatch enabled, and vice versa.
- **VL hierarchy** — `VL` is always the first child of `am.transform`. HL filters found by searching for a TMP child with text `"All"` (has trailing `\n`, use `.Trim()`).
- **Save/load ghost entries** — game may show "Technician:" entries in queue UI after reload. These are cosmetic artifacts from the game's save/load system and do not affect actual dispatch behavior.
- **Cost deduction only on dispatch** — money is only taken when a tech is actually sent. No charge for queued-but-not-dispatched devices.
- **Stuck techs auto-recover** — every 30s, idle techs holding devices are cleaned up, and busy techs frozen in place for >120s are force-reset. Only truly motionless techs are flagged — techs actively walking are never reset. Logged as `[AD] Tech ... idle but holding device` or `[AD] Tech ... stuck busy for ...s`.
- **Techs don't get stuck when the player is nearby** — this is expected. Unity throttles coroutines and NavMesh updates for off-screen objects. When the player is present, techs complete jobs normally. The stuck detection only fires when a coroutine has been frozen for 120 real seconds with no position change.
- **First stuck = free retry** — the first time a device causes a stuck tech, it's retried immediately at no charge. If the retry also stalls, a 5-min cooldown is applied on the second stuck.
- **Stuck cooldown prevents infinite loops** — devices with a broken repair coroutine used to spin all techs in a perpetual stuck/reset/dispatch cycle. The cooldown breaks this loop by blocking re-dispatch for 5 minutes after 2+ stucks on the same device.
- **Stuck device visual is likely a vanilla bug** — the game's Technician state machine has no explicit cancellation/cleanup path. When the game silently cancels a dispatch, `deviceInHand` isn't destroyed. AutoDispatcher triggers this more often due to aggressive dispatch volume.

---

## What Was Learned

- Per-tech queues and settings (from v2.0 experiment) created far more bugs than value
- HR screen injection was the most fragile part of the entire mod
- The game's `AssignJob` does not guarantee the job will run — silent cancellations happen
- Task filtering per tech didn't work — techs ignored their settings
- EOL threshold configurable UI added complexity with no real benefit — hardcoded behaviors are simpler and more reliable
- **`Technician.AssignJob()` causes duplicate queue entries on save/load** — always use `TechnicianManager.SendTechnician()` instead
- **`Technician.CurrentJob` is unreliable in IL2CPP** — nullable struct access throws NullReferenceException for save-loaded techs. Always wrap in try/catch.
- **`tm.technicians.Count` can inflate after reload** — stale IL2CPP objects persist in the list. Count only non-null entries.
- **`GetActiveJobs()`/`GetQueuedJobs()` return null-ref entries after load** — device refs are null for save-restored jobs. Dedup can only match jobs with valid refs; rely on `isBusy` check to prevent over-dispatching.
- **`IsDeviceAlreadyAssigned()` is unreliable** — does not catch duplicates after save/load. Use direct ID comparison against active/queued jobs instead.
- **Technician has no job cancellation cleanup** — `TechnicianState`, `deviceInHand`, and IK weights are only reset when the full coroutine chain runs to completion. Silent cancellations leave visual artifacts (device model stuck on tech, arms in carry pose).
- **Techs can stall when player is far away** — Unity coroutines or NavMeshAgent may pause/stall when the tech is off-screen or far from the player camera. A watchdog timeout is needed to recover these. This is confirmed behavior: techs complete repairs normally when the player is nearby.
- **Flat elapsed timer caused false mass-resets** — the original `_busySince` approach stamped all batch-dispatched techs with the same watchdog tick time, causing them all to timeout simultaneously even if only 1 was frozen. Replaced with position-based `_frozenSince` tracking so only individually frozen techs are reset.
- **Watchdog orphan cleanup causes re-dispatch loops** — when a stuck tech's repair coroutine stalls, the game silently removes the job from its queue. The watchdog then sees the device as an orphan and clears it from `_assignedDevices`. Without the cooldown system, the next poll immediately re-dispatches it, creating an infinite loop. The fix is to record the stuck device and apply a cooldown *before* the watchdog clears the orphan.
- **`tech.server`/`tech.networkSwitch` survive into stuck detection** — even when a tech is mid-repair (has `deviceInHand`), those job target refs remain set and are readable at stuck-detection time. Capture them before calling `UnstickTech` which nulls them out.
- **`RequestNextJob` after unstick is harmful** — calling it allows the game to immediately re-assign the tech to whatever is next in its internal queue, potentially the same broken device. Removed in favor of explicit `RetryDispatch` control.

---

## Code Quality Notes

- **IL2CPP foreach fragility** — `_serverCache` and `_switchCache` are typed as `Server[]` / `NetworkSwitch[]`, which triggers an implicit copy from `Il2CppArrayBase` to a managed array. This makes `foreach` safe on these caches. **Do NOT change these to `var`** — that would keep them as `Il2CppArrayBase` where `foreach` crashes at runtime. If refactoring, either keep the explicit `T[]` type or switch to indexed `for` loops.

---

## Future Research — Blueprint Feature

**Idea:** Look at a rack, copy its layout, paste it to another rack location, and have technicians automatically build/install it.

**Start next session by researching these in decompiled source:**

| Question | Where to look |
|---|---|
| How to read rack contents (which devices, which slots) | `Rack.cs`, `RackMount.cs` |
| How devices are purchased and spawned | `MainGameManager.cs`, `ComputerShop.cs` |
| Whether technicians have an "install" job type | `Technician.cs`, `TechnicianManager.cs` |
| How devices are placed into racks programmatically | `Rack.cs`, `Server.cs`, `NetworkSwitch.cs` |
| Whether the game exposes a buy/place flow we can call | `ComputerShop.cs`, `PlayerManager.cs` |

**Key unknowns before any planning:**
- Can we programmatically purchase and spawn devices without the shop UI?
- Does the game have an install/place job for technicians, or only repair jobs?
- Can we read rack slot positions reliably?

**Do not plan or write any code until this research is done.**

---

## Test Cases

| Test | What to confirm |
|---|---|
| Auto ON | Broken dispatch starts within 2s |
| Auto OFF | Nothing dispatches |
| Server breaks | Dispatched instantly via patch |
| All techs busy when device breaks | Retried every 2s until a tech is free |
| EOL ON, no broken | EOL devices dispatched, free techs only |
| Broken arrives while techs on EOL | Next free tech goes to broken, not EOL |
| EOL ON | Orange devices dispatched within 2s |
| EOL OFF | Orange devices not dispatched |
| Warn ON | Warning/error signs cleared every 2s |
| Warn OFF | Warning/error signs not touched |
| Cost deduction | Money decreases by device price on dispatch |
| Can't afford | Dispatch skipped, logged "Cannot afford" |
| Save then load (fresh) | Cooldown, diagnostic, clean dispatch — no duplicates |
| Save then reload (in-game) | Cooldown prevents re-dispatch, busy techs skipped, stale entries are cosmetic only |
| Run 4+ hours | No device accumulation, watchdog keeps state clean |
| Idle tech holding device | Detected within 30s, device destroyed, tech returns to idle |
| Tech stuck busy >2min | Detected at next watchdog tick after 120s frozen, force-reset, tech accepts new jobs |
| Tech walking to device (not frozen) | Not flagged as stuck — position changes reset the frozen timer |
| Multiple techs dispatched in batch | Only individually frozen techs reset — others continue working normally |
| Walk away from techs, return | Techs resume or are auto-recovered if they froze while player was away |
| Stuck detection after save/load | `_lastPosition` and `_frozenSince` cleared — no false positives from pre-load state |
| Tech stuck once on device | Unstuck, free retry dispatched immediately, no money deducted |
| Tech stuck twice on same device | 5-min cooldown applied, device skipped by poll until expired |
| Device repaired after being on cooldown | `_stuckCount` and `_stuckCooldowns` cleared — clean slate for next break |
| Cooldown expires (5 min) | Device re-enters dispatch pool with fresh `_stuckCount` |
| All techs busy during retry | Retry skipped, device re-dispatched next poll at full cost |
| Run AFK for extended period | No infinite stuck loops — cooldowns prevent tech starvation |
