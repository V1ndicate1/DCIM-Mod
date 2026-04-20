# Player & Economy

## Money System

```csharp
float balance = PlayerManager.instance.playerClass.money;

// Add money (positive) or deduct (negative)
PlayerManager.instance.playerClass.UpdateCoin(float amount, bool withoutSound);
// withoutSound = false → plays coin sound; true → silent

// Examples
PlayerManager.instance.playerClass.UpdateCoin(-500f, false);  // deduct $500 with sound
PlayerManager.instance.playerClass.UpdateCoin(+200f, true);   // add $200 silently

// Affordability check
if (PlayerManager.instance.playerClass.money >= price) { /* can afford */ }
```

## XP & Reputation

```csharp
PlayerManager.instance.playerClass.UpdateXP(float amount);
PlayerManager.instance.playerClass.UpdateReputation(float amount);
```

## Save System

```csharp
// Trigger saves
SaveSystem.SaveGame();
SaveSystem.LoadGame(string savename);
SaveSystem.AutoSave();

// Hook into save/load events
SaveSystem.onSavingData      += MyMethod;   // fires when game saves
SaveSystem.onLoadingData     += MyMethod;   // fires when load starts
SaveSystem.onLoadingDataLater += MyMethod;  // fires after load completes — safest for queue rebuild

// SaveData fields (read from SaveData.instance)
SaveData.instance.playerData
SaveData.instance.technicianData
SaveData.instance.repairJobQueue
SaveData.instance.modItemData
SaveData.instance.balanceSheetData
```

## MelonPreferences — Persistent Mod Settings

Use for mod-specific settings that persist between sessions.

```csharp
// Setup (call in OnInitializeMelon)
var cat = MelonPreferences.CreateCategory("MyMod", "My Mod Settings");

// ALWAYS use GetOrCreate — CreateEntry throws if key already exists
private static MelonPreferences_Entry<T> GetOrCreate<T>(string key, T defaultVal, string displayName)
    => _cat.GetEntry<T>(key) ?? _cat.CreateEntry<T>(key, defaultVal, displayName);

// Usage
var enabledEntry = GetOrCreate("Enabled", false, "Mod Enabled");
bool isEnabled   = enabledEntry.Value;
enabledEntry.Value = true;
MelonPreferences.Save();

// Supported types: bool, int, float, string
```

## ModItemSaveData — Game-Integrated Persistent Data

For saving mod data alongside the game's save file:

```csharp
// Fields
string modFolderName;
Vector3 position;
Vector3 rotation;
float[] saveValue;      // float array for generic data
int[]   saveIntArray;   // int array
int[]   saveIntArray2;  // second int array

// Hook into save/load
SaveSystem.onSavingData += () =>
{
    var data = new ModItemSaveData();
    data.modFolderName = "MyMod";
    data.saveValue     = new float[] { myFloatValue };
    data.saveIntArray  = new int[]   { myIntValue };
    SaveData.instance.modItemData.Add(data);
};

SaveSystem.onLoadingDataLater += () =>
{
    foreach (var item in SaveData.instance.modItemData)
    {
        if (item.modFolderName != "MyMod") continue;
        myFloatValue = item.saveValue[0];
        myIntValue   = item.saveIntArray[0];
    }
};
```

## Balance Sheet

```csharp
// BalanceSheet tracks per-customer revenue/penalties by month

// Inner types
BalanceSheet.CustomerRecord
{
    string customerID;
    string customerName;
    float  revenue;
    float  penalties;
    float  Total;   // property: revenue - penalties
}

BalanceSheet.MonthlySnapshot
{
    int   month;
    int   day;
    List<BalanceSheet.CustomerRecord> records;
    float salaryExpense;
    float TotalRevenue;    // property: sum of all record.revenue
    float TotalPenalties;  // property: sum of all record.penalties
    float GrandTotal;      // property: TotalRevenue - TotalPenalties - salaryExpense
}

// Access
BalanceSheet.MonthlySnapshot snap = BalanceSheet.instance.GetLatestSnapshot();
List<BalanceSheet.MonthlySnapshot> history = BalanceSheet.instance.history;
Dictionary<string, BalanceSheet.CustomerRecord> current = BalanceSheet.instance.currentRecords;

// Called by game internals — available to patch
BalanceSheet.instance.RegisterSalary(int amount);
BalanceSheet.instance.FillInBalanceSheet();
```
