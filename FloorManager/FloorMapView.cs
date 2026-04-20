using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public static class FloorMapView
    {
        private static GameObject _root;
        private static RectTransform _contentRT;
        private static readonly List<GameObject> _rackSquares = new List<GameObject>();
        private static readonly List<GameObject> _customerButtons = new List<GameObject>();
        private static float _rackSizePixels = 40f;

        // Filter state
        private static readonly HashSet<int> _activeStatusFilters = new HashSet<int>();
        private static int _customerFilter = -1;

        // Customer border tracking (custId → border GO child of customer square)
        private static readonly Dictionary<int, GameObject> _customerBorders = new Dictionary<int, GameObject>();

        // Live color refresh tracking
        private static readonly Dictionary<int, Image> _rackBorderImages = new Dictionary<int, Image>();
        private static readonly Dictionary<int, Image> _customerHealthImages = new Dictionary<int, Image>();
        private static object _liveRefreshCoroutine = null;

        // Multi-select state
        private static bool _selectModeActive = false;
        private static readonly HashSet<int> _selectedRackIds = new HashSet<int>();
        private static readonly Dictionary<int, Rack> _rackIdToRack = new Dictionary<int, Rack>();
        private static readonly Dictionary<int, GameObject> _selectionOverlays = new Dictionary<int, GameObject>();
        // Quick-select (aisle / row) state — rebuilt each Refresh
        private static readonly Dictionary<int, List<int>> _aisleRackIds = new Dictionary<int, List<int>>();
        private static readonly Dictionary<int, List<int>> _rowRackIds = new Dictionary<int, List<int>>();
        // Empty mount selection state — rebuilt each Refresh
        private static readonly HashSet<int> _selectedMountIds = new HashSet<int>();
        private static readonly Dictionary<int, RackMount> _mountIdToMount = new Dictionary<int, RackMount>();
        private static readonly Dictionary<int, GameObject> _mountSelectionOverlays = new Dictionary<int, GameObject>();
        private static readonly Dictionary<int, List<int>> _rowMountIds = new Dictionary<int, List<int>>();
        private static readonly Dictionary<int, List<int>> _aisleMountIds = new Dictionary<int, List<int>>();
        private static readonly List<GameObject> _rowLabelTexts   = new List<GameObject>();
        private static readonly List<GameObject> _rowCheckboxGos  = new List<GameObject>();
        private static readonly List<GameObject> _aisleLabelTexts = new List<GameObject>();
        private static readonly List<GameObject> _aisleCheckboxGos = new List<GameObject>();
        private static readonly Dictionary<int, Image> _rowCheckboxFills   = new Dictionary<int, Image>();
        private static readonly Dictionary<int, Image> _aisleCheckboxFills = new Dictionary<int, Image>();

        // Per-rack data (built during Refresh, used by ApplyFilters without rebuild)
        private static readonly Dictionary<int, CanvasGroup> _rackCanvasGroups = new Dictionary<int, CanvasGroup>();
        private static readonly Dictionary<int, int> _rackStatusLevels = new Dictionary<int, int>();
        private static readonly Dictionary<int, List<int>> _rackCustomers = new Dictionary<int, List<int>>();
        private static readonly List<int> _rackIdOrder = new List<int>(); // ordered for iteration

        // UI refs
        private static TextMeshProUGUI _statsText;
        private static GameObject _actionBar;
        private static TextMeshProUGUI _selectionCountLbl;
        private static TextMeshProUGUI _selectModeBtnLbl;
        private static Button _buyEmptyBtn;
        private static TextMeshProUGUI _buyEmptyBtnLbl;
        private static GameObject _customerAssignPanel;
        private static TextMeshProUGUI _assignSubtitleLbl;
        private static Transform _assignContentTransform;
        private static readonly List<GameObject> _assignPopupRows = new List<GameObject>();
        private static readonly List<Image> _filterBtnImages = new List<Image>();

        // ── Per-rack data entry ──────────────────────────────────────
        private class RackEntry
        {
            public int StatusLevel;     // 0=no devices, 1=healthy, 3=EOL (expired or within auto-repair threshold), 4=broken
            public int DeviceCount;
            public int TotalOccupiedU;  // sum of device.sizeInU
            public readonly List<int> CustomerIds = new List<int>();
        }

        // ── Build ────────────────────────────────────────────────────

        public static void Build(GameObject root)
        {
            _root = root;
            BuildScrollArea();          // viewport first (lowest draw order)
            BuildStatsStrip();          // fixed panels on top
            BuildFilterBar();
            BuildActionBar();
            BuildCustomerAssignPanel();
            RackDiagramPanel.Build(root);
        }

        private static void BuildStatsStrip()
        {
            var strip = new GameObject("StatsStrip");
            strip.transform.SetParent(_root.transform, false);
            var rt = strip.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, 24f);
            strip.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.07f, 1f);

            var textGo = new GameObject("StatsText");
            textGo.transform.SetParent(strip.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(8f, 0f);
            textRT.offsetMax = new Vector2(-8f, 0f);
            _statsText = textGo.AddComponent<TextMeshProUGUI>();
            _statsText.text = "Racks: 0  Devices: 0";
            _statsText.fontSize = 10f;
            _statsText.color = new Color(0.75f, 0.75f, 0.75f);
            _statsText.alignment = TextAlignmentOptions.MidlineLeft;
            _statsText.richText = true;
        }

        private static void BuildFilterBar()
        {
            var bar = new GameObject("FilterBar");
            bar.transform.SetParent(_root.transform, false);
            var barRT = bar.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 1f);
            barRT.anchorMax = new Vector2(1f, 1f);
            barRT.pivot = new Vector2(0.5f, 1f);
            barRT.anchoredPosition = new Vector2(0f, -24f);
            barRT.sizeDelta = new Vector2(0f, 28f);
            bar.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f, 1f);

            var hl = bar.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = false;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 4f;
            var pad = new RectOffset();
            pad.left = 8; pad.right = 8; pad.top = 1; pad.bottom = 1;
            hl.padding = pad;

            // Chip values: Broken=4, EOL=3, Healthy=1, Empty=0
            var statusColors = new Color[] { UIHelper.StatusRed, UIHelper.StatusYellow, UIHelper.StatusGreen, UIHelper.StatusGray };
            var statusLabels = new string[] { "Broken", "EOL", "Healthy", "Empty" };
            var statusLevels = new int[] { 4, 3, 1, 0 };

            _filterBtnImages.Clear();
            for (int fi = 0; fi < 4; fi++)
            {
                int capturedLevel = statusLevels[fi];
                Color capturedColor = statusColors[fi];
                var chipBtn = UIHelper.BuildFilterChip(bar.transform, statusLabels[fi], capturedColor, null);
                Image capturedChipImg = chipBtn.GetComponent<Image>();
                Button capturedChipBtn = chipBtn;
                _filterBtnImages.Add(capturedChipImg);

                chipBtn.onClick.AddListener(new System.Action(() =>
                {
                    bool isActive;
                    if (_activeStatusFilters.Contains(capturedLevel))
                    {
                        _activeStatusFilters.Remove(capturedLevel);
                        isActive = false;
                    }
                    else
                    {
                        _activeStatusFilters.Add(capturedLevel);
                        isActive = true;
                    }
                    Color normalCol = isActive
                        ? new Color(capturedColor.r * 0.55f, capturedColor.g * 0.55f, capturedColor.b * 0.55f, 1f)
                        : new Color(capturedColor.r * 0.3f, capturedColor.g * 0.3f, capturedColor.b * 0.3f, 0.9f);
                    capturedChipImg.color = normalCol;
                    var cb2 = capturedChipBtn.colors;
                    cb2.normalColor = normalCol;
                    capturedChipBtn.colors = cb2;
                    ApplyFilters();
                }));
            }

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bar.transform, false);
            spacer.AddComponent<RectTransform>();
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Select Mode toggle
            var selectBtn = UIHelper.BuildButton(bar.transform, "Select", 90f);
            _selectModeBtnLbl = selectBtn.GetComponentInChildren<TextMeshProUGUI>();
            selectBtn.onClick.AddListener(new System.Action(ToggleSelectMode));
        }

        private static void BuildScrollArea()
        {
            var scrollRect = _root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(_root.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.offsetMin = new Vector2(0f, 40f);   // leave room for action bar
            vpRT.offsetMax = new Vector2(0f, -52f);  // leave room for stats (24) + filter (28)
            var vpImg = viewport.AddComponent<Image>();
            vpImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);
            viewport.AddComponent<Mask>().showMaskGraphic = true;
            scrollRect.viewport = vpRT;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            _contentRT = content.AddComponent<RectTransform>();
            _contentRT.anchorMin = new Vector2(0f, 1f);
            _contentRT.anchorMax = new Vector2(1f, 1f);
            _contentRT.pivot = new Vector2(0f, 1f);
            _contentRT.sizeDelta = new Vector2(0f, 600f);
            scrollRect.content = _contentRT;
        }

        private static void BuildActionBar()
        {
            _actionBar = new GameObject("ActionBar");
            _actionBar.transform.SetParent(_root.transform, false);
            var barRT = _actionBar.AddComponent<RectTransform>();
            barRT.anchorMin = new Vector2(0f, 0f);
            barRT.anchorMax = new Vector2(1f, 0f);
            barRT.pivot = new Vector2(0.5f, 0f);
            barRT.anchoredPosition = Vector2.zero;
            barRT.sizeDelta = new Vector2(0f, 40f);
            _actionBar.AddComponent<Image>().color = new Color(0.07f, 0.07f, 0.09f, 1f);

            var hl = _actionBar.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = false;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 6f;
            var pad = new RectOffset();
            pad.left = 8; pad.right = 8; pad.top = 5; pad.bottom = 5;
            hl.padding = pad;

            // Count label
            var countGo = new GameObject("CountLbl");
            countGo.transform.SetParent(_actionBar.transform, false);
            _selectionCountLbl = countGo.AddComponent<TextMeshProUGUI>();
            _selectionCountLbl.text = "0 racks";
            _selectionCountLbl.fontSize = 11f;
            _selectionCountLbl.color = Color.white;
            _selectionCountLbl.alignment = TextAlignmentOptions.MidlineLeft;
            var countLE = countGo.AddComponent<LayoutElement>();
            countLE.preferredWidth = 80f;
            countLE.preferredHeight = 30f;

            // Dispatch Broken → open AM screen
            var dispatchBtn = UIHelper.BuildButton(_actionBar.transform, "Dispatch Broken", 110f);
            dispatchBtn.onClick.AddListener(new System.Action(() =>
            {
                if (FloorManagerMod.DCIMScreen != null)
                    FloorManagerMod.DCIMScreen.SetActive(false);
                var cs = FloorManagerMod.ComputerShopRef;
                if (cs != null) cs.ButtonAssetManagementScreen();
            }));

            // View Devices
            var viewBtn = UIHelper.BuildButton(_actionBar.transform, "View Devices", 90f);
            viewBtn.onClick.AddListener(new System.Action(() =>
            {
                var selectedRacks = new List<Rack>();
                foreach (int rid in _selectedRackIds)
                    if (_rackIdToRack.ContainsKey(rid))
                        selectedRacks.Add(_rackIdToRack[rid]);
                FloorMapApp.OpenMultiRackDevices(selectedRacks);
            }));

            // Assign Customer
            var assignBtn = UIHelper.BuildButton(_actionBar.transform, "Assign Customer", 110f);
            assignBtn.onClick.AddListener(new System.Action(ShowCustomerAssignPanel));

            // Power ON
            var powerOnBtn = UIHelper.BuildButton(_actionBar.transform, "Power ON", 76f);
            ReusableFunctions.ChangeButtonNormalColor(powerOnBtn, new Color(0.08f, 0.24f, 0.10f, 1f));
            powerOnBtn.onClick.AddListener(new System.Action(() => BulkPower(true)));

            // Power OFF
            var powerOffBtn = UIHelper.BuildButton(_actionBar.transform, "Power OFF", 76f);
            ReusableFunctions.ChangeButtonNormalColor(powerOffBtn, new Color(0.28f, 0.07f, 0.07f, 1f));
            powerOffBtn.onClick.AddListener(new System.Action(() => BulkPower(false)));

            // Recolor selected racks
            var recolorBtn = UIHelper.BuildButton(_actionBar.transform, "Recolor", 68f);
            ReusableFunctions.ChangeButtonNormalColor(recolorBtn, new Color(0.12f, 0.22f, 0.38f, 1f));
            recolorBtn.onClick.AddListener(new System.Action(() =>
            {
                var selectedRacks = new List<Rack>();
                foreach (int rid in _selectedRackIds)
                    if (_rackIdToRack.ContainsKey(rid))
                        selectedRacks.Add(_rackIdToRack[rid]);
                FloorMapApp.ShowMassRecolorPopup(selectedRacks);
            }));

            // Buy Slots (empty mounts)
            _buyEmptyBtn = UIHelper.BuildButton(_actionBar.transform, "Buy Slots (0)", 92f);
            _buyEmptyBtnLbl = _buyEmptyBtn.GetComponentInChildren<TextMeshProUGUI>();
            ReusableFunctions.ChangeButtonNormalColor(_buyEmptyBtn, new Color(0.10f, 0.18f, 0.28f, 1f));
            _buyEmptyBtn.onClick.AddListener(new System.Action(BuySelectedSlots));
            _buyEmptyBtn.gameObject.SetActive(false);

            // Spacer
            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(_actionBar.transform, false);
            spacer.AddComponent<RectTransform>();
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // Clear
            var clearBtn = UIHelper.BuildButton(_actionBar.transform, "Clear", 60f);
            clearBtn.onClick.AddListener(new System.Action(ClearSelection));

            _actionBar.SetActive(false);
        }

        private static void BuildCustomerAssignPanel()
        {
            _customerAssignPanel = new GameObject("CustomerAssignPanel");
            _customerAssignPanel.transform.SetParent(_root.transform, false);
            var overlayRT = _customerAssignPanel.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            _customerAssignPanel.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_customerAssignPanel.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.1f, 0.08f);
            panelRT.anchorMax = new Vector2(0.9f, 0.92f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 1f);

            var panelVL = panel.AddComponent<VerticalLayoutGroup>();
            panelVL.childControlWidth = true;
            panelVL.childControlHeight = true;
            panelVL.childForceExpandWidth = true;
            panelVL.childForceExpandHeight = false;
            panelVL.spacing = 4f;
            var panelPad = new RectOffset();
            panelPad.left = 12; panelPad.right = 12; panelPad.top = 12; panelPad.bottom = 12;
            panelVL.padding = panelPad;

            // Title row
            var titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(panel.transform, false);
            var titleHL = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleHL.childControlWidth = true;
            titleHL.childControlHeight = true;
            titleHL.childForceExpandWidth = false;
            titleHL.childForceExpandHeight = false;
            titleHL.spacing = 8f;
            var titleRowLE = titleRow.AddComponent<LayoutElement>();
            titleRowLE.preferredHeight = 30f;

            var titleLbl = UIHelper.BuildLabel(titleRow.transform, "Assign Customer to Unassigned Servers", 280f);
            titleLbl.fontSize = 13f;
            titleLbl.fontStyle = FontStyles.Bold;
            titleLbl.color = Color.white;
            titleLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var cancelBtn = UIHelper.BuildButton(titleRow.transform, "Cancel", 70f);
            cancelBtn.onClick.AddListener(new System.Action(() => { _customerAssignPanel.SetActive(false); }));

            // Subtitle
            var subtitleGo = new GameObject("Subtitle");
            subtitleGo.transform.SetParent(panel.transform, false);
            _assignSubtitleLbl = subtitleGo.AddComponent<TextMeshProUGUI>();
            _assignSubtitleLbl.text = "Servers with no customer in selected racks: 0";
            _assignSubtitleLbl.fontSize = 10f;
            _assignSubtitleLbl.color = new Color(0.65f, 0.65f, 0.65f);
            var subLE = subtitleGo.AddComponent<LayoutElement>();
            subLE.preferredHeight = 18f;

            // Scrollable customer list
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            scrollArea.AddComponent<LayoutElement>().flexibleHeight = 1f;

            ScrollRect assignScroll;
            _assignContentTransform = UIHelper.BuildScrollView(scrollArea, out assignScroll);

            _customerAssignPanel.SetActive(false);
        }

        // ── Refresh ──────────────────────────────────────────────────

        public static void Refresh()
        {
            // Stop any running live refresh before rebuilding
            if (_liveRefreshCoroutine != null)
            {
                MelonCoroutines.Stop(_liveRefreshCoroutine);
                _liveRefreshCoroutine = null;
            }

            RackDiagramPanel.Close();

            for (int i = 0; i < _rackSquares.Count; i++)
                if (_rackSquares[i] != null) Object.Destroy(_rackSquares[i]);
            _rackSquares.Clear();

            for (int i = 0; i < _customerButtons.Count; i++)
                if (_customerButtons[i] != null) Object.Destroy(_customerButtons[i]);
            _customerButtons.Clear();
            _customerBorders.Clear();

            // Clear selection overlays
            foreach (var kvp in _selectionOverlays)
                if (kvp.Value != null) Object.Destroy(kvp.Value);
            _selectionOverlays.Clear();
            _selectedRackIds.Clear();
            _rackIdToRack.Clear();

            // Clear mount selection
            foreach (var kvp in _mountSelectionOverlays)
                if (kvp.Value != null) Object.Destroy(kvp.Value);
            _mountSelectionOverlays.Clear();
            _selectedMountIds.Clear();
            _mountIdToMount.Clear();
            _rowMountIds.Clear();
            _aisleMountIds.Clear();

            // Clear per-rack state
            _rackCanvasGroups.Clear();
            _rackStatusLevels.Clear();
            _rackCustomers.Clear();
            _rackIdOrder.Clear();
            _rackBorderImages.Clear();
            _customerHealthImages.Clear();

            // Reset select mode UI
            if (_actionBar != null) _actionBar.SetActive(false);
            if (_selectModeBtnLbl != null) _selectModeBtnLbl.text = "Select";
            _selectModeActive = false;

            var installedRacks = SearchEngine.BuildRackGrid();

            var rackMounts = Object.FindObjectsOfType<RackMount>();
            var installedMountIds = new HashSet<int>();
            for (int i = 0; i < installedRacks.Count; i++)
                if (installedRacks[i].RackMount != null)
                    installedMountIds.Add(installedRacks[i].RackMount.GetInstanceID());

            float rackMinX = float.MaxValue, rackMaxX = float.MinValue;
            float rackMinZ = float.MaxValue, rackMaxZ = float.MinValue;
            for (int i = 0; i < installedRacks.Count; i++)
            {
                if (installedRacks[i].WorldX < rackMinX) rackMinX = installedRacks[i].WorldX;
                if (installedRacks[i].WorldX > rackMaxX) rackMaxX = installedRacks[i].WorldX;
                if (installedRacks[i].WorldZ < rackMinZ) rackMinZ = installedRacks[i].WorldZ;
                if (installedRacks[i].WorldZ > rackMaxZ) rackMaxZ = installedRacks[i].WorldZ;
            }

            var emptyMounts = new List<EmptyMountData>();
            for (int i = 0; i < rackMounts.Length; i++)
            {
                var rm = rackMounts[i];
                if (rm.isRackInstantiated) continue;
                if (installedMountIds.Contains(rm.GetInstanceID())) continue;
                var pos = rm.transform.position;
                const float EXPAND = 2.5f;
                if (installedRacks.Count > 0 &&
                    (pos.x < rackMinX - EXPAND || pos.x > rackMaxX + EXPAND ||
                     pos.z < rackMinZ - EXPAND || pos.z > rackMaxZ + EXPAND))
                    continue;
                emptyMounts.Add(new EmptyMountData { RackMount = rm, WorldX = pos.x, WorldZ = pos.z });
            }

            Dictionary<int, int> custHealthMap;
            var rackDataMap = BuildAllRackData(out custHealthMap);

            if (installedRacks.Count == 0 && emptyMounts.Count == 0) return;

            var distinctX = new List<float>();
            var distinctZ = new List<float>();
            for (int i = 0; i < installedRacks.Count; i++)
            {
                if (!SearchEngine.ContainsApprox(distinctX, installedRacks[i].WorldX, 0.3f)) distinctX.Add(installedRacks[i].WorldX);
                if (!SearchEngine.ContainsApprox(distinctZ, installedRacks[i].WorldZ, 0.3f)) distinctZ.Add(installedRacks[i].WorldZ);
            }
            for (int i = 0; i < emptyMounts.Count; i++)
            {
                if (!SearchEngine.ContainsApprox(distinctX, emptyMounts[i].WorldX, 0.3f)) distinctX.Add(emptyMounts[i].WorldX);
                if (!SearchEngine.ContainsApprox(distinctZ, emptyMounts[i].WorldZ, 0.3f)) distinctZ.Add(emptyMounts[i].WorldZ);
            }
            distinctX.Sort();
            distinctX.Reverse();
            distinctZ.Sort();

            int totalCols = distinctX.Count;
            int totalRows = distinctZ.Count;

            const float RACK_SIZE       = 40f;
            const float RACK_GAP        = 4f;
            const float AISLE_GAP       = 16f;
            const float PADDING         = 30f;
            const float AISLE_THRESHOLD = 1.5f;
            const float ROW_LABEL_W     = 28f;

            float vpW = _root.GetComponent<RectTransform>().rect.width;
            if (vpW < 50f) vpW = 400f;

            int numAisles = 0;
            for (int i = 1; i < distinctX.Count; i++)
                if (Mathf.Abs(distinctX[i] - distinctX[i - 1]) > AISLE_THRESHOLD) numAisles++;

            // Map each column index → aisle group index (0=A, 1=B, ...)
            var colIdxToAisleIdx = new int[distinctX.Count];
            int aisleGroup = 0;
            for (int i = 1; i < distinctX.Count; i++)
            {
                if (Mathf.Abs(distinctX[i] - distinctX[i - 1]) > AISLE_THRESHOLD) aisleGroup++;
                colIdxToAisleIdx[i] = aisleGroup;
            }

            _aisleRackIds.Clear();
            _rowRackIds.Clear();
            _rowLabelTexts.Clear();
            _rowCheckboxGos.Clear();
            _aisleLabelTexts.Clear();
            _aisleCheckboxGos.Clear();
            _rowCheckboxFills.Clear();
            _aisleCheckboxFills.Clear();

            float gapTotal = distinctX.Count > 1
                ? (distinctX.Count - 1 - numAisles) * RACK_GAP + numAisles * AISLE_GAP
                : 0f;
            float usableW = vpW - ROW_LABEL_W - PADDING;
            float rackSize = distinctX.Count > 0
                ? Mathf.Clamp((usableW - gapTotal) / distinctX.Count, 14f, RACK_SIZE)
                : RACK_SIZE;
            _rackSizePixels = rackSize;

            float customerRowHeight = rackSize + RACK_GAP + 18f;

            var screenXPositions = new float[distinctX.Count];
            screenXPositions[0] = ROW_LABEL_W;
            for (int i = 1; i < distinctX.Count; i++)
            {
                float worldGap = Mathf.Abs(distinctX[i] - distinctX[i - 1]);
                float gap = worldGap > AISLE_THRESHOLD ? AISLE_GAP : RACK_GAP;
                screenXPositions[i] = screenXPositions[i - 1] + rackSize + gap;
            }

            var screenYPositions = new float[distinctZ.Count];
            screenYPositions[0] = PADDING + customerRowHeight;
            for (int i = 1; i < distinctZ.Count; i++)
            {
                float worldGap = distinctZ[i] - distinctZ[i - 1];
                float gap = worldGap > AISLE_THRESHOLD ? AISLE_GAP : RACK_GAP;
                screenYPositions[i] = screenYPositions[i - 1] + rackSize + gap;
            }

            BuildCustomerSquares(installedRacks, screenXPositions, custHealthMap, rackSize);

            float contentH = screenYPositions[distinctZ.Count - 1] + rackSize + PADDING;
            _contentRT.sizeDelta = new Vector2(0f, contentH);

            // Row labels (text when normal, checkbox when select mode)
            for (int r = 0; r < distinctZ.Count; r++)
            {
                int capturedR = r;

                // Container
                var containerGo = new GameObject($"RowLabel_{r}");
                containerGo.transform.SetParent(_contentRT.transform, false);
                var containerRT = containerGo.AddComponent<RectTransform>();
                containerRT.anchorMin = new Vector2(0f, 1f);
                containerRT.anchorMax = new Vector2(0f, 1f);
                containerRT.pivot = new Vector2(0f, 1f);
                containerRT.anchoredPosition = new Vector2(2f, -screenYPositions[r] - rackSize * 0.3f);
                containerRT.sizeDelta = new Vector2(24f, 14f);
                _rackSquares.Add(containerGo);

                // Text child
                var textGo = new GameObject("Label");
                textGo.transform.SetParent(containerGo.transform, false);
                var textRT = textGo.AddComponent<RectTransform>();
                textRT.anchorMin = Vector2.zero;
                textRT.anchorMax = Vector2.one;
                textRT.offsetMin = Vector2.zero;
                textRT.offsetMax = Vector2.zero;
                var tmp = textGo.AddComponent<TextMeshProUGUI>();
                tmp.text = $"R{r + 1}";
                tmp.fontSize = 8f;
                tmp.color = new Color(0.55f, 0.55f, 0.60f);
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
                textGo.SetActive(!_selectModeActive);
                _rowLabelTexts.Add(textGo);

                // Checkbox child
                var cbGo = new GameObject("Checkbox");
                cbGo.transform.SetParent(containerGo.transform, false);
                var cbRT = cbGo.AddComponent<RectTransform>();
                cbRT.anchorMin = new Vector2(0.5f, 0.5f);
                cbRT.anchorMax = new Vector2(0.5f, 0.5f);
                cbRT.pivot = new Vector2(0.5f, 0.5f);
                cbRT.anchoredPosition = Vector2.zero;
                cbRT.sizeDelta = new Vector2(10f, 10f);
                var cbBorderImg = cbGo.AddComponent<Image>();
                cbBorderImg.color = new Color(0.45f, 0.45f, 0.50f, 1f);

                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(cbGo.transform, false);
                var fillRT = fillGo.AddComponent<RectTransform>();
                fillRT.anchorMin = Vector2.zero;
                fillRT.anchorMax = Vector2.one;
                fillRT.offsetMin = new Vector2(2f, 2f);
                fillRT.offsetMax = new Vector2(-2f, -2f);
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);
                fillImg.raycastTarget = false;
                _rowCheckboxFills[capturedR] = fillImg;

                var cbBtn = containerGo.AddComponent<Button>();
                cbBtn.targetGraphic = cbBorderImg;
                var cbNav = new Navigation();
                cbNav.mode = Navigation.Mode.None;
                cbBtn.navigation = cbNav;
                cbBtn.onClick.AddListener(new System.Action(() =>
                {
                    if (_selectModeActive)
                    {
                        var rList = _rowRackIds.ContainsKey(capturedR) ? _rowRackIds[capturedR] : null;
                        var mList = _rowMountIds.ContainsKey(capturedR) ? _rowMountIds[capturedR] : null;
                        SelectGroup(rList, mList);
                    }
                }));

                cbGo.SetActive(_selectModeActive);
                _rowCheckboxGos.Add(cbGo);
            }

            // Installed racks
            for (int idx = 0; idx < installedRacks.Count; idx++)
            {
                var rd = installedRacks[idx];
                int colIdx = SearchEngine.FindApproxIndex(distinctX, rd.WorldX, 0.3f);
                int rowIdx = SearchEngine.FindApproxIndex(distinctZ, rd.WorldZ, 0.3f);
                float sx = screenXPositions[colIdx];
                float sy = screenYPositions[rowIdx];

                int rackId = rd.Rack.GetInstanceID();
                RackEntry entry = rackDataMap.ContainsKey(rackId) ? rackDataMap[rackId] : null;
                int statusLevel = entry != null ? entry.StatusLevel : 0;
                Color statusColor = StatusLevelToColor(statusLevel);

                // Track per-rack state for filtering
                _rackIdOrder.Add(rackId);
                _rackStatusLevels[rackId] = statusLevel;

                // Track aisle/row membership for quick-select
                int aisleIdx = colIdx < colIdxToAisleIdx.Length ? colIdxToAisleIdx[colIdx] : 0;
                if (!_aisleRackIds.ContainsKey(aisleIdx)) _aisleRackIds[aisleIdx] = new List<int>();
                _aisleRackIds[aisleIdx].Add(rackId);
                if (!_rowRackIds.ContainsKey(rowIdx)) _rowRackIds[rowIdx] = new List<int>();
                _rowRackIds[rowIdx].Add(rackId);
                _rackCustomers[rackId] = entry != null ? new List<int>(entry.CustomerIds) : new List<int>();
                _rackIdToRack[rackId] = rd.Rack;

                var rackGo = new GameObject($"Rack_{colIdx}_{rowIdx}");
                rackGo.transform.SetParent(_contentRT.transform, false);
                var rackRT = rackGo.AddComponent<RectTransform>();
                rackRT.anchorMin = new Vector2(0f, 1f);
                rackRT.anchorMax = new Vector2(0f, 1f);
                rackRT.pivot = new Vector2(0f, 1f);
                rackRT.anchoredPosition = new Vector2(sx, -sy);
                rackRT.sizeDelta = new Vector2(rackSize, rackSize);

                var rackBorderImg = rackGo.AddComponent<Image>();
                rackBorderImg.color = statusColor;
                _rackBorderImages[rackId] = rackBorderImg;

                // CanvasGroup for dim system
                var cg = rackGo.AddComponent<CanvasGroup>();
                _rackCanvasGroups[rackId] = cg;

                // Dark inner background
                var innerGo = new GameObject("Inner");
                innerGo.transform.SetParent(rackGo.transform, false);
                var innerRT = innerGo.AddComponent<RectTransform>();
                innerRT.anchorMin = Vector2.zero;
                innerRT.anchorMax = Vector2.one;
                innerRT.offsetMin = new Vector2(2f, 2f);
                innerRT.offsetMax = new Vector2(-2f, -2f);
                var innerImg = innerGo.AddComponent<Image>();
                innerImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

                // Utilization fill bar at bottom
                if (entry != null && entry.TotalOccupiedU > 0 && rd.Rack.positions != null && rd.Rack.positions.Length > 0)
                {
                    float fillRatio = Mathf.Clamp01((float)entry.TotalOccupiedU / rd.Rack.positions.Length);
                    var fillGo = new GameObject("UtilFill");
                    fillGo.transform.SetParent(rackGo.transform, false);
                    var fillRT = fillGo.AddComponent<RectTransform>();
                    fillRT.anchorMin = new Vector2(0f, 0f);
                    fillRT.anchorMax = new Vector2(fillRatio, 0f);
                    fillRT.pivot = new Vector2(0f, 0f);
                    fillRT.anchoredPosition = Vector2.zero;
                    fillRT.sizeDelta = new Vector2(0f, 4f);
                    var fillImg = fillGo.AddComponent<Image>();
                    fillImg.color = new Color(0.65f, 0.65f, 0.65f, 0.9f);
                    fillImg.raycastTarget = false;
                }

                // Rack position label
                if (rackSize >= 16f)
                {
                    float vpad = Mathf.Max(1f, rackSize * 0.13f);
                    var numGo = new GameObject("Num");
                    numGo.transform.SetParent(rackGo.transform, false);
                    var numRT = numGo.AddComponent<RectTransform>();
                    numRT.anchorMin = Vector2.zero;
                    numRT.anchorMax = Vector2.one;
                    numRT.offsetMin = new Vector2(1f, vpad);
                    numRT.offsetMax = new Vector2(-1f, -vpad);
                    var numTmp = numGo.AddComponent<TextMeshProUGUI>();
                    numTmp.text = $"{rd.RowNum}/{rd.PosNum}";
                    numTmp.fontSize = Mathf.Max(5f, rackSize * 0.22f);
                    numTmp.color = new Color(0.6f, 0.6f, 0.6f, 0.8f);
                    numTmp.alignment = TextAlignmentOptions.Center;
                    numTmp.raycastTarget = false;
                }

                // Device count
                if (entry != null && entry.DeviceCount > 0)
                {
                    var countGo = new GameObject("Count");
                    countGo.transform.SetParent(rackGo.transform, false);
                    var countRT = countGo.AddComponent<RectTransform>();
                    countRT.anchorMin = Vector2.zero;
                    countRT.anchorMax = Vector2.one;
                    countRT.offsetMin = new Vector2(2f, 2f);
                    countRT.offsetMax = new Vector2(-3f, -2f);
                    var countTmp = countGo.AddComponent<TextMeshProUGUI>();
                    countTmp.text = entry.DeviceCount.ToString();
                    countTmp.fontSize = Mathf.Max(5f, rackSize * 0.18f);
                    countTmp.color = new Color(0.85f, 0.85f, 0.85f, 0.75f);
                    countTmp.alignment = TextAlignmentOptions.BottomRight;
                    countTmp.raycastTarget = false;
                }

                // Selection overlay (sibling of rack square, not child → bypasses CanvasGroup)
                var overlayGo = new GameObject($"SelOverlay_{rackId}");
                overlayGo.transform.SetParent(_contentRT.transform, false);
                var overlayRT2 = overlayGo.AddComponent<RectTransform>();
                overlayRT2.anchorMin = new Vector2(0f, 1f);
                overlayRT2.anchorMax = new Vector2(0f, 1f);
                overlayRT2.pivot = new Vector2(0f, 1f);
                overlayRT2.anchoredPosition = new Vector2(sx, -sy);
                overlayRT2.sizeDelta = new Vector2(rackSize, rackSize);
                var overlayImg = overlayGo.AddComponent<Image>();
                overlayImg.color = new Color(1f, 0.55f, 0f, 0.35f);
                overlayImg.raycastTarget = false;
                var overlayOutline = overlayGo.AddComponent<Outline>();
                overlayOutline.effectColor = new Color(1f, 0.65f, 0f, 1f);
                overlayOutline.effectDistance = new Vector2(2f, -2f);
                overlayGo.SetActive(false);
                _selectionOverlays[rackId] = overlayGo;
                _rackSquares.Add(overlayGo);

                // Button
                var btn = rackGo.AddComponent<Button>();
                btn.targetGraphic = innerImg;
                var cb = new ColorBlock();
                cb.normalColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cb.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
                cb.pressedColor = new Color(0.05f, 0.05f, 0.07f, 1f);
                cb.selectedColor = new Color(0.10f, 0.10f, 0.12f, 1f);
                cb.colorMultiplier = 1f;
                cb.fadeDuration = 0.1f;
                btn.colors = cb;
                var nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                btn.navigation = nav;

                int capturedRackId = rackId;
                Rack capturedRack = rd.Rack;
                int capturedRowNum = rd.RowNum;
                int capturedPosNum = rd.PosNum;
                btn.onClick.AddListener(new System.Action(() =>
                {
                    if (_selectModeActive)
                    {
                        if (_selectedRackIds.Contains(capturedRackId))
                        {
                            _selectedRackIds.Remove(capturedRackId);
                            if (_selectionOverlays.ContainsKey(capturedRackId) && _selectionOverlays[capturedRackId] != null)
                                _selectionOverlays[capturedRackId].SetActive(false);
                        }
                        else
                        {
                            _selectedRackIds.Add(capturedRackId);
                            if (_selectionOverlays.ContainsKey(capturedRackId) && _selectionOverlays[capturedRackId] != null)
                                _selectionOverlays[capturedRackId].SetActive(true);
                        }
                        UpdateActionBar();
                    }
                    else
                    {
                        RackDiagramPanel.Open(capturedRack, capturedRowNum, capturedPosNum);
                    }
                }));

                _rackSquares.Add(rackGo);
            }

            // Empty mounts
            for (int i = 0; i < emptyMounts.Count; i++)
            {
                var em = emptyMounts[i];
                int colIdx = SearchEngine.FindApproxIndex(distinctX, em.WorldX, 0.3f);
                int rowIdx = SearchEngine.FindApproxIndex(distinctZ, em.WorldZ, 0.3f);
                float sx = screenXPositions[colIdx];
                float sy = screenYPositions[rowIdx];

                var emptyGo = new GameObject($"Empty_{i}");
                emptyGo.transform.SetParent(_contentRT.transform, false);
                var emptyRT = emptyGo.AddComponent<RectTransform>();
                emptyRT.anchorMin = new Vector2(0f, 1f);
                emptyRT.anchorMax = new Vector2(0f, 1f);
                emptyRT.pivot = new Vector2(0f, 1f);
                emptyRT.anchoredPosition = new Vector2(sx, -sy);
                emptyRT.sizeDelta = new Vector2(rackSize, rackSize);

                var borderImg = emptyGo.AddComponent<Image>();
                borderImg.color = new Color(0.28f, 0.28f, 0.32f, 0.5f);

                var emptyInner = new GameObject("Inner");
                emptyInner.transform.SetParent(emptyGo.transform, false);
                var emptyInnerRT = emptyInner.AddComponent<RectTransform>();
                emptyInnerRT.anchorMin = Vector2.zero;
                emptyInnerRT.anchorMax = Vector2.one;
                emptyInnerRT.offsetMin = new Vector2(2f, 2f);
                emptyInnerRT.offsetMax = new Vector2(-2f, -2f);
                var emptyInnerImg = emptyInner.AddComponent<Image>();
                emptyInnerImg.color = new Color(0.06f, 0.06f, 0.08f, 0.4f);

                var plusGo = new GameObject("Plus");
                plusGo.transform.SetParent(emptyGo.transform, false);
                var plusRT = plusGo.AddComponent<RectTransform>();
                plusRT.anchorMin = Vector2.zero;
                plusRT.anchorMax = Vector2.one;
                plusRT.offsetMin = new Vector2(4f, 4f);
                plusRT.offsetMax = new Vector2(-4f, -4f);
                var plusTmp = plusGo.AddComponent<TextMeshProUGUI>();
                plusTmp.text = "+";
                plusTmp.fontSize = Mathf.Max(8f, rackSize * 0.35f);
                plusTmp.color = new Color(0.5f, 0.5f, 0.55f, 0.8f);
                plusTmp.alignment = TextAlignmentOptions.Center;
                plusTmp.raycastTarget = false;

                var emptyBtn = emptyGo.AddComponent<Button>();
                emptyBtn.targetGraphic = emptyInnerImg;
                var emptyCb = new ColorBlock();
                emptyCb.normalColor = new Color(0.06f, 0.06f, 0.08f, 0.4f);
                emptyCb.highlightedColor = new Color(0.18f, 0.18f, 0.22f, 0.7f);
                emptyCb.pressedColor = new Color(0.03f, 0.03f, 0.05f, 0.4f);
                emptyCb.selectedColor = new Color(0.06f, 0.06f, 0.08f, 0.4f);
                emptyCb.colorMultiplier = 1f;
                emptyCb.fadeDuration = 0.1f;
                emptyBtn.colors = emptyCb;
                var emptyNav = new Navigation();
                emptyNav.mode = Navigation.Mode.None;
                emptyBtn.navigation = emptyNav;

                RackMount capturedMount = em.RackMount;
                int mountId = capturedMount.GetInstanceID();
                _mountIdToMount[mountId] = capturedMount;

                // Track empty mount in row/aisle groups
                int emAisleIdx = colIdx < colIdxToAisleIdx.Length ? colIdxToAisleIdx[colIdx] : 0;
                if (!_rowMountIds.ContainsKey(rowIdx)) _rowMountIds[rowIdx] = new List<int>();
                _rowMountIds[rowIdx].Add(mountId);
                if (!_aisleMountIds.ContainsKey(emAisleIdx)) _aisleMountIds[emAisleIdx] = new List<int>();
                _aisleMountIds[emAisleIdx].Add(mountId);

                int capturedMountId = mountId;
                emptyBtn.onClick.AddListener(new System.Action(() =>
                {
                    if (_selectModeActive)
                    {
                        if (_selectedMountIds.Contains(capturedMountId))
                        {
                            _selectedMountIds.Remove(capturedMountId);
                            if (_mountSelectionOverlays.ContainsKey(capturedMountId) && _mountSelectionOverlays[capturedMountId] != null)
                                _mountSelectionOverlays[capturedMountId].SetActive(false);
                        }
                        else
                        {
                            _selectedMountIds.Add(capturedMountId);
                            if (_mountSelectionOverlays.ContainsKey(capturedMountId) && _mountSelectionOverlays[capturedMountId] != null)
                                _mountSelectionOverlays[capturedMountId].SetActive(true);
                        }
                        UpdateActionBar();
                        UpdateGroupCheckboxes();
                    }
                    else
                    {
                        FloorMapApp.ShowBuyRackPopup(capturedMount);
                    }
                }));

                // Selection overlay for empty mount (blue tint to distinguish from orange rack overlays)
                var mountOverlayGo = new GameObject($"MtSelOverlay_{mountId}");
                mountOverlayGo.transform.SetParent(_contentRT.transform, false);
                var mountOverlayRT = mountOverlayGo.AddComponent<RectTransform>();
                mountOverlayRT.anchorMin = new Vector2(0f, 1f);
                mountOverlayRT.anchorMax = new Vector2(0f, 1f);
                mountOverlayRT.pivot = new Vector2(0f, 1f);
                mountOverlayRT.anchoredPosition = new Vector2(sx, -sy);
                mountOverlayRT.sizeDelta = new Vector2(rackSize, rackSize);
                var mountOverlayImg = mountOverlayGo.AddComponent<Image>();
                mountOverlayImg.color = new Color(0.25f, 0.55f, 1f, 0.35f);
                mountOverlayImg.raycastTarget = false;
                var mountOutline = mountOverlayGo.AddComponent<Outline>();
                mountOutline.effectColor = new Color(0.4f, 0.7f, 1f, 1f);
                mountOutline.effectDistance = new Vector2(2f, -2f);
                mountOverlayGo.SetActive(false);
                _mountSelectionOverlays[mountId] = mountOverlayGo;
                _rackSquares.Add(mountOverlayGo);

                _rackSquares.Add(emptyGo);
            }

            // Aisle labels added last so they render on top of all rack/empty tiles
            BuildAisleLabels(distinctX, screenXPositions, screenYPositions[0], rackSize, AISLE_THRESHOLD);

            // Restore customer filter border if one was active
            if (_customerFilter >= 0 && _customerBorders.ContainsKey(_customerFilter))
                _customerBorders[_customerFilter].SetActive(true);

            // Update stats strip
            int totalDevices = 0;
            int brokenRacks = 0;
            int eolRacks = 0;
            foreach (var kvp in rackDataMap)
            {
                totalDevices += kvp.Value.DeviceCount;
                if (kvp.Value.StatusLevel == 4) brokenRacks++;
                else if (kvp.Value.StatusLevel == 3 || kvp.Value.StatusLevel == 2) eolRacks++;
            }

            string statsMsg = $"Racks: {installedRacks.Count}  Devices: {totalDevices}";
            if (brokenRacks > 0) statsMsg += $"  <color=#E63333>{brokenRacks} broken</color>";
            if (eolRacks > 0) statsMsg += $"  <color=#FFB31A>{eolRacks} EOL</color>";
            if (_statsText != null) _statsText.text = statsMsg;

            ApplyFilters();

            _liveRefreshCoroutine = MelonCoroutines.Start(LiveRefreshCoroutine());
        }

        private static void SelectByGroup(List<int> rackIds)
        {
            SelectGroup(rackIds, null);
        }

        private static void SelectGroup(List<int> rackIds, List<int> mountIds)
        {
            // If ALL racks + mounts in the group are already selected → deselect; else select all
            bool allSelected = true;
            if (rackIds != null)
                for (int i = 0; i < rackIds.Count; i++)
                    if (!_selectedRackIds.Contains(rackIds[i])) { allSelected = false; break; }
            if (allSelected && mountIds != null)
                for (int i = 0; i < mountIds.Count; i++)
                    if (!_selectedMountIds.Contains(mountIds[i])) { allSelected = false; break; }

            if (rackIds != null)
            {
                for (int i = 0; i < rackIds.Count; i++)
                {
                    int rid = rackIds[i];
                    if (allSelected)
                    {
                        _selectedRackIds.Remove(rid);
                        if (_selectionOverlays.ContainsKey(rid) && _selectionOverlays[rid] != null)
                            _selectionOverlays[rid].SetActive(false);
                    }
                    else
                    {
                        _selectedRackIds.Add(rid);
                        if (_selectionOverlays.ContainsKey(rid) && _selectionOverlays[rid] != null)
                            _selectionOverlays[rid].SetActive(true);
                    }
                }
            }

            if (mountIds != null)
            {
                for (int i = 0; i < mountIds.Count; i++)
                {
                    int mid = mountIds[i];
                    if (allSelected)
                    {
                        _selectedMountIds.Remove(mid);
                        if (_mountSelectionOverlays.ContainsKey(mid) && _mountSelectionOverlays[mid] != null)
                            _mountSelectionOverlays[mid].SetActive(false);
                    }
                    else
                    {
                        _selectedMountIds.Add(mid);
                        if (_mountSelectionOverlays.ContainsKey(mid) && _mountSelectionOverlays[mid] != null)
                            _mountSelectionOverlays[mid].SetActive(true);
                    }
                }
            }

            UpdateActionBar();
            UpdateGroupCheckboxes();

            // HUD feedback for group select/deselect
            int totalR = _selectedRackIds.Count, totalM = _selectedMountIds.Count;
            string selMsg = totalR > 0 && totalM > 0 ? $"Selected: {totalR} racks, {totalM} slots"
                          : totalR > 0               ? $"Selected: {totalR} rack(s)"
                          : totalM > 0               ? $"Selected: {totalM} slot(s)"
                          :                            "Selection cleared";
            if (StaticUIElements.instance != null)
                StaticUIElements.instance.AddMeesageInField(selMsg);
        }

        // ── Group checkbox state ──────────────────────────────────────

        private static void UpdateGroupCheckboxes()
        {
            foreach (var kvp in _rowCheckboxFills)
            {
                int rowIdx = kvp.Key;
                var fillImg = kvp.Value;
                if (fillImg == null) continue;
                bool allSel = true, anySel = false;
                if (_rowRackIds.ContainsKey(rowIdx))
                {
                    var ids = _rowRackIds[rowIdx];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (_selectedRackIds.Contains(ids[i])) anySel = true;
                        else allSel = false;
                    }
                }
                if (_rowMountIds.ContainsKey(rowIdx))
                {
                    var ids = _rowMountIds[rowIdx];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (_selectedMountIds.Contains(ids[i])) anySel = true;
                        else allSel = false;
                    }
                }
                fillImg.color = (allSel && anySel)
                    ? new Color(0.10f, 0.65f, 0.25f, 1f)
                    : anySel
                        ? new Color(0.55f, 0.35f, 0.05f, 1f)
                        : new Color(0.10f, 0.10f, 0.12f, 1f);
            }
            foreach (var kvp in _aisleCheckboxFills)
            {
                int aisleIdx = kvp.Key;
                var fillImg = kvp.Value;
                if (fillImg == null) continue;
                bool allSel = true, anySel = false;
                if (_aisleRackIds.ContainsKey(aisleIdx))
                {
                    var ids = _aisleRackIds[aisleIdx];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (_selectedRackIds.Contains(ids[i])) anySel = true;
                        else allSel = false;
                    }
                }
                if (_aisleMountIds.ContainsKey(aisleIdx))
                {
                    var ids = _aisleMountIds[aisleIdx];
                    for (int i = 0; i < ids.Count; i++)
                    {
                        if (_selectedMountIds.Contains(ids[i])) anySel = true;
                        else allSel = false;
                    }
                }
                fillImg.color = (allSel && anySel)
                    ? new Color(0.10f, 0.65f, 0.25f, 1f)
                    : anySel
                        ? new Color(0.55f, 0.35f, 0.05f, 1f)
                        : new Color(0.10f, 0.10f, 0.12f, 1f);
            }
        }

        // ── Aisle labels ─────────────────────────────────────────────

        private static void BuildAisleLabels(List<float> distinctX, float[] screenXPositions,
            float firstRackY, float rackSize, float aisleThreshold)
        {
            bool hasMultipleGroups = false;
            for (int i = 1; i < distinctX.Count; i++)
            {
                if (Mathf.Abs(distinctX[i] - distinctX[i - 1]) > aisleThreshold)
                { hasMultipleGroups = true; break; }
            }
            if (!hasMultipleGroups) return;

            float labelY = firstRackY - 17f;
            char letter = 'A';
            int groupStart = 0;
            int aisleIdx = 0;

            for (int i = 1; i <= distinctX.Count; i++)
            {
                bool isEnd = (i == distinctX.Count);
                bool isNewGroup = !isEnd && Mathf.Abs(distinctX[i] - distinctX[i - 1]) > aisleThreshold;

                if (isEnd || isNewGroup)
                {
                    int groupEnd = i - 1;
                    float startX = screenXPositions[groupStart];
                    float endX   = screenXPositions[groupEnd] + rackSize;
                    float centerX = (startX + endX) / 2f;
                    int capturedAisleIdx = aisleIdx;

                    // Container
                    var containerGo = new GameObject($"AisleLabel_{letter}");
                    containerGo.transform.SetParent(_contentRT.transform, false);
                    var containerRT = containerGo.AddComponent<RectTransform>();
                    containerRT.anchorMin = new Vector2(0f, 1f);
                    containerRT.anchorMax = new Vector2(0f, 1f);
                    containerRT.pivot = new Vector2(0.5f, 1f);
                    containerRT.anchoredPosition = new Vector2(centerX, -labelY);
                    containerRT.sizeDelta = new Vector2(80f, 12f);
                    _rackSquares.Add(containerGo);

                    // Text child
                    var textGo = new GameObject("Label");
                    textGo.transform.SetParent(containerGo.transform, false);
                    var textRT = textGo.AddComponent<RectTransform>();
                    textRT.anchorMin = Vector2.zero;
                    textRT.anchorMax = Vector2.one;
                    textRT.offsetMin = Vector2.zero;
                    textRT.offsetMax = Vector2.zero;
                    var tmp = textGo.AddComponent<TextMeshProUGUI>();
                    tmp.text = $"Aisle {letter}";
                    tmp.fontSize = 8f;
                    tmp.color = new Color(0.42f, 0.42f, 0.48f);
                    tmp.alignment = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    textGo.SetActive(!_selectModeActive);
                    _aisleLabelTexts.Add(textGo);

                    // Checkbox child
                    var cbGo = new GameObject("Checkbox");
                    cbGo.transform.SetParent(containerGo.transform, false);
                    var cbRT = cbGo.AddComponent<RectTransform>();
                    cbRT.anchorMin = new Vector2(0.5f, 0.5f);
                    cbRT.anchorMax = new Vector2(0.5f, 0.5f);
                    cbRT.pivot = new Vector2(0.5f, 0.5f);
                    cbRT.anchoredPosition = Vector2.zero;
                    cbRT.sizeDelta = new Vector2(10f, 10f);
                    var cbBorderImg = cbGo.AddComponent<Image>();
                    cbBorderImg.color = new Color(0.45f, 0.45f, 0.50f, 1f);

                    var fillGo = new GameObject("Fill");
                    fillGo.transform.SetParent(cbGo.transform, false);
                    var fillRT = fillGo.AddComponent<RectTransform>();
                    fillRT.anchorMin = Vector2.zero;
                    fillRT.anchorMax = Vector2.one;
                    fillRT.offsetMin = new Vector2(2f, 2f);
                    fillRT.offsetMax = new Vector2(-2f, -2f);
                    var fillImg = fillGo.AddComponent<Image>();
                    fillImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);
                    fillImg.raycastTarget = false;
                    _aisleCheckboxFills[capturedAisleIdx] = fillImg;

                    var cbBtn = containerGo.AddComponent<Button>();
                    cbBtn.targetGraphic = cbBorderImg;
                    var cbNav = new Navigation();
                    cbNav.mode = Navigation.Mode.None;
                    cbBtn.navigation = cbNav;
                    cbBtn.onClick.AddListener(new System.Action(() =>
                    {
                        if (_selectModeActive)
                        {
                            var rList = _aisleRackIds.ContainsKey(capturedAisleIdx) ? _aisleRackIds[capturedAisleIdx] : null;
                            var mList = _aisleMountIds.ContainsKey(capturedAisleIdx) ? _aisleMountIds[capturedAisleIdx] : null;
                            SelectGroup(rList, mList);
                        }
                    }));

                    cbGo.SetActive(_selectModeActive);
                    _aisleCheckboxGos.Add(cbGo);

                    letter++;
                    groupStart = i;
                    aisleIdx++;
                }
            }
        }

        // ── Data scan ────────────────────────────────────────────────

        private static Dictionary<int, RackEntry> BuildAllRackData(out Dictionary<int, int> custHealthOut)
        {
            var rackMap    = new Dictionary<int, RackEntry>();
            var custHealth = new Dictionary<int, int>();

            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var rack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (rack == null) continue;
                int rackId = rack.GetInstanceID();

                if (!rackMap.ContainsKey(rackId)) rackMap[rackId] = new RackEntry();
                var entry = rackMap[rackId];
                entry.DeviceCount++;
                entry.TotalOccupiedU += srv.sizeInU;

                int eolThreshold = GetEolThreshold();
                // eolTime counts DOWN. Negative = past EOL deadline (still running, not yet repaired).
                // "Repair EOL Xh+" means auto-repair fires at eolTime < -threshold.
                // Warn (yellow) when server has been expired longer than the auto-repair window.
                // When no auto-repair (threshold=0), warn as soon as server expires.
                bool srvEolFlag = eolThreshold > 0
                    ? srv.eolTime < -eolThreshold   // expired past auto-repair window
                    : srv.eolTime < 0;              // no auto-repair: warn all expired
                if (srv.isBroken)
                    entry.StatusLevel = 4;
                else if (srvEolFlag && entry.StatusLevel < 3)
                    entry.StatusLevel = 3;
                else if (entry.StatusLevel < 1)
                    entry.StatusLevel = 1;

                int custId = srv.GetCustomerID();
                if (custId >= 0)
                {
                    bool found = false;
                    for (int c = 0; c < entry.CustomerIds.Count; c++)
                        if (entry.CustomerIds[c] == custId) { found = true; break; }
                    if (!found) entry.CustomerIds.Add(custId);

                    int cur = custHealth.ContainsKey(custId) ? custHealth[custId] : 0;
                    if (srv.isBroken && cur < 4)
                        custHealth[custId] = 4;
                    else if (srvEolFlag && cur < 2)
                        custHealth[custId] = 2;
                    else if (cur < 1)
                        custHealth[custId] = 1;
                }
            }

            var allSwitches = Object.FindObjectsOfType<NetworkSwitch>();
            for (int i = 0; i < allSwitches.Length; i++)
            {
                var sw = allSwitches[i];
                if (sw.currentRackPosition == null) continue;
                var rack = sw.currentRackPosition.GetComponentInParent<Rack>();
                if (rack == null) continue;
                int rackId = rack.GetInstanceID();

                if (!rackMap.ContainsKey(rackId)) rackMap[rackId] = new RackEntry();
                var entry = rackMap[rackId];
                entry.DeviceCount++;
                entry.TotalOccupiedU += sw.sizeInU;

                int swEolThreshold = GetEolThreshold();
                bool swEolFlag = swEolThreshold > 0
                    ? sw.eolTime < -swEolThreshold
                    : sw.eolTime < 0;
                if (sw.isBroken)
                    entry.StatusLevel = 4;
                else if (swEolFlag && entry.StatusLevel < 3)
                    entry.StatusLevel = 3;
                else if (entry.StatusLevel < 1)
                    entry.StatusLevel = 1;
            }

            custHealthOut = custHealth;
            return rackMap;
        }

        // ── Filter + Selection ───────────────────────────────────────

        private static void ApplyFilters()
        {
            bool anyFilter = _activeStatusFilters.Count > 0 || _customerFilter >= 0;
            for (int i = 0; i < _rackIdOrder.Count; i++)
            {
                int rackId = _rackIdOrder[i];
                if (!_rackCanvasGroups.ContainsKey(rackId)) continue;

                bool pass = true;
                if (_activeStatusFilters.Count > 0)
                {
                    int level = _rackStatusLevels.ContainsKey(rackId) ? _rackStatusLevels[rackId] : 0;
                    bool chipMatch = _activeStatusFilters.Contains(level);
                    pass = chipMatch;
                }
                if (pass && _customerFilter >= 0)
                {
                    var custList = _rackCustomers.ContainsKey(rackId) ? _rackCustomers[rackId] : null;
                    pass = custList != null && custList.Contains(_customerFilter);
                }

                _rackCanvasGroups[rackId].alpha = (anyFilter && !pass) ? 0.2f : 1f;
            }
        }

        private static void UpdateActionBar()
        {
            bool show = _selectModeActive && (_selectedRackIds.Count > 0 || _selectedMountIds.Count > 0);
            if (_actionBar != null) _actionBar.SetActive(show);

            if (_selectionCountLbl != null)
            {
                int r = _selectedRackIds.Count, m = _selectedMountIds.Count;
                if (r > 0 && m > 0) _selectionCountLbl.text = $"{r}r/{m}s";
                else if (r > 0)     _selectionCountLbl.text = $"{r} racks";
                else                _selectionCountLbl.text = $"{m} slots";
            }

            if (_selectModeBtnLbl != null && _selectModeActive)
                _selectModeBtnLbl.text = $"Selecting ({_selectedRackIds.Count + _selectedMountIds.Count})";

            if (_buyEmptyBtn != null)
            {
                bool showBuy = _selectModeActive && _selectedMountIds.Count > 0;
                _buyEmptyBtn.gameObject.SetActive(showBuy);
                if (showBuy && _buyEmptyBtnLbl != null)
                    _buyEmptyBtnLbl.text = $"Buy Slots ({_selectedMountIds.Count})";
            }
        }

        private static void ToggleSelectMode()
        {
            _selectModeActive = !_selectModeActive;
            if (_selectModeActive)
            {
                foreach (var kvp in _selectionOverlays)
                    if (kvp.Value != null) kvp.Value.SetActive(false);
                _selectedRackIds.Clear();
                foreach (var kvp in _mountSelectionOverlays)
                    if (kvp.Value != null) kvp.Value.SetActive(false);
                _selectedMountIds.Clear();
                if (_selectModeBtnLbl != null) _selectModeBtnLbl.text = "Selecting (0)";
            }
            else
            {
                ClearSelection();
                if (_selectModeBtnLbl != null) _selectModeBtnLbl.text = "Select";
                if (_actionBar != null) _actionBar.SetActive(false);
            }
            // Swap row labels ↔ checkboxes
            for (int i = 0; i < _rowLabelTexts.Count; i++)
                if (_rowLabelTexts[i] != null) _rowLabelTexts[i].SetActive(!_selectModeActive);
            for (int i = 0; i < _rowCheckboxGos.Count; i++)
                if (_rowCheckboxGos[i] != null) _rowCheckboxGos[i].SetActive(_selectModeActive);
            // Swap aisle labels ↔ checkboxes
            for (int i = 0; i < _aisleLabelTexts.Count; i++)
                if (_aisleLabelTexts[i] != null) _aisleLabelTexts[i].SetActive(!_selectModeActive);
            for (int i = 0; i < _aisleCheckboxGos.Count; i++)
                if (_aisleCheckboxGos[i] != null) _aisleCheckboxGos[i].SetActive(_selectModeActive);
            if (_selectModeActive) UpdateGroupCheckboxes();
        }

        private static void ClearSelection()
        {
            foreach (int rackId in _selectedRackIds)
                if (_selectionOverlays.ContainsKey(rackId) && _selectionOverlays[rackId] != null)
                    _selectionOverlays[rackId].SetActive(false);
            _selectedRackIds.Clear();
            foreach (int mid in _selectedMountIds)
                if (_mountSelectionOverlays.ContainsKey(mid) && _mountSelectionOverlays[mid] != null)
                    _mountSelectionOverlays[mid].SetActive(false);
            _selectedMountIds.Clear();
            UpdateActionBar();
        }

        private static void BuySelectedSlots()
        {
            var mounts = new List<RackMount>();
            foreach (int mid in _selectedMountIds)
                if (_mountIdToMount.ContainsKey(mid))
                    mounts.Add(_mountIdToMount[mid]);
            if (mounts.Count > 0)
                FloorMapApp.ShowMassBuyPopup(mounts);
        }

        private static void BulkPower(bool on)
        {
            // Build set of selected Rack instance IDs
            var selectedRackIds = new HashSet<int>();
            foreach (int rid in _selectedRackIds)
                if (_rackIdToRack.ContainsKey(rid))
                    selectedRackIds.Add(_rackIdToRack[rid].GetInstanceID());

            if (selectedRackIds.Count == 0) return;

            int count = 0;

            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.isBroken || srv.currentRackPosition == null) continue;
                var rack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (rack == null || !selectedRackIds.Contains(rack.GetInstanceID())) continue;
                if (on && !srv.isOn)  { srv.PowerButton(); count++; }
                else if (!on && srv.isOn) { srv.PowerButton(); count++; }
            }

            var allSwitches = Object.FindObjectsOfType<NetworkSwitch>();
            for (int i = 0; i < allSwitches.Length; i++)
            {
                var sw = allSwitches[i];
                if (sw.currentRackPosition == null) continue;
                var rack = sw.currentRackPosition.GetComponentInParent<Rack>();
                if (rack == null || !selectedRackIds.Contains(rack.GetInstanceID())) continue;
                if (on && !sw.isOn)  { sw.PowerButton(); count++; }
                else if (!on && sw.isOn) { sw.PowerButton(); count++; }
            }

            if (count > 0)
            {
                string verb = on ? "Powered on" : "Powered off";
                StaticUIElements.instance.AddMeesageInField($"{verb} {count} device(s)");
            }
        }

        // ── Customer Assign Panel ─────────────────────────────────────

        private static void ShowCustomerAssignPanel()
        {
            // Count unassigned servers in selected racks
            int unassigned = 0;
            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var rack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (rack == null) continue;
                if (_selectedRackIds.Contains(rack.GetInstanceID()) && srv.GetCustomerID() < 0)
                    unassigned++;
            }

            if (_assignSubtitleLbl != null)
                _assignSubtitleLbl.text = $"Servers with no customer in selected racks: {unassigned}";

            // Rebuild customer rows
            for (int i = 0; i < _assignPopupRows.Count; i++)
                if (_assignPopupRows[i] != null) Object.Destroy(_assignPopupRows[i]);
            _assignPopupRows.Clear();

            var mgm = MainGameManager.instance;
            if (mgm != null)
            {
                var custBases = mgm.customerBases;
                for (int i = 0; i < custBases.Length; i++)
                {
                    var cb = custBases[i];
                    int custId = cb.customerID;
                    if (custId < 0) continue;

                    var custItem = mgm.GetCustomerItemByID(custId);
                    string custName = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}";

                    var row = new GameObject($"AssignRow_{custId}");
                    row.transform.SetParent(_assignContentTransform, false);

                    var rowImg = row.AddComponent<Image>();
                    rowImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);

                    var rowHL = row.AddComponent<HorizontalLayoutGroup>();
                    rowHL.childControlWidth = true;
                    rowHL.childControlHeight = true;
                    rowHL.childForceExpandWidth = false;
                    rowHL.childForceExpandHeight = false;
                    rowHL.spacing = 10f;
                    var rowPad = new RectOffset();
                    rowPad.left = 12; rowPad.right = 12; rowPad.top = 6; rowPad.bottom = 6;
                    rowHL.padding = rowPad;
                    row.AddComponent<LayoutElement>().preferredHeight = 38f;

                    if (custItem != null && custItem.logo != null)
                    {
                        var logoGo = new GameObject("Logo");
                        logoGo.transform.SetParent(row.transform, false);
                        var logoImg = logoGo.AddComponent<Image>();
                        logoImg.sprite = custItem.logo;
                        logoImg.color = Color.white;
                        logoImg.raycastTarget = false;
                        var logoLE = logoGo.AddComponent<LayoutElement>();
                        logoLE.preferredWidth = 26f;
                        logoLE.preferredHeight = 26f;
                    }

                    var nameLbl = UIHelper.BuildLabel(row.transform, custName, 250f);
                    nameLbl.fontSize = 12f;
                    nameLbl.color = Color.white;
                    nameLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

                    var btn = row.AddComponent<Button>();
                    btn.targetGraphic = rowImg;
                    var btnCb = new ColorBlock();
                    btnCb.normalColor = new Color(0.14f, 0.14f, 0.16f, 1f);
                    btnCb.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
                    btnCb.pressedColor = new Color(0.10f, 0.28f, 0.10f, 1f);
                    btnCb.selectedColor = new Color(0.14f, 0.14f, 0.16f, 1f);
                    btnCb.colorMultiplier = 1f;
                    btnCb.fadeDuration = 0.1f;
                    btn.colors = btnCb;
                    var nav = new Navigation();
                    nav.mode = Navigation.Mode.None;
                    btn.navigation = nav;

                    int capturedCustId = custId;
                    btn.onClick.AddListener(new System.Action(() =>
                    {
                        AssignCustomerToSelectedRacks(capturedCustId);
                        _customerAssignPanel.SetActive(false);
                    }));

                    _assignPopupRows.Add(row);
                }
            }

            _customerAssignPanel.SetActive(true);
        }

        private static void AssignCustomerToSelectedRacks(int custId)
        {
            int assigned = 0;
            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var rack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (rack == null) continue;
                if (!_selectedRackIds.Contains(rack.GetInstanceID())) continue;
                if (srv.GetCustomerID() >= 0) continue;
                srv.UpdateCustomer(custId);
                assigned++;
            }

            var custItem = MainGameManager.instance != null ? MainGameManager.instance.GetCustomerItemByID(custId) : null;
            string custName = custItem != null ? custItem.customerName : $"Customer {custId}";
            StaticUIElements.instance.AddMeesageInField($"Assigned to {assigned} servers → {custName}");
        }

        // ── Customer squares ──────────────────────────────────────────

        private static void BuildCustomerSquares(List<SearchEngine.RackInfo> installedRacks,
            float[] screenXPositions, Dictionary<int, int> custHealthMap, float rackSize)
        {
            float sq            = rackSize;
            const float GAP     = 4f;
            const float TOP_Y   = 30f;
            const float START_X = 28f;

            var mgm = MainGameManager.instance;
            if (mgm == null) return;

            var seenCustIds = new HashSet<int>();
            int slotIndex = 0;

            var existingIds = mgm.existingCustomerIDs;
            if (existingIds != null)
            {
                for (int i = 0; i < existingIds.Count; i++)
                {
                    int custId = existingIds[i];
                    if (custId < 0 || seenCustIds.Contains(custId)) continue;
                    seenCustIds.Add(custId);
                    var custItem = mgm.GetCustomerItemByID(custId);
                    Sprite logo = custItem != null ? custItem.logo : null;
                    string name = custItem != null ? custItem.customerName : $"#{custId}";
                    BuildCustomerSquare(custId, logo, name, START_X + slotIndex * (sq + GAP), TOP_Y, sq, custHealthMap);
                    slotIndex++;
                }
            }

            var allCustBases = Object.FindObjectsOfType<CustomerBase>();
            for (int i = 0; i < allCustBases.Length; i++)
            {
                int custId = allCustBases[i].customerID;
                if (custId < 0 || seenCustIds.Contains(custId)) continue;
                if (custId == 0 && allCustBases[i].customerItem == null) continue;
                seenCustIds.Add(custId);
                var custItem = mgm.GetCustomerItemByID(custId);
                Sprite logo = custItem != null ? custItem.logo : null;
                string name = custItem != null ? custItem.customerName : $"#{custId}";
                BuildCustomerSquare(custId, logo, name, START_X + slotIndex * (sq + GAP), TOP_Y, sq, custHealthMap);
                slotIndex++;
            }
        }

        private static void BuildCustomerSquare(int custId, Sprite logo, string name,
            float sx, float sy, float size, Dictionary<int, int> custHealthMap)
        {
            int health = custHealthMap.ContainsKey(custId) ? custHealthMap[custId] : 0;
            Color borderColor = health >= 3 ? UIHelper.StatusRed      // broken or expired timer
                              : health == 2 ? UIHelper.StatusYellow    // timer counting down
                              : health == 1 ? UIHelper.StatusGreen     // all healthy
                              : new Color(0.3f, 0.6f, 0.9f, 1f);      // no devices

            var sqGo = new GameObject($"Cust_{custId}");
            sqGo.transform.SetParent(_contentRT.transform, false);
            var sqRT = sqGo.AddComponent<RectTransform>();
            sqRT.anchorMin = new Vector2(0f, 1f);
            sqRT.anchorMax = new Vector2(0f, 1f);
            sqRT.pivot = new Vector2(0f, 1f);
            sqRT.anchoredPosition = new Vector2(sx, -sy);
            sqRT.sizeDelta = new Vector2(size, size);

            var healthImg = sqGo.AddComponent<Image>();
            healthImg.color = borderColor;
            _customerHealthImages[custId] = healthImg;

            var innerGo = new GameObject("Inner");
            innerGo.transform.SetParent(sqGo.transform, false);
            var innerRT = innerGo.AddComponent<RectTransform>();
            innerRT.anchorMin = Vector2.zero;
            innerRT.anchorMax = Vector2.one;
            innerRT.offsetMin = new Vector2(2f, 2f);
            innerRT.offsetMax = new Vector2(-2f, -2f);
            var innerImg = innerGo.AddComponent<Image>();
            innerImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            if (logo != null)
            {
                var logoGo = new GameObject("Logo");
                logoGo.transform.SetParent(sqGo.transform, false);
                var logoRT = logoGo.AddComponent<RectTransform>();
                logoRT.anchorMin = new Vector2(0.1f, 0.1f);
                logoRT.anchorMax = new Vector2(0.9f, 0.9f);
                logoRT.offsetMin = Vector2.zero;
                logoRT.offsetMax = Vector2.zero;
                var logoImg = logoGo.AddComponent<Image>();
                logoImg.sprite = logo;
                logoImg.color = Color.white;
                logoImg.raycastTarget = false;
            }
            else
            {
                var lblGo = new GameObject("Lbl");
                lblGo.transform.SetParent(sqGo.transform, false);
                var lblRT = lblGo.AddComponent<RectTransform>();
                lblRT.anchorMin = Vector2.zero;
                lblRT.anchorMax = Vector2.one;
                lblRT.offsetMin = new Vector2(2f, 2f);
                lblRT.offsetMax = new Vector2(-2f, -2f);
                var tmp = lblGo.AddComponent<TextMeshProUGUI>();
                tmp.text = name;
                tmp.fontSize = 7f;
                tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.raycastTarget = false;
            }

            // Cyan filter-active border overlay (initially hidden)
            var borderGo = new GameObject("FilterBorder");
            borderGo.transform.SetParent(sqGo.transform, false);
            var borderRT2 = borderGo.AddComponent<RectTransform>();
            borderRT2.anchorMin = Vector2.zero;
            borderRT2.anchorMax = Vector2.one;
            borderRT2.offsetMin = Vector2.zero;
            borderRT2.offsetMax = Vector2.zero;
            var borderImg2 = borderGo.AddComponent<Image>();
            borderImg2.color = new Color(0f, 0.9f, 0.9f, 0.15f);
            var borderOutline = borderGo.AddComponent<Outline>();
            borderOutline.effectColor = new Color(0f, 0.9f, 0.9f, 1f);
            borderOutline.effectDistance = new Vector2(2f, -2f);
            borderGo.SetActive(false);
            _customerBorders[custId] = borderGo;

            var btn = sqGo.AddComponent<Button>();
            btn.targetGraphic = innerImg;
            var cb = new ColorBlock();
            cb.normalColor = new Color(0.10f, 0.10f, 0.12f, 1f);
            cb.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
            cb.pressedColor = new Color(0.05f, 0.05f, 0.07f, 1f);
            cb.selectedColor = new Color(0.10f, 0.10f, 0.12f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            int capturedId = custId;
            btn.onClick.AddListener(new System.Action(() =>
            {
                if (_customerFilter == capturedId)
                {
                    // Clear filter
                    _customerFilter = -1;
                    if (_customerBorders.ContainsKey(capturedId))
                        _customerBorders[capturedId].SetActive(false);
                    ApplyFilters();
                }
                else
                {
                    // Deactivate old border
                    if (_customerFilter >= 0 && _customerBorders.ContainsKey(_customerFilter))
                        _customerBorders[_customerFilter].SetActive(false);
                    // Set new filter
                    _customerFilter = capturedId;
                    if (_customerBorders.ContainsKey(capturedId))
                        _customerBorders[capturedId].SetActive(true);
                    ApplyFilters();
                }
            }));

            _customerButtons.Add(sqGo);
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static Color StatusLevelToColor(int level)
        {
            switch (level)
            {
                case 4: return UIHelper.StatusRed;                        // broken
                case 3: return UIHelper.StatusYellow;                     // EOL (expired or within auto-repair threshold)
                case 1: return UIHelper.StatusGreen;                      // healthy
                default: return UIHelper.StatusGray;                      // empty
            }
        }

        // Returns the EOL warning threshold in seconds based on the game's auto-repair setting.
        // 0 = no threshold (Off or Repair Broken only — don't warn).
        // 2 = Repair EOL 4h+ → 14400s,  3 = Repair EOL 2h+ → 7200s.
        private static int GetEolThreshold()
        {
            try
            {
                var cc = CommandCenter.instance;
                if (cc == null) return 0;
                switch (cc.autoRepairMode)
                {
                    case 2: return FloorManagerMod.EOL_WARN_SECONDS;     // 2h = 7200
                    case 3: return FloorManagerMod.EOL_APPROACH_SECONDS; // 4h = 14400
                    default: return 0;
                }
            }
            catch { return 0; }
        }

        private static Color CustomerColor(int custId)
        {
            float hue = ((custId * 137.508f) % 360f) / 360f;
            return Color.HSVToRGB(hue, 0.60f, 0.90f);
        }

        // ── Live refresh ─────────────────────────────────────────────

        private static IEnumerator LiveRefreshCoroutine()
        {
            while (_root != null && _root.activeSelf)
            {
                yield return new WaitForSeconds(3f);
                if (_root == null || !_root.activeSelf) yield break;

                try
                {
                    Dictionary<int, int> custHealthMap;
                    var rackDataMap = BuildAllRackData(out custHealthMap);

                    // Update rack border colors in-place
                    foreach (var kvp in _rackBorderImages)
                    {
                        if (kvp.Value == null) continue;
                        int rackId = kvp.Key;
                        var entry = rackDataMap.ContainsKey(rackId) ? rackDataMap[rackId] : null;
                        int newLevel = entry != null ? entry.StatusLevel : 0;
                        kvp.Value.color = StatusLevelToColor(newLevel);
                        _rackStatusLevels[rackId] = newLevel;
                    }

                    // Update customer health border colors in-place
                    foreach (var kvp in _customerHealthImages)
                    {
                        if (kvp.Value == null) continue;
                        int custId = kvp.Key;
                        int health = custHealthMap.ContainsKey(custId) ? custHealthMap[custId] : 0;
                        kvp.Value.color = health >= 3 ? UIHelper.StatusRed
                                        : health == 2 ? UIHelper.StatusYellow
                                        : health == 1 ? UIHelper.StatusGreen
                                        : new Color(0.3f, 0.6f, 0.9f, 1f);
                    }

                    // Update stats strip
                    int totalDevices = 0, brokenRacks = 0, eolRacks = 0;
                    foreach (var kvp in rackDataMap)
                    {
                        totalDevices += kvp.Value.DeviceCount;
                        if (kvp.Value.StatusLevel == 4) brokenRacks++;
                        else if (kvp.Value.StatusLevel >= 3) eolRacks++;
                    }
                    if (_statsText != null)
                    {
                        string statsMsg = $"Racks: {_rackBorderImages.Count}  Devices: {totalDevices}";
                        if (brokenRacks > 0) statsMsg += $"  <color=#E63333>{brokenRacks} broken</color>";
                        if (eolRacks > 0) statsMsg += $"  <color=#FFB31A>{eolRacks} EOL</color>";
                        _statsText.text = statsMsg;
                    }
                }
                catch { }
            }
            _liveRefreshCoroutine = null;
        }

        private struct EmptyMountData
        {
            public RackMount RackMount;
            public float WorldX;
            public float WorldZ;
        }
    }
}
