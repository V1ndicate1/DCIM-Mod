using Il2Cpp;
using Il2CppTMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FloorManager
{
    public static class UIHelper
    {
        public static Button BuildButton(Transform parent, string label, float width)
        {
            var go = new GameObject("Btn_" + label);
            go.transform.SetParent(parent, false);

            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 30f);

            // Prevent layout groups from squishing the button
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = width;
            le.preferredWidth = width;
            le.preferredHeight = 30f;

            var img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var cb = new ColorBlock();
            cb.normalColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            cb.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            cb.pressedColor = new Color(0.1f, 0.1f, 0.1f, 1f);
            cb.selectedColor = new Color(0.2f, 0.2f, 0.2f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;

            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lblRT = lblGo.AddComponent<RectTransform>();
            lblRT.anchorMin = Vector2.zero;
            lblRT.anchorMax = Vector2.one;
            lblRT.sizeDelta = Vector2.zero;
            var tmp = lblGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 11f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;

            return btn;
        }

        public static TextMeshProUGUI BuildLabel(Transform parent, string text, float width)
        {
            var go = new GameObject("Lbl");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(width, 22f);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 11f;
            tmp.color = Color.white;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.preferredHeight = 22f;
            return tmp;
        }

        public static GameObject BuildDivider(Transform parent)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.3f, 0.3f, 0.3f, 0.8f);
            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 1f;
            le.flexibleWidth = 1f;
            return go;
        }

        public static Color GetDeviceTypeColor(string objName)
        {
            // Color is determined by the game object name (e.g., "Server.Purple2(Clone)")
            // NOT by the product type name (e.g., "RISC 3U 5000 IOPS")
            if (objName != null)
            {
                if (objName.Contains("Blue")) return new Color(0.3f, 0.5f, 1.0f);
                if (objName.Contains("Green")) return new Color(0.3f, 0.8f, 0.3f);
                if (objName.Contains("Purple")) return new Color(0.6f, 0.3f, 0.9f);
                if (objName.Contains("Yellow")) return new Color(1.0f, 0.85f, 0.2f);
            }
            return Color.white;
        }

        /// <summary>
        /// Derives server type name from game object name (e.g. "Server.Blue2(Clone)" → "System X 7U 12000 IOPS").
        /// Object name is always reliable; server.item may be null after save load and ReturnServerNameFromType can return wrong names.
        /// </summary>
        public static string GetServerTypeName(string objName)
        {
            if (objName != null)
            {
                // Blue = System X, Green = GPU, Purple = Mainframe, Yellow = RISC
                // 1 = 3U 5000 IOPS, 2 = 7U 12000 IOPS
                if (objName.Contains("Blue1")) return "System X 3U 5000 IOPS";
                if (objName.Contains("Blue2")) return "System X 7U 12000 IOPS";
                if (objName.Contains("Green1")) return "GPU 3U 5000 IOPS";
                if (objName.Contains("Green2")) return "GPU 7U 12000 IOPS";
                if (objName.Contains("Purple1")) return "Mainframe 3U 5000 IOPS";
                if (objName.Contains("Purple2")) return "Mainframe 7U 12000 IOPS";
                if (objName.Contains("Yellow1")) return "RISC 3U 5000 IOPS";
                if (objName.Contains("Yellow2")) return "RISC 7U 12000 IOPS";
            }
            return objName ?? "Unknown";
        }

        public static readonly Color SwitchColor = new Color(0.0f, 0.8f, 0.8f);
        public static readonly Color PatchPanelColor = new Color(0.5f, 0.5f, 0.5f);

        public static readonly Color StatusRed = new Color(0.9f, 0.2f, 0.2f);
        public static readonly Color StatusYellow = new Color(1.0f, 0.7f, 0.1f);
        public static readonly Color StatusGreen = new Color(0.2f, 0.8f, 0.2f);
        public static readonly Color StatusGray = new Color(0.4f, 0.4f, 0.4f);
        public static readonly Color StatusCyan   = new Color(0.0f, 0.85f, 0.90f, 1f);  // EOL countdown (approaching)
        public static readonly Color StatusOrange = new Color(1.0f, 0.5f,  0.1f,  1f);  // EOL overdue (+HH:MM:SS)

        // ── EOL time helpers ──────────────────────────────────────────────────────
        // Call these anywhere a live EOL countdown is needed.
        //
        //   eolTime > 0  → counting down   → "HH:MM:SS" in StatusCyan
        //   eolTime <= 0 → expired/overdue  → "+HH:MM:SS" in StatusOrange
        //
        // Usage in a live-refresh coroutine:
        //   lbl.text  = UIHelper.FormatEolTime(device.eolTime);
        //   lbl.color = UIHelper.EolTimeColor(device.eolTime);

        public static string FormatEolTime(int eolTime)
        {
            int t = System.Math.Abs(eolTime);
            string hms = $"{t / 3600:D2}:{(t % 3600) / 60:D2}:{t % 60:D2}";
            return eolTime <= 0 ? ("+" + hms) : hms;
        }

        public static Color EolTimeColor(int eolTime)
        {
            return eolTime <= 0 ? StatusOrange : StatusCyan;
        }

        // Apply EOL time text + color to a label in one call.
        public static void ApplyEolLabel(TextMeshProUGUI lbl, int eolTime)
        {
            lbl.text  = FormatEolTime(eolTime);
            lbl.color = EolTimeColor(eolTime);
        }

        // ── Device state helper ───────────────────────────────────────────────────
        // Single source of truth for broken/on/eol/eolTime from a Server or Switch.
        // Use this in every live-refresh coroutine and initial row build so the
        // "EOL server showing OFF" class of bug cannot recur.
        //
        //   broken  — device needs repair
        //   on      — device is powered on (independent of EOL state)
        //   eol     — EOL timer has expired (eolTime <= 0)
        //   eolTime — raw timer value (negative when expired)
        //
        // Note: on is intentionally NOT gated by eol. A server can be both EOL and ON.
        public static void GetDeviceState(Server srv,
            out bool broken, out bool on, out bool eol, out int eolTime)
        {
            broken  = srv.isBroken;
            on      = !broken && srv.isOn;
            eolTime = srv.eolTime;
            eol     = !broken && eolTime <= 0;
        }

        public static void GetDeviceState(NetworkSwitch sw,
            out bool broken, out bool on, out bool eol, out int eolTime)
        {
            broken  = sw.isBroken;
            on      = !broken && sw.isOn;
            eolTime = sw.eolTime;
            eol     = !broken && eolTime <= 0;
        }

        public static Button BuildFilterChip(Transform parent, string label, Color color, System.Action onClick)
        {
            var go = new GameObject("Chip_" + label);
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.color = new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 0.9f);

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = 26f;
            le.minWidth = 60f;

            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            var chipPad = new RectOffset();
            chipPad.left = 8; chipPad.right = 8; chipPad.top = 2; chipPad.bottom = 2;
            hl.padding = chipPad;

            // Dot
            var dotGo = new GameObject("Dot");
            dotGo.transform.SetParent(go.transform, false);
            var dotImg = dotGo.AddComponent<Image>();
            dotImg.color = color;
            var dotLE = dotGo.AddComponent<LayoutElement>();
            dotLE.preferredWidth = 8f;
            dotLE.preferredHeight = 8f;

            // Label
            var lblGo = new GameObject("Lbl");
            lblGo.transform.SetParent(go.transform, false);
            var lblRT = lblGo.AddComponent<RectTransform>();
            var tmp = lblGo.AddComponent<Il2CppTMPro.TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 10f;
            tmp.color = color;
            tmp.alignment = Il2CppTMPro.TextAlignmentOptions.MidlineLeft;
            var lblLE = lblGo.AddComponent<LayoutElement>();
            lblLE.preferredHeight = 22f;

            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = new ColorBlock();
            cb.normalColor = img.color;
            cb.highlightedColor = new Color(color.r * 0.5f, color.g * 0.5f, color.b * 0.5f, 1f);
            cb.pressedColor = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, 1f);
            cb.selectedColor = img.color;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            if (onClick != null)
                btn.onClick.AddListener(new System.Action(onClick));

            return btn;
        }

        public static Image BuildStatusDot(Transform parent, Color color, float size = 8f)
        {
            var go = new GameObject("StatusDot");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = color;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = size;
            le.preferredHeight = size;
            return img;
        }

        /// <summary>Build a scrollable view container with viewport, content VLG, and CSF.</summary>
        public static Transform BuildScrollView(GameObject root, out ScrollRect scrollRect)
        {
            scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(root.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = Vector2.zero;
            var vl = content.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.spacing = 2f;
            var pad = new RectOffset();
            pad.left = 8; pad.right = 8; pad.top = 8; pad.bottom = 8;
            vl.padding = pad;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            return content.transform;
        }
    }
}
