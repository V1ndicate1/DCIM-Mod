using Il2Cpp;
using Il2CppTMPro;
using HarmonyLib;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;

namespace FloorManager.Patches
{
    [HarmonyPatch(typeof(ComputerShop), "Awake")]
    public class ComputerShopAwakePatch
    {
        [HarmonyPostfix]
        public static void Postfix(ComputerShop __instance)
        {
            FloorManagerMod.ComputerShopRef = __instance;

            var canvas = __instance.canvasComputerShop;
            var mainScreen = __instance.mainScreen;
            FloorManagerMod.MainScreenRef = mainScreen;

            if (canvas == null || mainScreen == null)
            {
                MelonLogger.Warning("[DCIM] canvasComputerShop or mainScreen null in Awake postfix — app injection skipped.");
                return;
            }

            // Create the floor manager screen as a sibling of other screens.
            // Parent to mainScreen's parent (the screens container), NOT the canvas root —
            // the canvas root is full-screen; the screens container defines the laptop panel bounds.
            var fmScreen = new GameObject("DCIMScreen");
            fmScreen.transform.SetParent(mainScreen.transform.parent, false);
            var rt = fmScreen.AddComponent<RectTransform>();
            fmScreen.SetActive(false);

            // Copy mainScreen's RectTransform after a frame so layout has settled
            // (during Awake, mainRT values may be zero/default)
            MelonCoroutines.Start(CopyRTNextFrame(rt, mainScreen.GetComponent<RectTransform>()));
            FloorManagerMod.DCIMScreen = fmScreen;

            // Build the app content inside the screen
            FloorMapApp.Build(fmScreen);

            // Inject app button into the main screen layout (could be Grid, Horizontal, or Vertical)
            var layout = mainScreen.GetComponentInChildren<LayoutGroup>();
            if (layout != null)
            {
                var appBtn = BuildAppButton(layout.transform, "DCIM");
                appBtn.onClick.AddListener(new System.Action(() =>
                {
                    if (EventSystem.current != null)
                        EventSystem.current.SetSelectedGameObject(null);
                    __instance.ButtonReturnMainScreen();
                    mainScreen.SetActive(false);
                    fmScreen.SetActive(true);
                    FloorMapApp.OnAppOpened();
                }));
            }
            else
            {
                MelonLogger.Warning("[DCIM] Could not find layout group in mainScreen — button not injected.");
            }

        }

        private static IEnumerator CopyRTNextFrame(RectTransform target, RectTransform source)
        {
            yield return null;
            CopyRT(target, source);
        }

        internal static void CopyRT(RectTransform target, RectTransform source)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.sizeDelta = source.sizeDelta;
            target.anchoredPosition = source.anchoredPosition;
            target.offsetMin = source.offsetMin;
            target.offsetMax = source.offsetMax;
        }

        private static Button BuildAppButton(Transform parent, string label)
        {
            // Match game's native button style: "Icon XXX bcg" wrapper with button child
            // The game uses 100x100 gray background squares with icons
            var go = new GameObject("Icon DCIM bcg");
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(100f, 100f);

            // Gray background matching the game's icon buttons
            var img = go.AddComponent<Image>();
            img.color = new Color(0.72f, 0.72f, 0.72f, 1f);

            // Border outline to match game style
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.4f, 0.4f, 0.4f, 1f);
            outline.effectDistance = new Vector2(2f, -2f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var cb = new ColorBlock();
            cb.normalColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.highlightedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.pressedColor = new Color(0.55f, 0.55f, 0.55f, 1f);
            cb.selectedColor = new Color(0.72f, 0.72f, 0.72f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            // Icon area — dark square in the center like other icons
            var iconGo = new GameObject("Icon");
            iconGo.transform.SetParent(go.transform, false);
            var iconRT = iconGo.AddComponent<RectTransform>();
            iconRT.anchorMin = new Vector2(0.15f, 0.25f);
            iconRT.anchorMax = new Vector2(0.85f, 0.90f);
            iconRT.sizeDelta = Vector2.zero;
            iconRT.offsetMin = Vector2.zero;
            iconRT.offsetMax = Vector2.zero;
            var iconImg = iconGo.AddComponent<Image>();
            iconImg.color = new Color(0.15f, 0.15f, 0.18f, 1f);

            // Grid lines inside icon to suggest a floor plan
            BuildGridLine(iconGo.transform, 0.5f, 0f, 0.5f, 1f, true);  // vertical center
            BuildGridLine(iconGo.transform, 0f, 0.5f, 1f, 0.5f, false); // horizontal center

            // Label below the icon area
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lblRT = lblGo.AddComponent<RectTransform>();
            lblRT.anchorMin = new Vector2(0f, 0f);
            lblRT.anchorMax = new Vector2(1f, 0.22f);
            lblRT.sizeDelta = Vector2.zero;
            lblRT.offsetMin = Vector2.zero;
            lblRT.offsetMax = Vector2.zero;
            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 11f;
            tmp.color = new Color(0.1f, 0.1f, 0.1f, 1f);
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        private static void BuildGridLine(Transform parent, float x1, float y1, float x2, float y2, bool vertical)
        {
            var lineGo = new GameObject("GridLine");
            lineGo.transform.SetParent(parent, false);
            var lineRT = lineGo.AddComponent<RectTransform>();
            if (vertical)
            {
                lineRT.anchorMin = new Vector2(x1, 0f);
                lineRT.anchorMax = new Vector2(x1, 1f);
                lineRT.sizeDelta = new Vector2(2f, 0f);
                lineRT.anchoredPosition = Vector2.zero;
            }
            else
            {
                lineRT.anchorMin = new Vector2(0f, y1);
                lineRT.anchorMax = new Vector2(1f, y1);
                lineRT.sizeDelta = new Vector2(0f, 2f);
                lineRT.anchoredPosition = Vector2.zero;
            }
            var lineImg = lineGo.AddComponent<Image>();
            lineImg.color = new Color(0.45f, 0.45f, 0.45f, 0.6f);
        }
    }

    [HarmonyPatch(typeof(ComputerShop), "ButtonReturnMainScreen")]
    public class ReturnMainScreenPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (FloorManagerMod.DCIMScreen != null)
                FloorManagerMod.DCIMScreen.SetActive(false);
        }
    }
}
