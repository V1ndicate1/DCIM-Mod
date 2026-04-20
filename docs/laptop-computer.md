# Laptop / Computer Screen System

## How the Laptop Works

`ComputerShop` extends `Interact` — it's a world object the player clicks on. It manages a `canvasComputerShop` (world-space canvas) containing all screen GameObjects. Screens are shown/hidden by activating/deactivating their GameObjects.

## Screen GameObjects (private SerializeField — interop-accessible)

```
canvasComputerShop      ← root canvas for all laptop UI
  mainScreen            ← home screen / app grid
  shopScreen            ← buy servers/switches
  assetManagementScreen ← device line list
  networkMapScreen      ← network topology view
  balanceSheetScreen    ← revenue/expense history
  hireScreen            ← HR / technician hiring
```

All `[SerializeField]` private fields are accessible via IL2CPP interop directly on the instance.

Also confirmed via decompiled source — additional ComputerShop fields:
```csharp
ShopItem[] shopItems              // all shop item MonoBehaviours
TextMeshProUGUI textNetworkMap    // network map label text
```

## Navigation Methods

```csharp
var cs = Object.FindObjectOfType<ComputerShop>();
cs.ButtonShopScreen();
cs.ButtonAssetManagementScreen();
cs.ButtonNetworkMap();
cs.ButtonBalanceSheetScreen();
cs.ButtonHireScreen();
cs.ButtonReturnMainScreen();  // always returns to mainScreen, hides all others
```

## Adding a New Laptop App — Full Pattern

### Step 1: Patch ComputerShop.Awake()

```csharp
[HarmonyPatch(typeof(ComputerShop), "Awake")]
public class ComputerShopAwakePatch
{
    [HarmonyPostfix]
    public static void Postfix(ComputerShop __instance)
    {
        var canvas     = __instance.canvasComputerShop;  // interop-accessible
        var mainScreen = __instance.mainScreen;           // interop-accessible

        // 1. Create your screen as a sibling of the other app screens.
        // IMPORTANT: parent to mainScreen.transform.parent (the screens container),
        // NOT canvas.transform. The canvas root is full-screen; the screens container
        // one level below it defines the actual laptop panel bounds (1200×675 world-space).
        // Confirmed via runtime logging: canvas renderMode=WorldSpace, rect=1200×675;
        // mainScreen parent has no Canvas component — it is the screens container.
        var myScreen = new GameObject("MyDispatchScreen");
        myScreen.transform.SetParent(mainScreen.transform.parent, false);
        var rt = myScreen.AddComponent<RectTransform>();
        // Copy mainScreen's RT after one frame (anchor-fill + 10px inset); do NOT
        // anchor-fill directly — that fills the canvas root, not the laptop panel.
        MelonCoroutines.Start(CopyRTAfterFrame(rt, mainScreen.GetComponent<RectTransform>()));
        // ... build screen UI here ...
        myScreen.SetActive(false);
        MyMod._myScreen = myScreen;  // store static ref

        // 2. Inject app button into mainScreen
        // mainScreen contains a layout group with the existing app buttons
        // Find it and add your button as a child
        var layout = mainScreen.GetComponentInChildren<HorizontalLayoutGroup>()
                  ?? mainScreen.GetComponentInChildren<VerticalLayoutGroup>()?.gameObject
                               .GetComponent<LayoutGroup>();
        if (layout != null)
        {
            var appBtn = BuildAppButton(layout.transform, "Dispatch");
            appBtn.onClick.AddListener(new System.Action(() =>
            {
                __instance.ButtonReturnMainScreen(); // hides everything + shows mainScreen
                mainScreen.SetActive(false);          // then hide mainScreen too
                myScreen.SetActive(true);
            }));
        }
    }
}
```

### Step 2: Patch ButtonReturnMainScreen (close your screen on any navigation)

```csharp
[HarmonyPatch(typeof(ComputerShop), "ButtonReturnMainScreen")]
public class ReturnMainScreenPatch
{
    [HarmonyPostfix]
    public static void Postfix()
    {
        if (MyMod._myScreen != null)
            MyMod._myScreen.SetActive(false);
    }
}
```

### Step 3: Your screen's Back button

```csharp
backBtn.onClick.AddListener(new System.Action(() =>
{
    MyMod._myScreen.SetActive(false);
    Object.FindObjectOfType<ComputerShop>()?.ButtonReturnMainScreen();
}));
```

## Screen Layout Tips

- **Parent to `mainScreen.transform.parent`, not `canvas.transform`** — the canvas root is full-screen; the screens container one level below defines the laptop panel area.
- After parenting, copy mainScreen's RectTransform after one frame (`CopyRT` in a coroutine). Do NOT use anchor-fill (0,0→1,1 / sizeDelta=0) — that fills the full-screen canvas root, not the laptop panel.
- Your screen is world-space (same as the laptop canvas) — no changes needed for rendering
- Add a nested `Canvas` with `overrideSorting = true` on any overlay panels inside your screen (see `ui-building.md`)
- The laptop canvas sortingOrder is typically low — your screen inherits it automatically

## Portable Laptop (Alternative Approach)

If you want the player to open the laptop anywhere (key press, not clicking the desk):

**Option A — Screen-space overlay canvas (recommended):**
```csharp
// Create a new screen-space canvas, independent of the laptop world object
var go = new GameObject("PortableLaptop");
Object.DontDestroyOnLoad(go);
var canvas = go.AddComponent<Canvas>();
canvas.renderMode = RenderMode.ScreenSpaceOverlay;
canvas.sortingOrder = 200;
go.AddComponent<GraphicRaycaster>();
// Build your UI as children of this canvas
// Toggle with a key press in OnUpdate
```

**Option B — Follow the player (not recommended):** Moving the ComputerShop world object has side effects (colliders, animations, interaction range). Avoid unless you need the 3D laptop model visible.

## OnEnable Hook for Screens

If you need to react when the AM or HR screen opens (e.g. to inject UI):
```csharp
[HarmonyPatch(typeof(AssetManagement), "OnEnable")]
public class AMOpenPatch
{
    [HarmonyPostfix]
    public static void Postfix(AssetManagement __instance)
    {
        // fires every time AM screen is opened
    }
}
```
