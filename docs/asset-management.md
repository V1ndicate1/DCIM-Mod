# Asset Management Screen

## AssetManagement Fields & Methods

**Internal filter lists (private — interop-accessible):**
```csharp
List<AssetManagementDeviceLineData> deviceList;  // all devices
List<AssetManagementDeviceLineData> switchList;
List<AssetManagementDeviceLineData> serverList;
List<AssetManagementDeviceLineData> brokenList;
List<AssetManagementDeviceLineData> eolList;
List<AssetManagementDeviceLineData> offList;
int currentFilter;
```

**Other fields:**
```csharp
int            priceOfTechnician;       // current dispatch price — read after SendTechnician()
bool           firstInit;
NetworkSwitch  selectedNetworkSwitch;   // currently selected device for dispatch (private)
Server         selectedServer;          // currently selected device for dispatch (private)
GameObject     overlayConfirmTechnician;// the "confirm send technician?" overlay GO (private)
```

**Interface:** `IRecyclableScrollRectDataSource` — AM implements this for the virtual scroll. Methods: `GetItemCount()`, `SetCell(ICell, int)`.

**Public filter methods:**
```csharp
am.ButtonFilterAll();
am.ButtonFilterSwitches();
am.ButtonFilterServers();
am.ButtonFilterBroken();
am.ButtonFilterEOL();
am.ButtonFilterOff();
```

**Dispatch methods:**
```csharp
am.SendTechnician(NetworkSwitch sw, Server server);  // sets priceOfTechnician, shows confirm overlay
am.ButtonConfirmSendingTechnician();                  // deducts money, dispatches
am.ButtonCancelSendingTechnician();                   // hides overlay, no charge
am.ButtonClearAllWarnings();
```

## AssetManagementDeviceLineData — Full Fields

```csharp
string        deviceType;          // "Server" or "Switch"
string        deviceSubtype;       // model/subtype string
string        deviceNameText;      // display name (ServerID or switchId)
string        deviceEOL;           // EOL time formatted as string
bool          isBroken;
string        deviceState;         // state label string
bool          isWarningCleared;
NetworkSwitch networkSwitch;       // null if server entry
Server        server;              // null if switch entry
```

**Constructors:**
```csharp
new AssetManagementDeviceLineData(string type, NetworkSwitch device);
new AssetManagementDeviceLineData(string type, Server device);
```

## ⚠️ RecyclableScrollRect — Do Not Clone

The AM uses `PolyAndCode.UI.RecyclableScrollRect` (`Il2CppPolyAndCode.UI.*`) for virtual scrolling. Do NOT try to instantiate or clone this in mod code. Build your own scroll views from scratch using standard `ScrollRect` + `ContentSizeFitter` (see `ui-building.md`).

## Building Your Own Device List (Preferred)

Don't depend on AM's internal lists. Build directly from game objects:

```csharp
var servers  = Object.FindObjectsOfType<Server>();
var switches = Object.FindObjectsOfType<NetworkSwitch>();

foreach (var s in servers)
{
    if (s == null) continue;
    bool broken  = s.isBroken;
    bool eol     = s.eolTime > 0 && s.eolTime <= thresholdSeconds;
    bool off     = !s.isOn;
    string name  = s.ServerID;
    // build your own row
}
```

## Price Sampling Without UI

To get the current dispatch price without showing the confirm overlay to the player:

```csharp
var am = Object.FindObjectOfType<AssetManagement>();
if (am != null)
{
    am.SendTechnician(sw, server);           // sets priceOfTechnician
    int price = am.priceOfTechnician;
    am.ButtonCancelSendingTechnician();      // cancel — no charge, no visible overlay
}
```

## Patching AM Screen Open

`OnEnable()` fires every time the AM screen is opened — good injection point:

```csharp
[HarmonyPatch(typeof(AssetManagement), "OnEnable")]
public class AMOpenPatch
{
    [HarmonyPostfix]
    public static void Postfix(AssetManagement __instance)
    {
        // AM just opened — __instance has fresh lists
        WarmPriceCache(__instance);
    }
}
```
