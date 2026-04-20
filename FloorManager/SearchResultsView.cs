using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public static class SearchResultsView
    {
        private static GameObject _root;
        private static readonly List<GameObject> _elements = new List<GameObject>();

        // Filter state
        private static SearchEngine.DeviceTypeFilter _typeFilter = SearchEngine.DeviceTypeFilter.All;
        private static SearchEngine.StatusFilter _statusFilter = SearchEngine.StatusFilter.All;
        private static int _customerFilter = -1;
        private static SearchEngine.ServerColorFilter _colorFilter = SearchEngine.ServerColorFilter.All;

        // Filter button labels (to update text when filter changes)
        private static TextMeshProUGUI _typeBtnLabel;
        private static TextMeshProUGUI _statusBtnLabel;
        private static TextMeshProUGUI _customerBtnLabel;
        private static TextMeshProUGUI _colorBtnLabel;
        private static TextMeshProUGUI _countLabel;

        // Filter popup
        private static GameObject _filterPopup;
        private static Transform _filterPopupContent;
        private static readonly List<GameObject> _filterPopupRows = new List<GameObject>();

        // Multi-select for bulk power
        private static readonly List<SearchEngine.DeviceInfo> _selectedDevices = new List<SearchEngine.DeviceInfo>();
        private static readonly Dictionary<int, Image> _checkboxImages = new Dictionary<int, Image>();
        private static TextMeshProUGUI _powerOnLabel;
        private static TextMeshProUGUI _powerOffLabel;
        private static Button _powerOnBtn;
        private static Button _powerOffBtn;

        // Toolbar + results area (persistent, rebuilt on filter change)
        private static GameObject _toolbarGo;
        private static GameObject _resultsArea;
        private static Transform _resultsContent;
        private static ScrollRect _resultsScrollRect;
        private static float _savedScrollPos = 1f; // normalized vertical (1 = top)

        // Column visibility — false when result set has no servers
        private static bool _showIPCol = true;
        private static bool _showCustomerCol = true;

        // Live-refresh
        private struct LiveRow
        {
            public Server Server;
            public NetworkSwitch Switch;
            public Image DotImg;
            public TextMeshProUGUI PwrLbl;
            public TextMeshProUGUI EolLbl;
        }
        private static readonly List<LiveRow> _liveRows = new List<LiveRow>();
        private static object _refreshCoroutine;

        // Assign customer popup
        private static GameObject _assignPopupRoot;
        private static Transform _assignPopupContent;
        private static readonly List<GameObject> _assignPopupRows = new List<GameObject>();
        private static int _assignPopupSelectedCustId = -1;
        private static Image _assignPopupSelectedRowImg;
        private static Button _assignPopupConfirmBtn;
        private static TextMeshProUGUI _assignCustLabel;

        public static void Build(GameObject root)
        {
            _root = root;

            // Main layout — toolbar at top, results below
            var rt = root.GetComponent<RectTransform>();
            if (rt == null) rt = root.AddComponent<RectTransform>();

            var vl = root.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.spacing = 0f;

            // ── Filter toolbar ──────────────────────────────────────
            _toolbarGo = new GameObject("Toolbar");
            _toolbarGo.transform.SetParent(root.transform, false);
            var toolbarImg = _toolbarGo.AddComponent<Image>();
            toolbarImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);
            var toolbarHL = _toolbarGo.AddComponent<HorizontalLayoutGroup>();
            toolbarHL.childControlWidth = true;
            toolbarHL.childControlHeight = true;
            toolbarHL.childForceExpandWidth = false;
            toolbarHL.childForceExpandHeight = false;
            toolbarHL.spacing = 4f;
            var toolbarPad = new RectOffset();
            toolbarPad.left = 4; toolbarPad.right = 4; toolbarPad.top = 2; toolbarPad.bottom = 2;
            toolbarHL.padding = toolbarPad;
            var toolbarLE = _toolbarGo.AddComponent<LayoutElement>();
            toolbarLE.preferredHeight = 32f;

            // Type filter button
            var typeBtn = UIHelper.BuildButton(_toolbarGo.transform, "Type: All", 100f);
            _typeBtnLabel = typeBtn.GetComponentInChildren<TextMeshProUGUI>();
            typeBtn.onClick.AddListener(new System.Action(() => ShowTypeFilterPopup()));

            // Status filter button
            var statusBtn = UIHelper.BuildButton(_toolbarGo.transform, "Status: All", 100f);
            _statusBtnLabel = statusBtn.GetComponentInChildren<TextMeshProUGUI>();
            statusBtn.onClick.AddListener(new System.Action(() => ShowStatusFilterPopup()));

            // Customer filter button
            var custBtn = UIHelper.BuildButton(_toolbarGo.transform, "Customer: All", 120f);
            _customerBtnLabel = custBtn.GetComponentInChildren<TextMeshProUGUI>();
            custBtn.onClick.AddListener(new System.Action(() => ShowCustomerFilterPopup()));

            // Color filter button
            var colorBtn = UIHelper.BuildButton(_toolbarGo.transform, "Color: All", 100f);
            _colorBtnLabel = colorBtn.GetComponentInChildren<TextMeshProUGUI>();
            colorBtn.onClick.AddListener(new System.Action(() => ShowColorFilterPopup()));

            // ── Power toolbar row ───────────────────────────────────
            var powerRow = new GameObject("PowerRow");
            powerRow.transform.SetParent(root.transform, false);
            var powerImg = powerRow.AddComponent<Image>();
            powerImg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            var powerHL = powerRow.AddComponent<HorizontalLayoutGroup>();
            powerHL.childControlWidth = true;
            powerHL.childControlHeight = true;
            powerHL.childForceExpandWidth = false;
            powerHL.childForceExpandHeight = false;
            powerHL.spacing = 4f;
            var powerPad = new RectOffset();
            powerPad.left = 4; powerPad.right = 4; powerPad.top = 2; powerPad.bottom = 2;
            powerHL.padding = powerPad;
            var powerLE = powerRow.AddComponent<LayoutElement>();
            powerLE.preferredHeight = 26f;

            // Select All
            var selectAllBtn = UIHelper.BuildButton(powerRow.transform, "Select All", 80f);
            var selectAllLabel = selectAllBtn.GetComponentInChildren<TextMeshProUGUI>();
            selectAllBtn.onClick.AddListener(new System.Action(() =>
            {
                ToggleSelectAll();
                selectAllLabel.text = _selectedDevices.Count > 0 ? "Deselect" : "Select All";
            }));

            // Power ON
            _powerOnBtn = UIHelper.BuildButton(powerRow.transform, "Power ON", 80f);
            _powerOnLabel = _powerOnBtn.GetComponentInChildren<TextMeshProUGUI>();
            _powerOnBtn.onClick.AddListener(new System.Action(OnPowerOn));

            // Power OFF
            _powerOffBtn = UIHelper.BuildButton(powerRow.transform, "Power OFF", 80f);
            _powerOffLabel = _powerOffBtn.GetComponentInChildren<TextMeshProUGUI>();
            _powerOffBtn.onClick.AddListener(new System.Action(OnPowerOff));

            // Assign Customer
            var assignCustBtn = UIHelper.BuildButton(powerRow.transform, "Assign Customer", 110f);
            _assignCustLabel = assignCustBtn.GetComponentInChildren<TextMeshProUGUI>();
            assignCustBtn.onClick.AddListener(new System.Action(ShowAssignCustomerPopup));

            // Count label
            var countGo = new GameObject("Count");
            countGo.transform.SetParent(powerRow.transform, false);
            _countLabel = countGo.AddComponent<TextMeshProUGUI>();
            _countLabel.text = "";
            _countLabel.fontSize = 9f;
            _countLabel.color = new Color(0.5f, 0.5f, 0.5f);
            _countLabel.alignment = TextAlignmentOptions.MidlineRight;
            var countLE = countGo.AddComponent<LayoutElement>();
            countLE.flexibleWidth = 1f;
            countLE.preferredHeight = 22f;

            // ── Results scroll area ─────────────────────────────────
            _resultsArea = new GameObject("ResultsArea");
            _resultsArea.transform.SetParent(root.transform, false);
            var resultsLE = _resultsArea.AddComponent<LayoutElement>();
            resultsLE.flexibleHeight = 1f;

            ScrollRect sr;
            _resultsContent = UIHelper.BuildScrollView(_resultsArea, out sr);
            _resultsScrollRect = sr;

            // ── Filter popup (hidden overlay) ───────────────────────
            BuildFilterPopup(root);

            // ── Assign customer popup (hidden overlay) ───────────────
            BuildAssignPopup(root);
        }

        public static void SetFilters(SearchEngine.DeviceTypeFilter typeFilter, SearchEngine.StatusFilter statusFilter, int customerFilter)
        {
            _typeFilter = typeFilter;
            _statusFilter = statusFilter;
            _customerFilter = customerFilter;
            _colorFilter = SearchEngine.ServerColorFilter.All;
        }

        // Call before leaving to DeviceConfig so we can restore position on return
        public static void SaveScrollPosition()
        {
            if (_resultsScrollRect != null)
                _savedScrollPos = _resultsScrollRect.verticalNormalizedPosition;
        }

        // Return from DeviceConfig — skip repopulate, just restore scroll
        public static void RestoreView()
        {
            UpdateFilterLabels();
            if (_resultsScrollRect != null)
                _resultsScrollRect.verticalNormalizedPosition = _savedScrollPos;
        }

        public static void Populate()
        {
            StopRefresh();
            _liveRows.Clear();
            ClearResults();
            _selectedDevices.Clear();
            _checkboxImages.Clear();

            // Fresh scan so EOL times and device states are current
            SearchEngine.ScanAll();

            // Update toolbar labels
            UpdateFilterLabels();

            // Get filtered results
            var results = SearchEngine.Filter(_typeFilter, _statusFilter, _customerFilter, _colorFilter);
            int totalDevices = SearchEngine.LastDevices.Count;
            _countLabel.text = $"Showing {results.Count} of {totalDevices} devices";

            // Hide IP/Customer columns when no servers are present
            bool hasServers = false;
            for (int ri = 0; ri < results.Count; ri++)
                if (results[ri].IsServer) { hasServers = true; break; }
            _showIPCol = hasServers;
            _showCustomerCol = hasServers;

            BuildHeaderRow();

            if (results.Count == 0)
            {
                var emptyRow = new GameObject("NoResults");
                emptyRow.transform.SetParent(_resultsContent, false);
                var emptyLE = emptyRow.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 28f;
                var emptyLbl = UIHelper.BuildLabel(emptyRow.transform, "No devices match filters", 250f);
                emptyLbl.fontSize = 10f;
                emptyLbl.color = UIHelper.StatusGray;
                emptyLbl.alignment = TextAlignmentOptions.Center;
                _elements.Add(emptyRow);
                return;
            }

            for (int i = 0; i < results.Count; i++)
            {
                BuildResultRow(results[i], i);
            }

            UpdatePowerButtons();
            if (_liveRows.Count > 0)
                _refreshCoroutine = MelonCoroutines.Start(RefreshLiveRows());
        }

        // Fixed-width columns
        private const float COL_CB   = 16f;
        private const float COL_DOT  = 10f;
        private const float COL_PWR  = 30f;
        // Data columns use proportional flexibleWidth — preferred = minimum, flex = share of remaining
        private const float FLEX_TYPE = 3f;   // Type name
        private const float FLEX_IP   = 2f;   // IP address
        private const float FLEX_EOL  = 1.2f; // EOL countdown
        private const float FLEX_CUST = 4f;   // Customer name (most space)
        private const float FLEX_RACK = 0.8f; // Rack label

        private static void BuildColDivider(Transform parent)
        {
            var go = new GameObject("ColDiv");
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.22f, 0.22f, 0.26f, 1f);
            img.raycastTarget = false;
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = 1f;
            le.flexibleHeight = 1f;
            le.preferredHeight = 20f;
        }

        private static void BuildHeaderRow()
        {
            var row = new GameObject("HeaderRow");
            row.transform.SetParent(_resultsContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.08f, 0.08f, 0.10f, 1f);

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 4f;
            var pad = new RectOffset();
            pad.left = 4; pad.right = 4; pad.top = 2; pad.bottom = 2;
            hl.padding = pad;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 22f;

            // CB spacer
            var cbSpacer = new GameObject("H_CB");
            cbSpacer.transform.SetParent(row.transform, false);
            var cbSpLE = cbSpacer.AddComponent<LayoutElement>();
            cbSpLE.preferredWidth = COL_CB;
            cbSpLE.preferredHeight = COL_CB;

            // Dot spacer
            var dotSpacer = new GameObject("H_Dot");
            dotSpacer.transform.SetParent(row.transform, false);
            var dotSpLE = dotSpacer.AddComponent<LayoutElement>();
            dotSpLE.preferredWidth = COL_DOT;
            dotSpLE.preferredHeight = COL_DOT;

            // Pwr header
            var pwrHdr = UIHelper.BuildLabel(row.transform, "Pwr", COL_PWR);
            pwrHdr.fontSize = 8f;
            pwrHdr.color = new Color(0.45f, 0.45f, 0.45f);
            pwrHdr.alignment = TextAlignmentOptions.Center;

            BuildColDivider(row.transform);

            // Type header
            var typeHdr = UIHelper.BuildLabel(row.transform, "Type", 130f);
            typeHdr.fontSize = 8f;
            typeHdr.color = new Color(0.45f, 0.45f, 0.45f);
            typeHdr.alignment = TextAlignmentOptions.MidlineLeft;
            typeHdr.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_TYPE;

            // Role tag spacer — always 28px to match data rows
            var roleHdrSpacer = new GameObject("H_Role");
            roleHdrSpacer.transform.SetParent(row.transform, false);
            var roleHdrLE = roleHdrSpacer.AddComponent<LayoutElement>();
            roleHdrLE.preferredWidth = 28f;
            roleHdrLE.preferredHeight = 22f;

            if (_showIPCol)
            {
                BuildColDivider(row.transform);
                var ipHdr = UIHelper.BuildLabel(row.transform, "IP Address", 100f);
                ipHdr.fontSize = 8f;
                ipHdr.color = new Color(0.45f, 0.45f, 0.45f);
                ipHdr.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_IP;
            }

            BuildColDivider(row.transform);

            // EOL header
            var eolHdr = UIHelper.BuildLabel(row.transform, "EOL", 50f);
            eolHdr.fontSize = 8f;
            eolHdr.color = new Color(0.45f, 0.45f, 0.45f);
            eolHdr.alignment = TextAlignmentOptions.Center;
            eolHdr.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_EOL;

            if (_showCustomerCol)
            {
                BuildColDivider(row.transform);
                var custHdr = UIHelper.BuildLabel(row.transform, "Customer", 100f);
                custHdr.fontSize = 8f;
                custHdr.color = new Color(0.45f, 0.45f, 0.45f);
                custHdr.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_CUST;
            }

            BuildColDivider(row.transform);

            // Rack header
            var rackHdr = UIHelper.BuildLabel(row.transform, "Rack", 40f);
            rackHdr.fontSize = 8f;
            rackHdr.color = new Color(0.45f, 0.45f, 0.45f);
            rackHdr.alignment = TextAlignmentOptions.Center;
            rackHdr.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_RACK;

            _elements.Add(row);
        }

        private static void BuildResultRow(SearchEngine.DeviceInfo device, int index)
        {
            var row = new GameObject($"Result_{index}");
            row.transform.SetParent(_resultsContent, false);

            // Alternating row shading
            Color rowColor = index % 2 == 0
                ? new Color(0.12f, 0.12f, 0.14f, 1f)
                : new Color(0.10f, 0.10f, 0.115f, 1f);
            var rowImg = row.AddComponent<Image>();
            rowImg.color = rowColor;

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 4f;
            var rowPad = new RectOffset();
            rowPad.left = 4; rowPad.right = 4; rowPad.top = 3; rowPad.bottom = 3;
            hl.padding = rowPad;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 26f;

            // ── Checkbox (border style) — always reserves COL_CB width ──
            var cbGo = new GameObject("CB");
            cbGo.transform.SetParent(row.transform, false);
            var cbBorderImg = cbGo.AddComponent<Image>();
            cbBorderImg.color = new Color(0.35f, 0.35f, 0.38f, 1f); // border
            var cbLE = cbGo.AddComponent<LayoutElement>();
            cbLE.preferredWidth = COL_CB;
            cbLE.preferredHeight = COL_CB;

            // Inner fill (starts dark, turns green when selected)
            var cbFillGo = new GameObject("CBFill");
            cbFillGo.transform.SetParent(cbGo.transform, false);
            var cbFillRT = cbFillGo.AddComponent<RectTransform>();
            cbFillRT.anchorMin = Vector2.zero;
            cbFillRT.anchorMax = Vector2.one;
            cbFillRT.offsetMin = new Vector2(2f, 2f);
            cbFillRT.offsetMax = new Vector2(-2f, -2f);
            var cbFillImg = cbFillGo.AddComponent<Image>();
            cbFillImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            if (device.IsServer || device.IsSwitch)
            {
                var cbBtn = cbGo.AddComponent<Button>();
                cbBtn.targetGraphic = cbFillImg;
                var cbNav = new Navigation();
                cbNav.mode = Navigation.Mode.None;
                cbBtn.navigation = cbNav;

                _checkboxImages[index] = cbFillImg;

                SearchEngine.DeviceInfo capturedDevice = device;
                Image capturedFill = cbFillImg;
                cbBtn.onClick.AddListener(new System.Action(() =>
                {
                    if (_selectedDevices.Contains(capturedDevice))
                    {
                        _selectedDevices.Remove(capturedDevice);
                        capturedFill.color = new Color(0.10f, 0.10f, 0.12f, 1f);
                    }
                    else
                    {
                        _selectedDevices.Add(capturedDevice);
                        capturedFill.color = UIHelper.StatusGreen;
                    }
                    UpdatePowerButtons();
                }));
            }

            // ── Status dot ──
            Color statusColor;
            if (device.IsBroken) statusColor = UIHelper.StatusRed;
            else if (device.IsOn) statusColor = UIHelper.StatusGreen;
            else statusColor = UIHelper.StatusGray;
            var dotImg = UIHelper.BuildStatusDot(row.transform, statusColor, COL_DOT);

            // ── Power badge ──
            string pwrText;
            Color pwrColor;
            if (device.IsBroken)
            {
                pwrText = "BRK";
                pwrColor = UIHelper.StatusRed;
            }
            else if (device.IsOn)
            {
                pwrText = "ON";
                pwrColor = UIHelper.StatusGreen;
            }
            else
            {
                pwrText = "OFF";
                pwrColor = new Color(0.55f, 0.55f, 0.55f);
            }
            var pwrLbl = UIHelper.BuildLabel(row.transform, pwrText, COL_PWR);
            pwrLbl.fontSize = 8f;
            pwrLbl.color = pwrColor;
            pwrLbl.alignment = TextAlignmentOptions.Center;

            BuildColDivider(row.transform);

            // ── Type name ──
            Color typeColor;
            if (device.IsServer) typeColor = UIHelper.GetDeviceTypeColor(device.ObjName);
            else if (device.IsSwitch) typeColor = UIHelper.SwitchColor;
            else typeColor = UIHelper.PatchPanelColor;

            var nameLbl = UIHelper.BuildLabel(row.transform, device.TypeName, 130f);
            nameLbl.fontSize = 9f;
            nameLbl.color = typeColor;
            nameLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_TYPE;

            // Security role tag (FW / IDS / HP) — always 28px to keep column alignment
            {
                string srTag = "";
                Color srColor = new Color(0.45f, 0.45f, 0.45f);
#if !STRIP_HACKING
                if (device.IsSwitch && device.Switch != null)
                {
                    if (HackingSystem.IsFirewall(device.Switch))      { srTag = "FW";  srColor = UIHelper.StatusOrange; }
                    else if (HackingSystem.IsIDS(device.Switch))      { srTag = "IDS"; srColor = new Color(0.1f, 0.8f, 0.8f); }
                }
                else if (device.IsServer && device.Server != null && HackingSystem.IsHoneypot(device.Server))
                {
                    srTag = "HP"; srColor = new Color(0.7f, 0.2f, 0.9f);
                }
#endif
                var srLbl = UIHelper.BuildLabel(row.transform, srTag, 28f);
                srLbl.fontSize = 8f;
                srLbl.color = srColor;
                srLbl.fontStyle = FontStyles.Bold;
                srLbl.alignment = TextAlignmentOptions.Center;
            }

            if (_showIPCol)
            {
                BuildColDivider(row.transform);
                // ── IP ──
                var ipLbl = UIHelper.BuildLabel(row.transform, device.IP, 100f);
                ipLbl.fontSize = 9f;
                ipLbl.color = Color.white;
                ipLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_IP;
            }

            BuildColDivider(row.transform);

            // ── EOL time ──
            string eolText = "";
            Color eolColor = UIHelper.StatusYellow;
            if (device.IsBroken)
            {
                eolText = "BRK";
                eolColor = UIHelper.StatusRed;
            }
            else if (device.EolTime != 0 || device.IsEOL)
            {
                eolText = UIHelper.FormatEolTime(device.EolTime);
                eolColor = UIHelper.EolTimeColor(device.EolTime);
            }
            var eolLbl = UIHelper.BuildLabel(row.transform, eolText, 50f);
            eolLbl.fontSize = 8f;
            eolLbl.color = eolColor;
            eolLbl.alignment = TextAlignmentOptions.Center;
            eolLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_EOL;

            if (_showCustomerCol)
            {
                BuildColDivider(row.transform);
                // ── Customer ──
                var custLbl = UIHelper.BuildLabel(row.transform, device.CustomerName, 100f);
                custLbl.fontSize = 9f;
                custLbl.color = new Color(0.6f, 0.6f, 0.6f);
                custLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_CUST;
            }

            BuildColDivider(row.transform);

            // ── Rack ──
            var rackLbl = UIHelper.BuildLabel(row.transform, device.RackLabel, 40f);
            rackLbl.fontSize = 9f;
            rackLbl.color = new Color(0.5f, 0.5f, 0.5f);
            rackLbl.alignment = TextAlignmentOptions.Center;
            rackLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = FLEX_RACK;

            // Store refs for live refresh
            if (device.IsServer || device.IsSwitch)
            {
                _liveRows.Add(new LiveRow
                {
                    Server = device.Server,
                    Switch = device.Switch,
                    DotImg = dotImg,
                    PwrLbl = pwrLbl,
                    EolLbl = eolLbl
                });
            }

            // ── Row click → DeviceConfig ──
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb = new ColorBlock();
            cb.normalColor = rowColor;
            cb.highlightedColor = new Color(0.18f, 0.18f, 0.22f, 1f);
            cb.pressedColor = new Color(0.08f, 0.08f, 0.10f, 1f);
            cb.selectedColor = rowColor;
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            SearchEngine.DeviceInfo capturedDev = device;
            btn.onClick.AddListener(new System.Action(() =>
            {
                FloorMapApp.OpenDeviceFromSearch(capturedDev.Server, capturedDev.Switch, capturedDev.PatchPanel);
            }));

            _elements.Add(row);
        }

        // ── Power controls ──────────────────────────────────────────

        private static void ToggleSelectAll()
        {
            if (_selectedDevices.Count > 0)
            {
                _selectedDevices.Clear();
                foreach (var kvp in _checkboxImages)
                    kvp.Value.color = new Color(0.3f, 0.3f, 0.3f);
            }
            else
            {
                // Select all servers and switches from current results
                var results = SearchEngine.Filter(_typeFilter, _statusFilter, _customerFilter, _colorFilter);
                for (int i = 0; i < results.Count; i++)
                {
                    if (results[i].IsServer || results[i].IsSwitch)
                    {
                        _selectedDevices.Add(results[i]);
                        if (_checkboxImages.ContainsKey(i))
                            _checkboxImages[i].color = UIHelper.StatusGreen;
                    }
                }
            }
            UpdatePowerButtons();
        }

        private static void UpdatePowerButtons()
        {
            int count = _selectedDevices.Count;
            _powerOnLabel.text = count > 0 ? $"Power ON ({count})" : "Power ON";
            _powerOffLabel.text = count > 0 ? $"Power OFF ({count})" : "Power OFF";

            int serverCount = 0;
            for (int i = 0; i < _selectedDevices.Count; i++)
                if (_selectedDevices[i].IsServer) serverCount++;
            _assignCustLabel.text = serverCount > 0 ? $"Assign Customer ({serverCount})" : "Assign Customer";
        }

        private static void OnPowerOn()
        {
            int count = 0;
            for (int i = 0; i < _selectedDevices.Count; i++)
            {
                var d = _selectedDevices[i];
                if (d.Server != null && !d.Server.isOn && !d.Server.isBroken)
                {
                    d.Server.PowerButton();
                    count++;
                }
                else if (d.Switch != null && !d.Switch.isOn && !d.Switch.isBroken)
                {
                    d.Switch.PowerButton();
                    count++;
                }
            }
            if (count > 0)
                StaticUIElements.instance.AddMeesageInField($"Powered on {count} devices");
        }

        private static void OnPowerOff()
        {
            int count = 0;
            for (int i = 0; i < _selectedDevices.Count; i++)
            {
                var d = _selectedDevices[i];
                if (d.Server != null && d.Server.isOn)
                {
                    d.Server.PowerButton();
                    count++;
                }
                else if (d.Switch != null && d.Switch.isOn)
                {
                    d.Switch.PowerButton();
                    count++;
                }
            }
            if (count > 0)
                StaticUIElements.instance.AddMeesageInField($"Powered off {count} devices");
        }

        // ── Filter popup ────────────────────────────────────────────

        private static void BuildFilterPopup(GameObject root)
        {
            _filterPopup = new GameObject("FilterPopup");
            _filterPopup.transform.SetParent(root.transform, false);
            var overlayRT = _filterPopup.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            // Must ignore layout so VLG doesn't absorb the overlay
            var popupLE = _filterPopup.AddComponent<LayoutElement>();
            popupLE.ignoreLayout = true;

            var dimImg = _filterPopup.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.5f);

            // Dismiss on background click
            var dimBtn = _filterPopup.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            var dimNav = new Navigation();
            dimNav.mode = Navigation.Mode.None;
            dimBtn.navigation = dimNav;
            dimBtn.onClick.AddListener(new System.Action(() => _filterPopup.SetActive(false)));

            // Popup panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_filterPopup.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.05f, 0.15f);
            panelRT.anchorMax = new Vector2(0.95f, 0.85f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            ScrollRect sr;
            _filterPopupContent = UIHelper.BuildScrollView(panel, out sr);

            _filterPopup.SetActive(false);
        }

        private static void ShowTypeFilterPopup()
        {
            ClearFilterPopup();

            AddFilterOption("All Types", () => { _typeFilter = SearchEngine.DeviceTypeFilter.All; ApplyFilter(); });
            AddFilterOption("Servers", () => { _typeFilter = SearchEngine.DeviceTypeFilter.Servers; ApplyFilter(); });
            AddFilterOption("Switches", () => { _typeFilter = SearchEngine.DeviceTypeFilter.Switches; ApplyFilter(); });
            AddFilterOption("Patch Panels", () => { _typeFilter = SearchEngine.DeviceTypeFilter.PatchPanels; ApplyFilter(); });

            _filterPopup.SetActive(true);
        }

        private static void ShowStatusFilterPopup()
        {
            ClearFilterPopup();

            AddFilterOption("All Status", () => { _statusFilter = SearchEngine.StatusFilter.All; ApplyFilter(); });
            AddFilterOption("Online", () => { _statusFilter = SearchEngine.StatusFilter.Online; ApplyFilter(); });
            AddFilterOption("Offline", () => { _statusFilter = SearchEngine.StatusFilter.Offline; ApplyFilter(); });
            AddFilterOption("Broken", () => { _statusFilter = SearchEngine.StatusFilter.Broken; ApplyFilter(); });
            AddFilterOption("EOL", () => { _statusFilter = SearchEngine.StatusFilter.EOL; ApplyFilter(); });
            AddFilterOption("Unassigned (0.0.0.0)", () => { _statusFilter = SearchEngine.StatusFilter.Unassigned; ApplyFilter(); });

            _filterPopup.SetActive(true);
        }

        private static void ShowColorFilterPopup()
        {
            ClearFilterPopup();

            AddFilterOption("All Colors", () => { _colorFilter = SearchEngine.ServerColorFilter.All; ApplyFilter(); });
            AddColorOption("Blue (System X)",   SearchEngine.ServerColorFilter.Blue,   new Color(0.3f, 0.5f, 1.0f));
            AddColorOption("Green (GPU)",        SearchEngine.ServerColorFilter.Green,  new Color(0.3f, 0.8f, 0.3f));
            AddColorOption("Purple (Mainframe)", SearchEngine.ServerColorFilter.Purple, new Color(0.6f, 0.3f, 0.9f));
            AddColorOption("Yellow (RISC)",      SearchEngine.ServerColorFilter.Yellow, new Color(1.0f, 0.85f, 0.2f));

            _filterPopup.SetActive(true);
        }

        private static void AddColorOption(string label, SearchEngine.ServerColorFilter filter, Color dotColor)
        {
            var row = new GameObject("FilterOpt");
            row.transform.SetParent(_filterPopupContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 32f;

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 8f;
            var pad = new RectOffset();
            pad.left = 8; pad.right = 8; pad.top = 4; pad.bottom = 4;
            hl.padding = pad;

            // Colored dot
            UIHelper.BuildStatusDot(row.transform, dotColor);

            // Label
            var lbl = UIHelper.BuildLabel(row.transform, label, 260f);
            lbl.fontSize = 12f;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb = new ColorBlock();
            cb.normalColor = new Color(0.14f, 0.14f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
            cb.pressedColor = new Color(0.08f, 0.12f, 0.08f, 1f);
            cb.selectedColor = new Color(0.14f, 0.14f, 0.16f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            SearchEngine.ServerColorFilter capturedFilter = filter;
            btn.onClick.AddListener(new System.Action(() => { _colorFilter = capturedFilter; ApplyFilter(); }));

            _filterPopupRows.Add(row);
        }

        private static void ShowCustomerFilterPopup()
        {
            ClearFilterPopup();

            AddFilterOption("All Customers", () => { _customerFilter = -1; ApplyFilter(); });

            var customers = SearchEngine.GetCustomerList();
            for (int i = 0; i < customers.Count; i++)
            {
                int capturedId = customers[i].CustomerID;
                string name = customers[i].Name;
                AddFilterOption(name, () => { _customerFilter = capturedId; ApplyFilter(); });
            }

            _filterPopup.SetActive(true);
        }

        private static void AddFilterOption(string label, System.Action onClick)
        {
            var row = new GameObject("FilterOpt");
            row.transform.SetParent(_filterPopupContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 32f;

            var lbl = UIHelper.BuildLabel(row.transform, label, 300f);
            lbl.fontSize = 12f;
            lbl.color = Color.white;
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb = new ColorBlock();
            cb.normalColor = new Color(0.14f, 0.14f, 0.16f, 1f);
            cb.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
            cb.pressedColor = new Color(0.08f, 0.12f, 0.08f, 1f);
            cb.selectedColor = new Color(0.14f, 0.14f, 0.16f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            btn.onClick.AddListener(new System.Action(onClick));

            _filterPopupRows.Add(row);
        }

        private static void ApplyFilter()
        {
            _filterPopup.SetActive(false);
            Populate();
        }

        private static void UpdateFilterLabels()
        {
            string typeStr = _typeFilter switch
            {
                SearchEngine.DeviceTypeFilter.Servers => "Servers",
                SearchEngine.DeviceTypeFilter.Switches => "Switches",
                SearchEngine.DeviceTypeFilter.PatchPanels => "PPs",
                _ => "All"
            };
            _typeBtnLabel.text = $"Type: {typeStr}";

            string statusStr = _statusFilter switch
            {
                SearchEngine.StatusFilter.Online => "Online",
                SearchEngine.StatusFilter.Offline => "Offline",
                SearchEngine.StatusFilter.Broken => "Broken",
                SearchEngine.StatusFilter.EOL => "EOL",
                SearchEngine.StatusFilter.Unassigned => "Unassigned",
                _ => "All"
            };
            _statusBtnLabel.text = $"Status: {statusStr}";

            if (_customerFilter >= 0)
            {
                var ci = MainGameManager.instance.GetCustomerItemByID(_customerFilter);
                string name = ci != null ? ci.customerName : $"#{_customerFilter}";
                if (name.Length > 8) name = name.Substring(0, 8) + "..";
                _customerBtnLabel.text = $"Cust: {name}";
            }
            else
            {
                _customerBtnLabel.text = "Customer: All";
            }

            string colorStr = _colorFilter switch
            {
                SearchEngine.ServerColorFilter.Blue   => "Blue",
                SearchEngine.ServerColorFilter.Green  => "Green",
                SearchEngine.ServerColorFilter.Purple => "Purple",
                SearchEngine.ServerColorFilter.Yellow => "Yellow",
                _ => "All"
            };
            _colorBtnLabel.text = $"Color: {colorStr}";
        }

        private static void ClearFilterPopup()
        {
            for (int i = 0; i < _filterPopupRows.Count; i++)
            {
                if (_filterPopupRows[i] != null)
                    Object.Destroy(_filterPopupRows[i]);
            }
            _filterPopupRows.Clear();
        }

        private static void StopRefresh()
        {
            if (_refreshCoroutine != null)
            {
                MelonCoroutines.Stop(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private static IEnumerator RefreshLiveRows()
        {
            while (_root != null && _root.activeSelf)
            {
                yield return new WaitForSeconds(1f);
                for (int i = 0; i < _liveRows.Count; i++)
                {
                    var lr = _liveRows[i];
                    if (lr.DotImg == null || lr.PwrLbl == null || lr.EolLbl == null) continue;

                    bool broken, eol, on;
                    int eolTime;
                    if (lr.Server != null)
                        UIHelper.GetDeviceState(lr.Server, out broken, out on, out eol, out eolTime);
                    else if (lr.Switch != null)
                        UIHelper.GetDeviceState(lr.Switch, out broken, out on, out eol, out eolTime);
                    else continue;

                    // Status dot
                    lr.DotImg.color = broken ? UIHelper.StatusRed : on ? UIHelper.StatusGreen : UIHelper.StatusGray;

                    // Power badge
                    if (broken) { lr.PwrLbl.text = "BRK"; lr.PwrLbl.color = UIHelper.StatusRed; }
                    else if (on) { lr.PwrLbl.text = "ON";  lr.PwrLbl.color = UIHelper.StatusGreen; }
                    else         { lr.PwrLbl.text = "OFF"; lr.PwrLbl.color = new Color(0.55f, 0.55f, 0.55f); }

                    // EOL countdown
                    if (broken)
                    {
                        lr.EolLbl.text  = "BRK";
                        lr.EolLbl.color = UIHelper.StatusRed;
                    }
                    else if (eolTime != 0 || eol)
                    {
                        UIHelper.ApplyEolLabel(lr.EolLbl, eolTime);
                    }
                    else
                    {
                        lr.EolLbl.text  = "";
                        lr.EolLbl.color = Color.white;
                    }
                }
            }
            _refreshCoroutine = null;
        }

        private static void ClearResults()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i] != null)
                    Object.Destroy(_elements[i]);
            }
            _elements.Clear();
        }

        // ── Assign Customer popup ────────────────────────────────────

        private static void BuildAssignPopup(GameObject root)
        {
            _assignPopupRoot = new GameObject("AssignPopup");
            _assignPopupRoot.transform.SetParent(root.transform, false);
            var overlayRT = _assignPopupRoot.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var popupLE = _assignPopupRoot.AddComponent<LayoutElement>();
            popupLE.ignoreLayout = true;

            var dimImg = _assignPopupRoot.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.5f);
            var dimBtn = _assignPopupRoot.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            var dimNav = new Navigation(); dimNav.mode = Navigation.Mode.None;
            dimBtn.navigation = dimNav;
            dimBtn.onClick.AddListener(new System.Action(() => _assignPopupRoot.SetActive(false)));

            // Panel
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_assignPopupRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.05f, 0.10f);
            panelRT.anchorMax = new Vector2(0.95f, 0.90f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);
            var panelVL = panel.AddComponent<VerticalLayoutGroup>();
            panelVL.childControlWidth = true;
            panelVL.childControlHeight = true;
            panelVL.childForceExpandWidth = true;
            panelVL.childForceExpandHeight = false;
            panelVL.spacing = 4f;
            var panelPad = new RectOffset();
            panelPad.left = 8; panelPad.right = 8; panelPad.top = 8; panelPad.bottom = 8;
            panelVL.padding = panelPad;

            // Header label
            var hdrGo = new GameObject("Hdr");
            hdrGo.transform.SetParent(panel.transform, false);
            var hdrLbl = hdrGo.AddComponent<TextMeshProUGUI>();
            hdrLbl.text = "Assign Customer";
            hdrLbl.fontSize = 14f;
            hdrLbl.color = Color.white;
            hdrLbl.alignment = TextAlignmentOptions.Center;
            var hdrLE = hdrGo.AddComponent<LayoutElement>();
            hdrLE.preferredHeight = 28f;

            // Scroll area
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            var scrollLE = scrollArea.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;
            ScrollRect sr;
            _assignPopupContent = UIHelper.BuildScrollView(scrollArea, out sr);

            // Button row
            var btnRow = new GameObject("BtnRow");
            btnRow.transform.SetParent(panel.transform, false);
            var btnRowHL = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnRowHL.childControlWidth = true;
            btnRowHL.childControlHeight = true;
            btnRowHL.childForceExpandWidth = false;
            btnRowHL.childForceExpandHeight = false;
            btnRowHL.spacing = 8f;
            var btnRowLE = btnRow.AddComponent<LayoutElement>();
            btnRowLE.preferredHeight = 32f;

            // Spacer
            var spacerGo = new GameObject("Spacer");
            spacerGo.transform.SetParent(btnRow.transform, false);
            var spacerLE = spacerGo.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1f;

            // Cancel
            var cancelBtn = UIHelper.BuildButton(btnRow.transform, "Cancel", 80f);
            cancelBtn.onClick.AddListener(new System.Action(() => _assignPopupRoot.SetActive(false)));

            // Assign (confirm)
            var confirmBtn = UIHelper.BuildButton(btnRow.transform, "Assign", 80f);
            _assignPopupConfirmBtn = confirmBtn;
            confirmBtn.interactable = false;
            confirmBtn.onClick.AddListener(new System.Action(OnAssignConfirm));

            _assignPopupRoot.SetActive(false);
        }

        private static void ShowAssignCustomerPopup()
        {
            // Clear previous rows
            for (int i = 0; i < _assignPopupRows.Count; i++)
                if (_assignPopupRows[i] != null) Object.Destroy(_assignPopupRows[i]);
            _assignPopupRows.Clear();

            _assignPopupSelectedCustId = -1;
            _assignPopupSelectedRowImg = null;
            _assignPopupConfirmBtn.interactable = false;

            var custBases = MainGameManager.instance.customerBases;
            for (int i = 0; i < custBases.Length; i++)
            {
                var cb = custBases[i];
                int custId = cb.customerID;
                if (custId < 0) continue;

                var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                string custName = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}";

                var row = new GameObject($"AssignCust_{custId}");
                row.transform.SetParent(_assignPopupContent, false);

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

                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 38f;

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
                btnCb.pressedColor = new Color(0.08f, 0.12f, 0.08f, 1f);
                btnCb.selectedColor = new Color(0.14f, 0.14f, 0.16f, 1f);
                btnCb.colorMultiplier = 1f;
                btnCb.fadeDuration = 0.1f;
                btn.colors = btnCb;
                var nav = new Navigation(); nav.mode = Navigation.Mode.None;
                btn.navigation = nav;

                int capturedId = custId;
                Image capturedRowImg = rowImg;
                btn.onClick.AddListener(new System.Action(() =>
                {
                    if (_assignPopupSelectedRowImg != null)
                        _assignPopupSelectedRowImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);
                    _assignPopupSelectedCustId = capturedId;
                    _assignPopupSelectedRowImg = capturedRowImg;
                    capturedRowImg.color = new Color(0.12f, 0.28f, 0.12f, 1f);
                    _assignPopupConfirmBtn.interactable = true;
                }));

                _assignPopupRows.Add(row);
            }

            _assignPopupRoot.SetActive(true);
        }

        private static void OnAssignConfirm()
        {
            if (_assignPopupSelectedCustId < 0) return;

            var assignedIPs = DeviceConfigPanel.GetAllUsedIPs();
            int assigned = 0;
            for (int i = 0; i < _selectedDevices.Count; i++)
            {
                var d = _selectedDevices[i];
                if (d.Server == null) continue;
                d.Server.UpdateCustomer(_assignPopupSelectedCustId);
                AutoFillIP(d.Server, assignedIPs);
                assigned++;
            }

            var custItem = MainGameManager.instance.GetCustomerItemByID(_assignPopupSelectedCustId);
            string custName = custItem != null ? custItem.customerName : $"Customer {_assignPopupSelectedCustId}";
            StaticUIElements.instance.AddMeesageInField($"Assigned {assigned} servers to {custName}");

            _assignPopupRoot.SetActive(false);
            Populate();
        }

        private static void AutoFillIP(Server server, HashSet<string> assignedIPs)
        {
            try
            {
                int custId = server.GetCustomerID();
                var customerBases = MainGameManager.instance.customerBases;
                CustomerBase targetBase = null;
                for (int i = 0; i < customerBases.Length; i++)
                {
                    if (customerBases[i].customerID == custId)
                    {
                        targetBase = customerBases[i];
                        break;
                    }
                }
                if (targetBase == null) return;

                string subnet = DeviceConfigPanel.FindSubnetForServerType(targetBase, server.serverType);
                if (subnet == null) return;

                var setIP = Object.FindObjectOfType<SetIP>();
                if (setIP == null) return;

                string[] usableIPs = setIP.GetUsableIPsFromSubnet(subnet);
                if (usableIPs == null || usableIPs.Length == 0) return;

                for (int ui = 0; ui < usableIPs.Length; ui++)
                {
                    string ip = usableIPs[ui];
                    int lastDot = ip.LastIndexOf('.');
                    if (lastDot >= 0)
                    {
                        string lastOctet = ip.Substring(lastDot + 1);
                        if (lastOctet == "0" || lastOctet == "1") continue;
                    }
                    if (!assignedIPs.Contains(ip))
                    {
                        server.SetIP(ip);
                        assignedIPs.Add(ip);
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] Auto-fill IP failed: {ex.Message}");
            }
        }
    }
}
