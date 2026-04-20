# Devices — Servers & Network Switches

## Scanning for Devices

```csharp
// CORRECT — returns Il2CppArrayBase, use indexed for-loop (NOT foreach)
var servers  = Object.FindObjectsOfType<Server>();
var switches = Object.FindObjectsOfType<NetworkSwitch>();

// WRONG — returns Il2CppSystem IEnumerable, foreach fails
NetworkMap.instance.GetAllServers();
NetworkMap.instance.GetAllBrokenServers();
NetworkMap.instance.GetAllBrokenSwitches();
NetworkMap.instance.GetAllNetworkSwitches();

// Single lookup is fine
NetworkMap.instance.GetServer(string serverId);
```

## Server — Full Reference

**Fields:**
```csharp
string ServerID;              // unique identifier — use as key
string IP;
int    appID;                 // assigned app ID
int    serverType;            // type index (int, NOT string) — use MainGameManager.ReturnServerNameFromType(int) to get name
float  maxProcessingSpeed;    // max IOPS this server can handle
float  currentProcessingSpeed;// current processing speed
bool   isBroken;
bool   isOn;
int    eolTime;               // real-time seconds remaining (0 = no EOL, >0 = countdown)
int    timeToBrake;           // seconds until next break
bool   isWarningCleared;
CableLink[] cablelinks;       // all cable port slots
```

**Methods:**
```csharp
server.RepairDevice();                    // repair the server
server.PowerButton(bool forceState = false); // toggle on/off
server.ClearWarningSign(bool isPreserved = false);
server.ClearErrorSign();
bool connected = server.IsAnyCableConnected();

// Customer/app management
int custId = server.GetCustomerID();
server.UpdateCustomer(int newCustomerID);
server.UpdateAppID(int appID);
server.SetIP(string ip);
server.ButtonClickChangeCustomer(bool forward); // cycles through customers
server.ButtonClickChangeIP();                   // opens IP input overlay

// Rack
server.ServerInsertedInRack(ServerSaveData data = null); // called when placed in rack
bool valid = server.ValidateRackPosition();
```

**Base class:** `UsableObject` → `Interact`
- Also has: `item`, `prefabID`, `currentRackPosition`

## NetworkSwitch — Full Reference

**Fields:**
```csharp
int    switchType;            // type index (int, NOT string) — use MainGameManager.ReturnSwitchNameFromType(int) to get name
bool   isBroken;
bool   isOn;
// NOTE: label was REMOVED from NetworkSwitch in game update 6000.4.2f1
// label is now stored in SwitchSaveData — look up via SaveData._current.networkData.switches[i].label
// match by: switches[i].switchID == sw.GetSwitchId()
int    eolTime;               // real-time seconds remaining
int    timeToBrake;
bool   isWarningCleared;
CableLink[] cableLinkSwitchPorts;  // all port slots
// switchId field is PRIVATE — never access directly
```

**Methods:**
```csharp
string id = sw.GetSwitchId();       // ALWAYS use this for the ID — switchId field is private
sw.RepairDevice();
sw.PowerButton(bool forceState = false);
sw.ClearWarningSign(bool isPreserved = false);
sw.ClearErrorSign();
bool connected = sw.IsAnyCableConnected();

// Network info
List<(string deviceName, int cableId)> devices = sw.GetConnectedDevices();  // all connected devices
sw.SwitchInsertedInRack(SwitchSaveData data = null);
bool valid = sw.ValidateRackPosition();
sw.ButtonShowNetworkSwitchConfig();   // opens switch config UI
```

## eolTime — Display Notes

`eolTime` is **real-time seconds** (0–1800 = 0–30 min). NOT in-game days.

```csharp
// Display as MM:SS
string FormatEol(int seconds)
{
    if (seconds <= 0) return "--";
    int m = seconds / 60;
    int s = seconds % 60;
    return $"{m}:{s:D2}";
}
```

## Device ID Conventions

Use consistent IDs for tracking devices across systems:
```csharp
string ServerId(Server s)         => "server_" + s.ServerID;
string SwitchId(NetworkSwitch sw) => "switch_" + sw.GetSwitchId();
```

## Checking Device Status

```csharp
bool isBroken  = server.isBroken;
bool isEol     = server.eolTime > 0 && server.eolTime <= thresholdSeconds;
bool isOff     = !server.isOn;
bool isHealthy = !server.isBroken && !server.isOn == false && server.eolTime <= 0;
```

## NetworkMap — Topology

```csharp
NetworkMap.instance.GetServer(string serverId);      // single server lookup
NetworkMap.instance.AddBrokenServer(Server s);       // internal tracking
NetworkMap.instance.RemoveBrokenServer(Server s);
NetworkMap.instance.AddBrokenSwitch(NetworkSwitch s);
NetworkMap.instance.RemoveBrokenSwitch(NetworkSwitch s);
```

## Patching Device Events

```csharp
// Detect when a server breaks
[HarmonyPatch(typeof(Server), "ItIsBroken")]
public class ServerBreakPatch
{
    [HarmonyPostfix]
    public static void Postfix(Server __instance)
    {
        // __instance is the server that just broke
    }
}

// Detect when a server is repaired
[HarmonyPatch(typeof(Server), "RepairDevice")]
public class ServerRepairPatch
{
    [HarmonyPostfix]
    public static void Postfix(Server __instance) { }
}

// Same pattern for NetworkSwitch
[HarmonyPatch(typeof(NetworkSwitch), "ItIsBroken")]
[HarmonyPatch(typeof(NetworkSwitch), "RepairDevice")]
```

## Interacting With a Server (Walk-Up Interaction)

`Server` extends `Interact` — you can patch its interaction method:
```csharp
[HarmonyPatch(typeof(Server), "InteractOnClick")]
public class ServerClickPatch
{
    [HarmonyPrefix]
    public static bool Prefix(Server __instance)
    {
        // Open your custom UI focused on __instance
        // return false to suppress default interaction, true to allow it
        return true;
    }
}
```
