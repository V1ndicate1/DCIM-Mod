# World, Scene & Singletons

## All Singletons

All available at runtime with `using Il2Cpp;`:

| Singleton | Key Use |
|---|---|
| `TechnicianManager.instance` | Dispatch technicians, job queue — see `technicians.md` |
| `PlayerManager.instance` | Access `.playerClass` |
| `PlayerManager.instance.playerClass` | `.money`, `.UpdateCoin()`, `.UpdateReputation()`, `.UpdateXP()` |
| `StaticUIElements.instance` | HUD messages, notifications, error signs, text input overlay |
| `MainGameManager.instance` | Core game state, prefabs, difficulty, camera |
| `NetworkMap.instance` | Network topology, device tracking |
| `BalanceSheet.instance` | Revenue/expense tracking |
| `AudioManager.instance` | Sound effects and music |
| `Localisation.instance` | `.ReturnTextByID(int uid)` |
| `Objectives.instance` | Create/destroy/track objectives |
| `SaveData.instance` | Current save data |
| `TimeController.instance` | `.day`, `.currentTimeOfDay`, `.onEndOfTheDayCallback` |
| `ModLoader.instance` | Game's built-in mod loader |
| `RackAudioCuller.instance` | Rack audio management |
| `SettingsSingleton.instance` | Game settings |

## Scene Names

- `MainMenu` — main menu
- `BaseScene` — the data center game

Load a scene:
```csharp
LoadingScreen.instance.LoadLevel(int sceneIndex);
```

## NetworkMap

```csharp
// Single device lookups — safe
NetworkMap.instance.GetServer(string serverId);
NetworkMap.instance.GetSwitchById(string switchId);

// Broken device tracking
NetworkMap.instance.AddBrokenServer(Server s);
NetworkMap.instance.RemoveBrokenServer(Server s);
NetworkMap.instance.AddBrokenSwitch(NetworkSwitch sw);
NetworkMap.instance.RemoveBrokenSwitch(NetworkSwitch sw);

// Full device list — GetAllDevices returns List<Device> (IL2CPP list — use indexed for-loop, NOT foreach)
List<NetworkMap.Device> devices = NetworkMap.instance.GetAllDevices();

// These return Il2CppSystem IEnumerable — DO NOT foreach, use FindObjectsOfType instead
NetworkMap.instance.GetAllServers();
NetworkMap.instance.GetAllNetworkSwitches();
NetworkMap.instance.GetAllBrokenServers();
NetworkMap.instance.GetAllBrokenSwitches();

// Connection management
NetworkMap.instance.Connect(string deviceIdA, string deviceIdB);
NetworkMap.instance.Disconnect(string deviceIdA, string deviceIdB);
NetworkMap.instance.RegisterCableConnection(/* cable params */);
NetworkMap.instance.RemoveCableConnection(int cableId);

// Topology query
List<...> routes = NetworkMap.instance.FindAllRoutes(string fromId, string toId);

// Update customer data (called by game when rack/server changes)
NetworkMap.instance.UpdateCustomerServerCountAndSpeed(/* params */);
```

### NetworkMap.Device (inner class)

```csharp
NetworkMap.Device
{
    string Name;
    string Type;              // "Server", "NetworkSwitch", etc.
    string CustomerID;
    HashSet<NetworkMap.Device> Connections;
}
```

### NetworkMap.LACPGroup (link aggregation)

```csharp
NetworkMap.LACPGroup
{
    string groupId;
    NetworkMap.Device deviceA;
    NetworkMap.Device deviceB;
    List<int> cableIds;
    float GetAggregatedSpeed();
}
```

## TimeController

```csharp
int   day              = TimeController.instance.day;
float currentTimeOfDay = TimeController.instance.currentTimeOfDay;  // 0.0–1.0 (0=midnight, 0.5=noon)
float secondsInFullDay = TimeController.instance.secondsInFullDay;

TimeController.onEndOfTheDayCallback += MyMethod;  // fires at end of each in-game day

// Helper methods
bool  between = TimeController.instance.TimeIsBetween(float startHour, float endHour);  // 0–24h
float hours   = TimeController.instance.CurrentTimeInHours();                            // 0–24
int   elapsed = TimeController.instance.HoursFromDate(float time, int day);              // total hours since start
```

## MainGameManager

```csharp
MainGameManager.instance

// State
int  difficulty                          // game difficulty setting
bool isGamePaused
bool isPauseMenuDisallowed
bool isPlayerCameraDisallowed

// References
Camera playerCamera
ComputerShop computerShop               // direct ref to the laptop
CustomerBase[] customerBases            // all customer zones
CustomerItem[] customerItems            // customer item configs

// Prefab arrays (index by type int)
GameObject[] serverPrefabs
GameObject[] switchesPrefabs
GameObject[] patchPanelsPrefabs
GameObject[] techniciansPrefabs
GameObject   rackPrefab
GameObject   rackPackedPrefab

// Prefab helpers
GameObject GetServerPrefab(int type)
GameObject GetSwitchPrefab(int type)
GameObject GetPatchPanelPrefab(int type)

// Lookup helpers
CustomerItem GetCustomerItemByID(int id)
string ReturnServerNameFromType(int type)
string ReturnSwitchNameFromType(int type)

// UI
void ShowCustomerCardsCanvas()

// Autosave
MainGameManager.instance.SetAutoSaveEnabled(bool enabled)
MainGameManager.instance.SetAutoSaveInterval(float seconds)

// Events
MainGameManager.onBuyingWallEvent += MyMethod;  // fires when player buys a wall
```

## Objectives

```csharp
Objectives.instance

// Create a standard objective (localisationUID → displayed text)
Objectives.instance.CreateNewObjective(
    int localisationUID,
    int objectiveUID,
    Vector3 objectivePosition,
    int xpReward = 0,
    int reputationReward = 0,
    bool isSub = false
);

// Create a timed app-performance objective
int uid = Objectives.instance.CreateAppObjective(
    int customerID,
    int appID,
    int timeSeconds,
    int requiredIOPS
);

// Manage objectives
Objectives.instance.StartObjective(int objectiveUID, Vector3 position, bool loadSave = false);
Objectives.instance.StartObjective(int objectiveUID, bool loadSave = false);
Objectives.instance.DestroyObjective(int objectiveUID);
Objectives.instance.ClearObjectives();

// Query
bool inProgress  = Objectives.instance.IsTutorialInProgress();
ObjectiveTimed t = Objectives.instance.GetTimedObjective(int objectiveUID);
HashSet<int> active = Objectives.instance.activeObjectives;

// Position indicators (3D world markers)
Objectives.instance.InstantiateObjectiveSign(int objectiveUID, Vector3 worldPos);
Objectives.instance.RemoveObjectiveSign(int objectiveUID);
```

## Rack

```csharp
Rack  // MonoBehaviour — one per physical rack in scene

// Fields
RackPosition[] positions          // all slot positions in the rack
int[]          isPositionUsed     // parallel array — 0=free, >0=used
RackMount      rackMount          // handles mounting/unmounting logic

// Methods
bool IsPositionAvailable(int index, int sizeInU)
void MarkPositionAsUsed(int index, int sizeInU)
void MarkPositionAsUnused(int index, int sizeInU)
void InitializeLoadedRack(int[] loadedPositions)
void ButtonUnmountRack()

// Find all racks in scene
var racks = Object.FindObjectsOfType<Rack>();
```

## Finding Objects in Scene

```csharp
// Single object
var hr  = Object.FindObjectOfType<HRSystem>();
var am  = Object.FindObjectOfType<AssetManagement>();
var cs  = Object.FindObjectOfType<ComputerShop>();

// All objects of type (returns Il2CppArrayBase — use indexed for-loop, NOT foreach)
var servers  = Object.FindObjectsOfType<Server>();
var switches = Object.FindObjectsOfType<NetworkSwitch>();
var racks    = Object.FindObjectsOfType<Rack>();

// Include inactive GameObjects
var all = Object.FindObjectsOfType<Server>(true);
```

## SaveData Fields

```csharp
SaveData.instance.playerData                      // PlayerData struct
SaveData.instance.technicianData                  // List<TechnicianSaveData>
SaveData.instance.repairJobQueue                  // List<RepairJobSaveData>
SaveData.instance.networkData                     // NetworkSaveData
SaveData.instance.rackMountObjectData             // List<InteractObjectData>
SaveData.instance.interactObjectData              // List<InteractObjectData>
SaveData.instance.hiredTechnicians                // int[]
SaveData.instance.shopItemUnlockStates            // Dictionary<string,bool>
SaveData.instance.balanceSheetData                // BalanceSheetSaveData
SaveData.instance.modItemData                     // List<ModItemSaveData>
SaveData.instance.wallPrice                       // float
SaveData.instance.version                         // int
SaveData.instance.nameOfSave                      // string
```
