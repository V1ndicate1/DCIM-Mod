using HarmonyLib;
using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;

namespace AutoDispatcher
{
    // ── Harmony patch — reinjects UI every time the AM screen opens ──────────

    [HarmonyPatch(typeof(AssetManagement), "OnEnable")]
    public class AMOpenPatch
    {
        [HarmonyPostfix]
        public static void Postfix(AssetManagement __instance)
        {
            AssetManagementUI.Inject(__instance);
        }
    }

    // ── AM screen UI ─────────────────────────────────────────────────────────

    public static class AssetManagementUI
    {
        private static TextMeshProUGUI _toggleLabel;
        private static TextMeshProUGUI _eolToggleLabel;
        private static TextMeshProUGUI _warnToggleLabel;

        // ── Injection ─────────────────────────────────────────────────────────

        public static void Inject(AssetManagement am)
        {
            Canvas.ForceUpdateCanvases();

            if (am.transform.childCount == 0) return;
            var vl = am.transform.GetChild(0);

            // Find HL filters — VL child that contains a TMP with text "All"
            Transform hlFilters = null;
            for (int i = 0; i < vl.childCount; i++)
            {
                var child = vl.GetChild(i);
                var tmpArr = child.GetComponentsInChildren<TextMeshProUGUI>(true);
                for (int j = 0; j < tmpArr.Length; j++)
                {
                    if (tmpArr[j].text.Trim() == "All") { hlFilters = child; break; }
                }
                if (hlFilters != null) break;
            }
            if (hlFilters == null)
            {
                MelonLogger.Warning("[AD] Inject: could not find HL filters row");
                return;
            }

            // Destroy any existing panel from a prior OnEnable
            var stale = vl.Find("AD_ControlPanel");
            if (stale != null) Object.Destroy(stale.gameObject);

            var hlRT    = hlFilters.GetComponent<RectTransform>();
            float amWidth = hlRT.rect.width > 10f ? hlRT.rect.width : 1180f;

            // ── Build panel ────────────────────────────────────────────────────

            var panel = new GameObject("AD_ControlPanel");
            panel.transform.SetParent(vl, false);
            panel.transform.SetSiblingIndex(hlFilters.GetSiblingIndex());

            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = hlRT.anchorMin;
            panelRT.anchorMax = hlRT.anchorMax;
            panelRT.pivot     = hlRT.pivot;
            panelRT.sizeDelta = new Vector2(amWidth, 28f);
            float hlTopEdge   = hlRT.anchoredPosition.y + hlRT.rect.height * 0.5f;
            panelRT.anchoredPosition = new Vector2(hlRT.anchoredPosition.x, hlTopEdge + 14f);

            var panelLE = panel.AddComponent<LayoutElement>();
            panelLE.ignoreLayout   = true;
            panelLE.preferredWidth = amWidth;
            panelLE.minWidth       = 300f;

            var hl = panel.AddComponent<HorizontalLayoutGroup>();
            var hlPad = new RectOffset();
            hlPad.left = 8; hlPad.right = 8; hlPad.top = 3; hlPad.bottom = 3;
            hl.padding              = hlPad;
            hl.spacing              = 8f;
            hl.childControlWidth    = false;
            hl.childControlHeight   = true;
            hl.childForceExpandWidth  = false;
            hl.childForceExpandHeight = true;

            // ── Auto toggle button ─────────────────────────────────────────────

            var toggleGo  = BuildButton(panel.transform, "Auto: OFF", 110f);
            var toggleBtn = toggleGo.GetComponent<Button>();
            _toggleLabel  = toggleGo.GetComponentInChildren<TextMeshProUGUI>();
            toggleBtn.onClick.AddListener(new System.Action(OnToggleClicked));
            RefreshToggle();

            // ── EOL toggle button ──────────────────────────────────────────────

            var eolToggleGo  = BuildButton(panel.transform, "EOL: OFF", 90f);
            var eolToggleBtn = eolToggleGo.GetComponent<Button>();
            _eolToggleLabel  = eolToggleGo.GetComponentInChildren<TextMeshProUGUI>();
            eolToggleBtn.onClick.AddListener(new System.Action(OnEolToggleClicked));
            RefreshEolToggle();

            // ── Warn toggle button ─────────────────────────────────────────────

            var warnToggleGo  = BuildButton(panel.transform, "Warn: OFF", 90f);
            var warnToggleBtn = warnToggleGo.GetComponent<Button>();
            _warnToggleLabel  = warnToggleGo.GetComponentInChildren<TextMeshProUGUI>();
            warnToggleBtn.onClick.AddListener(new System.Action(OnWarnToggleClicked));
            RefreshWarnToggle();

            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRT);
        }

        // ── Callbacks ─────────────────────────────────────────────────────────

        private static void OnToggleClicked()
        {
            AutoDispatcherMod.SetEnabled(!AutoDispatcherMod.IsEnabled);
            RefreshToggle();
            MelonLogger.Msg($"[AD] Auto-Dispatch toggled: {AutoDispatcherMod.IsEnabled}");
        }

        public static void RefreshToggle()
        {
            if (_toggleLabel == null) return;
            bool on = AutoDispatcherMod.IsEnabled;
            _toggleLabel.text = on ? "Auto: ON" : "Auto: OFF";
            var btn = _toggleLabel.transform.parent.GetComponent<Button>();
            if (btn != null)
                ReusableFunctions.ChangeButtonNormalColor(btn,
                    on ? new Color(0.1f, 0.55f, 0.1f) : new Color(0.35f, 0.35f, 0.35f));
        }

        private static void OnEolToggleClicked()
        {
            AutoDispatcherMod.SetEolEnabled(!AutoDispatcherMod.IsEolEnabled);
            RefreshEolToggle();
        }

        public static void RefreshEolToggle()
        {
            if (_eolToggleLabel == null) return;
            bool on = AutoDispatcherMod.IsEolEnabled;
            _eolToggleLabel.text = on ? "EOL: ON" : "EOL: OFF";
            var btn = _eolToggleLabel.transform.parent.GetComponent<Button>();
            if (btn != null)
                ReusableFunctions.ChangeButtonNormalColor(btn,
                    on ? new Color(0.1f, 0.55f, 0.1f) : new Color(0.35f, 0.35f, 0.35f));
        }

        private static void OnWarnToggleClicked()
        {
            AutoDispatcherMod.SetWarnEnabled(!AutoDispatcherMod.IsWarnEnabled);
            RefreshWarnToggle();
        }

        public static void RefreshWarnToggle()
        {
            if (_warnToggleLabel == null) return;
            bool on = AutoDispatcherMod.IsWarnEnabled;
            _warnToggleLabel.text = on ? "Warn: ON" : "Warn: OFF";
            var btn = _warnToggleLabel.transform.parent.GetComponent<Button>();
            if (btn != null)
                ReusableFunctions.ChangeButtonNormalColor(btn,
                    on ? new Color(0.1f, 0.55f, 0.1f) : new Color(0.35f, 0.35f, 0.35f));
        }

        // ── UI helpers ────────────────────────────────────────────────────────

        private static GameObject BuildButton(Transform parent, string label, float width)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 22f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth  = width;
            le.preferredHeight = 22f;

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
            var lblRT = lblGo.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.sizeDelta = Vector2.zero;
            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 11f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return go;
        }
    }
}
