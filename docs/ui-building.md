# UI Building — Patterns & Pitfalls

## ⚠️ Never Clone Existing Buttons

Cloning copies Unity's serialized persistent `onClick` listeners. `RemoveAllListeners()` only removes runtime listeners — clones will fire the original action (hire/fire/buy) when your button is clicked.

**Always build from `new GameObject`:**

```csharp
private static GameObject BuildButton(Transform parent, string label, float width)
{
    var go = new GameObject("Btn_" + label);
    go.transform.SetParent(parent, false);

    var rt  = go.AddComponent<RectTransform>();
    rt.sizeDelta = new Vector2(width, 30f);

    var img = go.AddComponent<Image>();
    img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

    var btn = go.AddComponent<Button>();
    btn.targetGraphic = img;

    var cb = new ColorBlock();
    cb.normalColor      = new Color(0.2f, 0.2f, 0.2f, 1f);
    cb.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
    cb.pressedColor     = new Color(0.1f, 0.1f, 0.1f, 1f);
    cb.selectedColor    = new Color(0.2f, 0.2f, 0.2f, 1f);
    cb.colorMultiplier  = 1f;
    cb.fadeDuration     = 0.1f;
    btn.colors = cb;

    var lblGo = new GameObject("Label");
    lblGo.transform.SetParent(go.transform, false);
    var lblRT  = lblGo.AddComponent<RectTransform>();
    lblRT.anchorMin = Vector2.zero; lblRT.anchorMax = Vector2.one; lblRT.sizeDelta = Vector2.zero;
    var tmp = lblGo.AddComponent<TextMeshProUGUI>();
    tmp.text      = label;
    tmp.fontSize  = 11f;
    tmp.color     = Color.white;
    tmp.alignment = TextAlignmentOptions.Center;

    return go;
}
```

## Building a Label

```csharp
private static TextMeshProUGUI BuildLabel(Transform parent, string text, float width)
{
    var go = new GameObject("Lbl");
    go.transform.SetParent(parent, false);
    var rt = go.AddComponent<RectTransform>();
    rt.sizeDelta = new Vector2(width, 22f);
    var tmp = go.AddComponent<TextMeshProUGUI>();
    tmp.text     = text;
    tmp.fontSize = 11f;
    tmp.color    = Color.white;
    var le = go.AddComponent<LayoutElement>();
    le.preferredWidth  = width;
    le.preferredHeight = 22f;
    return tmp;
}
```

## Building a Row (HorizontalLayoutGroup)

```csharp
private static GameObject BuildRow(Transform parent)
{
    var go = new GameObject("Row");
    go.transform.SetParent(parent, false);
    var hl = go.AddComponent<HorizontalLayoutGroup>();
    hl.childControlWidth       = true;
    hl.childControlHeight      = true;
    hl.childForceExpandWidth   = false;
    hl.childForceExpandHeight  = false;
    hl.spacing = 4f;
    var le = go.AddComponent<LayoutElement>();
    le.preferredHeight = 22f;
    return go;
}
```

## ScrollView from Scratch

```csharp
// Scroll container
var scrollGo = new GameObject("Scroll");
scrollGo.transform.SetParent(parent, false);
var scrollLE = scrollGo.AddComponent<LayoutElement>();
scrollLE.preferredHeight = 220f;
scrollLE.flexibleWidth   = 1f;
var scrollRect = scrollGo.AddComponent<ScrollRect>();
scrollRect.horizontal = false;

// Viewport — clips content, needs Image for Mask to work
var viewport = new GameObject("Viewport");
viewport.transform.SetParent(scrollGo.transform, false);
var vpRT = viewport.AddComponent<RectTransform>();
vpRT.anchorMin = Vector2.zero; vpRT.anchorMax = Vector2.one;
vpRT.sizeDelta = Vector2.zero; vpRT.offsetMin = Vector2.zero; vpRT.offsetMax = Vector2.zero;
viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f); // invisible but required
viewport.AddComponent<Mask>().showMaskGraphic = false;
scrollRect.viewport = vpRT;

// Content — auto-grows to fit children
var content = new GameObject("Content");
content.transform.SetParent(viewport.transform, false);
var contentRT = content.AddComponent<RectTransform>();
contentRT.anchorMin = new Vector2(0f, 1f);
contentRT.anchorMax = new Vector2(1f, 1f);
contentRT.pivot     = new Vector2(0.5f, 1f);
contentRT.sizeDelta = Vector2.zero;
var contentVL = content.AddComponent<VerticalLayoutGroup>();
contentVL.childControlWidth       = true;
contentVL.childControlHeight      = true;
contentVL.childForceExpandWidth   = true;
contentVL.childForceExpandHeight  = false;
contentVL.spacing = 2f;
var csf = content.AddComponent<ContentSizeFitter>();
csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
scrollRect.content = contentRT;
// Add children to content.transform — they auto-stack
```

## Overlay Panel Above Scroll Masks

Any panel that needs to appear above a `Mask` in its parent hierarchy needs a nested Canvas:

```csharp
var nc = panel.AddComponent<Canvas>();
nc.overrideSorting = true;
nc.sortingOrder    = 100;   // 10 for minor, 100 for full overlays
panel.AddComponent<GraphicRaycaster>();
```

Without this the panel gets clipped by any Mask component in the parent chain.

## VerticalLayoutGroup — Required Flags

```csharp
var vl = go.AddComponent<VerticalLayoutGroup>();
vl.padding = new RectOffset();
vl.padding.left = 8; vl.padding.right = 8; vl.padding.top = 6; vl.padding.bottom = 6;
vl.spacing               = 4f;
vl.childControlWidth     = true;
vl.childControlHeight    = true;
vl.childForceExpandWidth = true;
vl.childForceExpandHeight = false;  // let children define their own height
LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
```

## Full Panel Template (Dark Background + Border)

```csharp
// Outer — teal border (the border IS this Image)
var panel = new GameObject("MyPanel");
panel.transform.SetParent(parent, false);
var rt = panel.AddComponent<RectTransform>();
rt.sizeDelta = new Vector2(360f, 460f);
panel.AddComponent<Image>().color = new Color(0.0f, 0.75f, 0.75f, 0.6f);

// Inner — dark background inset 2px
var inner = new GameObject("Inner");
inner.transform.SetParent(panel.transform, false);
var innerRT = inner.AddComponent<RectTransform>();
innerRT.anchorMin = Vector2.zero; innerRT.anchorMax = Vector2.one;
innerRT.offsetMin = new Vector2(2f, 2f); innerRT.offsetMax = new Vector2(-2f, -2f);
inner.AddComponent<Image>().color = new Color(0.12f, 0.14f, 0.16f, 0.97f);

var vl = inner.AddComponent<VerticalLayoutGroup>();
// ... set flags as above ...

// Nested canvas so it renders above other UI
var nc = panel.AddComponent<Canvas>();
nc.overrideSorting = true;
nc.sortingOrder    = 100;
panel.AddComponent<GraphicRaycaster>();
```

## Divider Line

```csharp
private static void BuildDivider(Transform parent)
{
    var go = new GameObject("Divider");
    go.transform.SetParent(parent, false);
    var img = go.AddComponent<Image>();
    img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
    var le = go.AddComponent<LayoutElement>();
    le.preferredHeight = 1f;
    le.flexibleWidth   = 1f;
}
```

## Stale Reference Cleanup Pattern

When UI panels get destroyed and rebuilt (e.g. after hire in HR screen):

```csharp
// In your inject method, before injecting:
var staleKeys = new List<int>();
foreach (var kv in _myButtons)
    if (kv.Value == null) staleKeys.Add(kv.Key);
foreach (var k in staleKeys) _myButtons.Remove(k);

// Null-check destroyed panel references
if (_detailPanel != null && !_detailPanel)
{
    _detailPanel   = null;
    _currentTechId = -1;
}
```

## ChangeButtonNormalColor Helper

```csharp
// Built-in game helper
ReusableFunctions.ChangeButtonNormalColor(Button button, Color color);

// Common colors used in this project:
// ON / active:   new Color(0.1f, 0.55f, 0.1f)   — green
// OFF / inactive: new Color(0.35f, 0.35f, 0.35f) — grey
// Danger:        new Color(0.55f, 0.1f, 0.1f)    — red
// Accent:        new Color(0.0f, 0.45f, 0.45f)   — teal
```

## ContentSizeFitter — Common Modes

```csharp
var csf = go.AddComponent<ContentSizeFitter>();
csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;  // grows to fit children
csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;  // don't touch width
```

## Force Layout Rebuild

Call after adding/removing children or changing layout settings:
```csharp
LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
Canvas.ForceUpdateCanvases(); // nuclear option — updates all canvases
```
