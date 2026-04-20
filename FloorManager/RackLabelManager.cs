using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FloorManager
{
    public static class RackLabelManager
    {
        private static readonly Dictionary<int, GameObject> _labels = new Dictionary<int, GameObject>();
        private static TMP_FontAsset _cachedFont;

        public static void RefreshAllLabels()
        {
            // Clean up existing labels
            foreach (var kvp in _labels)
            {
                if (kvp.Value != null)
                    Object.Destroy(kvp.Value);
            }
            _labels.Clear();

            // Grab font from an existing TMP in the scene
            if (_cachedFont == null)
            {
                var existingTMP = Object.FindObjectOfType<TextMeshProUGUI>();
                if (existingTMP != null)
                    _cachedFont = existingTMP.font;
            }

            if (_cachedFont == null)
            {
                MelonLogger.Warning("[DCIM] No TMP font found — skipping rack labels");
                return;
            }

            var rackInfos = SearchEngine.BuildRackGrid();

            int created = 0;
            for (int i = 0; i < rackInfos.Count; i++)
            {
                var ri = rackInfos[i];
                if (ri.Rack == null) continue;

                int rackId = ri.Rack.GetInstanceID();
                if (_labels.ContainsKey(rackId)) continue;

                var labelGo = CreateWorldLabel(ri.Rack.transform, ri.Label);
                if (labelGo != null)
                {
                    _labels[rackId] = labelGo;
                    created++;
                }
            }

        }

        private static GameObject CreateWorldLabel(Transform rackTransform, string text)
        {
            var canvasGo = new GameObject($"RackLabel_{text}");
            canvasGo.transform.SetParent(rackTransform, false);

            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 100;

            var canvasScaler = canvasGo.AddComponent<CanvasScaler>();
            canvasScaler.dynamicPixelsPerUnit = 100f;

            // Label: 100x30 UI units * 0.003 = 0.3m x 0.09m (~30cm x 9cm)
            var canvasRT = canvasGo.GetComponent<RectTransform>();
            canvasRT.sizeDelta = new Vector2(100f, 30f);
            canvasRT.localScale = Vector3.one * 0.002f;

            // Top-right corner of rack frame (X negative = right when rack rotated 180°)
            canvasRT.localPosition = new Vector3(-0.33f, 2.20f, 0.40f);
            canvasRT.localRotation = Quaternion.Euler(0f, 180f, 0f); // face outward from rack front

            // Text only — no background
            var textGo = new GameObject("Text");
            textGo.transform.SetParent(canvasGo.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.font = _cachedFont;
            tmp.fontSize = 24f;
            tmp.color = Color.cyan;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;

            return canvasGo;
        }
    }
}
