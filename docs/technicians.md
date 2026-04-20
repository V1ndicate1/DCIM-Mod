# Technicians & Dispatch System

## ⚠️ CRITICAL: SendTechnician Does NOT Let You Pick a Tech

`TechnicianManager.SendTechnician(sw, server)` uses internal round-robin (`lastAssignedIndex`) to assign to any available tech. **It ignores which tech you want.** If you need a specific technician, use `AssignJob` directly.

```csharp
// WRONG — game picks any free tech via round-robin
TechnicianManager.instance.SendTechnician(sw, server);

// CORRECT — targets the exact tech you want
var repairJob = new TechnicianManager.RepairJob
{
    networkSwitch      = sw,      // null for server jobs
    server             = server,  // null for switch jobs
    assignedTechnician = tech
};
tech.AssignJob(repairJob);
```

## TechnicianManager — Full Reference

**Public fields/methods:**
```csharp
List<Technician> technicians;           // all hired techs — use index loop, NOT foreach
int[]            hiredTechnicians;      // array of hired tech IDs
int              QueuedJobCount;        // property

SendTechnician(NetworkSwitch sw, Server server);              // dispatch (ignores tech selection — see warning above)
IsDeviceAlreadyAssigned(NetworkSwitch sw, Server server);     // check before dispatching
RequestNextJob(Technician tech);                              // give a specific tech their next job from game queue
GetQueuedJobs();                                              // List<RepairJob> — use index loop
GetActiveJobs();                                              // List<RepairJob> — use index loop
FireTechnician(int technicianID);
AddTechnician(Technician technician);
RestoreJobQueue(List<RepairJobSaveData> savedJobs);
```

**Private fields (interop-accessible):**
```csharp
int              lastAssignedIndex;     // round-robin state
Queue<RepairJob> jobQueue;              // internal job queue
Queue<RepairJob> pendingDispatches;     // pending dispatch queue
```

## RepairJob Struct

```csharp
public struct TechnicianManager.RepairJob
{
    public NetworkSwitch networkSwitch;
    public Server        server;
    public Technician    assignedTechnician;
    public string        DeviceName { get; }  // computed: server ID or switch ID
}
```

## Technician — Full Reference

**Public fields:**
```csharp
int    technicianID;
string technicianName;
int    salary;
bool   isBusy;
TechnicianManager.RepairJob? CurrentJob { get; }  // nullable struct — check .HasValue
```

**Private fields (interop-accessible):**
```csharp
NetworkSwitch networkSwitch;     // current job target
Server        server;            // current job target
TechnicianState currentState;    // Idle / GoingForNewServer / BringingNewServer / GoingBackWithOldServer / EndingHisWork
```

**Public method:**
```csharp
tech.AssignJob(TechnicianManager.RepairJob job);
// Sets isBusy = true, stores job, triggers movement/work coroutines
// Safe to call directly — this is what SendTechnician calls internally
```

## Dispatch Flow — Correct Pattern

```csharp
// 1. Check if already assigned
if (TechnicianManager.instance.IsDeviceAlreadyAssigned(sw, server)) return;

// 2. Find the tech you want (check isBusy, check your own task settings)
Technician target = null;
for (int i = 0; i < tm.technicians.Count; i++)
{
    var t = tm.technicians[i];
    if (t == null || t.isBusy) continue;
    // apply your own eligibility checks
    target = t;
    break;
}
if (target == null) return; // no eligible tech

// 3. Check money
float balance = PlayerManager.instance.playerClass.money;
if (balance < price) return; // defer

// 4. Dispatch directly to the target tech
var job = new TechnicianManager.RepairJob
{
    networkSwitch      = sw,
    server             = server,
    assignedTechnician = target
};
target.AssignJob(job);

// 5. Deduct money manually (bypasses AM UI)
PlayerManager.instance.playerClass.UpdateCoin(-price, false);
```

## Price Sampling (Without Showing the Confirm Overlay)

```csharp
// Open AM SendTechnician (sets priceOfTechnician, shows overlay internally)
am.SendTechnician(sw, server);
int price = am.priceOfTechnician;
// Cancel immediately — no charge, hides overlay
am.ButtonCancelSendingTechnician();
```

## Intercepting Tech-Finished Events (RequestNextJob)

Patch `RequestNextJob` to immediately dispatch from your own queue when a tech finishes:

```csharp
[HarmonyPatch(typeof(TechnicianManager), "RequestNextJob")]
public class RequestNextJobPatch
{
    [HarmonyPrefix]
    public static bool Prefix(TechnicianManager __instance, Technician technician)
    {
        // Check your queue for this tech
        var nextJob = MyQueue.GetNext(technician.technicianID);
        if (nextJob == null) return true; // nothing queued — let game handle it

        var repairJob = new TechnicianManager.RepairJob
        {
            networkSwitch      = nextJob.Sw,
            server             = nextJob.Server,
            assignedTechnician = technician
        };
        technician.AssignJob(repairJob);
        return false; // suppress game's queue lookup
    }
}
```

## Blocking Unwanted Game Dispatches

Prevent the game's internal `SendTechnician` from assigning when your mod owns the device:

```csharp
[HarmonyPatch(typeof(TechnicianManager), "SendTechnician")]
public class SendTechnicianPatch
{
    [HarmonyPrefix]
    public static bool Prefix(NetworkSwitch networkSwitch, Server server)
    {
        string id = server != null ? "server_" + server.ServerID
                                   : "switch_" + networkSwitch.GetSwitchId();
        if (MyDispatcher.IsManaged(id)) return false; // block — we own this
        return true; // let game handle unmanaged devices
    }
}
```

## Rebuild State After Save Load

```csharp
SaveSystem.onLoadingDataLater += (System.Action)RebuildFromGameState;

private static void RebuildFromGameState()
{
    var tm = TechnicianManager.instance;
    if (tm == null) return;

    var activeJobs = tm.GetActiveJobs();
    for (int i = 0; i < activeJobs.Count; i++)
    {
        var job = activeJobs[i];
        if (job.server == null && job.networkSwitch == null) continue;
        // re-register into your tracking system
        string id = job.server != null ? "server_" + job.server.ServerID
                                       : "switch_" + job.networkSwitch.GetSwitchId();
        MyDispatcher.MarkAssigned(id);
    }
}
```

## Hire / Fire Detection

```csharp
[HarmonyPatch(typeof(HRSystem), "ButtonConfirmHire")]
public class HirePatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        // New tech hired — find the new one by comparing against known IDs
        var tm = TechnicianManager.instance;
        for (int i = 0; i < tm.technicians.Count; i++)
        {
            var t = tm.technicians[i];
            if (!MySettings.IsKnown(t.technicianID))
                MySettings.InitTech(t.technicianID);
        }
    }
}

[HarmonyPatch(typeof(HRSystem), "ButtonConfirmFireEmployee")]
public class FirePatch
{
    [HarmonyPostfix]
    public static void Postfix() { /* drain queue for fired tech */ }
}
```
