# Data Center — Mod Creation Index

## Game Info

| | |
|---|---|
| Game | Data Center by Waseku |
| Engine | Unity 6 (6000.3.12f1), IL2CPP, DOTS/ECS |
| Game install | `D:\SteamLibrary\steamapps\common\Data Center` |
| Dev folder | `C:\Users\Jacob\Desktop\data center mods` |
| MelonLoader | v0.7.2 — installed and working |
| Interop assemblies | `...\MelonLoader\Il2CppAssemblies\` |
| Decompiled source | `C:\Users\Jacob\Desktop\dc_decompiled\` |
| MelonLoader mods | `...\Data Center\Mods\` (single DLL, no config) |
| Built-in mods | `...\Data Center_Data\StreamingAssets\Mods\` |
| MelonLoader log | `...\MelonLoader\Latest.log` |
| Player log | `C:\Users\Jacob\AppData\LocalLow\WASEKU\Data Center\Player.log` |

---

## Workflow

1. Create mod folder: `data center mods\<ModName>\`
2. Create `<ModName>-Masterplan.md` inside the mod folder
3. Build in the mod folder
4. Deploy when asked: copy DLL to `Mods\` (MelonLoader) or folder to `StreamingAssets\Mods\` (built-in)
5. Test — check `MelonLoader\Latest.log` for errors
6. Iterate

**Never auto-deploy — always wait for the user to ask.**

---

## Which System To Use

| Need | Use |
|---|---|
| Hook game logic, patch methods, custom UI, automation | **MelonLoader** (preferred) |
| Add shop items, custom 3D models, textures | Built-in mod system |
| Both assets + logic | MelonLoader (can load assets via code) |

---

## Domain Lookup — Read These Files for Each Mod Type

| Building a mod about... | Read these docs |
|---|---|
| New laptop app / screen | `core-setup` + `laptop-computer` + `ui-building` |
| Technician / dispatch automation | `core-setup` + `technicians` + `devices` |
| Device monitoring / control | `core-setup` + `devices` + `ui-building` |
| HR screen injection | `core-setup` + `hr-system` + `ui-building` |
| Asset Management integration | `core-setup` + `asset-management` + `devices` |
| Player economy / money | `core-setup` + `player-economy` |
| Game hooks / events / audio | `core-setup` + `game-events` |
| World objects / singletons | `core-setup` + `world-scene` |
| Customer performance / revenue | `core-setup` + `world-scene` + `player-economy` |
| Objectives / tutorial flow | `core-setup` + `world-scene` + `game-events` |
| Shop items / custom 3D models | `built-in-mod-system` |
| Full feature mod (multiple systems) | `core-setup` + all relevant domain files |

---

## Doc Files

All in `C:\Users\Jacob\Desktop\data center mods\docs\`

| File | Contents |
|---|---|
| `core-setup.md` | .csproj template, mod entry point, IL2CPP namespace rules, foreach gotcha, Harmony patching, MelonPreferences, coroutines, input system, polling strategy |
| `laptop-computer.md` | ComputerShop screen system, all screen GOs, adding a new app, navigation methods, portable laptop approach, OnEnable hooks |
| `devices.md` | Server + NetworkSwitch fields/methods, device scanning, eolTime, NetworkMap, break/repair patches, walk-up interaction |
| `technicians.md` | TechnicianManager, SendTechnician round-robin pitfall, AssignJob (correct pattern), RepairJob struct, Technician fields/states, RequestNextJob patch, hire/fire detection, save/load rebuild |
| `asset-management.md` | AM fields/methods, filter lists, DeviceLineData full fields, price sampling, RecyclableScrollRect warning |
| `hr-system.md` | HRSystem fields, card DOM structure, full injection pattern, GridLayoutGroup resize, reinject poll, global panel injection |
| `ui-building.md` | ScrollView from scratch, buttons from scratch (never clone), overlay/nested canvas, VerticalLayoutGroup flags, full panel template, stale ref cleanup, layout rebuild |
| `player-economy.md` | Money, XP, reputation, SaveSystem hooks, MelonPreferences GetOrCreate pattern, ModItemSaveData, BalanceSheet full API |
| `game-events.md` | All delegates/hooks, HUD feedback (AddMeesageInField typo), audio clips, ReusableFunctions, StaticUIElements full API |
| `world-scene.md` | All singletons, scene names, NetworkMap full API (Device, LACPGroup, Connect/Disconnect), MainGameManager full fields, TimeController methods, Objectives API, Rack fields, SaveData fields |
| `built-in-mod-system.md` | config.json schema, ShopItemConfig, StaticItemConfig, IModPlugin, collider sizes, model file specs, debug log messages |

---

## Useful Scripts

| Script | Usage |
|---|---|
| `deploy_mod.bat ModName` | Copy built-in mod into game |
| `deploy_mod.bat ModName remove` | Remove built-in mod from game |
| `debug_watch.bat` | Watch Player.log live |
| `validate_config.bat ModName` | Check config.json before deploying |

---

## Confirmed Game API — Quick Reference

Cross-referenced from RustBridge research (Joniii's reverse engineering). All confirmed working.

### Player Stats
```csharp
// Money, XP, Reputation live on playerClass, not directly on Player
PlayerManager.instance.playerClass.money        // float
PlayerManager.instance.playerClass.xp           // float
PlayerManager.instance.playerClass.reputation   // float

// Player world position
PlayerManager.instance.playerGO.transform.position      // Vector3
PlayerManager.instance.playerGO.transform.eulerAngles   // angles, .y = yaw

// What player is holding
PlayerManager.instance.objectInHand              // ObjectInHand enum
PlayerManager.instance.numberOfObjectsInHand     // int
```

### Time
```csharp
TimeController.instance.currentTimeOfDay   // float 0.0–1.0 (normalized)
TimeController.instance.CurrentTimeInHours() // float 0–24 (already converted)
TimeController.instance.day                // int, current day
TimeController.instance.secondsInFullDay   // float, real seconds per game day
Time.timeScale                             // Unity time scale (get/set)
```

### Scene
```csharp
UnityEngine.SceneManagement.SceneManager.GetActiveScene().name  // current scene name string
```

### NetworkMap — Confirmed Dictionaries
```csharp
// WARNING: These are IL2CPP dictionaries — use _entries pattern to iterate, NEVER foreach/GetEnumerator
NetworkMap.instance.servers           // Dictionary<string, Server>
NetworkMap.instance.switches          // Dictionary<string, NetworkSwitch>
NetworkMap.instance.brokenServers     // Dictionary<string, Server>
NetworkMap.instance.brokenSwitches    // Dictionary<string, NetworkSwitch>
NetworkMap.instance.lacpGroups        // Dictionary<int, LACPGroup>
NetworkMap.instance.GetNumberOfDevices()  // Il2CppStructArray<int>: [0]=server count, [1]=switch count

// For bulk device scanning, FindObjectsOfType is often easier than iterating these dicts:
var servers = Object.FindObjectsOfType<Server>();    // use indexed for-loop
var switches = Object.FindObjectsOfType<NetworkSwitch>();
```

### Server Fields & Methods — Confirmed Names
```csharp
server.ServerID          // string (capital ID — not serverId)
server.serverType        // int — type index
server.IP                // string
server.appID             // int
server.isOn              // bool
server.isBroken          // bool
server.eolTime           // int (counts down; <= 0 means EOL)
server.timeToBrake       // int (countdown to breakdown)
server.sizeInU           // int — rack unit height (from UsableObject base)
server.existingWarningSigns   // int (warning sign count)
server.existingErrorSigns     // int
server.currentRackPosition    // RackPosition ref (direct back-reference!)
server.rackPositionUID        // int (matches RackPosition.rackPosGlobalUID)
server.maxProcessingSpeed     // float
server.currentProcessingSpeed // float
server.gameObject.name        // e.g. "Server.Purple2(Clone)" — use for color detection

// Methods
server.PowerButton()                        // toggle power (no args). Disable when isBroken.
server.SetIP(string ip)                     // set IP directly (bypasses native keypad)
server.GetCustomerID()                      // int — current customer
server.UpdateCustomer(int id)               // reassign customer by ID
server.ButtonClickChangeCustomer(bool fwd)  // cycle customer (true=next, false=prev)
MainGameManager.instance.ReturnServerNameFromType(int serverType)  // product name (NOT color name!)
```

### NetworkSwitch Fields & Methods — Confirmed Names
```csharp
networkSwitch.switchId         // string (lowercase id) — PRIVATE, use GetSwitchId()
networkSwitch.switchType       // int
networkSwitch.isOn             // bool
networkSwitch.label            // string (persisted in SwitchSaveData)
networkSwitch.isBroken         // bool
networkSwitch.eolTime          // int
networkSwitch.timeToBrake      // int
networkSwitch.sizeInU          // int — rack unit height (from UsableObject base)
networkSwitch.existingWarningSigns  // int
networkSwitch.existingErrorSigns    // int
networkSwitch.currentRackPosition   // RackPosition ref (same as server)

// Methods
networkSwitch.GetSwitchId()            // string — use this, not the private field
networkSwitch.GetConnectedDevices()    // List<(string, int)> — .Item1=deviceName, .Item2=cableId
                                       // Use index loop (for i < Count), NOT foreach
MainGameManager.instance.ShowNetworkConfigCanvas(networkSwitch)  // open LACP config UI (works remotely with canvas boost)
MainGameManager.instance.ReturnSwitchNameFromType(int switchType)  // display name
```

### PatchPanel Fields — Confirmed Names
```csharp
patchPanel.patchPanelId        // string
patchPanel.sizeInU             // int — rack unit height (from UsableObject base)
patchPanel.currentRackPosition // RackPosition ref
patchPanel.rackPositionUID     // int
```

### RackPosition Fields — Confirmed Names
```csharp
rackPosition.rackPosGlobalUID  // int (the UID used everywhere)
rackPosition.positionIndex     // int (slot index within the rack)
rackPosition.rack              // Rack (back-reference to parent rack)
```

### Rack Fields — Confirmed Names
```csharp
rack.positions[]           // RackPosition[] — use .Length for slot count (e.g. 47 for 47U rack)
rack.isPositionUsed[]      // int[] — occupation flags
rack.gameObject.name       // string — e.g. "Rack(Clone)"
rack.GetInstanceID()       // int — unique Unity instance ID, use for matching
```

### Laptop App Injection — Confirmed Pattern
```csharp
// Patch ComputerShop.Awake (postfix) — mainScreen and canvasComputerShop are populated by then
// mainScreen uses GridLayoutGroup (NOT H/V LayoutGroup)
// Create screen GO as child of canvasComputerShop, copy mainScreen's RectTransform
// Add button to mainScreen's GridLayoutGroup
// Also patch ButtonReturnMainScreen to hide your screen
```

### EOL Detection — Confirmed Correct Approach
```csharp
// For SERVERS: only eolTime matters
bool serverEol = server.eolTime <= 0 && !server.isBroken;

// For SWITCHES: both conditions can indicate EOL — check both
bool switchEol = (networkSwitch.existingWarningSigns > 0 || networkSwitch.eolTime <= 0)
                 && !networkSwitch.isBroken;
```

### HUD / Notifications
```csharp
StaticUIElements.instance.AddMeesageInField(string message)    // message log (typo is in-game)
StaticUIElements.instance.SetNotification(int locUID, Sprite sprite, string text)  // banner
StaticUIElements.instance.CalculateRates(out float money, out float xp, out float expenses)
```

### Game State
```csharp
MainGameManager.instance.isGamePaused    // bool (get/set)
MainGameManager.instance.difficulty      // int (get)
MainGameManager.instance.lastUsedRackPositionGlobalUID  // int (UID counter)
SaveSystem.SaveGame()                    // static — trigger a save
```

### Technician State Enum Values (confirmed)
```csharp
Technician.TechnicianState.Idle                  // 0
Technician.TechnicianState.GoingForNewServer     // 1
Technician.TechnicianState.BringingNewServer     // 2
Technician.TechnicianState.GoingBackWithOldServer // 3
Technician.TechnicianState.EndingHisWork         // 4
```

### Technician Private Fields (IL2CPP interop-accessible)
```csharp
tech.currentState           // TechnicianState enum (private, offset 0xA8) — can assign int 0 for Idle
tech.deviceInHand           // GameObject (private, offset 0x70) — visual model carried during repair
tech.characterControl       // AICharacterControl (private, offset 0x60) — NavMesh movement controller
tech.transformIdle          // Transform (private, offset 0x38) — idle standing position
tech.server                 // Server (private, offset 0x80) — current job target
tech.networkSwitch          // NetworkSwitch (private, offset 0x78) — current job target
tech.leftHandIK             // TwoBoneIKConstraint (private, offset 0x88) — hand IK for carry pose
tech.rightHandIK            // TwoBoneIKConstraint (private, offset 0x90) — hand IK for carry pose
tech.transformContainer     // Transform (private, offset 0x40) — container position
tech.transformDumpster      // Transform (private, offset 0x48) — dumpster position
```

### AICharacterControl — Confirmed API
```csharp
charCtrl.SetTarget(Vector3 target)         // set NavMesh destination (Vector3, not Transform)
charCtrl.AgentReachTarget()                // bool — has agent reached destination?
charCtrl.agent                             // public NavMeshAgent — direct NavMesh access
charCtrl.npcStopped                        // public bool — is NPC stopped?
charCtrl.SetStopLoopingDestinationPoints() // stop waypoint loop
charCtrl.AnimSit(bool active)              // sit animation control

// Requires assembly references:
// UnityEngine.AIModule.dll (for NavMeshAgent type resolution)
```

### TwoBoneIKConstraint (Unity.Animation.Rigging)
```csharp
ikConstraint.weight = 0f;  // float 0-1, controls IK influence on hand position
// Requires assembly reference: Unity.Animation.Rigging.dll
```

### IL2CPP Iteration — Safe Patterns (CRITICAL)

**Arrays — NO foreach on Il2CppArrayBase:**
```csharp
// FindObjectsOfType returns Il2CppArrayBase<T>
// Using `var` keeps it as Il2CppArrayBase — foreach CRASHES on this type
// WRONG:
var servers = Object.FindObjectsOfType<Server>();
foreach (var srv in servers) { }  // CRASHES — Il2CppArrayBase has no working GetEnumerator

// SAFE OPTION 1 — indexed for-loop (always works, recommended):
var servers = Object.FindObjectsOfType<Server>();
for (int i = 0; i < servers.Length; i++) {
    var srv = servers[i];
}

// SAFE OPTION 2 — explicit T[] type triggers implicit copy to managed array:
Server[] servers = Object.FindObjectsOfType<Server>();
foreach (var srv in servers) { }  // works — iterating managed Server[], not Il2CppArrayBase
```

**Dictionaries — NO GetEnumerator, NO foreach. Use _entries array:**
```csharp
// WRONG: foreach (var kvp in dict) { ... }
// WRONG: var e = dict.GetEnumerator(); while (e.MoveNext()) { ... }
// RIGHT — access internal _entries array:
var entries = dict._entries;
for (int ei = 0; ei < entries.Length; ei++) {
    if (entries[ei].hashCode >= 0) {  // valid entry
        var key = entries[ei].key;
        var value = entries[ei].value;
        // ... use key/value
    }
}
```

**Lists — Use index loop, NOT foreach:**
```csharp
// WRONG: foreach (var item in il2cppList) { ... }
// RIGHT:
for (int i = 0; i < list.Count; i++) {
    var item = list[i];
}
```

### Harmony Patch Targets — Full Confirmed List
| Method | When It Fires |
|---|---|
| `Player.UpdateCoin` | Money changes |
| `Player.UpdateXP` | XP changes |
| `Player.UpdateReputation` | Reputation changes |
| `Server.PowerButton` | Server power toggled |
| `Server.ItIsBroken` | Server breaks |
| `Server.RepairDevice` | Server repaired |
| `Server.ServerInsertedInRack` | Server placed in rack slot |
| `NetworkSwitch.SwitchInsertedInRack` | Switch placed in rack |
| `PatchPanel.InsertedInRack` | Patch panel placed in rack |
| `Rack.MarkPositionAsUsed` | Rack slot occupation changes |
| `RackPosition.InteractOnClick` | Player clicks a rack slot |
| `MainGameManager.ButtonCustomerChosen` | Customer accepted |
| `ComputerShop.ButtonCheckOut` | Shop checkout |
| `HRSystem.ButtonConfirmHire` | Employee hired |
| `HRSystem.ButtonConfirmFireEmployee` | Employee fired |
| `SaveSystem.SaveGame` | Game saved |
| `SaveSystem.Load` | Game loaded |
| `TimeController` (day change) | Day ends |

### Full Event ID Table (confirmed from RustBridge EventSystem.cs)
| ID | Event | Data |
|---|---|---|
| 100 | MoneyChanged | old, new, delta |
| 101 | XPChanged | old, new, delta |
| 102 | ReputationChanged | old, new, delta |
| 200 | ServerPowered | poweredOn: bool |
| 201 | ServerBroken | — |
| 202 | ServerRepaired | — |
| 203 | ServerInstalled | serverId, objectType, rackPositionUid |
| 204 | CableConnected | — |
| 205 | CableDisconnected | — |
| 206 | ServerCustomerChanged | newCustomerId |
| 207 | ServerAppChanged | newAppId |
| 208 | RackUnmounted | — |
| 209 | SwitchBroken | — |
| 210 | SwitchRepaired | — |
| 211 | ObjectSpawned | objectId, objectType, prefabId, pos, rot |
| 212 | ObjectPickedUp | objectId, objectType |
| 213 | ObjectDropped | objectId, objectType, pos, rot |
| 300 | DayEnded | day: uint |
| 301 | MonthEnded | month: int |
| 400 | CustomerAccepted | customerId |
| 401 | CustomerSatisfied | customerBaseId |
| 402 | CustomerUnsatisfied | customerBaseId |
| 500 | ShopCheckout | — |
| 501 | ShopItemAdded | itemId, price, itemType |
| 502 | ShopCartCleared | — |
| 503 | ShopItemRemoved | uid |
| 600 | EmployeeHired | — |
| 601 | EmployeeFired | — |
| 700 | GameSaved | — |
| 701 | GameLoaded | — |
| 702 | GameAutoSaved | — |
| 800 | WallPurchased | — |
| 1000 | CustomEmployeeHired | employeeId (string) |
| 1001 | CustomEmployeeFired | employeeId (string) |

---

## Hard-Won Lessons (from deployed mods)

These are discoveries made during development of AutoDispatcher and FloorManager that apply to ALL mods for this game. Ignoring these will cause crashes or incorrect behavior.

### Device-to-Rack Matching
Devices (Server, NetworkSwitch, PatchPanel) are **NOT children of Rack in the Unity hierarchy**. They live under `Objects > UsableObjects > Server.XXX(Clone)`. You cannot use `rack.GetComponentsInChildren<Server>()` — it returns 0.

**Correct approach:** Scene-wide scan + hierarchy walk:
```csharp
var allServers = Object.FindObjectsOfType<Server>();
for (int i = 0; i < allServers.Length; i++) {
    var srv = allServers[i];
    if (srv.currentRackPosition == null) continue;
    var parentRack = srv.currentRackPosition.GetComponentInParent<Rack>();
    if (parentRack != null && parentRack.GetInstanceID() == targetRackId) {
        // This server belongs to this rack
    }
}
```

### Rack Discovery
`FindObjectsOfType<Rack>()` directly is more reliable than `FindObjectsOfType<RackMount>()` → `GetComponentInChildren<Rack>()`. Validate with `rack.GetComponentInParent<RackMount>()` if needed.

### Server Color Detection & Type Name
`ReturnServerNameFromType(int)` is **unreliable** — returns wrong names (e.g. all showing "3U 5000 IOPS" when some are "7U 12000 IOPS"). `server.item` is **null after save load** so `item.itemName` also doesn't work. Use the **game object name** for both color and type:
```csharp
string objName = server.gameObject.name; // e.g. "Server.Purple2(Clone)"
// Color:
if (objName.Contains("Blue")) ...    // System X
if (objName.Contains("Green")) ...   // GPU
if (objName.Contains("Purple")) ...  // Mainframe
if (objName.Contains("Yellow")) ...  // RISC
// Size: suffix 1 = 3U 5000 IOPS, suffix 2 = 7U 12000 IOPS
// e.g. "Blue1" = System X 3U 5000 IOPS, "Blue2" = System X 7U 12000 IOPS
```

### Customer API
```csharp
// Get customer info (name + logo sprite)
var custItem = MainGameManager.instance.GetCustomerItemByID(customerID);
custItem.customerName   // string
custItem.logo           // Sprite (can be null)

// Get customer's subnets
var customerBases = MainGameManager.instance.customerBases;  // CustomerBase[]
customerBase.GetSubnetsPerApp()   // Dictionary<int, string> — use _entries pattern!
// WARNING: Dictionary _entries[].key returns garbage in IL2CPP (int keys too!)
// To find correct subnet for a server type, find an existing server of same
// serverType + customerID that already has an IP, then extract its subnet prefix.

// Get available IPs in a subnet
var setIP = Object.FindObjectOfType<SetIP>();
string[] usableIPs = setIP.GetUsableIPsFromSubnet(subnet);  // returns ALL IPs, does NOT check for already-used
// Must manually check against all existing server IPs to avoid duplicates

// Set IP directly (bypasses native keypad which doesn't work remotely)
server.SetIP(string ip);

// Laptop open/close detection
ComputerShop.canvasComputerShop  // GameObject — inactive when laptop is closed (ESC)
```

### Native UI Calls That Don't Work Remotely
These game APIs expect physical proximity or world-space context and **fail when called from a laptop app**:
- `SetIP.ShowCanvas(server)` — keypad doesn't open from laptop. Use `server.SetIP()` directly instead.
- `networkSwitch.ButtonShowNetworkSwitchConfig()` — physical object context only. Use `MainGameManager.instance.ShowNetworkConfigCanvas(networkSwitch)` instead (this one works but needs canvas boosting to render on top of laptop).

### Canvas Boosting for Game UI on Laptop
When opening a game-native canvas from the laptop (e.g. LACP config), it renders behind the laptop. Fix by temporarily boosting:
```csharp
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.sortingOrder = 999;
// Poll for close, then restore original settings
```

### Cursor/Camera State After Native Canvas Close
When opening a game-native canvas from within a mod UI (e.g. LACP config from the laptop), the game's `CloseAnyCanvas()` calls `LockedCursorForPlayerMovement()` on close — this locks the cursor and re-enables camera movement, even though the player should still be in the mod's UI.

**Fix:** After detecting the native canvas close, explicitly restore cursor state:
```csharp
InputManager.ConfinedCursorforUI();       // static method — sets confined mode
Cursor.visible = true;                     // force visible (ConfinedCursorforUI alone may not restore)
Cursor.lockState = CursorLockMode.Confined;
var pm = PlayerManager.instance;
if (pm != null)
{
    pm.enabledMouseMovement = false;        // stop camera following mouse
    pm.enabledPlayerMovement = false;       // stop WASD movement
}
```

Key API:
- `InputManager.ConfinedCursorforUI()` — static, sets cursor confined for UI
- `InputManager.LockedCursorForPlayerMovement()` — static, locks cursor for gameplay
- `PlayerManager.instance.enabledMouseMovement` — bool, controls camera look
- `PlayerManager.instance.enabledPlayerMovement` — bool, controls WASD movement

### Technician Jobs Have No Cancellation Cleanup
The Technician state machine (coroutine chain: `GettingNewServer` → `ReplacingServer` → `ThrowingOutServer` → `SendToContainer`) only resets `deviceInHand`, `currentState`, IK weights, and `isBusy` when the **full chain runs to completion**. There is no explicit cancel/abort method. If the game silently cancels a dispatch (common with EOL jobs), the tech can end up:
- Idle (`isBusy=false`) but still visually holding a device model (`deviceInHand != null`)
- With hands stuck in carry pose (IK weights still 1.0)
- With stale `server`/`networkSwitch` references

**Fix pattern for stuck techs:**
```csharp
tech.StopAllCoroutines();                    // kill stalled coroutines
Object.Destroy(tech.deviceInHand);           // destroy visual model
tech.deviceInHand = null;                    // clear ref
tech.isBusy = false;
tech.currentState = 0;                       // Idle
tech.server = null;                          // clear stale refs
tech.networkSwitch = null;
tech.leftHandIK.weight = 0f;                // reset IK
tech.rightHandIK.weight = 0f;
tech.characterControl.SetTarget(tech.transformIdle.position);  // go home
TechnicianManager.instance.RequestNextJob(tech);               // pick up next job
// Wrap each private field access in try/catch for IL2CPP safety
```

### Techs Can Stall When Player Is Far Away
Unity coroutines and/or NavMeshAgent movement may pause or stall when the tech is off-screen or far from the player camera. This means a tech can be `isBusy=true` indefinitely without making progress. Any mod that dispatches techs should include a busy timeout watchdog (e.g. 120 seconds) that force-resets stuck techs.

### Save Load Timing
`OnSceneWasInitialized` fires **before** save data loads. All racks show `installed=False`, all walls show `opened=False`. Use `SaveSystem.onLoadingDataLater` callback instead for any logic that reads game state.

### Wall State
`wall.isWallOpened` may not load reliably even after `onLoadingDataLater`. In FloorManager, wall outlines were abandoned — racks only exist in opened areas, so just showing rack positions is sufficient.

### Rack World Coordinates
- **X axis**: rack columns (may need reversed sort to match physical left-to-right)
- **Z axis**: rack rows (reverse for screen Y — highest Z = top of map)
- Use `Mathf.Abs()` for gap detection when sort order is reversed
- Aisle gaps: world-space distance > 1.5 units between adjacent positions

### Batch IP Assignment — GetUsableIPsFromSubnet Caching
`SetIP.GetUsableIPsFromSubnet(subnet)` returns available IPs, but the result does **not update** between synchronous `server.SetIP()` calls in the same frame. If you assign `usableIPs[0]` to multiple servers in a loop, they all get the same IP.

**Fix options:**
1. Re-query `GetUsableIPsFromSubnet()` after each `SetIP()` call (may still not update same-frame)
2. Track assigned IPs locally in a `HashSet<string>` and skip already-used ones
3. Use a coroutine with `yield return null` between each assignment to let the game update its IP pool

### TextMeshPro Font Not Available During Early Init
`TextMeshProUGUI` components created in `OnInitializeMelon` or early coroutines will render invisible because TMP font resources aren't loaded yet. The rest of the game UI (which loads TMP fonts) hasn't initialized.

**Fix:** Wait for an existing `TextMeshProUGUI` in the scene, grab its font, and assign it explicitly:
```csharp
TMP_FontAsset font = null;
while (font == null) {
    var existing = Object.FindObjectOfType<TextMeshProUGUI>();
    if (existing != null && existing.font != null)
        font = existing.font;
    yield return null;
}
var tmp = go.AddComponent<TextMeshProUGUI>();
tmp.font = font;
```

### Unity EventSystem Blue Box Artifact
Clicking any `Button` in the game's UI causes Unity's `EventSystem` to "select" that button, drawing a cyan/blue selection rectangle. This persists across view changes and appears as a phantom box. Setting `Navigation.Mode.None` on individual buttons is NOT sufficient — clicking still selects the button.

**Fix:** Clear the selection every frame from the mod's main class:
```csharp
public override void OnLateUpdate()
{
    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
        EventSystem.current.SetSelectedGameObject(null);
}
```
This kills the blue box globally for ALL UI (mod and base game). Both AutoDispatcher and FloorManager include this fix. Any new mod should too.

### Laptop Screen RectTransform Copy Timing
Copying `mainScreen.GetComponent<RectTransform>()` values in a `ComputerShop.Awake` postfix can fail — the RT values may be zero/default before layout has settled. The mod's screen renders as a tiny box instead of filling the laptop.

**Fix:** Delay the copy by 2 frames:
```csharp
MelonCoroutines.Start(CopyRTNextFrame(fmScreenRT, mainScreenRT));

private static IEnumerator CopyRTNextFrame(RectTransform target, RectTransform source)
{
    yield return null;
    yield return null;
    target.anchorMin = source.anchorMin;
    target.anchorMax = source.anchorMax;
    target.pivot = source.pivot;
    target.sizeDelta = source.sizeDelta;
    target.anchoredPosition = source.anchoredPosition;
    target.offsetMin = source.offsetMin;
    target.offsetMax = source.offsetMax;
}
```

### TMP_InputField Does Not Work in IL2CPP
Manually creating a `TMP_InputField` component in IL2CPP/MelonLoader does not accept keyboard input. The internal state doesn't initialize correctly. Do NOT use for user text entry.

**Workarounds:**
- For IP addresses: show subnet prefix as label + last octet picker with -/+ buttons (cycles 2-254)
- For numeric values: use increment/decrement buttons
- For selections: use dropdown lists or cycling buttons

### IP Auto-Assignment — Skip Gateway Addresses
`SetIP.GetUsableIPsFromSubnet()` returns IPs starting at .1, but .1 is typically the gateway. Always skip .0 (network) and .1 (gateway):
```csharp
for (int i = 0; i < usableIPs.Length; i++)
{
    string ip = usableIPs[i];
    int lastDot = ip.LastIndexOf('.');
    string lastOctet = ip.Substring(lastDot + 1);
    if (lastOctet == "0" || lastOctet == "1") continue;
    // Use this IP
    break;
}
```

### Server IOPS Rating — Not in maxProcessingSpeed
`server.maxProcessingSpeed` and `server.currentProcessingSpeed` return small values (e.g. 0.1) unrelated to the rated IOPS. The IOPS rating is embedded in the server type name:
```csharp
string typeName = MainGameManager.instance.ReturnServerNameFromType(server.serverType);
// Returns e.g. "System X 3U 5000 IOPS"
var match = Regex.Match(typeName, @"(\d+)\s*IOPS");
if (match.Success) int iops = int.Parse(match.Groups[1].Value);
```

### EOL Timer Format
`server.eolTime` is in seconds. The game displays it in `H.M.S` format (e.g. `2.15.34`), not `MM:SS`:
```csharp
int hours = server.eolTime / 3600;
int mins = (server.eolTime % 3600) / 60;
int secs = server.eolTime % 60;
string eolStr = $"{hours}.{mins}.{secs}";
```

### Customer API — Additional Patterns
```csharp
// Direct customer assignment (no cycling)
server.UpdateCustomer(int newCustomerID);

// Get all valid customers — filter out ID < 0 (placeholder entries)
var custBases = MainGameManager.instance.customerBases;
for (int i = 0; i < custBases.Length; i++) {
    if (custBases[i].customerID < 0) continue;  // skip invalid
    // custBases[i].customerID is valid
}
```
