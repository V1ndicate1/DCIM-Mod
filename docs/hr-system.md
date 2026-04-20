# HR System — Employee Screen

## HRSystem Fields & Methods

```csharp
// Arrays — dynamic length (currently 6 slots), do NOT hardcode length
ButtonExtended[] buttonsHireEmployees;        // Il2CppReferenceArray
ButtonExtended[] buttonsFireEmployees;         // Il2CppReferenceArray
int[]            employeeRequiredReputations;  // Il2CppStructArray
int              selectedEmployeeIndex;

// Overlay GameObjects
GameObject     confirmHireOverlay;
GameObject     confirmFireOverlay;
ButtonExtended buttonReturn;              // back button to main screen

// Methods
ButtonHireEmployee(int i);
ButtonFireEmployee(int i);
ButtonConfirmHire();
ButtonConfirmFireEmployee();
ButtonCancelBuying();
```

## Employee Card DOM Structure

The fire button's parent IS the card:

```
card (VerticalLayoutGroup)
  text_employeeName            (TextMeshProUGUI) — displayed name
  text_requiredReputation      (TextMeshProUGUI) — rep requirement
  [inject your button here]    ← SetSiblingIndex after text_requiredReputation
  [hire or fire button]        ← fireButtons[i]

card.parent                    → cell in the GridLayoutGroup
card.parent.parent             → "Grid Technicians" GameObject (has GridLayoutGroup)
```

## Card Injection — Full Pattern

```csharp
public static void InjectIntoCards(HRSystem hr)
{
    // Clean stale refs first
    var staleKeys = new List<int>();
    foreach (var kv in _myButtons)
        if (kv.Value == null) staleKeys.Add(kv.Key);
    foreach (var k in staleKeys) { _myButtons.Remove(k); _spacers.Remove(k); }

    var fireButtons = hr.buttonsFireEmployees;
    for (int i = 0; i < fireButtons.Count; i++)
    {
        var fireBtn = fireButtons[i];
        if (fireBtn == null) continue;
        var card = fireBtn.transform.parent;

        // Read displayed name
        var nameComp = card.Find("text_employeeName")?.GetComponent<TextMeshProUGUI>();
        string displayedName = nameComp?.text;
        if (string.IsNullOrEmpty(displayedName)) continue;

        // Confirm slot is actually hired (only hired techs appear in tm.technicians)
        int techId = -1;
        var tm = TechnicianManager.instance;
        if (tm?.technicians != null)
        {
            for (int t = 0; t < tm.technicians.Count; t++)
            {
                var tech = tm.technicians[t];
                if (tech != null && tech.technicianName == displayedName)
                { techId = tech.technicianID; break; }
            }
        }

        // Slot is empty — destroy any stale injected button
        if (techId < 0)
        {
            if (_myButtons.TryGetValue(i, out var stale) && stale != null)
                Object.Destroy(stale);
            _myButtons.Remove(i);
            continue;
        }

        // Already injected — skip
        if (_myButtons.ContainsKey(i) && _myButtons[i] != null) continue;

        // Build button from scratch (NEVER clone — see ui-building.md)
        var btnGo = new GameObject("MyBtn_" + i);
        btnGo.transform.SetParent(card, false);
        // ... set up RectTransform, Image, Button, Label ...

        // Position: after text_requiredReputation, before fire button
        var repTF = card.Find("text_requiredReputation");
        btnGo.transform.SetSiblingIndex(repTF != null
            ? repTF.GetSiblingIndex() + 1
            : fireBtn.transform.GetSiblingIndex());

        // Store BEFORE anything else that could throw, to prevent duplicate inject
        _myButtons[i] = btnGo;

        // Capture for closure
        string capturedName = displayedName;
        int    capturedSlot = i;
        btnGo.GetComponent<Button>().onClick.AddListener(new System.Action(() =>
        {
            // re-resolve techId at click time (may have changed)
            int id = ResolveTechId(capturedName);
            if (id < 0) return;
            var hrs = Object.FindObjectOfType<HRSystem>();
            if (hrs != null) ShowMyPanel(hrs, id, capturedSlot);
        }));

        // Grow the GridLayoutGroup cell to fit the added button height
        try
        {
            var glg = card.parent?.parent?.GetComponent<GridLayoutGroup>();
            if (glg != null && glg.cellSize.y < 340f) // size guard — don't reapply
                glg.cellSize = new Vector2(glg.cellSize.x, glg.cellSize.y + 60f);
        }
        catch { }
    }
}
```

## Reinject Poll (Required — Game Rebuilds Cards on Hire)

After hiring, the game destroys and rebuilds card GameObjects — your injected buttons are gone. Poll every 0.5s:

```csharp
// In OnUpdate:
_hrPollTimer += Time.deltaTime;
if (_hrPollTimer >= 0.5f)
{
    _hrPollTimer = 0f;
    var hr = Object.FindObjectOfType<HRSystem>();
    if (hr != null) InjectIntoCards(hr);
}
```

## Patching HR Screen Open

```csharp
[HarmonyPatch(typeof(HRSystem), "OnEnable")]
public class HROpenPatch
{
    [HarmonyPostfix]
    public static void Postfix(HRSystem __instance)
    {
        InjectIntoCards(__instance);
    }
}
```

## Finding a Tech by Displayed Name

```csharp
private static int ResolveTechId(string displayedName)
{
    var tm = TechnicianManager.instance;
    if (tm?.technicians == null) return -1;
    for (int i = 0; i < tm.technicians.Count; i++)
    {
        var t = tm.technicians[i];
        if (t != null && t.technicianName == displayedName) return t.technicianID;
    }
    return -1;
}
```

## Global Panel Injection (Top of HR Screen)

```csharp
var hrRT    = hr.GetComponent<RectTransform>();
var panelGo = new GameObject("MyGlobalPanel");
panelGo.transform.SetParent(hr.transform, false);
var panelRT = panelGo.AddComponent<RectTransform>();
panelRT.anchorMin        = new Vector2(0f, 1f);
panelRT.anchorMax        = new Vector2(0f, 1f);
panelRT.pivot            = new Vector2(0f, 1f);
panelRT.anchoredPosition = new Vector2(8f, -5f);
panelRT.sizeDelta        = new Vector2(360f, 32f);

var hl = panelGo.AddComponent<HorizontalLayoutGroup>();
// ... configure layout ...

// Nested canvas to render above HR content
var nc = panelGo.AddComponent<Canvas>();
nc.overrideSorting = true;
nc.sortingOrder    = 10;
panelGo.AddComponent<GraphicRaycaster>();
```
