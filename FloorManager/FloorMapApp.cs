using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public enum ViewState { Dashboard, FloorMap, DeviceList, DeviceConfig, CustomerIPs, SearchResults, Shop }

    public static class FloorMapApp
    {
        private static ViewState _currentState;
        private static ViewState _previousState; // for back from DeviceConfig
        private static GameObject _dashboardView;
        private static GameObject _floorMapView;
        private static GameObject _deviceListView;
        private static GameObject _deviceConfigView;
        private static GameObject _customerIPView;
        private static GameObject _searchResultsView;
        private static GameObject _colorLegend;
        private static TextMeshProUGUI _headerText;
        private static Button _headerActionBtn;
        private static TextMeshProUGUI _headerActionLabel;
        // Tab bar
        private static GameObject _tabBar;
        private static RectTransform _contentAreaRT;
        private static readonly Image[] _tabIndicators = new Image[4];
        private static readonly TextMeshProUGUI[] _tabLbls = new TextMeshProUGUI[4];

        // Rack color popup (buy + recolor)
        private static GameObject _buyRackPopup;
        private static TextMeshProUGUI _buyRackPriceLabel;
        private static TextMeshProUGUI _confirmBtnLbl;
        private static RackMount _pendingBuyMount;
        private static Color? _buyRackSelectedColor = null;
        private static readonly Image[] _buyRackSwatchImgs = new Image[8];
        private static readonly Image[] _buyRackSwatchBorders = new Image[8];
        // HSV sliders
        private static Slider _hSlider, _sSlider, _vSlider;
        private static TextMeshProUGUI _hValLbl, _sValLbl, _vValLbl;
        private static Texture2D _satGradTex, _valGradTex;
        private static Image _satBgImg, _valBgImg;
        private static TMP_InputField _hexInputField;
        private static Image _colorPreviewImg;
        private static readonly Image[] _buyRackSaveSlotBorders = new Image[8];
        private static MelonPreferences_Entry<string> _rackColorsEntry;
        // Recolor mode (recolor already-placed rack(s), no purchase)
        private static bool _recolorMode = false;
        private static Rack _pendingRecolorRack = null;
        private static readonly List<Rack> _pendingRecolorRacks = new List<Rack>();
        // Mass buy mode (buy multiple empty slots at once)
        private static bool _massBuyMode = false;
        private static readonly List<RackMount> _pendingBuyMounts = new List<RackMount>();

        // Called from FloorManagerMod.OnInitializeMelon so rack color prefs load at game start.
        public static void InitPrefs()
        {
            if (_rackColorsEntry != null) return;
            var cat = MelonPreferences.CreateCategory("DCIM_RackColors");
            _rackColorsEntry = cat.CreateEntry("colors", "");
        }

        // Currently selected rack/device for drill-down
        public static Rack CurrentRack;
        public static int CurrentRackColumn;
        public static int CurrentRackIndex;
        public static Server CurrentServer;
        public static NetworkSwitch CurrentSwitch;
        public static PatchPanel CurrentPatchPanel;
        public static int CurrentCustomerID;

        public static void Build(GameObject root)
        {
            // Header bar
            var headerGo = new GameObject("Header");
            headerGo.transform.SetParent(root.transform, false);
            var headerRT = headerGo.AddComponent<RectTransform>();
            headerRT.anchorMin = new Vector2(0f, 1f);
            headerRT.anchorMax = new Vector2(1f, 1f);
            headerRT.pivot = new Vector2(0.5f, 1f);
            headerRT.sizeDelta = new Vector2(0f, 36f);
            headerRT.anchoredPosition = Vector2.zero;
            var headerBg = headerGo.AddComponent<Image>();
            headerBg.color = new Color(0.05f, 0.05f, 0.07f, 1f);

            // Back button
            var backBtn = UIHelper.BuildButton(headerGo.transform, "< Back", 80f);
            var backBtnRT = backBtn.gameObject.GetComponent<RectTransform>();
            backBtnRT.anchorMin = new Vector2(0f, 0f);
            backBtnRT.anchorMax = new Vector2(0f, 1f);
            backBtnRT.pivot = new Vector2(0f, 0.5f);
            backBtnRT.anchoredPosition = new Vector2(6f, 0f);
            backBtnRT.sizeDelta = new Vector2(80f, -4f);
            var backLE = backBtn.gameObject.GetComponent<LayoutElement>();
            if (backLE != null) Object.Destroy(backLE);
            var backNav = new Navigation();
            backNav.mode = Navigation.Mode.None;
            backBtn.navigation = backNav;
            backBtn.onClick.AddListener(new System.Action(OnBackPressed));

            // Header action button (context-dependent, far right)
            _headerActionBtn = UIHelper.BuildButton(headerGo.transform, "Action", 130f);
            var actionBtnRT = _headerActionBtn.gameObject.GetComponent<RectTransform>();
            actionBtnRT.anchorMin = new Vector2(1f, 0f);
            actionBtnRT.anchorMax = new Vector2(1f, 1f);
            actionBtnRT.pivot = new Vector2(1f, 0.5f);
            actionBtnRT.anchoredPosition = new Vector2(-6f, 0f);
            actionBtnRT.sizeDelta = new Vector2(130f, -4f);
            var actionLE = _headerActionBtn.gameObject.GetComponent<LayoutElement>();
            if (actionLE != null) Object.Destroy(actionLE);
            var actionNav = new Navigation();
            actionNav.mode = Navigation.Mode.None;
            _headerActionBtn.navigation = actionNav;
            _headerActionLabel = _headerActionBtn.GetComponentInChildren<TextMeshProUGUI>();
            _headerActionBtn.gameObject.SetActive(false);

            // Header title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleRT = titleGo.AddComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0f, 0f);
            titleRT.anchorMax = new Vector2(1f, 1f);
            titleRT.offsetMin = new Vector2(96f, 0f);
            titleRT.offsetMax = new Vector2(-142f, 0f);
            _headerText = titleGo.AddComponent<TextMeshProUGUI>();
            _headerText.text = "DCIM";
            _headerText.fontSize = 16f;
            _headerText.fontStyle = FontStyles.Bold;
            _headerText.color = Color.white;
            _headerText.alignment = TextAlignmentOptions.Left;

            // Tab bar — persistent navigation between top-level views
            _tabBar = new GameObject("TabBar");
            _tabBar.transform.SetParent(root.transform, false);
            var tbRT = _tabBar.AddComponent<RectTransform>();
            tbRT.anchorMin = new Vector2(0f, 1f);
            tbRT.anchorMax = new Vector2(1f, 1f);
            tbRT.pivot = new Vector2(0.5f, 1f);
            tbRT.anchoredPosition = new Vector2(0f, -36f);
            tbRT.sizeDelta = new Vector2(0f, 30f);
            _tabBar.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.055f, 1f);
            var tbHL = _tabBar.AddComponent<HorizontalLayoutGroup>();
            tbHL.childControlWidth = true;
            tbHL.childControlHeight = true;
            tbHL.childForceExpandWidth = true;
            tbHL.childForceExpandHeight = true;
            tbHL.spacing = 0f;
            tbHL.padding = new RectOffset();

            string[] tabNames   = { "Dashboard", "Floor Map", "Search", "Shop" };
            ViewState[] tabStates = { ViewState.Dashboard, ViewState.FloorMap, ViewState.SearchResults, ViewState.Shop };
            for (int ti = 0; ti < 4; ti++)
            {
                int capturedTi = ti;
                ViewState capturedTabState = tabStates[ti];
                var tabGo = new GameObject($"Tab_{tabNames[ti]}");
                tabGo.transform.SetParent(_tabBar.transform, false);
                var tabImg = tabGo.AddComponent<Image>();
                tabImg.color = new Color(0.04f, 0.04f, 0.055f, 1f);
                // Active underline indicator
                var indGo = new GameObject("Indicator");
                indGo.transform.SetParent(tabGo.transform, false);
                var indRT = indGo.AddComponent<RectTransform>();
                indRT.anchorMin = new Vector2(0f, 0f);
                indRT.anchorMax = new Vector2(1f, 0f);
                indRT.pivot = new Vector2(0.5f, 0f);
                indRT.anchoredPosition = Vector2.zero;
                indRT.sizeDelta = new Vector2(0f, 2f);
                var indImg = indGo.AddComponent<Image>();
                indImg.color = new Color(0.3f, 0.75f, 1f, 1f);
                indImg.raycastTarget = false;
                _tabIndicators[capturedTi] = indImg;
                indGo.SetActive(false);
                // Label
                var tabLblGo = new GameObject("Label");
                tabLblGo.transform.SetParent(tabGo.transform, false);
                var tabLblRT = tabLblGo.AddComponent<RectTransform>();
                tabLblRT.anchorMin = Vector2.zero;
                tabLblRT.anchorMax = Vector2.one;
                tabLblRT.offsetMin = Vector2.zero;
                tabLblRT.offsetMax = Vector2.zero;
                var tabLbl = tabLblGo.AddComponent<TextMeshProUGUI>();
                tabLbl.text = tabNames[capturedTi];
                tabLbl.fontSize = 11f;
                tabLbl.color = new Color(0.55f, 0.55f, 0.60f);
                tabLbl.alignment = TextAlignmentOptions.Center;
                tabLbl.raycastTarget = false;
                _tabLbls[capturedTi] = tabLbl;
                // Button
                var tabBtn = tabGo.AddComponent<Button>();
                tabBtn.targetGraphic = tabImg;
                var tabCb = new ColorBlock();
                tabCb.normalColor      = new Color(0.04f, 0.04f, 0.055f, 1f);
                tabCb.highlightedColor = new Color(0.09f, 0.09f, 0.11f, 1f);
                tabCb.pressedColor     = new Color(0.02f, 0.02f, 0.03f, 1f);
                tabCb.selectedColor    = tabCb.normalColor;
                tabCb.colorMultiplier  = 1f;
                tabCb.fadeDuration     = 0.1f;
                tabBtn.colors = tabCb;
                var tabNav = new Navigation(); tabNav.mode = Navigation.Mode.None;
                tabBtn.navigation = tabNav;
                tabBtn.onClick.AddListener(new System.Action(() => SwitchToState(capturedTabState)));
            }

            // Content area
            var contentGo = new GameObject("Content");
            contentGo.transform.SetParent(root.transform, false);
            var contentRT = contentGo.AddComponent<RectTransform>();
            contentRT.anchorMin = Vector2.zero;
            contentRT.anchorMax = Vector2.one;
            contentRT.offsetMin = new Vector2(0f, 0f);
            contentRT.offsetMax = new Vector2(0f, -66f); // 36px header + 30px tab bar
            _contentAreaRT = contentRT;
            var contentBg = contentGo.AddComponent<Image>();
            contentBg.color = new Color(0.08f, 0.08f, 0.10f, 1f);

            // Dashboard View (NEW home screen)
            _dashboardView = new GameObject("DashboardView");
            _dashboardView.transform.SetParent(contentGo.transform, false);
            SetFill(_dashboardView);
            DashboardView.Build(_dashboardView);
            _dashboardView.SetActive(false);

            // Floor Map View
            _floorMapView = new GameObject("FloorMapView");
            _floorMapView.transform.SetParent(contentGo.transform, false);
            SetFill(_floorMapView);
            FloorMapView.Build(_floorMapView);
            _floorMapView.SetActive(false);

            // Device List View
            _deviceListView = new GameObject("DeviceListView");
            _deviceListView.transform.SetParent(contentGo.transform, false);
            SetFill(_deviceListView);
            DeviceListView.Build(_deviceListView);
            _deviceListView.SetActive(false);

            // Device Config View
            _deviceConfigView = new GameObject("DeviceConfigView");
            _deviceConfigView.transform.SetParent(contentGo.transform, false);
            SetFill(_deviceConfigView);
            DeviceConfigPanel.Build(_deviceConfigView);
            _deviceConfigView.SetActive(false);

            // Customer IP View
            _customerIPView = new GameObject("CustomerIPView");
            _customerIPView.transform.SetParent(contentGo.transform, false);
            SetFill(_customerIPView);
            CustomerIPView.Build(_customerIPView);
            _customerIPView.SetActive(false);
            // Parent popup to the canvas (fmScreen's parent) so it covers the header too
            var popupParent = root.transform.parent != null ? root.transform.parent.gameObject : root;
            CustomerIPView.InitPopup(popupParent);

            // Search Results View (NEW)
            _searchResultsView = new GameObject("SearchResultsView");
            _searchResultsView.transform.SetParent(contentGo.transform, false);
            SetFill(_searchResultsView);
            SearchResultsView.Build(_searchResultsView);
            _searchResultsView.SetActive(false);

            // Color legend (persistent, visible on FloorMap only)
            BuildColorLegend(root);

            // Buy rack popup
            BuildBuyRackPopup(root);

            // When mini shop is closed from the Shop tab, restore the Dashboard tab.
            RackDiagramPanel.OnMiniShopClosed = () =>
            {
                if (_currentState == ViewState.Shop)
                    SwitchToState(ViewState.Dashboard);
            };
        }

        public static void OnAppOpened()
        {
            // Re-sync DCIMScreen RT to mainScreen every open — the canvas is
            // full-screen so anchor-fill goes full-screen; mainScreen's RT defines
            // the laptop panel area and must be copied instead.
            if (FloorManagerMod.DCIMScreen != null && FloorManagerMod.MainScreenRef != null)
            {
                var rt  = FloorManagerMod.DCIMScreen.GetComponent<RectTransform>();
                var src = FloorManagerMod.MainScreenRef.GetComponent<RectTransform>();
                if (rt != null && src != null)
                    Patches.ComputerShopAwakePatch.CopyRT(rt, src);
            }
            SwitchToState(ViewState.Dashboard);
        }

        public static void SwitchToState(ViewState state)
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            // Hide mini shop when navigating away from Shop tab
            if (_currentState == ViewState.Shop && state != ViewState.Shop)
                RackDiagramPanel.HideMiniShop();

            _currentState = state;
            UpdateTabBar(state);
            _dashboardView.SetActive(state == ViewState.Dashboard || state == ViewState.Shop);
            _floorMapView.SetActive(state == ViewState.FloorMap);
            _deviceListView.SetActive(state == ViewState.DeviceList);
            _deviceConfigView.SetActive(state == ViewState.DeviceConfig);
            _customerIPView.SetActive(state == ViewState.CustomerIPs);
            _searchResultsView.SetActive(state == ViewState.SearchResults);
            _colorLegend.SetActive(state == ViewState.FloorMap);

            _headerActionBtn.gameObject.SetActive(false);
            _headerActionBtn.onClick.RemoveAllListeners();

            switch (state)
            {
                case ViewState.Dashboard:
                    _headerText.text = "DCIM";
                    DashboardView.Populate();
                    break;
                case ViewState.FloorMap:
                    _headerText.text = "Floor Map";
                    FloorMapView.Refresh();
                    break;
                case ViewState.DeviceList:
                    if (CurrentRackColumn < 0)
                        _headerText.text = "Multiple Racks";
                    else
                        _headerText.text = $"R{CurrentRackColumn}/{CurrentRackIndex}";
                    if (CurrentRack != null)
                        DeviceListView.Populate(CurrentRack);
                    _headerActionBtn.gameObject.SetActive(true);
                    _headerActionLabel.text = "Assign Customer";
                    _headerActionBtn.onClick.AddListener(new System.Action(() =>
                    {
                        DeviceListView.OnAssignCustomerClicked();
                    }));
                    break;
                case ViewState.DeviceConfig:
                    if (CurrentServer != null)
                    {
                        string name = UIHelper.GetServerTypeName(CurrentServer.gameObject.name);
                        _headerText.text = name;
                    }
                    else if (CurrentSwitch != null)
                    {
                        string name = MainGameManager.instance.ReturnSwitchNameFromType(CurrentSwitch.switchType);
                        _headerText.text = name;
                        _headerActionBtn.gameObject.SetActive(true);
                        _headerActionLabel.text = "Configure LACP";
                        NetworkSwitch capturedSw = CurrentSwitch;
                        _headerActionBtn.onClick.AddListener(new System.Action(() =>
                        {
                            OpenLACPConfig(capturedSw);
                        }));
                    }
                    else if (CurrentPatchPanel != null)
                    {
                        _headerText.text = "Patch Panel";
                    }
                    DeviceConfigPanel.Populate(CurrentServer, CurrentSwitch, CurrentPatchPanel);
                    break;
                case ViewState.CustomerIPs:
                    var ci = MainGameManager.instance.GetCustomerItemByID(CurrentCustomerID);
                    _headerText.text = ci != null ? ci.customerName : $"Customer {CurrentCustomerID}";
                    CustomerIPView.Populate(CurrentCustomerID);
                    _headerActionBtn.gameObject.SetActive(true);
                    _headerActionLabel.text = "Add Server";
                    int capturedCustID = CurrentCustomerID;
                    _headerActionBtn.onClick.AddListener(new System.Action(() =>
                    {
                        CustomerIPView.ShowAddServerPopup(capturedCustID);
                    }));
                    break;
                case ViewState.SearchResults:
                    _headerText.text = "Search Results";
                    SearchResultsView.Populate();
                    break;
                case ViewState.Shop:
                    _headerText.text = "Buy Device";
                    RackDiagramPanel.ShowMiniShop();
                    break;
            }
        }

        private static void UpdateTabBar(ViewState state)
        {
            bool showTabs = state == ViewState.Dashboard
                         || state == ViewState.FloorMap
                         || state == ViewState.SearchResults
                         || state == ViewState.Shop;
            _tabBar.SetActive(showTabs);
            _contentAreaRT.offsetMax = new Vector2(0f, showTabs ? -66f : -36f);

            ViewState[] tabStates = { ViewState.Dashboard, ViewState.FloorMap, ViewState.SearchResults, ViewState.Shop };
            for (int i = 0; i < 4; i++)
            {
                bool active = tabStates[i] == state;
                if (_tabIndicators[i] != null) _tabIndicators[i].gameObject.SetActive(active);
                if (_tabLbls[i] != null)
                    _tabLbls[i].color = active ? Color.white : new Color(0.55f, 0.55f, 0.60f);
            }
        }

        private static void OnBackPressed()
        {
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);

            switch (_currentState)
            {
                case ViewState.Dashboard:
                    // Back to laptop main screen
                    FloorManagerMod.DCIMScreen.SetActive(false);
                    var cs = FloorManagerMod.ComputerShopRef;
                    if (cs != null)
                        cs.mainScreen.SetActive(true);
                    break;
                case ViewState.FloorMap:
                    SwitchToState(ViewState.Dashboard);
                    break;
                case ViewState.DeviceList:
                    CurrentRack = null;
                    SwitchToState(ViewState.FloorMap);
                    break;
                case ViewState.DeviceConfig:
                    CurrentServer = null;
                    CurrentSwitch = null;
                    CurrentPatchPanel = null;
                    if (_previousState == ViewState.SearchResults)
                    {
                        // Repopulate for fresh security tags, then restore scroll position
                        SwitchToState(ViewState.SearchResults);
                        SearchResultsView.RestoreView();
                    }
                    else if (_previousState == ViewState.CustomerIPs)
                        SwitchToState(ViewState.CustomerIPs);
                    else if (_previousState == ViewState.FloorMap)
                        SwitchToState(ViewState.FloorMap);
                    else
                        SwitchToState(ViewState.DeviceList);
                    break;
                case ViewState.CustomerIPs:
                    SwitchToState(ViewState.Dashboard);
                    break;
                case ViewState.SearchResults:
                    SwitchToState(ViewState.Dashboard);
                    break;
                case ViewState.Shop:
                    RackDiagramPanel.HideMiniShop();
                    SwitchToState(ViewState.Dashboard);
                    break;
            }
        }

        public static void OpenRack(Rack rack, int rowNum, int posNum)
        {
            CurrentRack = rack;
            CurrentRackColumn = rowNum;
            CurrentRackIndex = posNum;
            SwitchToState(ViewState.DeviceList);
        }

        public static void OpenDevice(Server server, NetworkSwitch sw, PatchPanel pp)
        {
            CurrentServer = server;
            CurrentSwitch = sw;
            CurrentPatchPanel = pp;
            _previousState = ViewState.DeviceList;
            SwitchToState(ViewState.DeviceConfig);
        }

        public static void OpenDeviceFromSearch(Server server, NetworkSwitch sw, PatchPanel pp)
        {
            CurrentServer = server;
            CurrentSwitch = sw;
            CurrentPatchPanel = pp;
            _previousState = ViewState.SearchResults;
            SearchResultsView.SaveScrollPosition();
            SwitchToState(ViewState.DeviceConfig);
        }

        public static void OpenDeviceFromCustomer(Server server)
        {
            CurrentServer = server;
            CurrentSwitch = null;
            CurrentPatchPanel = null;
            _previousState = ViewState.CustomerIPs;
            SwitchToState(ViewState.DeviceConfig);
        }

        public static void OpenDeviceFromDiagram(Server server, NetworkSwitch sw)
        {
            CurrentServer = server;
            CurrentSwitch = sw;
            CurrentPatchPanel = null;
            _previousState = ViewState.FloorMap;
            SwitchToState(ViewState.DeviceConfig);
        }

        public static void OpenMultiRackDevices(List<Rack> racks)
        {
            CurrentRack = null;
            CurrentRackColumn = -1;
            SwitchToState(ViewState.DeviceList);
            DeviceListView.PopulateMultiRack(racks);
        }

        public static void OpenCustomerIPs(int customerID)
        {
            CurrentCustomerID = customerID;
            SwitchToState(ViewState.CustomerIPs);
        }

        public static void OpenSearchResults(SearchEngine.DeviceTypeFilter typeFilter, SearchEngine.StatusFilter statusFilter, int customerFilter)
        {
            SearchResultsView.SetFilters(typeFilter, statusFilter, customerFilter);
            SwitchToState(ViewState.SearchResults);
        }

        public static void UpdateHeaderActionLabel(string text)
        {
            if (_headerActionLabel != null)
                _headerActionLabel.text = text;
        }

        // ── Buy Rack Popup ──────────────────────────────────────────

        public static void ShowBuyRackPopup(RackMount rackMount)
        {
            _recolorMode = false;
            _pendingRecolorRack = null;
            _pendingBuyMount = rackMount;

            int price = SearchEngine.GetRackPrice();
            float money = PlayerManager.instance.playerClass.money;
            _buyRackPriceLabel.text = $"Build Rack?\nCost: ${price}\nBalance: ${money:F0}";
            if (_confirmBtnLbl != null) _confirmBtnLbl.text = "Confirm";

            RefreshSwatches();
            // Reset to white (H=0, S=0, V=100)
            _buyRackSelectedColor = null;
            SetSliderHSV(0f, 0f, 100f);
            if (_colorPreviewImg != null) _colorPreviewImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            for (int i = 0; i < 8; i++)
                if (_buyRackSaveSlotBorders[i] != null) _buyRackSaveSlotBorders[i].color = Color.clear;

            _buyRackPopup.SetActive(true);
        }

        public static void ShowRecolorPopup(Rack rack)
        {
            _recolorMode = true;
            _pendingRecolorRack = rack;
            _pendingBuyMount = null;

            _buyRackPriceLabel.text = "Recolor Rack";
            if (_confirmBtnLbl != null) _confirmBtnLbl.text = "Apply Color";

            RefreshSwatches();

            // Pre-load current rack color
            Color current = Color.white;
            var mount = rack.gameObject.GetComponentInParent<RackMount>();
            if (mount != null)
            {
                string key = $"{mount.transform.position.x:F1},{mount.transform.position.z:F1}";
                string data = _rackColorsEntry?.Value ?? "";
                var parts = data.Split(';');
                for (int i = 0; i < parts.Length; i++)
                {
                    if (parts[i].StartsWith(key + ","))
                    {
                        string hex = parts[i].Substring(key.Length + 1);
                        if (ColorUtility.TryParseHtmlString("#" + hex, out Color saved))
                            current = saved;
                        break;
                    }
                }
            }

            _buyRackSelectedColor = current;
            Color.RGBToHSV(current, out float h, out float s, out float v);
            SetSliderHSV(h * 360f, s * 100f, v * 100f);
            if (_colorPreviewImg != null) _colorPreviewImg.color = current;
            for (int i = 0; i < 8; i++)
                if (_buyRackSaveSlotBorders[i] != null) _buyRackSaveSlotBorders[i].color = Color.clear;

            _buyRackPopup.SetActive(true);
        }

        public static void ShowMassRecolorPopup(List<Rack> racks)
        {
            if (racks == null || racks.Count == 0) return;
            _recolorMode = true;
            _pendingRecolorRack = null;
            _pendingRecolorRacks.Clear();
            for (int i = 0; i < racks.Count; i++)
                if (racks[i] != null) _pendingRecolorRacks.Add(racks[i]);
            _pendingBuyMount = null;

            _buyRackPriceLabel.text = $"Recolor {_pendingRecolorRacks.Count} Racks";
            if (_confirmBtnLbl != null) _confirmBtnLbl.text = "Apply Color";

            RefreshSwatches();
            _buyRackSelectedColor = null;
            SetSliderHSV(0f, 0f, 100f);
            if (_colorPreviewImg != null) _colorPreviewImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            for (int i = 0; i < 8; i++)
                if (_buyRackSaveSlotBorders[i] != null) _buyRackSaveSlotBorders[i].color = Color.clear;

            _buyRackPopup.SetActive(true);
        }

        public static void ShowMassBuyPopup(List<RackMount> mounts)
        {
            if (mounts == null || mounts.Count == 0) return;
            _massBuyMode = true;
            _recolorMode = false;
            _pendingBuyMount = null;
            _pendingBuyMounts.Clear();
            for (int i = 0; i < mounts.Count; i++)
                if (mounts[i] != null) _pendingBuyMounts.Add(mounts[i]);

            int price = SearchEngine.GetRackPrice();
            int total = price * _pendingBuyMounts.Count;
            float money = PlayerManager.instance.playerClass.money;
            _buyRackPriceLabel.text = $"Build {_pendingBuyMounts.Count} racks?\nCost: ${total}  Balance: ${money:F0}";
            if (_confirmBtnLbl != null) _confirmBtnLbl.text = "Build All";

            RefreshSwatches();
            _buyRackSelectedColor = null;
            SetSliderHSV(0f, 0f, 100f);
            if (_colorPreviewImg != null) _colorPreviewImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
            for (int i = 0; i < 8; i++)
                if (_buyRackSaveSlotBorders[i] != null) _buyRackSaveSlotBorders[i].color = Color.clear;

            _buyRackPopup.SetActive(true);
        }

        private static void RefreshSwatches()
        {
            for (int i = 0; i < 8; i++)
            {
                Color c = RackDiagramPanel.GetFavoriteColor(i);
                if (_buyRackSwatchImgs[i] != null) _buyRackSwatchImgs[i].color = c;
                if (_buyRackSwatchBorders[i] != null) _buyRackSwatchBorders[i].color = Color.clear;
            }
        }

        private static void OnBuyRackConfirm()
        {
            if (_recolorMode)
            {
                OnRecolorConfirm();
                return;
            }

            if (_massBuyMode)
            {
                OnMassBuyConfirm();
                return;
            }

            if (_pendingBuyMount == null)
            {
                _buyRackPopup.SetActive(false);
                return;
            }

            int price = SearchEngine.GetRackPrice();
            float money = PlayerManager.instance.playerClass.money;

            if (money < price)
            {
                StaticUIElements.instance.AddMeesageInField("Insufficient funds to build rack");
                _buyRackPopup.SetActive(false);
                return;
            }

            // Deduct money
            PlayerManager.instance.playerClass.UpdateCoin(-price, false);

            // Place rack
            _pendingBuyMount.InstantiateRack(null);

            // Apply custom color after coroutine has a chance to instantiate the rack.
            // If no color was chosen, clear any stale saved entry so a rebuild doesn't
            // inherit the old color after a save/load.
            if (_buyRackSelectedColor.HasValue)
            {
                Color rackColor = _buyRackSelectedColor.Value;
                RackMount capturedMount = _pendingBuyMount;
                MelonCoroutines.Start(ApplyRackColorDelayed(capturedMount, rackColor));
            }
            else
            {
                RemoveRackColor(_pendingBuyMount.transform.position);
            }

            StaticUIElements.instance.AddMeesageInField($"Rack built for ${price}");

            _buyRackPopup.SetActive(false);
            _pendingBuyMount = null;
            _buyRackSelectedColor = null;

            // Refresh map + 3D labels
            RackLabelManager.RefreshAllLabels();
            FloorMapView.Refresh();
        }

        private static void OnMassBuyConfirm()
        {
            _massBuyMode = false;

            if (_pendingBuyMounts.Count == 0)
            {
                _buyRackPopup.SetActive(false);
                return;
            }

            int price = SearchEngine.GetRackPrice();
            int total = price * _pendingBuyMounts.Count;
            float money = PlayerManager.instance.playerClass.money;

            if (money < total)
            {
                StaticUIElements.instance.AddMeesageInField($"Insufficient funds (need ${total}, have ${money:F0})");
                _buyRackPopup.SetActive(false);
                _pendingBuyMounts.Clear();
                _buyRackSelectedColor = null;
                return;
            }

            // Close popup immediately; racks build one-per-frame via coroutine to avoid stutter
            _buyRackPopup.SetActive(false);
            var mounts = new List<RackMount>(_pendingBuyMounts);
            _pendingBuyMounts.Clear();
            Color? chosenColor = _buyRackSelectedColor;
            _buyRackSelectedColor = null;

            MelonCoroutines.Start(MassBuyCoroutine(mounts, price, chosenColor));
        }

        private static IEnumerator MassBuyCoroutine(List<RackMount> mounts, int price, Color? chosenColor)
        {
            int built = 0;
            for (int i = 0; i < mounts.Count; i++)
            {
                var mount = mounts[i];
                if (mount == null) { yield return null; continue; }
                PlayerManager.instance.playerClass.UpdateCoin(-price, false);
                mount.InstantiateRack(null);
                if (chosenColor.HasValue)
                {
                    Color c = chosenColor.Value;
                    RackMount capturedMount = mount;
                    MelonCoroutines.Start(ApplyRackColorDelayed(capturedMount, c));
                }
                else
                {
                    RemoveRackColor(mount.transform.position);
                }
                built++;
                yield return null; // one rack per frame — no stutter
            }
            if (StaticUIElements.instance != null)
                StaticUIElements.instance.AddMeesageInField($"Built {built} rack(s) for ${price * built}");
            RackLabelManager.RefreshAllLabels();
            FloorMapView.Refresh();
        }

        private static void OnRecolorConfirm()
        {
            _buyRackPopup.SetActive(false);

            // Build the list of racks to recolor (single or mass)
            var targets = new List<Rack>();
            if (_pendingRecolorRack != null) targets.Add(_pendingRecolorRack);
            for (int i = 0; i < _pendingRecolorRacks.Count; i++)
                if (_pendingRecolorRacks[i] != null) targets.Add(_pendingRecolorRacks[i]);

            _pendingRecolorRack = null;
            _pendingRecolorRacks.Clear();
            _recolorMode = false;

            if (targets.Count == 0) return;

            if (_buyRackSelectedColor.HasValue)
            {
                Color color = _buyRackSelectedColor.Value;
                _buyRackSelectedColor = null;

                for (int t = 0; t < targets.Count; t++)
                {
                    var rack = targets[t];
                    var renderers = rack.gameObject.GetComponentsInChildren<MeshRenderer>();
                    for (int i = 0; i < renderers.Length; i++)
                        renderers[i].material.color = color;
                    var mount = rack.gameObject.GetComponentInParent<RackMount>();
                    if (mount != null) SaveRackColor(mount.transform.position, color);
                }

                string msg = targets.Count == 1 ? "Rack color updated" : $"{targets.Count} racks recolored";
                StaticUIElements.instance.AddMeesageInField(msg);
                FloorMapView.Refresh();
            }
            else
            {
                // No color — remove saved entries and reset renderers
                for (int t = 0; t < targets.Count; t++)
                {
                    var rack = targets[t];
                    var mount = rack.gameObject.GetComponentInParent<RackMount>();
                    if (mount != null) RemoveRackColor(mount.transform.position);
                    var renderers = rack.gameObject.GetComponentsInChildren<MeshRenderer>();
                    for (int i = 0; i < renderers.Length; i++)
                        renderers[i].material.color = Color.white;
                }

                string msg = targets.Count == 1 ? "Rack color reset" : $"{targets.Count} rack colors reset";
                StaticUIElements.instance.AddMeesageInField(msg);
                FloorMapView.Refresh();
            }
        }

        private static IEnumerator ApplyRackColorDelayed(RackMount mount, Color color)
        {
            // Wait a few frames for InstantiateRack coroutine to place the rack GO
            yield return null;
            yield return null;
            yield return null;

            if (mount == null) yield break;
            var rack = mount.GetComponentInChildren<Rack>();
            if (rack == null) yield break;

            var renderers = rack.gameObject.GetComponentsInChildren<MeshRenderer>();
            for (int i = 0; i < renderers.Length; i++)
                renderers[i].material.color = color;

            // Persist so color survives save/load
            SaveRackColor(mount.transform.position, color);
        }

        private static void SaveRackColor(Vector3 worldPos, Color color)
        {
            if (_rackColorsEntry == null) return;
            string newEntry = $"{worldPos.x:F1},{worldPos.z:F1},{ColorUtility.ToHtmlStringRGB(color)}";
            string existing = _rackColorsEntry.Value ?? "";
            string prefix = $"{worldPos.x:F1},{worldPos.z:F1},";
            var kept = new System.Text.StringBuilder();
            var parts = existing.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0 && !parts[i].StartsWith(prefix))
                {
                    if (kept.Length > 0) kept.Append(';');
                    kept.Append(parts[i]);
                }
            }
            if (kept.Length > 0) kept.Append(';');
            kept.Append(newEntry);
            _rackColorsEntry.Value = kept.ToString();
            MelonPreferences.Save();
        }

        private static void RemoveRackColor(Vector3 worldPos)
        {
            if (_rackColorsEntry == null) return;
            string prefix = $"{worldPos.x:F1},{worldPos.z:F1},";
            string existing = _rackColorsEntry.Value ?? "";
            var kept = new System.Text.StringBuilder();
            var parts = existing.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length > 0 && !parts[i].StartsWith(prefix))
                {
                    if (kept.Length > 0) kept.Append(';');
                    kept.Append(parts[i]);
                }
            }
            _rackColorsEntry.Value = kept.ToString();
            MelonPreferences.Save();
        }

        public static void RestoreRackColors()
        {
            if (_rackColorsEntry == null) return;
            string data = _rackColorsEntry.Value ?? "";
            if (data.Length == 0) return;

            // Build position → color lookup
            var map = new Dictionary<string, Color>();
            var entries = data.Split(';');
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].Length == 0) continue;
                var p = entries[i].Split(',');
                if (p.Length < 3) continue;
                string posKey = $"{p[0]},{p[1]}";
                if (ColorUtility.TryParseHtmlString("#" + p[2], out Color c))
                    map[posKey] = c;
            }
            if (map.Count == 0) return;

            var racks = Object.FindObjectsOfType<Rack>();
            for (int i = 0; i < racks.Length; i++)
            {
                var rack = racks[i];
                var mountParent = rack.GetComponentInParent<RackMount>();
                if (mountParent == null) continue;
                Vector3 pos = mountParent.transform.position;
                string key = $"{pos.x:F1},{pos.z:F1}";
                if (!map.ContainsKey(key)) continue;
                var renderers = rack.gameObject.GetComponentsInChildren<MeshRenderer>();
                for (int ri = 0; ri < renderers.Length; ri++)
                    renderers[ri].material.color = map[key];
            }
        }

        // Sets H/S/V sliders WITHOUT triggering the onValueChanged chain (avoids feedback loops)
        private static bool _suppressHSVUpdate = false;

        private static void SetSliderHSV(float h, float s, float v)
        {
            _suppressHSVUpdate = true;
            if (_hSlider != null) { _hSlider.value = h; if (_hValLbl != null) _hValLbl.text = ((int)h).ToString() + "°"; }
            if (_sSlider != null) { _sSlider.value = s; if (_sValLbl != null) _sValLbl.text = ((int)s).ToString() + "%"; }
            if (_vSlider != null) { _vSlider.value = v; if (_vValLbl != null) _vValLbl.text = ((int)v).ToString() + "%"; }
            _suppressHSVUpdate = false;
            UpdateHSVDerivedVisuals();
        }

        private static void OnHSVSliderChanged()
        {
            if (_suppressHSVUpdate) return;
            if (_hSlider == null) return;
            if (_hValLbl != null) _hValLbl.text = ((int)_hSlider.value).ToString() + "°";
            if (_sValLbl != null) _sValLbl.text = ((int)_sSlider.value).ToString() + "%";
            if (_vValLbl != null) _vValLbl.text = ((int)_vSlider.value).ToString() + "%";
            UpdateHSVDerivedVisuals();
        }

        private static void UpdateHSVDerivedVisuals()
        {
            if (_hSlider == null) return;
            float h = _hSlider.value / 360f;
            float s = _sSlider.value / 100f;
            float v = _vSlider.value / 100f;
            Color result = Color.HSVToRGB(h, s, v);
            _buyRackSelectedColor = result;

            // Update S slider gradient: white → pure hue
            Color hueColor = Color.HSVToRGB(h, 1f, 1f);
            UpdateGradientTexture(_satGradTex, Color.white, hueColor);
            // Update V slider gradient: black → full-sat hue
            UpdateGradientTexture(_valGradTex, Color.black, hueColor);

            if (_colorPreviewImg != null) _colorPreviewImg.color = result;

            if (_hexInputField != null && !_hexInputField.isFocused)
                _hexInputField.text = ColorUtility.ToHtmlStringRGB(result);

            for (int k = 0; k < 8; k++)
                if (_buyRackSwatchBorders[k] != null) _buyRackSwatchBorders[k].color = Color.clear;
        }

        private static void UpdateGradientTexture(Texture2D tex, Color left, Color right)
        {
            if (tex == null) return;
            int w = tex.width;
            int h = tex.height;
            for (int x = 0; x < w; x++)
            {
                Color c = Color.Lerp(left, right, x / (float)(w - 1));
                for (int y = 0; y < h; y++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
        }

        private static Texture2D BuildHueGradientTexture()
        {
            int w = 256;
            var tex = new Texture2D(w, 4, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            for (int x = 0; x < w; x++)
            {
                Color c = Color.HSVToRGB(x / (float)(w - 1), 1f, 1f);
                for (int y = 0; y < 4; y++)
                    tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return tex;
        }

        private static Sprite TextureToSprite(Texture2D tex)
            => Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));

        private static void OnHexInputConfirmed(string value)
        {
            string hex = value.TrimStart('#');
            if (hex.Length == 6 && ColorUtility.TryParseHtmlString("#" + hex, out Color parsed))
            {
                Color.RGBToHSV(parsed, out float h, out float s, out float v);
                SetSliderHSV(h * 360f, s * 100f, v * 100f);
            }
            else if (_hexInputField != null && _buyRackSelectedColor.HasValue)
            {
                // Restore valid value if bad input
                _hexInputField.text = ColorUtility.ToHtmlStringRGB(_buyRackSelectedColor.Value);
            }
        }

        // Builds a single labeled HSV slider row.
        // gradientTex: if provided, used as the slider track background (stretched).
        //              Pass null to use a plain dark background.
        // outBgImg: receives the background Image so gradients can be updated later.
        private static Slider BuildHSVSlider(Transform parent, string label, Color labelColor,
                                             Texture2D gradientTex, out Image outBgImg,
                                             out TextMeshProUGUI valLbl,
                                             float maxValue, float defaultValue)
        {
            var row = new GameObject($"Row_{label}");
            row.transform.SetParent(parent, false);
            var rowHL = row.AddComponent<HorizontalLayoutGroup>();
            rowHL.childControlWidth = false;
            rowHL.childControlHeight = true;
            rowHL.childForceExpandWidth = false;
            rowHL.childForceExpandHeight = false;
            rowHL.spacing = 6f;
            rowHL.childAlignment = TextAnchor.MiddleLeft;
            row.AddComponent<LayoutElement>().preferredHeight = 22f;

            // Label
            var nameLblGo = new GameObject("Name");
            nameLblGo.transform.SetParent(row.transform, false);
            nameLblGo.AddComponent<LayoutElement>().preferredWidth = 16f;
            var nameTmp = nameLblGo.AddComponent<TextMeshProUGUI>();
            nameTmp.text = label;
            nameTmp.fontSize = 10f;
            nameTmp.color = labelColor;
            nameTmp.alignment = TextAlignmentOptions.MidlineRight;

            // Track container (rounded-looking via thin vertical margin)
            var trackContainer = new GameObject("Track");
            trackContainer.transform.SetParent(row.transform, false);
            var trackLE = trackContainer.AddComponent<LayoutElement>();
            trackLE.preferredWidth = 150f;
            trackLE.preferredHeight = 14f;

            var sliderBg = trackContainer.AddComponent<Image>();
            if (gradientTex != null)
            {
                sliderBg.sprite = TextureToSprite(gradientTex);
                sliderBg.color = Color.white;
                sliderBg.type = Image.Type.Simple;
            }
            else
            {
                sliderBg.color = new Color(0.18f, 0.18f, 0.22f, 1f);
            }
            outBgImg = sliderBg;

            // Fill (semi-transparent dark overlay — shows "selected" region on gradient)
            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(trackContainer.transform, false);
            var fillAreaRT = fillArea.AddComponent<RectTransform>();
            fillAreaRT.anchorMin = new Vector2(0f, 0.15f);
            fillAreaRT.anchorMax = new Vector2(1f, 0.85f);
            fillAreaRT.offsetMin = new Vector2(5f, 0f);
            fillAreaRT.offsetMax = new Vector2(-5f, 0f);

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRT = fill.AddComponent<RectTransform>();
            fillRT.anchorMin = Vector2.zero;
            fillRT.anchorMax = new Vector2(0f, 1f);
            fillRT.sizeDelta = new Vector2(0f, 0f);
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0f, 0f, 0f, 0.25f);

            // Handle slide area
            var handleArea = new GameObject("Handle Slide Area");
            handleArea.transform.SetParent(trackContainer.transform, false);
            var handleAreaRT = handleArea.AddComponent<RectTransform>();
            handleAreaRT.anchorMin = Vector2.zero;
            handleAreaRT.anchorMax = Vector2.one;
            handleAreaRT.offsetMin = new Vector2(7f, 0f);
            handleAreaRT.offsetMax = new Vector2(-7f, 0f);

            var handle = new GameObject("Handle");
            handle.transform.SetParent(handleArea.transform, false);
            var handleRT = handle.AddComponent<RectTransform>();
            handleRT.sizeDelta = new Vector2(12f, 0f);
            var handleImg = handle.AddComponent<Image>();
            handleImg.color = Color.white;

            var slider = trackContainer.AddComponent<Slider>();
            slider.fillRect = fillRT;
            slider.handleRect = handleRT;
            slider.targetGraphic = handleImg;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = maxValue;
            slider.wholeNumbers = true;
            slider.value = defaultValue;
            var sNav = new Navigation();
            sNav.mode = Navigation.Mode.None;
            slider.navigation = sNav;
            slider.onValueChanged.AddListener(new System.Action<float>(_ => OnHSVSliderChanged()));

            // Value label
            var valLblGo = new GameObject("ValLbl");
            valLblGo.transform.SetParent(row.transform, false);
            valLblGo.AddComponent<LayoutElement>().preferredWidth = 36f;
            valLbl = valLblGo.AddComponent<TextMeshProUGUI>();
            valLbl.text = ((int)defaultValue).ToString();
            valLbl.fontSize = 10f;
            valLbl.color = new Color(0.85f, 0.85f, 0.85f, 1f);
            valLbl.alignment = TextAlignmentOptions.MidlineLeft;

            return slider;
        }

        private static TMP_InputField BuildHexInputField(Transform parent)
        {
            var go = new GameObject("HexInput");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(78f, 22f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.12f, 0.15f, 1f);

            var textArea = new GameObject("Text Area");
            textArea.transform.SetParent(go.transform, false);
            var taRT = textArea.AddComponent<RectTransform>();
            taRT.anchorMin = Vector2.zero;
            taRT.anchorMax = Vector2.one;
            taRT.offsetMin = new Vector2(5f, 2f);
            taRT.offsetMax = new Vector2(-5f, -2f);
            textArea.AddComponent<RectMask2D>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(textArea.transform, false);
            var textRT = textGo.AddComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.sizeDelta = Vector2.zero;
            var textComp = textGo.AddComponent<TextMeshProUGUI>();
            textComp.fontSize = 11f;
            textComp.color = Color.white;
            textComp.alignment = TextAlignmentOptions.MidlineLeft;

            var phGo = new GameObject("Placeholder");
            phGo.transform.SetParent(textArea.transform, false);
            var phRT = phGo.AddComponent<RectTransform>();
            phRT.anchorMin = Vector2.zero;
            phRT.anchorMax = Vector2.one;
            phRT.sizeDelta = Vector2.zero;
            var phText = phGo.AddComponent<TextMeshProUGUI>();
            phText.text = "RRGGBB";
            phText.fontSize = 10f;
            phText.color = new Color(0.45f, 0.45f, 0.45f, 1f);
            phText.fontStyle = FontStyles.Italic;
            phText.alignment = TextAlignmentOptions.MidlineLeft;

            var field = go.AddComponent<TMP_InputField>();
            field.textViewport = taRT;
            field.textComponent = textComp;
            field.placeholder = phText;
            field.characterLimit = 7;
            field.text = "FFFFFF";
            field.onEndEdit.AddListener(new System.Action<string>(OnHexInputConfirmed));

            return field;
        }

        private static void BuildBuyRackPopup(GameObject root)
        {
            _buyRackPopup = new GameObject("BuyRackPopup");
            _buyRackPopup.transform.SetParent(root.transform, false);
            var overlayRT = _buyRackPopup.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            _buyRackPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            // Panel — taller to accommodate sliders
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_buyRackPopup.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.15f, 0.05f);
            panelRT.anchorMax = new Vector2(0.85f, 0.95f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            panel.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 1f);

            var panelVL = panel.AddComponent<VerticalLayoutGroup>();
            panelVL.childControlWidth = true;
            panelVL.childControlHeight = true;
            panelVL.childForceExpandWidth = true;
            panelVL.childForceExpandHeight = false;
            panelVL.spacing = 7f;
            var panelPad = new RectOffset();
            panelPad.left = 16; panelPad.right = 16; panelPad.top = 14; panelPad.bottom = 14;
            panelVL.padding = panelPad;

            // ── Price label ──
            var priceLblGo = new GameObject("PriceLabel");
            priceLblGo.transform.SetParent(panel.transform, false);
            _buyRackPriceLabel = priceLblGo.AddComponent<TextMeshProUGUI>();
            _buyRackPriceLabel.text = "";
            _buyRackPriceLabel.fontSize = 13f;
            _buyRackPriceLabel.color = Color.white;
            _buyRackPriceLabel.alignment = TextAlignmentOptions.Center;
            priceLblGo.AddComponent<LayoutElement>().preferredHeight = 52f;

            // ── Swatch row label ──
            var favLblGo = new GameObject("FavLabel");
            favLblGo.transform.SetParent(panel.transform, false);
            var favLbl = favLblGo.AddComponent<TextMeshProUGUI>();
            favLbl.text = "Favorites:";
            favLbl.fontSize = 10f;
            favLbl.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            favLbl.alignment = TextAlignmentOptions.Center;
            favLblGo.AddComponent<LayoutElement>().preferredHeight = 14f;

            // ── Swatch row ──
            var swatchRow = new GameObject("SwatchRow");
            swatchRow.transform.SetParent(panel.transform, false);
            var swatchHL = swatchRow.AddComponent<HorizontalLayoutGroup>();
            swatchHL.childControlWidth = false;
            swatchHL.childControlHeight = false;
            swatchHL.childForceExpandWidth = false;
            swatchHL.childForceExpandHeight = false;
            swatchHL.spacing = 6f;
            swatchHL.childAlignment = TextAnchor.MiddleCenter;
            swatchRow.AddComponent<LayoutElement>().preferredHeight = 26f;

            for (int si = 0; si < 8; si++)
            {
                int capturedSI = si;

                var outer = new GameObject($"Swatch_{si}");
                outer.transform.SetParent(swatchRow.transform, false);
                outer.AddComponent<RectTransform>().sizeDelta = new Vector2(24f, 24f);
                var borderImg = outer.AddComponent<Image>();
                borderImg.color = Color.clear;
                _buyRackSwatchBorders[si] = borderImg;

                var inner = new GameObject("Fill");
                inner.transform.SetParent(outer.transform, false);
                var innerRT = inner.AddComponent<RectTransform>();
                innerRT.anchorMin = new Vector2(0.1f, 0.1f);
                innerRT.anchorMax = new Vector2(0.9f, 0.9f);
                innerRT.sizeDelta = Vector2.zero;
                var fillImg = inner.AddComponent<Image>();
                fillImg.color = RackDiagramPanel.GetFavoriteColor(si);
                _buyRackSwatchImgs[si] = fillImg;

                var btn = outer.AddComponent<Button>();
                var nav = new Navigation(); nav.mode = Navigation.Mode.None; btn.navigation = nav;
                btn.onClick.AddListener(new System.Action(() =>
                {
                    Color c = RackDiagramPanel.GetFavoriteColor(capturedSI);
                    // Sync HSV sliders to match selected swatch
                    Color.RGBToHSV(c, out float h, out float s, out float v);
                    SetSliderHSV(h * 360f, s * 100f, v * 100f);
                    // _buyRackSelectedColor is set inside SetSliderHSV→UpdateHSVDerivedVisuals
                    for (int k = 0; k < 8; k++)
                        if (_buyRackSwatchBorders[k] != null)
                            _buyRackSwatchBorders[k].color = k == capturedSI ? Color.white : Color.clear;
                }));
            }

            // ── Divider ──
            var div1 = new GameObject("Div1");
            div1.transform.SetParent(panel.transform, false);
            div1.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
            div1.AddComponent<LayoutElement>().preferredHeight = 1f;

            // ── Custom color label ──
            var customLblGo = new GameObject("CustomLabel");
            customLblGo.transform.SetParent(panel.transform, false);
            var customLbl = customLblGo.AddComponent<TextMeshProUGUI>();
            customLbl.text = "Custom Color:";
            customLbl.fontSize = 10f;
            customLbl.color = new Color(0.65f, 0.65f, 0.65f, 1f);
            customLbl.alignment = TextAlignmentOptions.Center;
            customLblGo.AddComponent<LayoutElement>().preferredHeight = 14f;

            // ── HSV sliders ──
            var hueTex = BuildHueGradientTexture();
            _satGradTex = new Texture2D(128, 4, TextureFormat.RGBA32, false);
            _satGradTex.wrapMode = TextureWrapMode.Clamp;
            _satGradTex.filterMode = FilterMode.Bilinear;
            _valGradTex = new Texture2D(128, 4, TextureFormat.RGBA32, false);
            _valGradTex.wrapMode = TextureWrapMode.Clamp;
            _valGradTex.filterMode = FilterMode.Bilinear;
            // Seed gradients with a neutral starting state
            UpdateGradientTexture(_satGradTex, Color.white, Color.white);
            UpdateGradientTexture(_valGradTex, Color.black, Color.white);

            var slidersSection = new GameObject("SlidersSection");
            slidersSection.transform.SetParent(panel.transform, false);
            var slidVL = slidersSection.AddComponent<VerticalLayoutGroup>();
            slidVL.childControlWidth = true;
            slidVL.childControlHeight = true;
            slidVL.childForceExpandWidth = true;
            slidVL.childForceExpandHeight = false;
            slidVL.spacing = 5f;
            slidersSection.AddComponent<LayoutElement>().preferredHeight = 80f;

            Image hBg;
            _hSlider = BuildHSVSlider(slidersSection.transform, "H", new Color(0.95f, 0.85f, 0.3f),
                hueTex, out hBg, out _hValLbl, 360f, 0f);
            _sSlider = BuildHSVSlider(slidersSection.transform, "S", new Color(0.65f, 0.85f, 0.65f),
                _satGradTex, out _satBgImg, out _sValLbl, 100f, 0f);
            _vSlider = BuildHSVSlider(slidersSection.transform, "V", new Color(0.65f, 0.75f, 0.95f),
                _valGradTex, out _valBgImg, out _vValLbl, 100f, 100f);

            // ── Preview + Save-to-slot row ──
            var previewRow = new GameObject("PreviewRow");
            previewRow.transform.SetParent(panel.transform, false);
            var prevHL = previewRow.AddComponent<HorizontalLayoutGroup>();
            prevHL.childControlWidth = false;
            prevHL.childControlHeight = false;
            prevHL.childForceExpandWidth = false;
            prevHL.childForceExpandHeight = false;
            prevHL.spacing = 8f;
            prevHL.childAlignment = TextAnchor.MiddleLeft;
            previewRow.AddComponent<LayoutElement>().preferredHeight = 28f;

            // Color preview box
            var previewGo = new GameObject("Preview");
            previewGo.transform.SetParent(previewRow.transform, false);
            previewGo.AddComponent<RectTransform>().sizeDelta = new Vector2(28f, 26f);
            _colorPreviewImg = previewGo.AddComponent<Image>();
            _colorPreviewImg.color = Color.white;

            // Hex input
            var hexLblGo = new GameObject("HexLbl");
            hexLblGo.transform.SetParent(previewRow.transform, false);
            hexLblGo.AddComponent<RectTransform>().sizeDelta = new Vector2(14f, 22f);
            var hexLbl = hexLblGo.AddComponent<TextMeshProUGUI>();
            hexLbl.text = "#";
            hexLbl.fontSize = 11f;
            hexLbl.color = new Color(0.6f, 0.6f, 0.6f, 1f);
            hexLbl.alignment = TextAlignmentOptions.MidlineRight;

            _hexInputField = BuildHexInputField(previewRow.transform);

            // Spacer
            var previewSpacer = new GameObject("Spacer");
            previewSpacer.transform.SetParent(previewRow.transform, false);
            previewSpacer.AddComponent<RectTransform>();
            previewSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            // "★ Save:" label
            var saveLblGo = new GameObject("SaveLbl");
            saveLblGo.transform.SetParent(previewRow.transform, false);
            saveLblGo.AddComponent<RectTransform>().sizeDelta = new Vector2(46f, 26f);
            var saveLblTmp = saveLblGo.AddComponent<TextMeshProUGUI>();
            saveLblTmp.text = "★ Save:";
            saveLblTmp.fontSize = 10f;
            saveLblTmp.color = new Color(0.85f, 0.75f, 0.2f, 1f);
            saveLblTmp.alignment = TextAlignmentOptions.MidlineRight;

            // Save-to-slot buttons (1–8)
            for (int si = 0; si < 8; si++)
            {
                int capturedSI = si;
                var slotOuter = new GameObject($"SaveSlot_{si}");
                slotOuter.transform.SetParent(previewRow.transform, false);
                slotOuter.AddComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
                var slotBorder = slotOuter.AddComponent<Image>();
                slotBorder.color = new Color(0.35f, 0.35f, 0.38f, 1f);
                _buyRackSaveSlotBorders[si] = slotBorder;

                var slotBtn = UIHelper.BuildButton(slotOuter.transform, (si + 1).ToString(), 0f);
                var slotBtnRT = slotBtn.gameObject.GetComponent<RectTransform>();
                slotBtnRT.anchorMin = new Vector2(0.1f, 0.1f);
                slotBtnRT.anchorMax = new Vector2(0.9f, 0.9f);
                slotBtnRT.sizeDelta = Vector2.zero;
                var slotLE = slotBtn.gameObject.GetComponent<LayoutElement>();
                if (slotLE != null) Object.Destroy(slotLE);
                var slotTmp = slotBtn.gameObject.GetComponentInChildren<TextMeshProUGUI>();
                if (slotTmp != null) slotTmp.fontSize = 9f;

                slotBtn.onClick.AddListener(new System.Action(() =>
                {
                    if (!_buyRackSelectedColor.HasValue) return;
                    RackDiagramPanel.SetFavoriteColor(capturedSI, _buyRackSelectedColor.Value);
                    // Update swatch visuals
                    if (_buyRackSwatchImgs[capturedSI] != null)
                        _buyRackSwatchImgs[capturedSI].color = _buyRackSelectedColor.Value;
                    // Flash save slot border briefly
                    for (int k = 0; k < 8; k++)
                        if (_buyRackSaveSlotBorders[k] != null)
                            _buyRackSaveSlotBorders[k].color = k == capturedSI
                                ? new Color(0.85f, 0.75f, 0.2f, 1f)
                                : new Color(0.35f, 0.35f, 0.38f, 1f);
                    StaticUIElements.instance.AddMeesageInField($"Saved to favorite slot {capturedSI + 1}");
                }));
            }

            // ── Divider ──
            var div2 = new GameObject("Div2");
            div2.transform.SetParent(panel.transform, false);
            div2.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.28f, 1f);
            div2.AddComponent<LayoutElement>().preferredHeight = 1f;

            // ── No Color toggle ──
            var noColorRow = new GameObject("NoColorRow");
            noColorRow.transform.SetParent(panel.transform, false);
            var ncHL = noColorRow.AddComponent<HorizontalLayoutGroup>();
            ncHL.childControlWidth = true;
            ncHL.childControlHeight = true;
            ncHL.childForceExpandWidth = false;
            ncHL.childForceExpandHeight = false;
            ncHL.spacing = 8f;
            ncHL.childAlignment = TextAnchor.MiddleCenter;
            noColorRow.AddComponent<LayoutElement>().preferredHeight = 22f;

            var noColorBtn = UIHelper.BuildButton(noColorRow.transform, "✕ No Color (default)", 160f);
            noColorBtn.onClick.AddListener(new System.Action(() =>
            {
                _buyRackSelectedColor = null;
                if (_colorPreviewImg != null) _colorPreviewImg.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                for (int k = 0; k < 8; k++)
                {
                    if (_buyRackSwatchBorders[k] != null) _buyRackSwatchBorders[k].color = Color.clear;
                    if (_buyRackSaveSlotBorders[k] != null) _buyRackSaveSlotBorders[k].color = new Color(0.35f, 0.35f, 0.38f, 1f);
                }
            }));

            // ── Confirm / Cancel buttons ──
            var btnRow = new GameObject("Buttons");
            btnRow.transform.SetParent(panel.transform, false);
            var btnHL = btnRow.AddComponent<HorizontalLayoutGroup>();
            btnHL.childControlWidth = true;
            btnHL.childControlHeight = true;
            btnHL.childForceExpandWidth = false;
            btnHL.childForceExpandHeight = false;
            btnHL.spacing = 12f;
            var btnPad = new RectOffset();
            btnPad.left = 20; btnPad.right = 20;
            btnHL.padding = btnPad;
            btnRow.AddComponent<LayoutElement>().preferredHeight = 34f;

            var cancelBtn = UIHelper.BuildButton(btnRow.transform, "Cancel", 100f);
            cancelBtn.onClick.AddListener(new System.Action(() =>
            {
                _buyRackPopup.SetActive(false);
                _pendingBuyMount = null;
                _pendingRecolorRack = null;
                _pendingRecolorRacks.Clear();
                _recolorMode = false;
            }));

            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(btnRow.transform, false);
            spacer.AddComponent<RectTransform>();
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var confirmBtn = UIHelper.BuildButton(btnRow.transform, "Confirm", 110f);
            ReusableFunctions.ChangeButtonNormalColor(confirmBtn, new Color(0.1f, 0.4f, 0.1f));
            _confirmBtnLbl = confirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            confirmBtn.onClick.AddListener(new System.Action(OnBuyRackConfirm));

            _buyRackPopup.SetActive(false);
        }

        // ── LACP ────────────────────────────────────────────────────

        private static void OpenLACPConfig(NetworkSwitch sw)
        {
            try
            {
                var beforeIds = new HashSet<int>();
                var allBefore = Object.FindObjectsOfType<Canvas>();
                for (int i = 0; i < allBefore.Length; i++)
                {
                    if (allBefore[i].gameObject.activeSelf)
                        beforeIds.Add(allBefore[i].GetInstanceID());
                }

                FloorManagerMod.DCIMScreen.SetActive(false);
                MainGameManager.instance.ShowNetworkConfigCanvas(sw);
                MelonCoroutines.Start(WaitForLACPOpen(beforeIds));
            }
            catch (System.Exception ex)
            {
                FloorManagerMod.DCIMScreen.SetActive(true);
                MelonLogger.Warning($"[DCIM] ShowNetworkConfigCanvas failed: {ex.Message}");
                StaticUIElements.instance.AddMeesageInField("LACP config unavailable — visit switch physically");
            }
        }

        private static IEnumerator WaitForLACPOpen(HashSet<int> beforeIds)
        {
            yield return null;

            Canvas lacpCanvas = null;
            var allAfter = Object.FindObjectsOfType<Canvas>();
            for (int i = 0; i < allAfter.Length; i++)
            {
                var c = allAfter[i];
                if (c.gameObject.activeSelf && !beforeIds.Contains(c.GetInstanceID()))
                {
                    lacpCanvas = c;
                    break;
                }
            }

            if (lacpCanvas != null)
            {
                int origSortingOrder = lacpCanvas.sortingOrder;
                bool origOverrideSorting = lacpCanvas.overrideSorting;
                RenderMode origRenderMode = lacpCanvas.renderMode;
                lacpCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                lacpCanvas.sortingOrder = 999;

                while (lacpCanvas != null && lacpCanvas.gameObject.activeSelf)
                    yield return new WaitForSeconds(0.3f);

                if (lacpCanvas != null)
                {
                    lacpCanvas.renderMode = origRenderMode;
                    lacpCanvas.sortingOrder = origSortingOrder;
                    lacpCanvas.overrideSorting = origOverrideSorting;
                }
            }
            else
            {
                yield return new WaitForSeconds(3f);
            }

            // Restore cursor to UI mode — CloseAnyCanvas() locked it for player movement
            InputManager.ConfinedCursorforUI();
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.Confined;
            var pm = PlayerManager.instance;
            if (pm != null)
            {
                pm.enabledMouseMovement = false;
                pm.enabledPlayerMovement = false;
            }

            if (FloorManagerMod.DCIMScreen != null)
                FloorManagerMod.DCIMScreen.SetActive(true);
        }

        // ── Color legend ────────────────────────────────────────────

        private static void BuildColorLegend(GameObject root)
        {
            _colorLegend = new GameObject("ColorLegend");
            _colorLegend.transform.SetParent(root.transform, false);
            var rt = _colorLegend.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-6f, 6f);
            rt.sizeDelta = new Vector2(110f, 96f);

            var bg = _colorLegend.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.06f, 0.08f, 0.90f);

            var vl = _colorLegend.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.spacing = 1f;
            var pad = new RectOffset();
            pad.left = 4; pad.right = 4; pad.top = 4; pad.bottom = 4;
            vl.padding = pad;

            var csf = _colorLegend.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            LegendEntry("Broken",   UIHelper.StatusRed);
            LegendEntry("EOL",      UIHelper.StatusYellow);
            LegendEntry("Healthy",  UIHelper.StatusGreen);
            LegendEntry("Empty",    UIHelper.StatusGray);

            void LegendEntry(string label, Color color)
            {
                var row = new GameObject("Legend_" + label);
                row.transform.SetParent(_colorLegend.transform, false);
                var hl = row.AddComponent<HorizontalLayoutGroup>();
                hl.childControlWidth = true;
                hl.childControlHeight = true;
                hl.childForceExpandWidth = false;
                hl.childForceExpandHeight = false;
                hl.spacing = 4f;
                var rowLE = row.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 14f;

                var swatchGo = new GameObject("Swatch");
                swatchGo.transform.SetParent(row.transform, false);
                var swatchImg = swatchGo.AddComponent<Image>();
                swatchImg.color = color;
                var swatchLE = swatchGo.AddComponent<LayoutElement>();
                swatchLE.preferredWidth = 10f;
                swatchLE.preferredHeight = 10f;

                var tmp = UIHelper.BuildLabel(row.transform, label, 100f);
                tmp.fontSize = 9f;
                tmp.color = color;
            }
        }

        private static void SetFill(GameObject go)
        {
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
