using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public static class DeviceListView
    {
        private static GameObject _root;
        private static Transform _contentTransform;
        private static readonly List<GameObject> _rows = new List<GameObject>();

        // Multi-select state
        private static readonly List<Server> _selectedServers = new List<Server>();
        private static readonly Dictionary<Server, Image> _checkboxImages = new Dictionary<Server, Image>();
        private static readonly List<NetworkSwitch> _selectedSwitches = new List<NetworkSwitch>();
        private static readonly Dictionary<NetworkSwitch, Image> _switchCheckboxImages = new Dictionary<NetworkSwitch, Image>();
        private static Button _selectAllBtn;
        private static TextMeshProUGUI _selectAllLabel;
        private static bool _allSelected;

        // Customer popup
        private static GameObject _popupRoot;
        private static Transform _popupContentTransform;
        private static readonly List<GameObject> _popupRows = new List<GameObject>();
        private static int _popupSelectedCustId = -1;
        private static Image _popupSelectedRowImg;
        private static Button _popupConfirmBtn;

        // All devices in current rack (for select all)
        private static readonly List<Server> _allServersInRack = new List<Server>();
        private static readonly List<NetworkSwitch> _allSwitchesInRack = new List<NetworkSwitch>();

        // Live-refresh
        private struct LiveRow
        {
            public Server Server;
            public NetworkSwitch Switch;
            public Image DotImg;
            public TextMeshProUGUI BadgeLbl;
            public TextMeshProUGUI EolLbl;
        }
        private static readonly List<LiveRow> _liveRows = new List<LiveRow>();
        private static object _refreshCoroutine;

        public static void Build(GameObject root)
        {
            _root = root;

            // ScrollRect
            var scrollRect = root.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            // Viewport
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

            // Content
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

            _contentTransform = content.transform;

            // Build popup (hidden by default)
            BuildCustomerPopup(root);
        }

        public static void Populate(Rack rack)
        {
            StopRefresh();
            _liveRows.Clear();

            // Clear old rows
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    Object.Destroy(_rows[i]);
            }
            _rows.Clear();
            _selectedServers.Clear();
            _checkboxImages.Clear();
            _selectedSwitches.Clear();
            _switchCheckboxImages.Clear();
            _allServersInRack.Clear();
            _allSwitchesInRack.Clear();
            _allSelected = false;

            if (rack == null) return;

            int rackId = rack.GetInstanceID();
            var devices = new List<DeviceEntry>();

            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var parentRack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || parentRack.GetInstanceID() != rackId) continue;
                devices.Add(new DeviceEntry
                {
                    SlotIndex = srv.currentRackPosition.positionIndex,
                    Server = srv,
                    SizeInU = srv.sizeInU
                });
                _allServersInRack.Add(srv);
            }

            var allSwitches = Object.FindObjectsOfType<NetworkSwitch>();
            for (int i = 0; i < allSwitches.Length; i++)
            {
                var sw = allSwitches[i];
                if (sw.currentRackPosition == null) continue;
                var parentRack = sw.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || parentRack.GetInstanceID() != rackId) continue;
                devices.Add(new DeviceEntry
                {
                    SlotIndex = sw.currentRackPosition.positionIndex,
                    Switch = sw,
                    SizeInU = sw.sizeInU
                });
                _allSwitchesInRack.Add(sw);
            }

            var allPPs = Object.FindObjectsOfType<PatchPanel>();
            for (int i = 0; i < allPPs.Length; i++)
            {
                var pp = allPPs[i];
                if (pp.currentRackPosition == null) continue;
                var parentRack = pp.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || parentRack.GetInstanceID() != rackId) continue;
                devices.Add(new DeviceEntry
                {
                    SlotIndex = pp.currentRackPosition.positionIndex,
                    PatchPanel = pp,
                    SizeInU = pp.sizeInU
                });
            }

            devices.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            if (devices.Count == 0)
            {
                BuildEmptyRow();
                return;
            }

            // Toolbar row: Select All + Assign Customer
            BuildToolbar();

            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                BuildDeviceRow(d.SlotIndex, d.Server, d.Switch, d.PatchPanel);
            }

            UpdateHeaderButton();
            if (_liveRows.Count > 0)
                _refreshCoroutine = MelonCoroutines.Start(RefreshLiveRows());
        }

        private static void BuildToolbar()
        {
            var toolbar = new GameObject("Toolbar");
            toolbar.transform.SetParent(_contentTransform, false);
            var toolbarImg = toolbar.AddComponent<Image>();
            toolbarImg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            var toolbarHL = toolbar.AddComponent<HorizontalLayoutGroup>();
            toolbarHL.childControlWidth = true;
            toolbarHL.childControlHeight = true;
            toolbarHL.childForceExpandWidth = false;
            toolbarHL.childForceExpandHeight = false;
            toolbarHL.spacing = 8f;
            var toolbarPad = new RectOffset();
            toolbarPad.left = 6; toolbarPad.right = 6; toolbarPad.top = 4; toolbarPad.bottom = 4;
            toolbarHL.padding = toolbarPad;
            var toolbarLE = toolbar.AddComponent<LayoutElement>();
            toolbarLE.preferredHeight = 32f;

            // Select All button
            _selectAllBtn = UIHelper.BuildButton(toolbar.transform, "Select All", 100f);
            _selectAllLabel = _selectAllBtn.GetComponentInChildren<TextMeshProUGUI>();
            _selectAllBtn.onClick.AddListener(new System.Action(() =>
            {
                _allSelected = !_allSelected;
                _selectedServers.Clear();
                _selectedSwitches.Clear();
                if (_allSelected)
                {
                    for (int i = 0; i < _allServersInRack.Count; i++)
                        _selectedServers.Add(_allServersInRack[i]);
                    for (int i = 0; i < _allSwitchesInRack.Count; i++)
                        _selectedSwitches.Add(_allSwitchesInRack[i]);
                }
                foreach (var kvp in _checkboxImages)
                {
                    bool sel = _selectedServers.Contains(kvp.Key);
                    kvp.Value.color = sel ? UIHelper.StatusGreen : new Color(0.3f, 0.3f, 0.3f);
                }
                foreach (var kvp in _switchCheckboxImages)
                {
                    bool sel = _selectedSwitches.Contains(kvp.Key);
                    kvp.Value.color = sel ? UIHelper.StatusGreen : new Color(0.3f, 0.3f, 0.3f);
                }
                _selectAllLabel.text = _allSelected ? "Deselect All" : "Select All";
                UpdateHeaderButton();
            }));

            // Power ON button
            var powerOnBtn = UIHelper.BuildButton(toolbar.transform, "Power ON", 80f);
            powerOnBtn.onClick.AddListener(new System.Action(() =>
            {
                int count = 0;
                for (int i = 0; i < _selectedServers.Count; i++)
                {
                    var srv = _selectedServers[i];
                    if (!srv.isOn && !srv.isBroken) { srv.PowerButton(); count++; }
                }
                for (int i = 0; i < _selectedSwitches.Count; i++)
                {
                    var sw = _selectedSwitches[i];
                    if (!sw.isOn && !sw.isBroken) { sw.PowerButton(); count++; }
                }
                if (count > 0)
                    StaticUIElements.instance.AddMeesageInField($"Powered on {count} devices");
            }));

            // Power OFF button
            var powerOffBtn = UIHelper.BuildButton(toolbar.transform, "Power OFF", 80f);
            powerOffBtn.onClick.AddListener(new System.Action(() =>
            {
                int count = 0;
                for (int i = 0; i < _selectedServers.Count; i++)
                {
                    var srv = _selectedServers[i];
                    if (srv.isOn) { srv.PowerButton(); count++; }
                }
                for (int i = 0; i < _selectedSwitches.Count; i++)
                {
                    var sw = _selectedSwitches[i];
                    if (sw.isOn) { sw.PowerButton(); count++; }
                }
                if (count > 0)
                    StaticUIElements.instance.AddMeesageInField($"Powered off {count} devices");
            }));

            _rows.Add(toolbar);
        }

        private struct DeviceEntry
        {
            public int SlotIndex;
            public Server Server;
            public NetworkSwitch Switch;
            public PatchPanel PatchPanel;
            public int SizeInU;
        }

        private static void BuildDeviceRow(int slot, Server server, NetworkSwitch sw, PatchPanel pp)
        {
            var row = new GameObject($"DeviceRow_{slot}");
            row.transform.SetParent(_contentTransform, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 6f;
            var rowPad = new RectOffset();
            rowPad.left = 6; rowPad.right = 6; rowPad.top = 4; rowPad.bottom = 4;
            hl.padding = rowPad;

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;

            // Checkbox for servers and switches
            Image checkboxImg = null;
            if (server != null || sw != null)
            {
                var cbGo = new GameObject("Checkbox");
                cbGo.transform.SetParent(row.transform, false);
                checkboxImg = cbGo.AddComponent<Image>();
                checkboxImg.color = new Color(0.3f, 0.3f, 0.3f);
                var cbLE = cbGo.AddComponent<LayoutElement>();
                cbLE.preferredWidth = 18f;
                cbLE.preferredHeight = 18f;

                // Inner check mark area
                var checkInner = new GameObject("Check");
                checkInner.transform.SetParent(cbGo.transform, false);
                var checkRT = checkInner.AddComponent<RectTransform>();
                checkRT.anchorMin = new Vector2(0.2f, 0.2f);
                checkRT.anchorMax = new Vector2(0.8f, 0.8f);
                checkRT.offsetMin = Vector2.zero;
                checkRT.offsetMax = Vector2.zero;
                var checkImg = checkInner.AddComponent<Image>();
                checkImg.color = new Color(0.08f, 0.08f, 0.10f, 1f);

                if (server != null)
                    _checkboxImages[server] = checkboxImg;
                else if (sw != null)
                    _switchCheckboxImages[sw] = checkboxImg;

                // Checkbox click
                var cbBtn = cbGo.AddComponent<Button>();
                cbBtn.targetGraphic = checkboxImg;
                var cbNav = new Navigation();
                cbNav.mode = Navigation.Mode.None;
                cbBtn.navigation = cbNav;

                Server capturedSrv = server;
                NetworkSwitch capturedSwCb = sw;
                Image capturedCbImg = checkboxImg;
                cbBtn.onClick.AddListener(new System.Action(() =>
                {
                    if (capturedSrv != null)
                    {
                        if (_selectedServers.Contains(capturedSrv))
                        {
                            _selectedServers.Remove(capturedSrv);
                            capturedCbImg.color = new Color(0.3f, 0.3f, 0.3f);
                        }
                        else
                        {
                            _selectedServers.Add(capturedSrv);
                            capturedCbImg.color = UIHelper.StatusGreen;
                        }
                    }
                    else if (capturedSwCb != null)
                    {
                        if (_selectedSwitches.Contains(capturedSwCb))
                        {
                            _selectedSwitches.Remove(capturedSwCb);
                            capturedCbImg.color = new Color(0.3f, 0.3f, 0.3f);
                        }
                        else
                        {
                            _selectedSwitches.Add(capturedSwCb);
                            capturedCbImg.color = UIHelper.StatusGreen;
                        }
                    }
                    int totalSelected = _selectedServers.Count + _selectedSwitches.Count;
                    int totalDevices = _allServersInRack.Count + _allSwitchesInRack.Count;
                    _allSelected = totalSelected == totalDevices;
                    _selectAllLabel.text = _allSelected ? "Deselect All" : "Select All";
                    UpdateHeaderButton();
                }));
            }

            // Determine display info
            string typeName;
            string detail;
            Color typeColor;

            if (server != null)
            {
                typeName = UIHelper.GetServerTypeName(server.gameObject.name);
                typeColor = UIHelper.GetDeviceTypeColor(server.gameObject.name);
                detail = server.IP ?? "";
            }
            else if (sw != null)
            {
                typeName = MainGameManager.instance.ReturnSwitchNameFromType(sw.switchType);
                typeColor = UIHelper.SwitchColor;
                detail = GetSwitchLabel(sw);
            }
            else
            {
                typeName = "Patch Panel";
                typeColor = UIHelper.PatchPanelColor;
                detail = "";
            }

            // ── Status dot + badge (matches SearchResults style) ──────
            bool rowBroken = (server != null && server.isBroken) || (sw != null && sw.isBroken);
            bool rowEol    = !rowBroken && ((server != null && server.eolTime <= 0) || (sw != null && sw.eolTime <= 0));
            bool rowOn     = !rowBroken && ((server != null && server.isOn) || (sw != null && sw.isOn));
            Color dotColor = rowBroken ? UIHelper.StatusRed : rowOn ? UIHelper.StatusGreen : UIHelper.StatusGray;
            var dot = UIHelper.BuildStatusDot(row.transform, dotColor, 10f);

            string badgeText = rowBroken ? "BRK" : rowOn ? "ON" : "OFF";
            var pwrLbl = UIHelper.BuildLabel(row.transform, badgeText, 30f);
            pwrLbl.fontSize = 8f;
            pwrLbl.color = dotColor;
            pwrLbl.alignment = TextAlignmentOptions.Center;

            // EOL countdown label (servers and switches only)
            TextMeshProUGUI eolLbl = null;
            if (server != null || sw != null)
            {
                int rowEolTime = server != null ? server.eolTime : sw.eolTime;
                bool rowShowEol = rowEol || (rowEolTime > 0 && rowEolTime <= FloorManagerMod.EOL_WARN_SECONDS);
                string eolInitText = rowShowEol ? UIHelper.FormatEolTime(rowEolTime) : "";
                Color eolInitColor = rowShowEol ? UIHelper.EolTimeColor(rowEolTime) : Color.clear;
                eolLbl = UIHelper.BuildLabel(row.transform, eolInitText, 56f);
                eolLbl.fontSize = 8f;
                eolLbl.color = eolInitColor;
                eolLbl.alignment = TextAlignmentOptions.Right;
            }

            // Store refs for live refresh (servers and switches only)
            if (server != null || sw != null)
                _liveRows.Add(new LiveRow { Server = server, Switch = sw, DotImg = dot, BadgeLbl = pwrLbl, EolLbl = eolLbl });

            // Slot number
            var slotLbl = UIHelper.BuildLabel(row.transform, $"[{slot + 1}]", 30f);
            slotLbl.fontSize = 10f;
            slotLbl.color = typeColor;

            // Type name
            var nameLbl = UIHelper.BuildLabel(row.transform, typeName, 120f);
            nameLbl.fontSize = 10f;
            nameLbl.color = typeColor;
            var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>();
            nameLE.flexibleWidth = 1f;

            // Security role tag
            string roleTag = "";
            Color roleColor = Color.white;
#if !STRIP_HACKING
            if (sw != null)
            {
                if (HackingSystem.IsFirewall(sw))  { roleTag = "FW";  roleColor = UIHelper.StatusOrange; }
                else if (HackingSystem.IsIDS(sw))  { roleTag = "IDS"; roleColor = new Color(0.1f, 0.8f, 0.8f); }
            }
            else if (server != null && HackingSystem.IsHoneypot(server))
            {
                roleTag = "HP"; roleColor = new Color(0.7f, 0.2f, 0.9f);
            }
#endif
            if (!string.IsNullOrEmpty(roleTag))
            {
                var roleLbl = UIHelper.BuildLabel(row.transform, roleTag, 28f);
                roleLbl.fontSize = 8f;
                roleLbl.color = roleColor;
                roleLbl.fontStyle = FontStyles.Bold;
                roleLbl.alignment = TextAlignmentOptions.Center;
            }

            // Detail (IP or switch label)
            var detailLbl = UIHelper.BuildLabel(row.transform, detail, 120f);
            detailLbl.fontSize = 10f;
            detailLbl.color = Color.white;

            // Row click → open device config (clicking the row itself, not checkbox)
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb = new ColorBlock();
            cb.normalColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            cb.highlightedColor = new Color(0.18f, 0.18f, 0.22f, 1f);
            cb.pressedColor = new Color(0.08f, 0.08f, 0.10f, 1f);
            cb.selectedColor = new Color(0.12f, 0.12f, 0.14f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;

            Server capturedServer = server;
            NetworkSwitch capturedSw = sw;
            PatchPanel capturedPP = pp;
            btn.onClick.AddListener(new System.Action(() =>
            {
                FloorMapApp.OpenDevice(capturedServer, capturedSw, capturedPP);
            }));

            _rows.Add(row);
        }

        public static void PopulateMultiRack(List<Rack> racks)
        {
            StopRefresh();
            _liveRows.Clear();

            for (int i = 0; i < _rows.Count; i++)
                if (_rows[i] != null) Object.Destroy(_rows[i]);
            _rows.Clear();
            _selectedServers.Clear();
            _checkboxImages.Clear();
            _selectedSwitches.Clear();
            _switchCheckboxImages.Clear();
            _allServersInRack.Clear();
            _allSwitchesInRack.Clear();
            _allSelected = false;

            if (racks == null || racks.Count == 0) return;

            // Build set of rack instance IDs for fast lookup
            var rackIds = new HashSet<int>();
            for (int ri = 0; ri < racks.Count; ri++)
                rackIds.Add(racks[ri].GetInstanceID());

            var devices = new List<DeviceEntry>();

            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var parentRack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || !rackIds.Contains(parentRack.GetInstanceID())) continue;
                devices.Add(new DeviceEntry { SlotIndex = srv.currentRackPosition.positionIndex, Server = srv, SizeInU = srv.sizeInU });
                _allServersInRack.Add(srv);
            }

            var allSwitches = Object.FindObjectsOfType<NetworkSwitch>();
            for (int i = 0; i < allSwitches.Length; i++)
            {
                var sw = allSwitches[i];
                if (sw.currentRackPosition == null) continue;
                var parentRack = sw.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || !rackIds.Contains(parentRack.GetInstanceID())) continue;
                devices.Add(new DeviceEntry { SlotIndex = sw.currentRackPosition.positionIndex, Switch = sw, SizeInU = sw.sizeInU });
                _allSwitchesInRack.Add(sw);
            }

            var allPPs = Object.FindObjectsOfType<PatchPanel>();
            for (int i = 0; i < allPPs.Length; i++)
            {
                var pp = allPPs[i];
                if (pp.currentRackPosition == null) continue;
                var parentRack = pp.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || !rackIds.Contains(parentRack.GetInstanceID())) continue;
                devices.Add(new DeviceEntry { SlotIndex = pp.currentRackPosition.positionIndex, PatchPanel = pp, SizeInU = pp.sizeInU });
            }

            devices.Sort((a, b) => a.SlotIndex.CompareTo(b.SlotIndex));

            // Header row showing rack + device counts
            var headerRow = new GameObject("MultiRackHeader");
            headerRow.transform.SetParent(_contentTransform, false);
            var hdrImg = headerRow.AddComponent<Image>();
            hdrImg.color = new Color(0.08f, 0.08f, 0.10f, 1f);
            var hdrLE = headerRow.AddComponent<LayoutElement>();
            hdrLE.preferredHeight = 24f;
            var hdrLbl = UIHelper.BuildLabel(headerRow.transform, $"{racks.Count} racks selected  —  {devices.Count} devices", 300f);
            hdrLbl.fontSize = 10f;
            hdrLbl.color = new Color(0.7f, 0.7f, 0.7f);
            hdrLbl.alignment = TextAlignmentOptions.Center;
            var hdrLblLE = hdrLbl.gameObject.GetComponent<LayoutElement>();
            hdrLblLE.flexibleWidth = 1f;
            _rows.Add(headerRow);

            if (devices.Count == 0)
            {
                BuildEmptyRow();
                return;
            }

            BuildToolbar();

            for (int i = 0; i < devices.Count; i++)
            {
                var d = devices[i];
                BuildDeviceRow(d.SlotIndex, d.Server, d.Switch, d.PatchPanel);
            }

            UpdateHeaderButton();
            if (_liveRows.Count > 0)
                _refreshCoroutine = MelonCoroutines.Start(RefreshLiveRows());
        }

        public static void UpdateHeaderButton()
        {
            // Update the header action button label in FloorMapApp
            int count = _selectedServers.Count;
            FloorMapApp.UpdateHeaderActionLabel(count > 0
                ? $"Assign Customer ({count})"
                : "Assign Customer");
        }

        public static void OnAssignCustomerClicked()
        {
            if (_selectedServers.Count > 0)
                ShowCustomerPopup();
            else
                StaticUIElements.instance.AddMeesageInField("Select servers first using checkboxes");
        }

        // ── Customer popup ──────────────────────────────────────────

        private static void BuildCustomerPopup(GameObject root)
        {
            // Full overlay behind popup to block clicks
            _popupRoot = new GameObject("CustomerPopup");
            _popupRoot.transform.SetParent(root.transform, false);
            var overlayRT = _popupRoot.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;

            // Dim background
            var dimImg = _popupRoot.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);

            // Popup panel — centered
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_popupRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.1f, 0.08f);
            panelRT.anchorMax = new Vector2(0.9f, 0.92f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            // Panel layout
            var panelVL = panel.AddComponent<VerticalLayoutGroup>();
            panelVL.childControlWidth = true;
            panelVL.childControlHeight = true;
            panelVL.childForceExpandWidth = true;
            panelVL.childForceExpandHeight = false;
            panelVL.spacing = 4f;
            var panelPad = new RectOffset();
            panelPad.left = 8; panelPad.right = 8; panelPad.top = 8; panelPad.bottom = 8;
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
            var titleLE = titleRow.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 30f;

            var titleLbl = UIHelper.BuildLabel(titleRow.transform, "Select Customer", 200f);
            titleLbl.fontSize = 14f;
            titleLbl.fontStyle = FontStyles.Bold;
            titleLbl.color = Color.white;
            var titleLblLE = titleLbl.gameObject.GetComponent<LayoutElement>();
            titleLblLE.flexibleWidth = 1f;

            // Cancel button
            var cancelBtn = UIHelper.BuildButton(titleRow.transform, "Cancel", 80f);
            cancelBtn.onClick.AddListener(new System.Action(() =>
            {
                _popupRoot.SetActive(false);
            }));

            // Scrollable customer list area
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            var scrollLE = scrollArea.AddComponent<LayoutElement>();
            scrollLE.flexibleHeight = 1f;

            var scrollRect = scrollArea.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollArea.transform, false);
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
            var contentVL = content.AddComponent<VerticalLayoutGroup>();
            contentVL.childControlWidth = true;
            contentVL.childControlHeight = true;
            contentVL.childForceExpandWidth = true;
            contentVL.childForceExpandHeight = false;
            contentVL.spacing = 2f;
            var contentPad = new RectOffset();
            contentPad.left = 4; contentPad.right = 4; contentPad.top = 4; contentPad.bottom = 4;
            contentVL.padding = contentPad;
            var contentCSF = content.AddComponent<ContentSizeFitter>();
            contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            _popupContentTransform = content.transform;

            // Bottom row — confirm button
            var bottomRow = new GameObject("BottomRow");
            bottomRow.transform.SetParent(panel.transform, false);
            var bottomHL = bottomRow.AddComponent<HorizontalLayoutGroup>();
            bottomHL.childControlWidth = true;
            bottomHL.childControlHeight = true;
            bottomHL.childForceExpandWidth = false;
            bottomHL.childForceExpandHeight = false;
            bottomHL.spacing = 8f;
            var bottomPad = new RectOffset();
            bottomPad.left = 4; bottomPad.right = 4; bottomPad.top = 4; bottomPad.bottom = 4;
            bottomHL.padding = bottomPad;
            var bottomLE = bottomRow.AddComponent<LayoutElement>();
            bottomLE.preferredHeight = 36f;

            // Spacer
            var bSpacer = new GameObject("Spacer");
            bSpacer.transform.SetParent(bottomRow.transform, false);
            bSpacer.AddComponent<RectTransform>();
            var bSpacerLE = bSpacer.AddComponent<LayoutElement>();
            bSpacerLE.flexibleWidth = 1f;

            _popupConfirmBtn = UIHelper.BuildButton(bottomRow.transform, "Select", 120f);
            _popupConfirmBtn.interactable = false;
            _popupConfirmBtn.onClick.AddListener(new System.Action(OnPopupConfirm));

            _popupRoot.SetActive(false);
        }

        private static void ShowCustomerPopup()
        {
            // Clear old popup rows
            for (int i = 0; i < _popupRows.Count; i++)
            {
                if (_popupRows[i] != null)
                    Object.Destroy(_popupRows[i]);
            }
            _popupRows.Clear();
            _popupSelectedCustId = -1;
            _popupSelectedRowImg = null;
            _popupConfirmBtn.interactable = false;

            // Update confirm button label
            var confirmLabel = _popupConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            if (confirmLabel != null)
                confirmLabel.text = $"Select ({_selectedServers.Count} servers)";

            // Build customer rows
            var custBases = MainGameManager.instance.customerBases;
            for (int i = 0; i < custBases.Length; i++)
            {
                var cb = custBases[i];
                int custId = cb.customerID;
                if (custId < 0) continue;

                var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                string custName = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}";

                var row = new GameObject($"PopupCust_{custId}");
                row.transform.SetParent(_popupContentTransform, false);

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

                // Logo
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

                // Name
                var nameLbl = UIHelper.BuildLabel(row.transform, custName, 250f);
                nameLbl.fontSize = 12f;
                nameLbl.color = Color.white;
                var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>();
                nameLE.flexibleWidth = 1f;

                // Click handler
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
                var nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                btn.navigation = nav;

                int capturedId = custId;
                var capturedRowImg = rowImg;
                btn.onClick.AddListener(new System.Action(() =>
                {
                    // Deselect previous
                    if (_popupSelectedRowImg != null)
                        _popupSelectedRowImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);

                    // Select this one
                    _popupSelectedCustId = capturedId;
                    _popupSelectedRowImg = capturedRowImg;
                    capturedRowImg.color = new Color(0.12f, 0.28f, 0.12f, 1f);
                    _popupConfirmBtn.interactable = true;
                }));

                _popupRows.Add(row);
            }

            _popupRoot.SetActive(true);
        }

        private static void OnPopupConfirm()
        {
            if (_popupSelectedCustId < 0 || _selectedServers.Count == 0)
                return;

            // Seed with all IPs already in use, then track batch additions
            var assignedIPs = DeviceConfigPanel.GetAllUsedIPs();

            int assigned = 0;
            for (int i = 0; i < _selectedServers.Count; i++)
            {
                var srv = _selectedServers[i];
                srv.UpdateCustomer(_popupSelectedCustId);
                AutoFillIP(srv, assignedIPs);
                assigned++;
            }

            var custItem = MainGameManager.instance.GetCustomerItemByID(_popupSelectedCustId);
            string custName = custItem != null ? custItem.customerName : $"Customer {_popupSelectedCustId}";
            StaticUIElements.instance.AddMeesageInField($"Assigned {assigned} servers to {custName}");

            _popupRoot.SetActive(false);

            // Refresh the device list to show updated IPs/customers
            FloorMapApp.SwitchToState(ViewState.DeviceList);
        }

        internal static void AutoFillIP(Server server, HashSet<string> assignedIPs)
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
                if (usableIPs != null && usableIPs.Length > 0)
                {
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
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] Auto-fill IP failed: {ex.Message}");
            }
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
                    if (lr.DotImg == null || lr.BadgeLbl == null) continue;

                    bool broken, eol, on;
                    int eolTime;
                    if (lr.Server != null)
                        UIHelper.GetDeviceState(lr.Server, out broken, out on, out eol, out eolTime);
                    else if (lr.Switch != null)
                        UIHelper.GetDeviceState(lr.Switch, out broken, out on, out eol, out eolTime);
                    else continue;

                    Color dotColor = broken ? UIHelper.StatusRed : on ? UIHelper.StatusGreen : UIHelper.StatusGray;
                    string badge   = broken ? "BRK" : on ? "ON" : "OFF";
                    lr.DotImg.color   = dotColor;
                    lr.BadgeLbl.text  = badge;
                    lr.BadgeLbl.color = dotColor;

                    if (lr.EolLbl != null)
                    {
                        bool showEol = eol || (!broken && eolTime > 0 && eolTime <= FloorManagerMod.EOL_WARN_SECONDS);
                        if (broken || !showEol) { lr.EolLbl.text = ""; lr.EolLbl.color = Color.clear; }
                        else                     UIHelper.ApplyEolLabel(lr.EolLbl, eolTime);
                    }
                }
            }
            _refreshCoroutine = null;
        }

        private static void BuildEmptyRow()
        {
            var row = new GameObject("EmptyRow");
            row.transform.SetParent(_contentTransform, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 28f;

            var emptyLbl = UIHelper.BuildLabel(row.transform, "No devices installed", 200f);
            emptyLbl.fontSize = 10f;
            emptyLbl.color = UIHelper.StatusGray;
            emptyLbl.alignment = TextAlignmentOptions.Center;

            _rows.Add(row);
        }

        static string GetSwitchLabel(NetworkSwitch sw)
        {
            try
            {
                var nd = SaveData._current?.networkData;
                if (nd == null) return "";
                string id = sw.GetSwitchId();
                var list = nd.switches;
                for (int i = 0; i < list.Count; i++)
                {
                    var ssd = list[i];
                    if (ssd.switchID == id) return ssd.label ?? "";
                }
            }
            catch { }
            return "";
        }
    }
}
