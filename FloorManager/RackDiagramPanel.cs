using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public static class RackDiagramPanel
    {
        private const float SLOT_H = 18f;

        private static GameObject _panelRoot;
        private static TextMeshProUGUI _titleLabel;
        private static Transform _slotContent;
        private static Rack _currentRack;
        private static readonly List<GameObject> _slotRows = new List<GameObject>();

        private static GameObject _miniShopPanel;
        private static Transform _shopContent;
        private static readonly List<GameObject> _shopItemRows = new List<GameObject>();

        // Invoked after HideMiniShop completes — used by FloorMapApp to restore tab state.
        public static System.Action OnMiniShopClosed;

        private static MelonPreferences_Category _colorCategory;
        private static readonly MelonPreferences_Entry<string>[] _colorEntries = new MelonPreferences_Entry<string>[8];
        private static bool _prefsInitialized = false;

        private struct LiveRow
        {
            public Server Server;
            public NetworkSwitch Switch;
            public Image BorderImg;
            public TextMeshProUGUI StatusLbl;
            public TextMeshProUGUI EolLbl;
        }
        private static readonly List<LiveRow> _liveRows = new List<LiveRow>();
        private static object _refreshCoroutine;

        // Multi-select state
        private static bool _checkboxClick = false;
        private static readonly List<Server> _selectedServers = new List<Server>();
        private static readonly List<NetworkSwitch> _selectedSwitches = new List<NetworkSwitch>();
        private static readonly Dictionary<int, Image> _checkFillImages = new Dictionary<int, Image>();
        private static GameObject _actionBar;
        private static TextMeshProUGUI _selectionCountLbl;
        private static GameObject _assignCustPanel;
        private static Transform _assignCustContent;
        private static readonly List<GameObject> _assignCustRows = new List<GameObject>();

        private static readonly string[] DefaultColors = {
            "#4A90D9", "#2ECC71", "#E74C3C", "#F39C12",
            "#9B59B6", "#1ABC9C", "#F0F0F0", "#808080"
        };

        // One purchasable SFP module type (backed by a shop SFP box item)
        private struct SFPModuleOption
        {
            public ShopItemSO SO;
            public int SFPType;           // sfpBoxType — matches CableLink.sfpTypeSupported
            public int BoxCapacity;       // modules per box (e.g. 5)
            public string DisplayName;    // cleaned, "Nx " prefix removed
            public GameObject ModulePrefab; // sfpPrefabs entry whose sfpType == SFPType
            public int PricePerModule => BoxCapacity > 0 ? SO.price / BoxCapacity : SO.price;
        }

        // One physical SFP port slot on a switch prefab
        private struct SFPPortSlot
        {
            public int PortIndex;
            public List<SFPModuleOption> Options;   // compatible modules in priority order; empty = none available
            public int SelectedOptionIdx;           // -1 = leave empty, >=0 = chosen option in Options
            public int OriginalPortType;            // actual CableLink.sfpTypeSupported (for port lookup at install)

            public bool HasModule => Options != null && Options.Count > 0;
            public bool IsSelected => SelectedOptionIdx >= 0;
            public SFPModuleOption SelectedModule =>
                (IsSelected && Options != null && SelectedOptionIdx < Options.Count)
                    ? Options[SelectedOptionIdx] : default(SFPModuleOption);
        }

        private static GameObject _sfpConfigPopup;
        private static List<SFPPortSlot> _popupSlots;
        private static TextMeshProUGUI _popupBuyLbl;
        private static ShopItemSO _popupSwitchSO;
        private static Image[] _portBtnImages;
        private static int[] _portToSlotMap;
        private static int _popupSelectedPortIdx = -1;
        private static Transform _portDetailContent;
        private static int _popupQty = 1;
        private static TextMeshProUGUI _popupQtyLbl;

        // ── Cart system ──────────────────────────────────────────────
        private struct CartItem
        {
            public ShopItemSO SO;
            public string DisplayName;
            public int Quantity;
            public bool IsColorable;
            public Color SelectedColor;
            public bool IsSFPSwitch;
            public List<SFPPortSlot> SFPSlots; // null for non-SFP
            public int UnitPrice;              // base + selected modules
        }
        private static readonly List<CartItem> _cart = new List<CartItem>();
        private static GameObject _shopView;
        private static GameObject _cartView;
        private static TextMeshProUGUI _cartTabLbl;
        private static Transform _cartContent;
        private static readonly List<GameObject> _cartRows = new List<GameObject>();
        private static TextMeshProUGUI _cartTotalLbl;
        private static Button _checkoutBtn;
        private static Button _shopTabBtnRef;
        private static Button _cartTabBtnRef;

        // ── SFP Presets ──────────────────────────────────────────────
        private static MelonPreferences_Category _presetCategory;
        private static MelonPreferences_Entry<string> _presetEntry;

        // Called from FloorManagerMod.OnInitializeMelon so prefs load at game start,
        // not lazily on first laptop open. Build() keeps the guard as a safety fallback.
        public static void InitPrefs()
        {
            if (_prefsInitialized) return;
            _colorCategory = MelonPreferences.CreateCategory("DCIM_Colors");
            for (int i = 0; i < 8; i++)
            {
                int idx = i;
                _colorEntries[idx] = _colorCategory.CreateEntry($"fav_{idx}", DefaultColors[idx]);
            }
            // SFP Presets
            _presetCategory = MelonPreferences.CreateCategory("DCIM_SFPPresets");
            _presetEntry = _presetCategory.CreateEntry("presets", "");

            _prefsInitialized = true;
        }

        public static Color GetFavoriteColor(int index)
        {
            if (index < 0 || index >= 8) return Color.gray;
            string hex = _colorEntries[index] != null ? _colorEntries[index].Value : DefaultColors[index];
            if (!ColorUtility.TryParseHtmlString(hex, out Color c)) c = Color.gray;
            return c;
        }

        public static void SetFavoriteColor(int index, Color color)
        {
            if (index < 0 || index >= 8) return;
            string hex = "#" + ColorUtility.ToHtmlStringRGB(color);
            if (_colorEntries[index] != null)
            {
                _colorEntries[index].Value = hex;
                MelonPreferences.Save();
            }
        }

        public static void Build(GameObject parentRoot)
        {
            // Initialize MelonPreferences for favorite colors (guard: only on first Build)
            if (!_prefsInitialized)
            {
                _colorCategory = MelonPreferences.CreateCategory("DCIM_Colors");
                for (int i = 0; i < 8; i++)
                {
                    int idx = i;
                    _colorEntries[idx] = _colorCategory.CreateEntry($"fav_{idx}", DefaultColors[idx]);
                }
                _presetCategory = MelonPreferences.CreateCategory("DCIM_SFPPresets");
                _presetEntry = _presetCategory.CreateEntry("presets", "");
                _prefsInitialized = true;
            }

            // Root — Canvas with overrideSorting so it renders above everything in the laptop panel
            _panelRoot = new GameObject("RackDiagramPanel");
            _panelRoot.transform.SetParent(parentRoot.transform, false);
            var rootRT = _panelRoot.AddComponent<RectTransform>();
            rootRT.anchorMin = Vector2.zero;
            rootRT.anchorMax = Vector2.one;
            rootRT.offsetMin = Vector2.zero;
            rootRT.offsetMax = Vector2.zero;
            var rootCanvas = _panelRoot.AddComponent<Canvas>();
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 100;
            _panelRoot.AddComponent<GraphicRaycaster>();

            // Dim overlay — click to close
            var dimGo = new GameObject("DimOverlay");
            dimGo.transform.SetParent(_panelRoot.transform, false);
            var dimRT = dimGo.AddComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.65f);
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            var dimNav = new Navigation();
            dimNav.mode = Navigation.Mode.None;
            dimBtn.navigation = dimNav;
            dimBtn.onClick.AddListener(new System.Action(Close));

            // Panel — 380px wide, 85% height, centered
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_panelRoot.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            // anchorMin.x == anchorMax.x = 0.5 → width comes from sizeDelta.x
            // anchorMin.y != anchorMax.y → height = 85% of parent
            panelRT.anchorMin = new Vector2(0.5f, 0.02f);
            panelRT.anchorMax = new Vector2(0.5f, 0.98f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.sizeDelta = new Vector2(460f, 0f);
            panelRT.anchoredPosition = Vector2.zero;
            var panelImg = panel.AddComponent<Image>();
            panelImg.color = new Color(0.09f, 0.09f, 0.11f, 1f);

            var panelVL = panel.AddComponent<VerticalLayoutGroup>();
            panelVL.childControlWidth = true;
            panelVL.childControlHeight = true;
            panelVL.childForceExpandWidth = true;
            panelVL.childForceExpandHeight = false;
            panelVL.spacing = 0f;

            // Header
            BuildHeader(panel.transform);

            // Slot scroll area (fills remaining vertical space)
            var scrollAreaGo = new GameObject("SlotScrollArea");
            scrollAreaGo.transform.SetParent(panel.transform, false);
            var scrollAreaLE = scrollAreaGo.AddComponent<LayoutElement>();
            scrollAreaLE.flexibleHeight = 1f;

            var scrollRect = scrollAreaGo.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;

            var viewport = new GameObject("Viewport");
            viewport.transform.SetParent(scrollAreaGo.transform, false);
            var vpRT = viewport.AddComponent<RectTransform>();
            vpRT.anchorMin = Vector2.zero;
            vpRT.anchorMax = Vector2.one;
            vpRT.sizeDelta = Vector2.zero;
            vpRT.offsetMin = Vector2.zero;
            vpRT.offsetMax = Vector2.zero;
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
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
            contentVL.spacing = 1f;
            var contentPad = new RectOffset();
            contentPad.left = 4; contentPad.right = 4; contentPad.top = 4; contentPad.bottom = 4;
            contentVL.padding = contentPad;
            var contentCSF = content.AddComponent<ContentSizeFitter>();
            contentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;
            _slotContent = content.transform;

            // Action bar (shown when devices are selected)
            BuildActionBar(panel.transform);

            // Mini shop panel (overlays the panel, hidden by default)
            BuildMiniShopPanel(panel.transform);

            // Customer assignment overlay (parented to root canvas so it covers everything)
            BuildAssignCustomerPanel(_panelRoot.transform);

            _panelRoot.SetActive(false);
        }

        private static void BuildHeader(Transform parent)
        {
            var header = new GameObject("Header");
            header.transform.SetParent(parent, false);
            var headerImg = header.AddComponent<Image>();
            headerImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);
            var headerHL = header.AddComponent<HorizontalLayoutGroup>();
            headerHL.childControlWidth = true;
            headerHL.childControlHeight = true;
            headerHL.childForceExpandWidth = false;
            headerHL.childForceExpandHeight = false;
            headerHL.spacing = 6f;
            var headerPad = new RectOffset();
            headerPad.left = 8; headerPad.right = 8; headerPad.top = 0; headerPad.bottom = 0;
            headerHL.padding = headerPad;
            var headerLE = header.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 36f;

            _titleLabel = UIHelper.BuildLabel(header.transform, "Rack", 200f);
            _titleLabel.fontSize = 13f;
            _titleLabel.fontStyle = FontStyles.Bold;
            _titleLabel.color = Color.white;
            var titleLE = _titleLabel.gameObject.GetComponent<LayoutElement>();
            titleLE.flexibleWidth = 1f;

            var recolorBtn = UIHelper.BuildButton(header.transform, "Recolor", 62f);
            ReusableFunctions.ChangeButtonNormalColor(recolorBtn, new Color(0.12f, 0.22f, 0.38f, 1f));
            recolorBtn.onClick.AddListener(new System.Action(() =>
            {
                if (_currentRack == null) return;
                Close();
                FloorMapApp.ShowRecolorPopup(_currentRack);
            }));

            var closeBtn = UIHelper.BuildButton(header.transform, "X", 32f);
            closeBtn.onClick.AddListener(new System.Action(Close));
        }

        private static void BuildMiniShopPanel(Transform parent)
        {
            _miniShopPanel = new GameObject("MiniShopPanel");
            var shopParent = FloorManagerMod.DCIMScreen != null
                ? FloorManagerMod.DCIMScreen.transform : parent;
            _miniShopPanel.transform.SetParent(shopParent, false);

            var rt = _miniShopPanel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var ignoreLE = _miniShopPanel.AddComponent<LayoutElement>();
            ignoreLE.ignoreLayout = true;

            var canvas = _miniShopPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 10;
            _miniShopPanel.AddComponent<GraphicRaycaster>();

            var bg = _miniShopPanel.AddComponent<Image>();
            bg.color = new Color(0.07f, 0.07f, 0.09f, 0.97f);

            var vl = _miniShopPanel.AddComponent<VerticalLayoutGroup>();
            vl.childControlWidth = true;
            vl.childControlHeight = true;
            vl.childForceExpandWidth = true;
            vl.childForceExpandHeight = false;
            vl.spacing = 4f;
            var pad = new RectOffset();
            pad.left = 8; pad.right = 8; pad.top = 8; pad.bottom = 8;
            vl.padding = pad;

            // Title row
            var titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(_miniShopPanel.transform, false);
            var titleHL = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleHL.childControlWidth = true;
            titleHL.childControlHeight = true;
            titleHL.childForceExpandWidth = false;
            titleHL.childForceExpandHeight = false;
            titleHL.spacing = 8f;
            var titleLE = titleRow.AddComponent<LayoutElement>();
            titleLE.preferredHeight = 30f;

            var shopTitle = UIHelper.BuildLabel(titleRow.transform, "Buy Device", 200f);
            shopTitle.fontSize = 14f;
            shopTitle.fontStyle = FontStyles.Bold;
            shopTitle.color = Color.white;
            var shopTitleLE = shopTitle.gameObject.GetComponent<LayoutElement>();
            shopTitleLE.flexibleWidth = 1f;

            var closeBtn = UIHelper.BuildButton(titleRow.transform, "X", 32f);
            closeBtn.onClick.AddListener(new System.Action(() => { HideMiniShop(); OnMiniShopClosed?.Invoke(); }));

            // ── Tab toggle row ──────────────────────────────────────
            var tabRow = new GameObject("TabRow");
            tabRow.transform.SetParent(_miniShopPanel.transform, false);
            var tabHL = tabRow.AddComponent<HorizontalLayoutGroup>();
            tabHL.childControlWidth = true;
            tabHL.childControlHeight = true;
            tabHL.childForceExpandWidth = true;
            tabHL.childForceExpandHeight = false;
            tabHL.spacing = 4f;
            var tabLE = tabRow.AddComponent<LayoutElement>();
            tabLE.preferredHeight = 28f;

            var shopTabBtn = UIHelper.BuildButton(tabRow.transform, "Shop", 80f);
            _shopTabBtnRef = shopTabBtn;
            ReusableFunctions.ChangeButtonNormalColor(shopTabBtn, new Color(0.18f, 0.24f, 0.32f, 1f));

            var cartTabBtn = UIHelper.BuildButton(tabRow.transform, "Cart (0)", 80f);
            _cartTabBtnRef = cartTabBtn;
            _cartTabLbl = cartTabBtn.GetComponentInChildren<TextMeshProUGUI>();
            ReusableFunctions.ChangeButtonNormalColor(cartTabBtn, new Color(0.12f, 0.12f, 0.15f, 1f));

            shopTabBtn.onClick.AddListener(new System.Action(() => SwitchToShopTab()));
            cartTabBtn.onClick.AddListener(new System.Action(() => SwitchToCartTab()));

            // ── Shop view (visible by default) ─────────────────────
            _shopView = new GameObject("ShopView");
            _shopView.transform.SetParent(_miniShopPanel.transform, false);
            var shopViewVL = _shopView.AddComponent<VerticalLayoutGroup>();
            shopViewVL.childControlWidth = true;
            shopViewVL.childControlHeight = true;
            shopViewVL.childForceExpandWidth = true;
            shopViewVL.childForceExpandHeight = false;
            shopViewVL.spacing = 4f;
            var shopViewLE = _shopView.AddComponent<LayoutElement>();
            shopViewLE.flexibleHeight = 1f;

            // Note label
            var noteLbl = UIHelper.BuildLabel(_shopView.transform, "Item spawns at your location - carry to rack to install", 300f);
            noteLbl.fontSize = 9f;
            noteLbl.color = new Color(0.6f, 0.6f, 0.6f);
            noteLbl.enableWordWrapping = true;
            noteLbl.alignment = TextAlignmentOptions.Center;
            var noteLE = noteLbl.gameObject.GetComponent<LayoutElement>();
            noteLE.preferredHeight = 24f;

            // Scrollable shop items
            var scrollArea = new GameObject("ShopScrollArea");
            scrollArea.transform.SetParent(_shopView.transform, false);
            var scrollAreaLE = scrollArea.AddComponent<LayoutElement>();
            scrollAreaLE.flexibleHeight = 1f;

            var shopScroll = scrollArea.AddComponent<ScrollRect>();
            shopScroll.horizontal = false;

            var shopVp = new GameObject("Viewport");
            shopVp.transform.SetParent(scrollArea.transform, false);
            var shopVpRT = shopVp.AddComponent<RectTransform>();
            shopVpRT.anchorMin = Vector2.zero;
            shopVpRT.anchorMax = Vector2.one;
            shopVpRT.sizeDelta = Vector2.zero;
            shopVpRT.offsetMin = Vector2.zero;
            shopVpRT.offsetMax = Vector2.zero;
            shopVp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            shopVp.AddComponent<Mask>().showMaskGraphic = false;
            shopScroll.viewport = shopVpRT;

            var shopContent = new GameObject("Content");
            shopContent.transform.SetParent(shopVp.transform, false);
            var shopContentRT = shopContent.AddComponent<RectTransform>();
            shopContentRT.anchorMin = new Vector2(0f, 1f);
            shopContentRT.anchorMax = new Vector2(1f, 1f);
            shopContentRT.pivot = new Vector2(0.5f, 1f);
            shopContentRT.sizeDelta = Vector2.zero;
            var shopContentVL = shopContent.AddComponent<VerticalLayoutGroup>();
            shopContentVL.childControlWidth = true;
            shopContentVL.childControlHeight = true;
            shopContentVL.childForceExpandWidth = true;
            shopContentVL.childForceExpandHeight = false;
            shopContentVL.spacing = 2f;
            var shopContentPad = new RectOffset();
            shopContentPad.left = 4; shopContentPad.right = 4; shopContentPad.top = 4; shopContentPad.bottom = 4;
            shopContentVL.padding = shopContentPad;
            var shopContentCSF = shopContent.AddComponent<ContentSizeFitter>();
            shopContentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            shopScroll.content = shopContentRT;
            _shopContent = shopContent.transform;

            // ── Cart view (hidden by default) ──────────────────────
            _cartView = new GameObject("CartView");
            _cartView.transform.SetParent(_miniShopPanel.transform, false);
            var cartViewVL = _cartView.AddComponent<VerticalLayoutGroup>();
            cartViewVL.childControlWidth = true;
            cartViewVL.childControlHeight = true;
            cartViewVL.childForceExpandWidth = true;
            cartViewVL.childForceExpandHeight = false;
            cartViewVL.spacing = 4f;
            var cartViewLE = _cartView.AddComponent<LayoutElement>();
            cartViewLE.flexibleHeight = 1f;

            // Cart scroll area
            var cartScrollArea = new GameObject("CartScrollArea");
            cartScrollArea.transform.SetParent(_cartView.transform, false);
            var cartScrollAreaLE = cartScrollArea.AddComponent<LayoutElement>();
            cartScrollAreaLE.flexibleHeight = 1f;

            var cartScroll = cartScrollArea.AddComponent<ScrollRect>();
            cartScroll.horizontal = false;

            var cartVp = new GameObject("Viewport");
            cartVp.transform.SetParent(cartScrollArea.transform, false);
            var cartVpRT = cartVp.AddComponent<RectTransform>();
            cartVpRT.anchorMin = Vector2.zero;
            cartVpRT.anchorMax = Vector2.one;
            cartVpRT.sizeDelta = Vector2.zero;
            cartVpRT.offsetMin = Vector2.zero;
            cartVpRT.offsetMax = Vector2.zero;
            cartVp.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            cartVp.AddComponent<Mask>().showMaskGraphic = false;
            cartScroll.viewport = cartVpRT;

            var cartContent = new GameObject("CartContent");
            cartContent.transform.SetParent(cartVp.transform, false);
            var cartContentRT = cartContent.AddComponent<RectTransform>();
            cartContentRT.anchorMin = new Vector2(0f, 1f);
            cartContentRT.anchorMax = new Vector2(1f, 1f);
            cartContentRT.pivot = new Vector2(0.5f, 1f);
            cartContentRT.sizeDelta = Vector2.zero;
            var cartContentVL = cartContent.AddComponent<VerticalLayoutGroup>();
            cartContentVL.childControlWidth = true;
            cartContentVL.childControlHeight = true;
            cartContentVL.childForceExpandWidth = true;
            cartContentVL.childForceExpandHeight = false;
            cartContentVL.spacing = 2f;
            var cartContentPad = new RectOffset();
            cartContentPad.left = 4; cartContentPad.right = 4; cartContentPad.top = 4; cartContentPad.bottom = 4;
            cartContentVL.padding = cartContentPad;
            var cartContentCSF = cartContent.AddComponent<ContentSizeFitter>();
            cartContentCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            cartScroll.content = cartContentRT;
            _cartContent = cartContent.transform;

            // Cart footer
            var cartFooter = new GameObject("CartFooter");
            cartFooter.transform.SetParent(_cartView.transform, false);
            cartFooter.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 1f);
            var footerVL = cartFooter.AddComponent<VerticalLayoutGroup>();
            footerVL.childControlWidth = true;
            footerVL.childControlHeight = true;
            footerVL.childForceExpandWidth = true;
            footerVL.childForceExpandHeight = false;
            footerVL.spacing = 4f;
            var footerPad = new RectOffset();
            footerPad.left = 8; footerPad.right = 8; footerPad.top = 6; footerPad.bottom = 6;
            footerVL.padding = footerPad;

            _cartTotalLbl = UIHelper.BuildLabel(cartFooter.transform, "Total: $0", 200f);
            _cartTotalLbl.fontSize = 13f;
            _cartTotalLbl.fontStyle = FontStyles.Bold;
            _cartTotalLbl.color = Color.white;
            _cartTotalLbl.alignment = TextAlignmentOptions.Center;
            _cartTotalLbl.GetComponent<LayoutElement>().preferredHeight = 24f;

            var cartBtnRow = new GameObject("CartBtnRow");
            cartBtnRow.transform.SetParent(cartFooter.transform, false);
            var cartBtnHL = cartBtnRow.AddComponent<HorizontalLayoutGroup>();
            cartBtnHL.childControlWidth = true;
            cartBtnHL.childControlHeight = true;
            cartBtnHL.childForceExpandWidth = false;
            cartBtnHL.childForceExpandHeight = false;
            cartBtnHL.spacing = 8f;
            cartBtnRow.AddComponent<LayoutElement>().preferredHeight = 32f;

            var clearCartBtn = UIHelper.BuildButton(cartBtnRow.transform, "Clear Cart", 80f);
            ReusableFunctions.ChangeButtonNormalColor(clearCartBtn, new Color(0.35f, 0.10f, 0.10f, 1f));
            clearCartBtn.onClick.AddListener(new System.Action(() =>
            {
                _cart.Clear();
                UpdateCartBadge();
                RebuildCartView();
            }));

            // Spacer
            var footerSpacer = new GameObject("Spacer");
            footerSpacer.transform.SetParent(cartBtnRow.transform, false);
            footerSpacer.AddComponent<RectTransform>();
            footerSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _checkoutBtn = UIHelper.BuildButton(cartBtnRow.transform, "Checkout", 120f);
            ReusableFunctions.ChangeButtonNormalColor(_checkoutBtn, new Color(0.10f, 0.32f, 0.10f, 1f));
            _checkoutBtn.onClick.AddListener(new System.Action(ExecuteCheckout));

            _cartView.SetActive(false);
            _miniShopPanel.SetActive(false);
        }

        // ── Public API ──────────────────────────────────────────────

        public static void Open(Rack rack, int rowNum, int posNum)
        {
            // Clear selection state before destroying rows
            _selectedServers.Clear();
            _selectedSwitches.Clear();
            _checkFillImages.Clear();
            if (_actionBar != null) _actionBar.SetActive(false);
            if (_assignCustPanel != null) _assignCustPanel.SetActive(false);

            // Clear old slot rows
            for (int i = 0; i < _slotRows.Count; i++)
                if (_slotRows[i] != null)
                    Object.Destroy(_slotRows[i]);
            _slotRows.Clear();
            _liveRows.Clear();
            if (_refreshCoroutine != null)
            {
                MelonCoroutines.Stop(_refreshCoroutine);
                _refreshCoroutine = null;
            }

            _currentRack = rack;
            _titleLabel.text = $"R{rowNum}/{posNum}";
            HideMiniShop();
            BuildSlotRows(rack);
            _panelRoot.SetActive(true);

            if (_liveRows.Count > 0)
                _refreshCoroutine = MelonCoroutines.Start(RefreshLiveRows());
        }

        public static void Close()
        {
            _panelRoot.SetActive(false);
        }

        // ── Slot rows ───────────────────────────────────────────────

        private static void BuildSlotRows(Rack rack)
        {
            // Build positionIndex → device maps
            var serverMap = new Dictionary<int, Server>();
            var switchMap = new Dictionary<int, NetworkSwitch>();
            int rackId = rack.GetInstanceID();

            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var parentRack = srv.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || parentRack.GetInstanceID() != rackId) continue;
                serverMap[srv.currentRackPosition.positionIndex] = srv;
            }

            var allSwitches = Object.FindObjectsOfType<NetworkSwitch>();
            for (int i = 0; i < allSwitches.Length; i++)
            {
                var sw = allSwitches[i];
                if (sw.currentRackPosition == null) continue;
                var parentRack = sw.currentRackPosition.GetComponentInParent<Rack>();
                if (parentRack == null || parentRack.GetInstanceID() != rackId) continue;
                switchMap[sw.currentRackPosition.positionIndex] = sw;
            }

            int totalSlots = rack.positions != null ? rack.positions.Length : 47;

            int u = 0;
            while (u < totalSlots)
            {
                if (serverMap.ContainsKey(u))
                {
                    var srv = serverMap[u];
                    BuildOccupiedRow(u, Mathf.Max(1, srv.sizeInU), srv, null);
                    u += Mathf.Max(1, srv.sizeInU);
                }
                else if (switchMap.ContainsKey(u))
                {
                    var sw = switchMap[u];
                    BuildOccupiedRow(u, Mathf.Max(1, sw.sizeInU), null, sw);
                    u += Mathf.Max(1, sw.sizeInU);
                }
                else if (rack.isPositionUsed != null && u < rack.isPositionUsed.Length && rack.isPositionUsed[u] != 0)
                {
                    // Continuation slot of a multi-U device — skip
                    u++;
                }
                else
                {
                    BuildEmptySlotRow(u);
                    u++;
                }
            }
        }

        private static void BuildOccupiedRow(int slotIndex, int sizeInU, Server server, NetworkSwitch sw)
        {
            float rowHeight = SLOT_H * sizeInU;

            var row = new GameObject($"Slot_{slotIndex}");
            row.transform.SetParent(_slotContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.14f, 0.14f, 0.17f, 1f);

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = rowHeight;

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.spacing = 4f;
            var pad = new RectOffset();
            pad.left = 0; pad.right = 4; pad.top = 0; pad.bottom = 0;
            hl.padding = pad;

            int eolTime = server != null ? server.eolTime : (sw != null ? sw.eolTime : int.MaxValue);
            bool broken   = server != null ? server.isBroken : (sw != null && sw.isBroken);
            bool eolWarn  = !broken && eolTime <= 0;
            bool eolAppr  = !broken && !eolWarn && eolTime > 0 && eolTime <= FloorManagerMod.EOL_WARN_SECONDS;
            bool on       = !broken && (server != null ? server.isOn : (sw != null && sw.isOn));
            Color statusColor = broken  ? UIHelper.StatusRed
                              : eolWarn ? UIHelper.StatusOrange
                              : eolAppr ? UIHelper.StatusCyan
                              : on      ? UIHelper.StatusGreen
                              :           UIHelper.StatusGray;

            // Checkbox (multi-select)
            int deviceId = server != null ? server.GetInstanceID() : (sw != null ? sw.GetInstanceID() : -1);
            var checkGo = new GameObject("Checkbox");
            checkGo.transform.SetParent(row.transform, false);
            var checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.30f, 0.30f, 0.38f, 1f);
            var checkLE = checkGo.AddComponent<LayoutElement>();
            checkLE.preferredWidth = 14f;
            checkLE.preferredHeight = 14f;
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(checkGo.transform, false);
            var fillRT = fillGo.AddComponent<RectTransform>();
            fillRT.anchorMin = new Vector2(0.15f, 0.15f);
            fillRT.anchorMax = new Vector2(0.85f, 0.85f);
            fillRT.offsetMin = Vector2.zero;
            fillRT.offsetMax = Vector2.zero;
            var fillImg = fillGo.AddComponent<Image>();
            fillImg.color = UIHelper.StatusGreen;
            fillImg.raycastTarget = false;
            fillGo.SetActive(false);
            if (deviceId >= 0) _checkFillImages[deviceId] = fillImg;
            var checkBtn = checkGo.AddComponent<Button>();
            checkBtn.targetGraphic = checkImg;
            var checkNav = new Navigation();
            checkNav.mode = Navigation.Mode.None;
            checkBtn.navigation = checkNav;
            Server capturedSrvCb = server;
            NetworkSwitch capturedSwCb = sw;
            int capturedDevId = deviceId;
            Image capturedFill = fillImg;
            checkBtn.onClick.AddListener(new System.Action(() =>
            {
                _checkboxClick = true;
                ToggleSelection(capturedSrvCb, capturedSwCb, capturedDevId, capturedFill);
            }));

            // Left colored border
            var border = new GameObject("Border");
            border.transform.SetParent(row.transform, false);
            var borderImg = border.AddComponent<Image>();
            borderImg.color = statusColor;
            var borderLE = border.AddComponent<LayoutElement>();
            borderLE.preferredWidth = 3f;
            borderLE.preferredHeight = rowHeight;

            // Slot label
            var slotLbl = UIHelper.BuildLabel(row.transform, $"U{slotIndex + 1}", 32f);
            slotLbl.fontSize = 8f;
            slotLbl.color = new Color(0.5f, 0.5f, 0.5f);

            // Type badge
            string typeBadge = server != null ? "SRV" : "SW";
            Color typeColor = server != null
                ? UIHelper.GetDeviceTypeColor(server.gameObject.name)
                : UIHelper.SwitchColor;
            var typeLbl = UIHelper.BuildLabel(row.transform, typeBadge, 28f);
            typeLbl.fontSize = 8f;
            typeLbl.fontStyle = FontStyles.Bold;
            typeLbl.color = typeColor;
            typeLbl.alignment = TextAlignmentOptions.Center;

            // Info container: device name + optional sub-line (IP / countdown)
            string devName = server != null
                ? UIHelper.GetServerTypeName(server.gameObject.name)
                : MainGameManager.instance.ReturnSwitchNameFromType(sw.switchType);

            var infoGo = new GameObject("Info");
            infoGo.transform.SetParent(row.transform, false);
            var infoVL = infoGo.AddComponent<VerticalLayoutGroup>();
            infoVL.childControlWidth = true;
            infoVL.childControlHeight = true;
            infoVL.childForceExpandWidth = true;
            infoVL.childForceExpandHeight = false;
            infoVL.spacing = 0f;
            var infoLE = infoGo.AddComponent<LayoutElement>();
            infoLE.flexibleWidth = 1f;

            var nameLbl = UIHelper.BuildLabel(infoGo.transform, devName, 120f);
            nameLbl.fontSize = 9f;
            nameLbl.color = Color.white;
            var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>();
            nameLE.preferredHeight = 10f;

            bool hasIp = server != null && server.IP != null && server.IP != "" && server.IP != "0.0.0.0";
            if (hasIp)
            {
                var ipLbl = UIHelper.BuildLabel(infoGo.transform, server.IP, 120f);
                ipLbl.fontSize = 7f;
                ipLbl.color = new Color(0.5f, 0.75f, 0.95f, 1f);
                ipLbl.gameObject.GetComponent<LayoutElement>().preferredHeight = 8f;
            }

            // EOL countdown label (always created, shown/hidden by coroutine)
            bool initShowEol = eolWarn || eolAppr;
            var eolLbl = UIHelper.BuildLabel(infoGo.transform, initShowEol ? UIHelper.FormatEolTime(eolTime) : "", 120f);
            eolLbl.fontSize = 7f;
            eolLbl.color = initShowEol ? UIHelper.EolTimeColor(eolTime) : Color.clear;
            eolLbl.gameObject.GetComponent<LayoutElement>().preferredHeight = initShowEol ? 8f : 0f;

            // Customer logo (servers only)
            if (server != null)
            {
                int custId = server.GetCustomerID();
                if (custId >= 0)
                {
                    var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                    if (custItem != null && custItem.logo != null)
                    {
                        var logoGo = new GameObject("CustLogo");
                        logoGo.transform.SetParent(row.transform, false);
                        var logoImg = logoGo.AddComponent<Image>();
                        logoImg.sprite = custItem.logo;
                        logoImg.color = Color.white;
                        logoImg.preserveAspect = true;
                        logoImg.raycastTarget = false;
                        var logoLE = logoGo.AddComponent<LayoutElement>();
                        logoLE.preferredWidth = 22f;
                        logoLE.preferredHeight = 22f;
                    }
                }
            }

            // Status badge — live-refreshed by coroutine
            string statusText;
            Color statusTextColor;
            if (broken) { statusText = "BRK"; statusTextColor = UIHelper.StatusRed; }
            else if (on) { statusText = "ON";  statusTextColor = UIHelper.StatusGreen; }
            else         { statusText = "OFF"; statusTextColor = UIHelper.StatusGray; }

            var statusLbl = UIHelper.BuildLabel(row.transform, statusText, 56f);
            statusLbl.fontSize = 8f;
            statusLbl.color = statusTextColor;
            statusLbl.alignment = TextAlignmentOptions.Right;

            // Row is clickable → open device config (guarded: skip if checkbox was clicked)
            Server capturedSrv = server;
            NetworkSwitch capturedSw = sw;
            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb = new ColorBlock();
            cb.normalColor = new Color(0.14f, 0.14f, 0.17f, 1f);
            cb.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
            cb.pressedColor = new Color(0.08f, 0.08f, 0.10f, 1f);
            cb.selectedColor = new Color(0.14f, 0.14f, 0.17f, 1f);
            cb.colorMultiplier = 1f;
            cb.fadeDuration = 0.1f;
            btn.colors = cb;
            var nav = new Navigation();
            nav.mode = Navigation.Mode.None;
            btn.navigation = nav;
            btn.onClick.AddListener(new System.Action(() =>
            {
                if (_checkboxClick) { _checkboxClick = false; return; }
                Close();
                FloorMapApp.OpenDeviceFromDiagram(capturedSrv, capturedSw);
            }));

            // Track for live refresh
            if (server != null || sw != null)
                _liveRows.Add(new LiveRow { Server = server, Switch = sw, BorderImg = borderImg, StatusLbl = statusLbl, EolLbl = eolLbl });

            _slotRows.Add(row);
        }

        private static IEnumerator RefreshLiveRows()
        {
            while (_panelRoot != null && _panelRoot.activeSelf)
            {
                yield return new WaitForSeconds(1f);
                for (int i = 0; i < _liveRows.Count; i++)
                {
                    var lr = _liveRows[i];
                    if (lr.BorderImg == null || lr.StatusLbl == null) continue;

                    bool broken, eol, on;
                    int eolTime;
                    if (lr.Server != null)
                        UIHelper.GetDeviceState(lr.Server, out broken, out on, out eol, out eolTime);
                    else if (lr.Switch != null)
                        UIHelper.GetDeviceState(lr.Switch, out broken, out on, out eol, out eolTime);
                    else continue;

                    bool eolAppr = !broken && !eol && eolTime > 0 && eolTime <= FloorManagerMod.EOL_WARN_SECONDS;
                    Color borderColor = broken  ? UIHelper.StatusRed
                                      : eol     ? UIHelper.StatusOrange
                                      : eolAppr ? UIHelper.StatusCyan
                                      : on       ? UIHelper.StatusGreen
                                      :            UIHelper.StatusGray;
                    lr.BorderImg.color = borderColor;

                    if (broken)      { lr.StatusLbl.text = "BRK"; lr.StatusLbl.color = UIHelper.StatusRed; }
                    else if (on)     { lr.StatusLbl.text = "ON";  lr.StatusLbl.color = UIHelper.StatusGreen; }
                    else             { lr.StatusLbl.text = "OFF"; lr.StatusLbl.color = UIHelper.StatusGray; }

                    if (lr.EolLbl != null)
                    {
                        bool showEol = !broken && (eol || (eolTime > 0 && eolTime <= FloorManagerMod.EOL_WARN_SECONDS));
                        if (!showEol) { lr.EolLbl.text = ""; lr.EolLbl.color = Color.clear; }
                        else           UIHelper.ApplyEolLabel(lr.EolLbl, eolTime);
                    }
                }
            }
            _refreshCoroutine = null;
        }

        private static void BuildEmptySlotRow(int slotIndex)
        {
            var row = new GameObject($"EmptySlot_{slotIndex}");
            row.transform.SetParent(_slotContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.10f, 0.10f, 0.12f, 1f);

            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = SLOT_H;

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 4f;
            var pad = new RectOffset();
            pad.left = 0; pad.right = 4; pad.top = 0; pad.bottom = 0;
            hl.padding = pad;

            // Left border (dim)
            var border = new GameObject("Border");
            border.transform.SetParent(row.transform, false);
            var borderImg = border.AddComponent<Image>();
            borderImg.color = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            var borderLE = border.AddComponent<LayoutElement>();
            borderLE.preferredWidth = 3f;
            borderLE.preferredHeight = SLOT_H;

            // Slot label
            var slotLbl = UIHelper.BuildLabel(row.transform, $"U{slotIndex + 1}", 32f);
            slotLbl.fontSize = 8f;
            slotLbl.color = UIHelper.StatusGray;

            // "— empty —" dim label
            var emptyLbl = UIHelper.BuildLabel(row.transform, "- empty -", 80f);
            emptyLbl.fontSize = 8f;
            emptyLbl.color = new Color(0.35f, 0.35f, 0.35f);
            var emptyLE = emptyLbl.gameObject.GetComponent<LayoutElement>();
            emptyLE.flexibleWidth = 1f;

            // Buy button
            var buyBtn = UIHelper.BuildButton(row.transform, "Buy", 40f);
            var buyLE = buyBtn.gameObject.GetComponent<LayoutElement>();
            buyLE.preferredHeight = SLOT_H - 2f;
            buyBtn.onClick.AddListener(new System.Action(ShowMiniShop));

            _slotRows.Add(row);
        }

        // ── Action bar ──────────────────────────────────────────────

        private static void BuildActionBar(Transform parent)
        {
            _actionBar = new GameObject("ActionBar");
            _actionBar.transform.SetParent(parent, false);
            var barImg = _actionBar.AddComponent<Image>();
            barImg.color = new Color(0.06f, 0.06f, 0.08f, 1f);
            var barHL = _actionBar.AddComponent<HorizontalLayoutGroup>();
            barHL.childControlWidth = true;
            barHL.childControlHeight = true;
            barHL.childForceExpandWidth = false;
            barHL.childForceExpandHeight = false;
            barHL.spacing = 6f;
            var barPad = new RectOffset();
            barPad.left = 8; barPad.right = 8; barPad.top = 0; barPad.bottom = 0;
            barHL.padding = barPad;
            var barLE = _actionBar.AddComponent<LayoutElement>();
            barLE.preferredHeight = 36f;

            _selectionCountLbl = UIHelper.BuildLabel(_actionBar.transform, "0 selected", 80f);
            _selectionCountLbl.fontSize = 10f;
            _selectionCountLbl.color = Color.white;
            var countLE = _selectionCountLbl.gameObject.GetComponent<LayoutElement>();
            countLE.flexibleWidth = 1f;

            var powerBtn = UIHelper.BuildButton(_actionBar.transform, "Power ON", 70f);
            powerBtn.onClick.AddListener(new System.Action(PowerOnSelected));

            var powerOffBtn = UIHelper.BuildButton(_actionBar.transform, "Power OFF", 74f);
            powerOffBtn.onClick.AddListener(new System.Action(PowerOffSelected));

            var assignBtn = UIHelper.BuildButton(_actionBar.transform, "Assign Customer", 110f);
            assignBtn.onClick.AddListener(new System.Action(ShowAssignCustomerPanel));

            var clearBtn = UIHelper.BuildButton(_actionBar.transform, "Clear", 46f);
            clearBtn.onClick.AddListener(new System.Action(ClearSelection));

            _actionBar.SetActive(false);
        }

        private static void ToggleSelection(Server server, NetworkSwitch sw, int deviceId, Image fillImg)
        {
            bool wasSelected = false;
            if (server != null)
            {
                for (int i = 0; i < _selectedServers.Count; i++)
                {
                    if (_selectedServers[i].GetInstanceID() == deviceId)
                    {
                        wasSelected = true;
                        _selectedServers.RemoveAt(i);
                        break;
                    }
                }
                if (!wasSelected) _selectedServers.Add(server);
            }
            else if (sw != null)
            {
                for (int i = 0; i < _selectedSwitches.Count; i++)
                {
                    if (_selectedSwitches[i].GetInstanceID() == deviceId)
                    {
                        wasSelected = true;
                        _selectedSwitches.RemoveAt(i);
                        break;
                    }
                }
                if (!wasSelected) _selectedSwitches.Add(sw);
            }
            if (fillImg != null)
                fillImg.gameObject.SetActive(!wasSelected);
            UpdateActionBar();
        }

        private static void UpdateActionBar()
        {
            int total = _selectedServers.Count + _selectedSwitches.Count;
            if (total == 0) { _actionBar.SetActive(false); return; }
            _actionBar.SetActive(true);
            _selectionCountLbl.text = $"{total} selected";
        }

        private static void ClearSelection()
        {
            for (int i = 0; i < _selectedServers.Count; i++)
            {
                int id = _selectedServers[i].GetInstanceID();
                if (_checkFillImages.ContainsKey(id) && _checkFillImages[id] != null)
                    _checkFillImages[id].gameObject.SetActive(false);
            }
            for (int i = 0; i < _selectedSwitches.Count; i++)
            {
                int id = _selectedSwitches[i].GetInstanceID();
                if (_checkFillImages.ContainsKey(id) && _checkFillImages[id] != null)
                    _checkFillImages[id].gameObject.SetActive(false);
            }
            _selectedServers.Clear();
            _selectedSwitches.Clear();
            _actionBar.SetActive(false);
            if (_selectionCountLbl != null) _selectionCountLbl.text = "0 selected";
        }

        private static void PowerOnSelected()
        {
            int count = 0;
            for (int i = 0; i < _selectedServers.Count; i++)
            {
                var srv = _selectedServers[i];
                if (srv != null && !srv.isOn && !srv.isBroken) { srv.PowerButton(); count++; }
            }
            for (int i = 0; i < _selectedSwitches.Count; i++)
            {
                var s = _selectedSwitches[i];
                if (s != null && !s.isOn && !s.isBroken) { s.PowerButton(); count++; }
            }
            if (count > 0)
                StaticUIElements.instance.AddMeesageInField($"Powered on {count} devices");
        }

        private static void PowerOffSelected()
        {
            int count = 0;
            for (int i = 0; i < _selectedServers.Count; i++)
            {
                var srv = _selectedServers[i];
                if (srv != null && srv.isOn) { srv.PowerButton(); count++; }
            }
            for (int i = 0; i < _selectedSwitches.Count; i++)
            {
                var s = _selectedSwitches[i];
                if (s != null && s.isOn) { s.PowerButton(); count++; }
            }
            if (count > 0)
                StaticUIElements.instance.AddMeesageInField($"Powered off {count} devices");
        }

        // ── Assign customer panel ────────────────────────────────────

        private static void BuildAssignCustomerPanel(Transform parent)
        {
            _assignCustPanel = new GameObject("AssignCustPanel");
            _assignCustPanel.transform.SetParent(parent, false);
            var overlayRT = _assignCustPanel.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            var overlayCanvas = _assignCustPanel.AddComponent<Canvas>();
            overlayCanvas.overrideSorting = true;
            overlayCanvas.sortingOrder = 200;
            _assignCustPanel.AddComponent<GraphicRaycaster>();

            // Dim backdrop — click to close
            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(_assignCustPanel.transform, false);
            var dimRT = dimGo.AddComponent<RectTransform>();
            dimRT.anchorMin = Vector2.zero;
            dimRT.anchorMax = Vector2.one;
            dimRT.offsetMin = Vector2.zero;
            dimRT.offsetMax = Vector2.zero;
            var dimImg = dimGo.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.7f);
            var dimBtn = dimGo.AddComponent<Button>();
            dimBtn.targetGraphic = dimImg;
            var dimNav2 = new Navigation();
            dimNav2.mode = Navigation.Mode.None;
            dimBtn.navigation = dimNav2;
            dimBtn.onClick.AddListener(new System.Action(() => _assignCustPanel.SetActive(false)));

            // Inner panel
            var custPanel = new GameObject("CustPanel");
            custPanel.transform.SetParent(_assignCustPanel.transform, false);
            var custPanelRT = custPanel.AddComponent<RectTransform>();
            custPanelRT.anchorMin = new Vector2(0.1f, 0.15f);
            custPanelRT.anchorMax = new Vector2(0.9f, 0.85f);
            custPanelRT.offsetMin = Vector2.zero;
            custPanelRT.offsetMax = Vector2.zero;
            custPanel.AddComponent<Image>().color = new Color(0.09f, 0.09f, 0.11f, 1f);
            var custPanelVL = custPanel.AddComponent<VerticalLayoutGroup>();
            custPanelVL.childControlWidth = true;
            custPanelVL.childControlHeight = true;
            custPanelVL.childForceExpandWidth = true;
            custPanelVL.childForceExpandHeight = false;
            custPanelVL.spacing = 4f;
            var custPanelPad = new RectOffset();
            custPanelPad.left = 8; custPanelPad.right = 8; custPanelPad.top = 8; custPanelPad.bottom = 8;
            custPanelVL.padding = custPanelPad;

            // Title row
            var titleRow = new GameObject("TitleRow");
            titleRow.transform.SetParent(custPanel.transform, false);
            var titleHL = titleRow.AddComponent<HorizontalLayoutGroup>();
            titleHL.childControlWidth = true;
            titleHL.childControlHeight = true;
            titleHL.childForceExpandWidth = false;
            titleHL.childForceExpandHeight = false;
            titleHL.spacing = 8f;
            var titleRowLE = titleRow.AddComponent<LayoutElement>();
            titleRowLE.preferredHeight = 30f;

            var titleLbl = UIHelper.BuildLabel(titleRow.transform, "Assign Customer", 160f);
            titleLbl.fontSize = 13f;
            titleLbl.fontStyle = FontStyles.Bold;
            titleLbl.color = Color.white;
            var titleLblLE = titleLbl.gameObject.GetComponent<LayoutElement>();
            titleLblLE.flexibleWidth = 1f;

            var closeBtn2 = UIHelper.BuildButton(titleRow.transform, "X", 32f);
            closeBtn2.onClick.AddListener(new System.Action(() => _assignCustPanel.SetActive(false)));

            // Scroll view
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(custPanel.transform, false);
            var scrollAreaLE = scrollArea.AddComponent<LayoutElement>();
            scrollAreaLE.flexibleHeight = 1f;

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
            viewport.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scrollRect.viewport = vpRT;

            var content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);
            var contentRT = content.AddComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);
            contentRT.sizeDelta = Vector2.zero;
            var contentVL2 = content.AddComponent<VerticalLayoutGroup>();
            contentVL2.childControlWidth = true;
            contentVL2.childControlHeight = true;
            contentVL2.childForceExpandWidth = true;
            contentVL2.childForceExpandHeight = false;
            contentVL2.spacing = 2f;
            var contentCSF2 = content.AddComponent<ContentSizeFitter>();
            contentCSF2.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;
            _assignCustContent = content.transform;

            _assignCustPanel.SetActive(false);
        }

        private static void ShowAssignCustomerPanel()
        {
            for (int i = 0; i < _assignCustRows.Count; i++)
                if (_assignCustRows[i] != null) Object.Destroy(_assignCustRows[i]);
            _assignCustRows.Clear();

            var custBases = MainGameManager.instance.customerBases;
            if (custBases != null)
            {
                for (int i = 0; i < custBases.Count; i++)
                {
                    int custId = custBases[i].customerID;
                    var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                    string custName = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}";
                    Sprite logo = custItem != null ? custItem.logo : null;
                    BuildAssignCustRow(custId, custName, logo);
                }
            }
            _assignCustPanel.SetActive(true);
        }

        private static void BuildAssignCustRow(int custId, string custName, Sprite logo)
        {
            var row = new GameObject($"CustRow_{custId}");
            row.transform.SetParent(_assignCustContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.13f, 0.13f, 0.16f, 1f);

            var rowHL = row.AddComponent<HorizontalLayoutGroup>();
            rowHL.childControlWidth = true;
            rowHL.childControlHeight = true;
            rowHL.childForceExpandWidth = false;
            rowHL.childForceExpandHeight = false;
            rowHL.spacing = 8f;
            var rowPad = new RectOffset();
            rowPad.left = 8; rowPad.right = 8; rowPad.top = 0; rowPad.bottom = 0;
            rowHL.padding = rowPad;
            var rowLE = row.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 32f;

            if (logo != null)
            {
                var logoGo = new GameObject("Logo");
                logoGo.transform.SetParent(row.transform, false);
                var logoImg = logoGo.AddComponent<Image>();
                logoImg.sprite = logo;
                logoImg.color = Color.white;
                logoImg.preserveAspect = true;
                logoImg.raycastTarget = false;
                var logoLE = logoGo.AddComponent<LayoutElement>();
                logoLE.preferredWidth = 24f;
                logoLE.preferredHeight = 24f;
            }

            var nameLbl = UIHelper.BuildLabel(row.transform, custName ?? $"Customer {custId}", 140f);
            nameLbl.fontSize = 11f;
            nameLbl.color = Color.white;
            var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>();
            nameLE.flexibleWidth = 1f;

            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb2 = new ColorBlock();
            cb2.normalColor = new Color(0.13f, 0.13f, 0.16f, 1f);
            cb2.highlightedColor = new Color(0.20f, 0.20f, 0.24f, 1f);
            cb2.pressedColor = new Color(0.08f, 0.08f, 0.10f, 1f);
            cb2.selectedColor = new Color(0.13f, 0.13f, 0.16f, 1f);
            cb2.colorMultiplier = 1f;
            cb2.fadeDuration = 0.1f;
            btn.colors = cb2;
            var nav2 = new Navigation();
            nav2.mode = Navigation.Mode.None;
            btn.navigation = nav2;
            int capturedCustId = custId;
            btn.onClick.AddListener(new System.Action(() =>
            {
                AssignCustomerToSelected(capturedCustId);
                _assignCustPanel.SetActive(false);
            }));

            _assignCustRows.Add(row);
        }

        private static void AssignCustomerToSelected(int custId)
        {
            int assigned = 0;
            for (int i = 0; i < _selectedServers.Count; i++)
            {
                var srv = _selectedServers[i];
                if (srv == null) continue;
                srv.UpdateCustomer(custId);
                assigned++;
            }
            if (assigned > 0)
                StaticUIElements.instance.AddMeesageInField($"Assigned customer to {assigned} server(s)");
        }

        // ── Mini shop ───────────────────────────────────────────────

        // Returns the correct display name for an SFP module by its box type.
        // ShopItemSO.itemName is always empty in IL2CPP, so we can't rely on it.
        private static string GetSFPModuleDisplayName(int sfpBoxType)
        {
            switch (sfpBoxType)
            {
                case 0: return "SFP+ RJ45";
                case 1: return "SFP+ Fiber";
                case 2: return "SFP28";
                case 3: return "QSFP+";
                default: return $"SFP ({sfpBoxType})";
            }
        }

        // Resolves the display name for a switch ShopItemSO via its prefab.
        // ShopItemSO.itemName is always empty in IL2CPP for switches.
        private static string ResolveSwitchDisplayName(ShopItemSO so)
        {
            var mgr = MainGameManager.instance;
            if (mgr == null) return $"Switch {so.itemID}";
            var prefab = mgr.GetSwitchPrefab(so.itemID);
            if (prefab == null) return $"Switch {so.itemID}";
            var ns = prefab.GetComponent<NetworkSwitch>();
            if (ns == null) return $"Switch {so.itemID}";
            string name = mgr.ReturnSwitchNameFromType(ns.switchType);
            return (name != null && name.Length > 0) ? name : $"Switch {so.itemID}";
        }

        // Builds one SFPPortSlot per SFP port on the switch prefab.
        // HasModule=true when there's an unlocked shop SFP box matching that port type.
        private static List<SFPPortSlot> GetPortSlots(ShopItemSO switchSO)
        {
            var result = new List<SFPPortSlot>();
            var mgr = MainGameManager.instance;
            if (mgr == null) return result;

            var switchPrefab = mgr.GetSwitchPrefab(switchSO.itemID);
            if (switchPrefab == null) return result;

            // Build option map: sfpType -> SFPModuleOption
            var optionByType = new Dictionary<int, SFPModuleOption>();
            var cs = FloorManagerMod.ComputerShopRef;
            if (cs?.shopItems != null)
            {
                for (int i = 0; i < cs.shopItems.Length; i++)
                {
                    var item = cs.shopItems[i];
                    if (!item.isUnlocked) continue;
                    var so = item.shopItemSO;
                    if (so == null || so.itemType != PlayerManager.ObjectInHand.SFPBox) continue;

                    var boxPrefab = mgr.GetSfpBoxPrefab(so.itemID);
                    if (boxPrefab == null) continue;
                    var box = boxPrefab.GetComponent<SFPBox>();
                    if (box == null) continue;

                    int sfpType = box.sfpBoxType;
                    if (optionByType.ContainsKey(sfpType)) continue;

                    // sfpPositions is null on prefabs (set in Awake on instantiated objects).
                    // Fall back to live scene SFPBox instances, then prefab child count.
                    int capacity = 1;
                    var prefabPos = box.sfpPositions;
                    if (prefabPos != null && prefabPos.Length > 0)
                    {
                        capacity = prefabPos.Length;
                    }
                    else
                    {
                        var liveBoxes = Object.FindObjectsOfType<SFPBox>();
                        for (int li = 0; li < liveBoxes.Length; li++)
                        {
                            var lb = liveBoxes[li];
                            if (lb.sfpBoxType != sfpType) continue;
                            var lp = lb.sfpPositions;
                            if (lp != null && lp.Length > 0) { capacity = lp.Length; break; }
                        }
                        if (capacity <= 1)
                            capacity = Mathf.Max(1, boxPrefab.transform.childCount);
                    }

                    // Find the SFP module prefab whose sfpType matches this box type.
                    // Do NOT use GetSfpPrefab(sfpType) — sfpPrefabs may not be indexed by sfpBoxType.
                    GameObject modulePrefab = null;
                    var allSfpPrefabs = mgr.sfpPrefabs;
                    if (allSfpPrefabs != null)
                    {
                        for (int pi = 0; pi < allSfpPrefabs.Length; pi++)
                        {
                            var pgo = allSfpPrefabs[pi];
                            if (pgo == null) continue;
                            var pmod = pgo.GetComponent<SFPModule>();
                            if (pmod != null && pmod.sfpType == sfpType) { modulePrefab = pgo; break; }
                        }
                    }
                    if (modulePrefab == null) continue; // can't install without a prefab

                    optionByType[sfpType] = new SFPModuleOption
                    {
                        SO = so,
                        SFPType = sfpType,
                        BoxCapacity = capacity,
                        DisplayName = GetSFPModuleDisplayName(sfpType),
                        ModulePrefab = modulePrefab
                    };
                }
            }

            // One slot per SFP port on the prefab.
            // The game's sfpTypeSupported values don't match switch names (e.g. "32 x QSFP+" ports
            // have sfpTypeSupported=1 which maps to SFP+ Fiber, not QSFP+). Override the module
            // lookup using the switch's display name so the correct module type is shown and installed.
            var links = switchPrefab.GetComponentsInChildren<CableLink>();

            var nsCmp = switchPrefab.GetComponent<NetworkSwitch>();
            string swDisplayName = nsCmp != null ? mgr.ReturnSwitchNameFromType(nsCmp.switchType) : "";
            bool nameHasQSFP  = swDisplayName.Contains("QSFP+");
            bool nameHasSFP28 = swDisplayName.Contains("SFP28");

            int portIdx = 0;
            for (int i = 0; i < links.Length; i++)
            {
                var cl = links[i];
                if (!cl.isSFPPort) continue;

                int originalType = cl.sfpTypeSupported;

                // Build the list of compatible module types for this port based on switch name.
                // "QSFP+" fiber ports → QSFP+ module only
                // "SFP+/SFP28" copper-class ports → offer RJ45, Fiber SFP+, and SFP28
                // Default → use the port's own sfpTypeSupported
                var compatibleTypes = new List<int>();
                if (nameHasQSFP && originalType == 1)
                    compatibleTypes.Add(3);
                else if (nameHasSFP28 && originalType == 0)
                {
                    compatibleTypes.Add(0); // SFP+ RJ45
                    compatibleTypes.Add(1); // SFP+ Fiber
                    compatibleTypes.Add(2); // SFP28
                }
                else
                    compatibleTypes.Add(originalType);

                var portOptions = new List<SFPModuleOption>();
                for (int ci = 0; ci < compatibleTypes.Count; ci++)
                {
                    if (optionByType.TryGetValue(compatibleTypes[ci], out var opt))
                        portOptions.Add(opt);
                }
                // Fallback: if name override yielded nothing, use original type
                if (portOptions.Count == 0 && optionByType.TryGetValue(originalType, out var fallback))
                    portOptions.Add(fallback);

                result.Add(new SFPPortSlot
                {
                    PortIndex        = portIdx,
                    Options          = portOptions,
                    SelectedOptionIdx = -1,
                    OriginalPortType = originalType
                });
                portIdx++;
            }
            return result;
        }

        private static void InsertSelectedSFPs(NetworkSwitch sw, List<SFPPortSlot> slots)
        {
            var mgr = MainGameManager.instance;
            if (mgr == null) return;

            // Use GetComponentsInChildren to iterate CableLinks in the SAME hierarchy
            // order as the prefab (which is how _popupSlots indices were built in GetPortSlots).
            // Do NOT use cableLinkSwitchPorts — its array order may differ from hierarchy order.
            var allLinks = sw.GetComponentsInChildren<CableLink>();
            var sfpPorts = new List<CableLink>();
            for (int i = 0; i < allLinks.Length; i++)
            {
                if (allLinks[i].isSFPPort) sfpPorts.Add(allLinks[i]);
            }

            if (sfpPorts.Count == 0) return;

            // sfpPorts[i] now corresponds to _popupSlots[i] (same iteration order as prefab).
            int inserted = 0;
            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (!slot.IsSelected || !slot.HasModule) continue;
                if (i >= sfpPorts.Count) continue;

                var targetPort = sfpPorts[i];
                if (targetPort.insertedSFP != null) continue;

                var sfpPrefab = slot.SelectedModule.ModulePrefab;
                if (sfpPrefab == null) continue;

                var sfpGo = Object.Instantiate(sfpPrefab);
                var sfpMod = sfpGo.GetComponent<SFPModule>();
                if (sfpMod != null)
                {
                    PlayerManager.instance.playerClass.UpdateCoin(-(float)slot.SelectedModule.PricePerModule, false);
                    sfpMod.InsertDirectlyIntoPort(targetPort);
                    inserted++;
                }
                else
                {
                    Object.Destroy(sfpGo);
                }
            }

            if (inserted > 0)
                StaticUIElements.instance.AddMeesageInField($"Installed {inserted} SFP module(s) in new switch");
        }

        private static void ShowSFPConfigPopup(ShopItemSO switchSO, List<SFPPortSlot> slots)
        {
            _popupSwitchSO = switchSO;
            _popupSlots = new List<SFPPortSlot>(slots);
            _popupSelectedPortIdx = -1;
            _portDetailContent = null;

            if (_sfpConfigPopup != null) Object.Destroy(_sfpConfigPopup);

            _sfpConfigPopup = new GameObject("SFPConfigPopup");
            _sfpConfigPopup.transform.SetParent(_miniShopPanel.transform, false);

            var rt = _sfpConfigPopup.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;

            var canvas = _sfpConfigPopup.AddComponent<Canvas>();
            canvas.overrideSorting = true; canvas.sortingOrder = 20;
            _sfpConfigPopup.AddComponent<GraphicRaycaster>();
            _sfpConfigPopup.AddComponent<Image>().color = new Color(0.06f, 0.06f, 0.08f, 0.98f);

            var rootVL = _sfpConfigPopup.AddComponent<VerticalLayoutGroup>();
            rootVL.childControlWidth = true; rootVL.childControlHeight = true;
            rootVL.childForceExpandWidth = true; rootVL.childForceExpandHeight = false;
            rootVL.spacing = 4f;
            var rootPad = new RectOffset();
            rootPad.left = 10; rootPad.right = 10; rootPad.top = 8; rootPad.bottom = 8;
            rootVL.padding = rootPad;

            // ── Resolve switch display name ──
            var mgr = MainGameManager.instance;
            var switchPrefab = mgr != null ? mgr.GetSwitchPrefab(switchSO.itemID) : null;
            var allLinks = switchPrefab != null
                ? switchPrefab.GetComponentsInChildren<CableLink>()
                : new CableLink[0];
            string swPopupName = ResolveSwitchDisplayName(switchSO);

            // ── Header ─────────────────────────────────────────────────
            var header = new GameObject("Header");
            header.transform.SetParent(_sfpConfigPopup.transform, false);
            var headerHL = header.AddComponent<HorizontalLayoutGroup>();
            headerHL.childControlWidth = true; headerHL.childControlHeight = true;
            headerHL.childForceExpandWidth = false; headerHL.spacing = 8f;
            header.AddComponent<LayoutElement>().preferredHeight = 28f;

            var titleLbl = UIHelper.BuildLabel(header.transform, $"Configure  —  {swPopupName}", 200f);
            titleLbl.fontSize = 11f; titleLbl.fontStyle = FontStyles.Bold; titleLbl.color = Color.white;
            titleLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var closeBtn = UIHelper.BuildButton(header.transform, "X", 28f);
            closeBtn.onClick.AddListener(new System.Action(HideSFPConfigPopup));

            // Map each port index → slot index (-1 = not an SFP port)
            _portToSlotMap = new int[allLinks.Length];
            _portBtnImages = new Image[allLinks.Length];
            int sfpSlotCounter = 0;
            for (int i = 0; i < allLinks.Length; i++)
            {
                if (allLinks[i].isSFPPort && sfpSlotCounter < _popupSlots.Count)
                    _portToSlotMap[i] = sfpSlotCounter++;
                else
                    _portToSlotMap[i] = -1;
            }

            // Color key row – compact fixed-height strip
            // childControlHeight=false + childForceExpandHeight=false keeps items at their sizeDelta.y
            var keyRow = new GameObject("Key");
            keyRow.transform.SetParent(_sfpConfigPopup.transform, false);
            var keyHL = keyRow.AddComponent<HorizontalLayoutGroup>();
            keyHL.childControlWidth = false; keyHL.childControlHeight = false;
            keyHL.childForceExpandWidth = false; keyHL.childForceExpandHeight = false;
            keyHL.spacing = 10f;
            var keyPad2 = new RectOffset(); keyPad2.top = 3; keyPad2.bottom = 3;
            keyHL.padding = keyPad2;
            var keyRowLE = keyRow.AddComponent<LayoutElement>();
            keyRowLE.minHeight = 16f; keyRowLE.preferredHeight = 16f;

            BuildKeyChip(keyRow.transform, new Color(0.18f, 0.18f, 0.20f), "Other");
            BuildKeyChip(keyRow.transform, new Color(0.10f, 0.18f, 0.38f), "SFP");
            BuildKeyChip(keyRow.transform, new Color(0.10f, 0.38f, 0.15f), "Added");
            BuildKeyChip(keyRow.transform, new Color(0.35f, 0.28f, 0.08f), "No module");

            // ── Physical port layout ─────────────────────────────────────
            // Sample each CableLink's position in switch-local space, cluster
            // by Y into rows and sort by X within each row, so the diagram
            // matches the real front-panel layout.
            const float PORT_SIZE = 22f;
            const float PORT_GAP  = 3f;
            const float DIAG_PAD  = 5f;

            var portPositions = new Vector3[allLinks.Length];
            var rootT = switchPrefab != null ? switchPrefab.transform : null;
            for (int i = 0; i < allLinks.Length; i++)
                portPositions[i] = rootT != null
                    ? rootT.InverseTransformPoint(allLinks[i].transform.position)
                    : Vector3.zero;

            // Determine which axis (Y or Z) has more spread — use that as the row axis.
            // Switches may be oriented so Z is vertical on the front panel rather than Y.
            float minY2 = float.MaxValue, maxY2 = float.MinValue;
            float minZ2 = float.MaxValue, maxZ2 = float.MinValue;
            for (int i = 0; i < allLinks.Length; i++)
            {
                if (portPositions[i].y < minY2) minY2 = portPositions[i].y;
                if (portPositions[i].y > maxY2) maxY2 = portPositions[i].y;
                if (portPositions[i].z < minZ2) minZ2 = portPositions[i].z;
                if (portPositions[i].z > maxZ2) maxZ2 = portPositions[i].z;
            }
            bool useZForRows = (maxZ2 - minZ2) > (maxY2 - minY2);

            // Cluster row values with a tight snap (5 mm) so 2-row 1U switches separate correctly
            const float Y_SNAP = 0.005f;
            var uniqueYs = new List<float>();
            for (int i = 0; i < allLinks.Length; i++)
            {
                float y = useZForRows ? portPositions[i].z : portPositions[i].y;
                bool found = false;
                for (int j = 0; j < uniqueYs.Count; j++)
                    if (Mathf.Abs(uniqueYs[j] - y) < Y_SNAP) { found = true; break; }
                if (!found) uniqueYs.Add(y);
            }
            // Sort descending: higher value = top of panel = first row in UI
            for (int a = 0; a < uniqueYs.Count - 1; a++)
                for (int b = a + 1; b < uniqueYs.Count; b++)
                    if (uniqueYs[b] > uniqueYs[a])
                    { float tmp = uniqueYs[a]; uniqueYs[a] = uniqueYs[b]; uniqueYs[b] = tmp; }

            // Build rows: each row = allLinks indices sorted left-to-right by X
            var portRows = new List<List<int>>();
            int maxCols = 0;
            for (int r = 0; r < uniqueYs.Count; r++)
            {
                var row = new List<int>();
                for (int i = 0; i < allLinks.Length; i++)
                {
                    float rv = useZForRows ? portPositions[i].z : portPositions[i].y;
                    if (Mathf.Abs(rv - uniqueYs[r]) < Y_SNAP) row.Add(i);
                }
                // Insertion sort by X descending (largest X = physically leftmost when facing the switch)
                for (int a = 1; a < row.Count; a++)
                {
                    int key = row[a]; int b = a - 1;
                    while (b >= 0 && portPositions[row[b]].x < portPositions[key].x)
                    { row[b + 1] = row[b]; b--; }
                    row[b + 1] = key;
                }
                portRows.Add(row);
                if (row.Count > maxCols) maxCols = row.Count;
            }
            if (portRows.Count == 0) { portRows.Add(new List<int>()); maxCols = 0; }

            int numRows = portRows.Count;

            // ── Detect physical port group boundaries ─────────────────────────────
            // When the X gap between two adjacent sorted ports exceeds 1.8× the median
            // gap in that row, treat it as a cluster boundary and add GROUP_GAP pixels.
            const float GROUP_GAP = 6f;
            var rowGroupOffsets = new List<float[]>(); // per row, per col: cumulative extra px
            int maxBoundariesAnyRow = 0;
            for (int r = 0; r < portRows.Count; r++)
            {
                var row = portRows[r];
                var offsets = new float[row.Count];
                if (row.Count > 1)
                {
                    var gaps = new float[row.Count - 1];
                    for (int c2 = 1; c2 < row.Count; c2++)
                        gaps[c2 - 1] = Mathf.Abs(portPositions[row[c2 - 1]].x - portPositions[row[c2]].x);
                    // median gap
                    var sg = new List<float>(gaps);
                    for (int a2 = 0; a2 < sg.Count - 1; a2++)
                        for (int b2 = a2 + 1; b2 < sg.Count; b2++)
                            if (sg[b2] < sg[a2]) { float t2 = sg[a2]; sg[a2] = sg[b2]; sg[b2] = t2; }
                    float medGap = sg[sg.Count / 2];
                    float thresh = medGap * 1.8f;
                    float cum = 0f; int boundaries = 0;
                    offsets[0] = 0f;
                    for (int c2 = 1; c2 < row.Count; c2++)
                    {
                        if (gaps[c2 - 1] > thresh) { cum += GROUP_GAP; boundaries++; }
                        offsets[c2] = cum;
                    }
                    if (boundaries > maxBoundariesAnyRow) maxBoundariesAnyRow = boundaries;
                }
                rowGroupOffsets.Add(offsets);
            }

            // Scale port cells to fit all columns within the panel — no horizontal scroll.
            // Panel inner width = 460 (sizeDelta) - 20 (rootVL padding 10+10) = 440.
            // Reserve space for group gap inserts so ports still fit.
            const float PANEL_INNER_W = 440f;
            float groupGapTotal = maxBoundariesAnyRow * GROUP_GAP;
            float availPortW = PANEL_INNER_W - 2f * DIAG_PAD - groupGapTotal;
            float cellStep = maxCols > 0 ? availPortW / maxCols : (PORT_SIZE + PORT_GAP);
            float dynSize = Mathf.Clamp(cellStep - PORT_GAP, 8f, PORT_SIZE);
            float dynGap  = cellStep - dynSize;

            float diagH = numRows * (dynSize + dynGap) - dynGap + 2f * DIAG_PAD;
            float diagAreaH = Mathf.Min(diagH + 4f, 120f);

            var diagArea = new GameObject("DiagramArea");
            diagArea.transform.SetParent(_sfpConfigPopup.transform, false);
            diagArea.AddComponent<LayoutElement>().preferredHeight = diagAreaH;

            var diagScroll = diagArea.AddComponent<ScrollRect>();
            diagScroll.horizontal = false; diagScroll.vertical = diagH > diagAreaH;

            var diagVP = new GameObject("Viewport");
            diagVP.transform.SetParent(diagArea.transform, false);
            var diagVPRT = diagVP.AddComponent<RectTransform>();
            diagVPRT.anchorMin = Vector2.zero; diagVPRT.anchorMax = Vector2.one;
            diagVPRT.sizeDelta = Vector2.zero;
            diagVPRT.offsetMin = Vector2.zero; diagVPRT.offsetMax = Vector2.zero;
            diagVP.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.12f, 1f);
            diagVP.AddComponent<Mask>().showMaskGraphic = true;
            diagScroll.viewport = diagVPRT;

            var diagContent = new GameObject("Content");
            diagContent.transform.SetParent(diagVP.transform, false);
            var diagContentRT = diagContent.AddComponent<RectTransform>();
            // Stretch full width of viewport; only sizeDelta.y matters (content height)
            diagContentRT.anchorMin = new Vector2(0f, 1f); diagContentRT.anchorMax = new Vector2(1f, 1f);
            diagContentRT.pivot = new Vector2(0f, 1f);
            diagContentRT.sizeDelta = new Vector2(0f, diagH);
            diagScroll.content = diagContentRT;

            // Place one port square per CableLink at its physical row/column position
            for (int r = 0; r < portRows.Count; r++)
            {
                var row = portRows[r];
                var rOffsets = rowGroupOffsets[r];
                for (int c = 0; c < row.Count; c++)
                {
                    int i = row[c];
                    int portIdx = i;
                    int slotIdx = _portToSlotMap[i];
                    bool isSFP = allLinks[i].isSFPPort;

                    float px = DIAG_PAD + c * (dynSize + dynGap) + rOffsets[c];
                    float py = -(DIAG_PAD + r * (dynSize + dynGap));

                    var portGo = new GameObject($"P{i}");
                    portGo.transform.SetParent(diagContent.transform, false);
                    var portRT = portGo.AddComponent<RectTransform>();
                    portRT.anchorMin = new Vector2(0f, 1f); portRT.anchorMax = new Vector2(0f, 1f);
                    portRT.pivot = new Vector2(0f, 1f);
                    portRT.sizeDelta = new Vector2(dynSize, dynSize);
                    portRT.anchoredPosition = new Vector2(px, py);

                    var portImg = portGo.AddComponent<Image>();
                    portImg.color = GetPortColor(slotIdx, false);
                    _portBtnImages[i] = portImg;

                    var numLbl = UIHelper.BuildLabel(portGo.transform, (i + 1).ToString(), dynSize);
                    numLbl.fontSize = 7f;
                    numLbl.color = new Color(0.80f, 0.80f, 0.80f);
                    numLbl.alignment = TextAlignmentOptions.Center;
                    numLbl.raycastTarget = false;
                    var numRT = numLbl.GetComponent<RectTransform>();
                    numRT.anchorMin = Vector2.zero; numRT.anchorMax = Vector2.one;
                    numRT.offsetMin = Vector2.zero; numRT.offsetMax = Vector2.zero;

                    if (isSFP && slotIdx >= 0)
                    {
                        var portBtn = portGo.AddComponent<Button>();
                        portBtn.targetGraphic = portImg;
                        var pcb = new ColorBlock();
                        pcb.normalColor = Color.white;
                        pcb.highlightedColor = new Color(0.85f, 0.85f, 1.0f, 1f);
                        pcb.pressedColor = new Color(0.60f, 0.60f, 0.60f, 1f);
                        pcb.selectedColor = Color.white;
                        pcb.colorMultiplier = 1f; pcb.fadeDuration = 0.08f;
                        portBtn.colors = pcb;
                        var pNav = new Navigation(); pNav.mode = Navigation.Mode.None; portBtn.navigation = pNav;
                        int capturedPort = portIdx;
                        int capturedSlot = slotIdx;
                        portBtn.onClick.AddListener(new System.Action(() =>
                        {
                            _popupSelectedPortIdx = capturedPort;
                            RefreshPortVisuals();
                            BuildPortDetail(capturedSlot);
                        }));
                    }
                }
            }

            UIHelper.BuildDivider(_sfpConfigPopup.transform);

            // ── Preset bar ──────────────────────────────────────────────
            var presetBar = new GameObject("PresetBar");
            presetBar.transform.SetParent(_sfpConfigPopup.transform, false);
            var presetHL = presetBar.AddComponent<HorizontalLayoutGroup>();
            presetHL.childControlWidth = true; presetHL.childControlHeight = true;
            presetHL.childForceExpandWidth = false; presetHL.spacing = 6f;
            var presetPad = new RectOffset(); presetPad.left = 4; presetPad.right = 4;
            presetHL.padding = presetPad;
            presetBar.AddComponent<LayoutElement>().preferredHeight = 28f;

            int capturedSwitchIdPreset = switchSO.itemID;
            var loadBtn = UIHelper.BuildButton(presetBar.transform, "Load", 60f);
            loadBtn.GetComponent<LayoutElement>().preferredHeight = 24f;
            ReusableFunctions.ChangeButtonNormalColor(loadBtn, new Color(0.12f, 0.18f, 0.30f, 1f));
            loadBtn.onClick.AddListener(new System.Action(() => ShowPresetLoadPopup(capturedSwitchIdPreset)));

            var savePresetBtn = UIHelper.BuildButton(presetBar.transform, "Save Preset", 90f);
            savePresetBtn.GetComponent<LayoutElement>().preferredHeight = 24f;
            ReusableFunctions.ChangeButtonNormalColor(savePresetBtn, new Color(0.12f, 0.25f, 0.12f, 1f));
            savePresetBtn.onClick.AddListener(new System.Action(() => SavePresetFromPopup(capturedSwitchIdPreset)));

            var presetSpacer = new GameObject("S"); presetSpacer.transform.SetParent(presetBar.transform, false);
            presetSpacer.AddComponent<RectTransform>(); presetSpacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            UIHelper.BuildDivider(_sfpConfigPopup.transform);

            // ── Detail panel (rebuilt when port clicked) ────────────────
            var detailOuter = new GameObject("Detail");
            detailOuter.transform.SetParent(_sfpConfigPopup.transform, false);
            detailOuter.AddComponent<LayoutElement>().flexibleHeight = 1f;
            var detailVL = detailOuter.AddComponent<VerticalLayoutGroup>();
            detailVL.childControlWidth = true; detailVL.childControlHeight = true;
            detailVL.childForceExpandWidth = true; detailVL.childForceExpandHeight = false;
            detailVL.spacing = 2f;
            _portDetailContent = detailOuter.transform;

            var hintLbl = UIHelper.BuildLabel(_portDetailContent, "Click an SFP port above to configure it", 300f);
            hintLbl.fontSize = 10f; hintLbl.color = new Color(0.40f, 0.40f, 0.40f);
            hintLbl.alignment = TextAlignmentOptions.Center;
            hintLbl.GetComponent<LayoutElement>().preferredHeight = 28f;

            UIHelper.BuildDivider(_sfpConfigPopup.transform);

            // ── Footer ──────────────────────────────────────────────────
            _popupQty = 1;

            var footer = new GameObject("Footer");
            footer.transform.SetParent(_sfpConfigPopup.transform, false);
            var footerVL = footer.AddComponent<VerticalLayoutGroup>();
            footerVL.childControlWidth = true; footerVL.childControlHeight = true;
            footerVL.childForceExpandWidth = true; footerVL.childForceExpandHeight = false;
            footerVL.spacing = 3f;
            footer.AddComponent<LayoutElement>().preferredHeight = 64f;

            // Qty row
            var qtyRow = new GameObject("QtyRow");
            qtyRow.transform.SetParent(footer.transform, false);
            var qtyHL = qtyRow.AddComponent<HorizontalLayoutGroup>();
            qtyHL.childControlWidth = true; qtyHL.childControlHeight = true;
            qtyHL.childForceExpandWidth = false; qtyHL.spacing = 6f;
            qtyRow.AddComponent<LayoutElement>().preferredHeight = 26f;

            var qtyLabelLbl = UIHelper.BuildLabel(qtyRow.transform, "Quantity:", 60f);
            qtyLabelLbl.fontSize = 9f; qtyLabelLbl.color = new Color(0.6f, 0.6f, 0.6f);
            qtyLabelLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var minusBtn = UIHelper.BuildButton(qtyRow.transform, "−", 26f);
            minusBtn.GetComponent<LayoutElement>().preferredHeight = 24f;

            _popupQtyLbl = UIHelper.BuildLabel(qtyRow.transform, "1", 30f);
            _popupQtyLbl.fontSize = 11f; _popupQtyLbl.fontStyle = FontStyles.Bold;
            _popupQtyLbl.color = Color.white; _popupQtyLbl.alignment = TextAlignmentOptions.Center;

            var plusBtn = UIHelper.BuildButton(qtyRow.transform, "+", 26f);
            plusBtn.GetComponent<LayoutElement>().preferredHeight = 24f;

            // Buy row
            var buyRow = new GameObject("BuyRow");
            buyRow.transform.SetParent(footer.transform, false);
            var buyHL = buyRow.AddComponent<HorizontalLayoutGroup>();
            buyHL.childControlWidth = true; buyHL.childControlHeight = true;
            buyHL.childForceExpandWidth = false; buyHL.spacing = 8f;
            buyRow.AddComponent<LayoutElement>().preferredHeight = 34f;

            var basePriceLbl = UIHelper.BuildLabel(buyRow.transform, $"Switch: ${switchSO.price:N0}", 110f);
            basePriceLbl.fontSize = 9f; basePriceLbl.color = new Color(0.6f, 0.6f, 0.6f);
            basePriceLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var buyBtn = UIHelper.BuildButton(buyRow.transform, $"Add  ${switchSO.price:N0}", 130f);
            _popupBuyLbl = buyBtn.GetComponentInChildren<TextMeshProUGUI>();
            ReusableFunctions.ChangeButtonNormalColor(buyBtn, new Color(0.10f, 0.28f, 0.10f, 1f));
            ShopItemSO capturedSOp = switchSO;
            string capturedSwName = swPopupName;
            buyBtn.onClick.AddListener(new System.Action(() => AddSFPToCart(capturedSOp, capturedSwName, _popupSlots)));

            // Qty stepper listeners
            minusBtn.onClick.AddListener(new System.Action(() =>
            {
                if (_popupQty > 1) { _popupQty--; RefreshPopupTotal(); }
            }));
            plusBtn.onClick.AddListener(new System.Action(() =>
            {
                _popupQty++;
                RefreshPopupTotal();
            }));

            RefreshPopupTotal();
        }

        // Compact colored square + label for the diagram legend.
        // Parent HLG must have childControlHeight=false so sizeDelta.y is respected.
        private static void BuildKeyChip(Transform parent, Color color, string label)
        {
            var go = new GameObject("KC_" + label);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(62f, 10f);

            var hl = go.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = false; hl.childControlHeight = false;
            hl.childForceExpandWidth = false; hl.childForceExpandHeight = false;
            hl.spacing = 3f;

            // Colored dot
            var dot = new GameObject("D");
            dot.transform.SetParent(go.transform, false);
            var drt = dot.AddComponent<RectTransform>();
            drt.sizeDelta = new Vector2(8f, 8f);
            dot.AddComponent<Image>().color = color;

            // Text label
            var lbl = new GameObject("L");
            lbl.transform.SetParent(go.transform, false);
            var lrt = lbl.AddComponent<RectTransform>();
            lrt.sizeDelta = new Vector2(51f, 10f);
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 7f;
            tmp.color = new Color(0.52f, 0.52f, 0.52f);
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
        }

        // Returns the color a port square should display.
        private static Color GetPortColor(int slotIdx, bool highlighted)
        {
            Color col;
            if (slotIdx < 0)
                col = new Color(0.18f, 0.18f, 0.20f, 1f);
            else if (_popupSlots != null && slotIdx < _popupSlots.Count && _popupSlots[slotIdx].IsSelected)
                col = new Color(0.10f, 0.38f, 0.15f, 1f);
            else if (_popupSlots != null && slotIdx < _popupSlots.Count && _popupSlots[slotIdx].HasModule)
                col = new Color(0.10f, 0.18f, 0.38f, 1f);
            else
                col = new Color(0.35f, 0.28f, 0.08f, 1f);
            if (highlighted)
                col = new Color(Mathf.Min(col.r + 0.18f, 1f), Mathf.Min(col.g + 0.18f, 1f), Mathf.Min(col.b + 0.18f, 1f), 1f);
            return col;
        }

        private static void RefreshPortVisuals()
        {
            if (_portBtnImages == null || _portToSlotMap == null) return;
            for (int i = 0; i < _portBtnImages.Length; i++)
            {
                if (_portBtnImages[i] == null) continue;
                _portBtnImages[i].color = GetPortColor(_portToSlotMap[i], i == _popupSelectedPortIdx);
            }
        }

        // Rebuilds the detail/dropdown area for the clicked SFP slot.
        private static void BuildPortDetail(int slotIdx)
        {
            if (_portDetailContent == null) return;
            for (int i = _portDetailContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_portDetailContent.GetChild(i).gameObject);

            var slot = _popupSlots[slotIdx];

            string portTypeName = !slot.HasModule ? "SFP port"
                : slot.Options.Count == 1 ? slot.Options[0].DisplayName
                : "SFP+ / SFP28";
            var portHeader = UIHelper.BuildLabel(_portDetailContent,
                $"Port {slotIdx + 1}  —  {portTypeName}", 300f);
            portHeader.fontSize = 10f; portHeader.fontStyle = FontStyles.Bold; portHeader.color = Color.white;
            portHeader.GetComponent<LayoutElement>().preferredHeight = 22f;

            if (!slot.HasModule)
            {
                var noLbl = UIHelper.BuildLabel(_portDetailContent, "No compatible module is unlocked in the shop.", 300f);
                noLbl.fontSize = 9f; noLbl.color = new Color(0.45f, 0.45f, 0.45f);
                noLbl.GetComponent<LayoutElement>().preferredHeight = 20f;
                return;
            }

            BuildDetailOption(slotIdx, -1); // None (leave empty)
            for (int o = 0; o < slot.Options.Count; o++)
                BuildDetailOption(slotIdx, o);

            // Fill All — applies the current selection (or option 0) to all same-type unfilled ports
            int fillOrigType = slot.OriginalPortType;
            int fillOptIdx = slot.IsSelected ? slot.SelectedOptionIdx : 0;
            int unfilledCount = 0;
            for (int i = 0; i < _popupSlots.Count; i++)
            {
                var s = _popupSlots[i];
                if (s.OriginalPortType == fillOrigType && !s.IsSelected && s.HasModule)
                    unfilledCount++;
            }

            if (unfilledCount > 0)
            {
                var fillRow = new GameObject("FillAll");
                fillRow.transform.SetParent(_portDetailContent, false);
                var fillHL = fillRow.AddComponent<HorizontalLayoutGroup>();
                fillHL.childControlWidth = true; fillHL.childControlHeight = true;
                fillHL.childForceExpandWidth = false; fillHL.spacing = 6f;
                var fillPad = new RectOffset(); fillPad.left = 10; fillPad.right = 10;
                fillHL.padding = fillPad;
                fillRow.AddComponent<LayoutElement>().preferredHeight = 26f;

                var spacer = new GameObject("S"); spacer.transform.SetParent(fillRow.transform, false);
                spacer.AddComponent<RectTransform>(); spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

                int capturedOrigType = fillOrigType;
                int capturedOptIdx   = fillOptIdx;
                int capturedSlot     = slotIdx;
                var fillBtn = UIHelper.BuildButton(fillRow.transform,
                    $"Fill all open  (+{unfilledCount})", 140f);
                fillBtn.GetComponent<LayoutElement>().preferredHeight = 22f;
                ReusableFunctions.ChangeButtonNormalColor(fillBtn, new Color(0.08f, 0.20f, 0.28f, 1f));
                fillBtn.onClick.AddListener(new System.Action(() =>
                {
                    for (int i = 0; i < _popupSlots.Count; i++)
                    {
                        var s = _popupSlots[i];
                        if (s.OriginalPortType == capturedOrigType && !s.IsSelected && s.HasModule)
                        { s.SelectedOptionIdx = capturedOptIdx; _popupSlots[i] = s; }
                    }
                    RefreshPortVisuals();
                    RefreshPopupTotal();
                    BuildPortDetail(capturedSlot);
                }));
            }
        }

        // Builds one radio-button-style option row in the detail panel.
        // optionIdx = -1 → "None (leave empty)"; >=0 → index into slot.Options
        private static void BuildDetailOption(int slotIdx, int optionIdx)
        {
            var slot = _popupSlots[slotIdx];
            bool selected = optionIdx < 0 ? !slot.IsSelected : (slot.SelectedOptionIdx == optionIdx);

            var row = new GameObject(optionIdx < 0 ? "None" : $"Opt{optionIdx}");
            row.transform.SetParent(_portDetailContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = selected ? new Color(0.10f, 0.22f, 0.12f, 1f) : new Color(0.12f, 0.12f, 0.15f, 1f);

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true; hl.childControlHeight = true;
            hl.childForceExpandWidth = false; hl.spacing = 8f;
            var hlPad = new RectOffset();
            hlPad.left = 10; hlPad.right = 10; hlPad.top = 0; hlPad.bottom = 0;
            hl.padding = hlPad;
            row.AddComponent<LayoutElement>().preferredHeight = 28f;

            // Radio dot
            var radio = new GameObject("R");
            radio.transform.SetParent(row.transform, false);
            radio.AddComponent<Image>().color = new Color(0.25f, 0.25f, 0.30f, 1f);
            var radioLE = radio.AddComponent<LayoutElement>();
            radioLE.preferredWidth = 12f; radioLE.preferredHeight = 12f;
            if (selected)
            {
                var fill = new GameObject("F");
                fill.transform.SetParent(radio.transform, false);
                var fillRT = fill.AddComponent<RectTransform>();
                fillRT.anchorMin = new Vector2(0.2f, 0.2f); fillRT.anchorMax = new Vector2(0.8f, 0.8f);
                fillRT.offsetMin = Vector2.zero; fillRT.offsetMax = Vector2.zero;
                fill.AddComponent<Image>().color = UIHelper.StatusGreen;
            }

            string text = optionIdx < 0
                ? "None  (leave empty)"
                : $"{slot.Options[optionIdx].DisplayName}   ${slot.Options[optionIdx].PricePerModule:N0}";
            var lbl = UIHelper.BuildLabel(row.transform, text, 200f);
            lbl.fontSize = 10f;
            lbl.color = selected ? Color.white : new Color(0.72f, 0.72f, 0.72f);
            lbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var btn = row.AddComponent<Button>();
            btn.targetGraphic = rowImg;
            var cb = new ColorBlock();
            cb.normalColor = rowImg.color;
            cb.highlightedColor = new Color(0.22f, 0.32f, 0.24f, 1f);
            cb.pressedColor = new Color(0.07f, 0.14f, 0.08f, 1f);
            cb.selectedColor = cb.normalColor; cb.colorMultiplier = 1f; cb.fadeDuration = 0.08f;
            btn.colors = cb;
            var nav = new Navigation(); nav.mode = Navigation.Mode.None; btn.navigation = nav;

            int capturedSlot = slotIdx;
            int capturedIdx  = optionIdx;
            btn.onClick.AddListener(new System.Action(() =>
            {
                var s = _popupSlots[capturedSlot];
                s.SelectedOptionIdx = capturedIdx;
                _popupSlots[capturedSlot] = s;
                RefreshPortVisuals();
                RefreshPopupTotal();
                BuildPortDetail(capturedSlot);
            }));
        }

        private static void RefreshPopupTotal()
        {
            if (_popupBuyLbl == null || _popupSwitchSO == null || _popupSlots == null) return;
            int perUnit = _popupSwitchSO.price;
            for (int i = 0; i < _popupSlots.Count; i++)
            {
                var s = _popupSlots[i];
                if (s.IsSelected && s.HasModule)
                    perUnit += s.SelectedModule.PricePerModule;
            }
            int qty = _popupQty > 0 ? _popupQty : 1;
            if (_popupQtyLbl != null) _popupQtyLbl.text = qty.ToString();
            _popupBuyLbl.text = qty == 1
                ? $"Add  ${perUnit:N0}"
                : $"Add x{qty}  ${perUnit * qty:N0}";
        }

        private static void HideSFPConfigPopup()
        {
            if (_sfpConfigPopup != null) { Object.Destroy(_sfpConfigPopup); _sfpConfigPopup = null; }
            _portBtnImages = null;
            _portToSlotMap = null;
            _portDetailContent = null;
            _popupSelectedPortIdx = -1;
            _popupSlots = null;
            _popupSwitchSO = null;
            _popupBuyLbl = null;
            _popupQtyLbl = null;
            _popupQty = 1;
        }

        public static void ShowMiniShop()
        {
            // Clear previous shop rows
            for (int i = 0; i < _shopItemRows.Count; i++)
                if (_shopItemRows[i] != null)
                    Object.Destroy(_shopItemRows[i]);
            _shopItemRows.Clear();

            var cs = FloorManagerMod.ComputerShopRef;
            if (cs == null || cs.shopItems == null)
            {
                _miniShopPanel.SetActive(true);
                return;
            }

            for (int i = 0; i < cs.shopItems.Length; i++)
            {
                var shopItem = cs.shopItems[i];
                if (!shopItem.isUnlocked) continue;

                var so = shopItem.shopItemSO;
                if (so == null) continue;

                var t = so.itemType;
                bool isServer = t == PlayerManager.ObjectInHand.Server1U
                             || t == PlayerManager.ObjectInHand.Server2U
                             || t == PlayerManager.ObjectInHand.Server3U;
                bool isSwitch = t == PlayerManager.ObjectInHand.Switch;
                if (!isServer && !isSwitch) continue;

                BuildShopItemRow(so);
            }

            // Default to shop tab, update cart badge
            SwitchToShopTab();
            UpdateCartBadge();
            _miniShopPanel.SetActive(true);
        }

        public static void HideMiniShop()
        {
            HideSFPConfigPopup();
            _miniShopPanel.SetActive(false);
        }

        // Clears any stale items from the game's native shop cart so our purchase
        // doesn't accidentally spawn leftovers from a previous shop interaction.
        private static void ClearCart(ComputerShop cs)
        {
            if (cs == null) return;
            var cart = cs.cartUIItems;
            if (cart == null) return;
            for (int i = cart.Count - 1; i >= 0; i--)
                cs.RemoveCartUIItem(cart[i]);
        }

        private static void BuildShopItemRow(ShopItemSO so)
        {
            bool isSwitch = so.itemType == PlayerManager.ObjectInHand.Switch;
            bool isColorable = so.isCustomColor;

            // For switches: build per-port slot list with compatible shop modules
            var slots = isSwitch ? GetPortSlots(so) : new List<SFPPortSlot>();
            bool hasSFPs = false;
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].HasModule) { hasSFPs = true; break; }

            // ── Row container ─────────────────────────────────────────
            var row = new GameObject($"ShopItem_{so.itemID}");
            row.transform.SetParent(_shopContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.13f, 0.13f, 0.16f, 1f);

            var rowVL = row.AddComponent<VerticalLayoutGroup>();
            rowVL.childControlWidth = true;
            rowVL.childControlHeight = true;
            rowVL.childForceExpandWidth = true;
            rowVL.childForceExpandHeight = false;
            rowVL.spacing = 2f;
            var rowPad = new RectOffset();
            rowPad.left = 6; rowPad.right = 6; rowPad.top = 4; rowPad.bottom = 4;
            rowVL.padding = rowPad;

            // ── Main info row: icon / name / [price] / buy ────────────
            var mainRow = new GameObject("MainRow");
            mainRow.transform.SetParent(row.transform, false);
            var mainHL = mainRow.AddComponent<HorizontalLayoutGroup>();
            mainHL.childControlWidth = true;
            mainHL.childControlHeight = true;
            mainHL.childForceExpandWidth = false;
            mainHL.childForceExpandHeight = false;
            mainHL.spacing = 6f;
            var mainLE = mainRow.AddComponent<LayoutElement>();
            mainLE.preferredHeight = 28f;

            // Icon
            if (so.sprite != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(mainRow.transform, false);
                var iconImg = iconGo.AddComponent<Image>();
                iconImg.sprite = so.sprite;
                iconImg.color = Color.white;
                iconImg.preserveAspect = true;
                iconImg.raycastTarget = false;
                var iconLE = iconGo.AddComponent<LayoutElement>();
                iconLE.preferredWidth = 24f;
                iconLE.preferredHeight = 24f;
            }

            // Item name — ShopItemSO.itemName is always empty in IL2CPP.
            string displayName;
            if (isSwitch)
            {
                displayName = ResolveSwitchDisplayName(so);
            }
            else
            {
                switch (so.itemID)
                {
                    case 0: displayName = "System X 3U 5000 IOPS"; break;
                    case 1: displayName = "System X 7U 12000 IOPS"; break;
                    case 2: displayName = "RISC 3U 5000 IOPS"; break;
                    case 3: displayName = "RISC 7U 12000 IOPS"; break;
                    case 4: displayName = "GPU 3U 5000 IOPS"; break;
                    case 5: displayName = "GPU 7U 12000 IOPS"; break;
                    case 6: displayName = "Mainframe 3U 5000 IOPS"; break;
                    case 7: displayName = "Mainframe 7U 12000 IOPS"; break;
                    default: displayName = $"Server {so.itemID}"; break;
                }
            }
            var nameLbl = UIHelper.BuildLabel(mainRow.transform, displayName, 140f);
            nameLbl.fontSize = 11f;
            nameLbl.color = Color.white;
            nameLbl.enableWordWrapping = false;
            nameLbl.overflowMode = TextOverflowModes.Ellipsis;
            nameLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Static price label — omitted for switches with SFPs (total shown in buy button instead)
            if (!hasSFPs)
            {
                var priceLbl = UIHelper.BuildLabel(mainRow.transform, $"${so.price:N0}", 50f);
                priceLbl.fontSize = 10f;
                priceLbl.color = new Color(0.8f, 0.8f, 0.5f);
                priceLbl.alignment = TextAlignmentOptions.Right;
            }

            int capturedID = so.itemID;
            int capturedPrice = so.price;
            PlayerManager.ObjectInHand capturedType = so.itemType;
            string capturedName = so.itemName ?? "Item";

            // Declared early so buy-click lambdas can close over them
            int[] selIdx    = new int[] { 0 };
            Color[] selColor = new Color[] { Color.white };
            Image[] swatchImgs = new Image[8];
            Image[] borderImgs = new Image[8];
            int[] directQty = new int[] { 1 };

            // Switches with SFP ports → "Configure" opens the per-slot popup
            // Everything else → quantity selector + "Buy" button
            Button buyBtn;
            if (hasSFPs)
            {
                buyBtn = UIHelper.BuildButton(mainRow.transform, "Configure", 80f);
                ReusableFunctions.ChangeButtonNormalColor(buyBtn, new Color(0.10f, 0.20f, 0.35f, 1f));
                ShopItemSO capturedSO2 = so;
                List<SFPPortSlot> capturedSlots2 = slots;
                buyBtn.onClick.AddListener(new System.Action(() =>
                    ShowSFPConfigPopup(capturedSO2, capturedSlots2)));
            }
            else
            {
                // Qty controls: [-] [n] [+]
                var minBtn = UIHelper.BuildButton(mainRow.transform, "-", 22f);
                minBtn.GetComponent<LayoutElement>().preferredHeight = 22f;
                var qtyLbl = UIHelper.BuildLabel(mainRow.transform, "1", 22f);
                qtyLbl.alignment = TextAlignmentOptions.Center; qtyLbl.fontSize = 10f;
                var plusBtn = UIHelper.BuildButton(mainRow.transform, "+", 22f);
                plusBtn.GetComponent<LayoutElement>().preferredHeight = 22f;

                buyBtn = UIHelper.BuildButton(mainRow.transform, $"Add  ${so.price:N0}", 80f);
                ReusableFunctions.ChangeButtonNormalColor(buyBtn, new Color(0.10f, 0.28f, 0.10f, 1f));

                TextMeshProUGUI capturedQtyLbl = qtyLbl;
                TextMeshProUGUI capturedBuyLbl = buyBtn.GetComponentInChildren<TextMeshProUGUI>();
                int capturedUnitPrice = so.price;
                string capturedDisplayName = displayName;
                ShopItemSO capturedSOAdd = so;
                minBtn.onClick.AddListener(new System.Action(() => {
                    if (directQty[0] > 1) { directQty[0]--; capturedQtyLbl.text = directQty[0].ToString(); capturedBuyLbl.text = $"Add  ${capturedUnitPrice * directQty[0]:N0}"; }
                }));
                plusBtn.onClick.AddListener(new System.Action(() => {
                    directQty[0]++; capturedQtyLbl.text = directQty[0].ToString(); capturedBuyLbl.text = $"Add  ${capturedUnitPrice * directQty[0]:N0}";
                }));
                buyBtn.onClick.AddListener(new System.Action(() =>
                {
                    _cart.Add(new CartItem
                    {
                        SO = capturedSOAdd,
                        DisplayName = capturedDisplayName,
                        Quantity = directQty[0],
                        IsColorable = isColorable,
                        SelectedColor = selColor[0],
                        IsSFPSwitch = false,
                        SFPSlots = null,
                        UnitPrice = capturedUnitPrice
                    });
                    // Reset qty to 1
                    directQty[0] = 1;
                    capturedQtyLbl.text = "1";
                    capturedBuyLbl.text = $"Add  ${capturedUnitPrice:N0}";
                    UpdateCartBadge();
                }));
            }

            // ── Swatch init (colorable items only) ────────────────────
            if (isColorable)
            {
                string hex0 = _colorEntries[0].Value;
                if (!ColorUtility.TryParseHtmlString(hex0, out selColor[0]))
                    selColor[0] = Color.white;
            }

            _shopItemRows.Add(row);

            // Color swatch row (colorable items only)
            if (!isColorable) return;

            var swatchRow = new GameObject("SwatchRow");
            swatchRow.transform.SetParent(row.transform, false);
            var swatchHL = swatchRow.AddComponent<HorizontalLayoutGroup>();
            swatchHL.childControlWidth = true;
            swatchHL.childControlHeight = true;
            swatchHL.childForceExpandWidth = false;
            swatchHL.childForceExpandHeight = false;
            swatchHL.spacing = 3f;
            var swatchRowLE = swatchRow.AddComponent<LayoutElement>();
            swatchRowLE.preferredHeight = 24f;

            for (int si = 0; si < 8; si++)
            {
                int capturedSI = si;
                string hexVal = _colorEntries[si].Value;
                Color swatchColor;
                if (!ColorUtility.TryParseHtmlString(hexVal, out swatchColor))
                    swatchColor = Color.gray;

                var swatchGo = new GameObject($"Swatch_{si}");
                swatchGo.transform.SetParent(swatchRow.transform, false);

                var swatchImg = swatchGo.AddComponent<Image>();
                swatchImg.color = swatchColor;
                var swatchLE = swatchGo.AddComponent<LayoutElement>();
                swatchLE.preferredWidth = 20f;
                swatchLE.preferredHeight = 20f;
                swatchImgs[si] = swatchImg;

                // White border for active state (invisible by default)
                var borderGo = new GameObject("Border");
                borderGo.transform.SetParent(swatchGo.transform, false);
                var borderRT = borderGo.AddComponent<RectTransform>();
                borderRT.anchorMin = Vector2.zero;
                borderRT.anchorMax = Vector2.one;
                borderRT.offsetMin = new Vector2(-2f, -2f);
                borderRT.offsetMax = new Vector2(2f, 2f);
                var borderImg = borderGo.AddComponent<Image>();
                borderImg.color = si == 0 ? Color.white : new Color(1f, 1f, 1f, 0f);
                borderImg.raycastTarget = false;
                borderImgs[si] = borderImg;

                var swatchBtn = swatchGo.AddComponent<Button>();
                swatchBtn.targetGraphic = swatchImg;
                var swatchNav = new Navigation();
                swatchNav.mode = Navigation.Mode.None;
                swatchBtn.navigation = swatchNav;

                swatchBtn.onClick.AddListener(new System.Action(() =>
                {
                    // Deselect old
                    if (borderImgs[selIdx[0]] != null)
                        borderImgs[selIdx[0]].color = new Color(1f, 1f, 1f, 0f);

                    selIdx[0] = capturedSI;

                    // Select new
                    if (borderImgs[capturedSI] != null)
                        borderImgs[capturedSI].color = Color.white;

                    string h = _colorEntries[capturedSI].Value;
                    if (!ColorUtility.TryParseHtmlString(h, out selColor[0]))
                        selColor[0] = Color.white;
                }));
            }

            // Spacer
            var spacerGo = new GameObject("Spacer");
            spacerGo.transform.SetParent(swatchRow.transform, false);
            spacerGo.AddComponent<RectTransform>();
            var spacerLE = spacerGo.AddComponent<LayoutElement>();
            spacerLE.flexibleWidth = 1f;

            // Save button — saves the FlexibleColorPicker's current color to the selected swatch slot
            var saveBtn = UIHelper.BuildButton(swatchRow.transform, "Save", 48f);
            saveBtn.onClick.AddListener(new System.Action(() =>
            {
                try
                {
                    var cs = FloorManagerMod.ComputerShopRef;
                    if (cs == null || cs.flexibleColorPicker == null) return;

                    Color picked = cs.flexibleColorPicker.color;
                    string newHex = "#" + ColorUtility.ToHtmlStringRGB(picked);
                    int slot = selIdx[0];

                    _colorEntries[slot].Value = newHex;
                    MelonPreferences.Save();

                    // Update swatch visual
                    if (swatchImgs[slot] != null)
                        swatchImgs[slot].color = picked;
                    selColor[0] = picked;

                    StaticUIElements.instance.AddMeesageInField($"Color saved to slot {slot + 1}");
                }
                catch (System.Exception ex)
                {
                    MelonLogger.Warning($"[DCIM] SaveCurrentSwatchColor failed: {ex.Message}");
                }
            }));
        }

        // ── Tab switching ───────────────────────────────────────────

        private static void SwitchToShopTab()
        {
            if (_shopView != null) _shopView.SetActive(true);
            if (_cartView != null) _cartView.SetActive(false);
            if (_shopTabBtnRef != null)
                ReusableFunctions.ChangeButtonNormalColor(_shopTabBtnRef, new Color(0.18f, 0.24f, 0.32f, 1f));
            if (_cartTabBtnRef != null)
                ReusableFunctions.ChangeButtonNormalColor(_cartTabBtnRef, new Color(0.12f, 0.12f, 0.15f, 1f));
        }

        private static void SwitchToCartTab()
        {
            if (_shopView != null) _shopView.SetActive(false);
            if (_cartView != null) _cartView.SetActive(true);
            if (_shopTabBtnRef != null)
                ReusableFunctions.ChangeButtonNormalColor(_shopTabBtnRef, new Color(0.12f, 0.12f, 0.15f, 1f));
            if (_cartTabBtnRef != null)
                ReusableFunctions.ChangeButtonNormalColor(_cartTabBtnRef, new Color(0.18f, 0.24f, 0.32f, 1f));
            RebuildCartView();
        }

        private static void UpdateCartBadge()
        {
            int count = 0;
            for (int i = 0; i < _cart.Count; i++)
                count += _cart[i].Quantity;
            if (_cartTabLbl != null)
                _cartTabLbl.text = count > 0 ? $"Cart ({count})" : "Cart (0)";
        }

        // ── Cart view ───────────────────────────────────────────────

        private static void RebuildCartView()
        {
            for (int i = 0; i < _cartRows.Count; i++)
                if (_cartRows[i] != null) Object.Destroy(_cartRows[i]);
            _cartRows.Clear();

            if (_cart.Count == 0)
            {
                var emptyLbl = UIHelper.BuildLabel(_cartContent, "Cart is empty", 300f);
                emptyLbl.fontSize = 11f;
                emptyLbl.color = new Color(0.45f, 0.45f, 0.45f);
                emptyLbl.alignment = TextAlignmentOptions.Center;
                emptyLbl.GetComponent<LayoutElement>().preferredHeight = 40f;
                _cartRows.Add(emptyLbl.gameObject);
            }
            else
            {
                for (int i = 0; i < _cart.Count; i++)
                    BuildCartItemRow(i);
            }

            RefreshCartTotal();
        }

        private static void BuildCartItemRow(int cartIdx)
        {
            var item = _cart[cartIdx];

            var row = new GameObject($"CartItem_{cartIdx}");
            row.transform.SetParent(_cartContent, false);
            row.AddComponent<Image>().color = new Color(0.13f, 0.13f, 0.16f, 1f);

            var rowVL = row.AddComponent<VerticalLayoutGroup>();
            rowVL.childControlWidth = true; rowVL.childControlHeight = true;
            rowVL.childForceExpandWidth = true; rowVL.childForceExpandHeight = false;
            rowVL.spacing = 2f;
            var rowPad = new RectOffset();
            rowPad.left = 6; rowPad.right = 6; rowPad.top = 4; rowPad.bottom = 4;
            rowVL.padding = rowPad;

            // Main row: icon, name, color swatch, qty controls, line total, remove
            var mainRow = new GameObject("MainRow");
            mainRow.transform.SetParent(row.transform, false);
            var mainHL = mainRow.AddComponent<HorizontalLayoutGroup>();
            mainHL.childControlWidth = true; mainHL.childControlHeight = true;
            mainHL.childForceExpandWidth = false; mainHL.spacing = 4f;
            mainRow.AddComponent<LayoutElement>().preferredHeight = 26f;

            // Icon
            if (item.SO != null && item.SO.sprite != null)
            {
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(mainRow.transform, false);
                iconGo.AddComponent<Image>().sprite = item.SO.sprite;
                iconGo.GetComponent<Image>().preserveAspect = true;
                iconGo.GetComponent<Image>().raycastTarget = false;
                var iconLE = iconGo.AddComponent<LayoutElement>();
                iconLE.preferredWidth = 22f; iconLE.preferredHeight = 22f;
            }

            // Name
            var nameLbl = UIHelper.BuildLabel(mainRow.transform, item.DisplayName, 120f);
            nameLbl.fontSize = 10f; nameLbl.color = Color.white;
            nameLbl.enableWordWrapping = false;
            nameLbl.overflowMode = TextOverflowModes.Ellipsis;
            nameLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

            // Color swatch (if colorable)
            if (item.IsColorable)
            {
                var swGo = new GameObject("Swatch");
                swGo.transform.SetParent(mainRow.transform, false);
                swGo.AddComponent<Image>().color = item.SelectedColor;
                var swLE = swGo.AddComponent<LayoutElement>();
                swLE.preferredWidth = 16f; swLE.preferredHeight = 16f;
            }

            // Qty controls
            int capturedIdx = cartIdx;
            var minBtn = UIHelper.BuildButton(mainRow.transform, "-", 20f);
            minBtn.GetComponent<LayoutElement>().preferredHeight = 20f;
            var qtyLbl = UIHelper.BuildLabel(mainRow.transform, item.Quantity.ToString(), 22f);
            qtyLbl.fontSize = 10f; qtyLbl.alignment = TextAlignmentOptions.Center;
            var plusBtn = UIHelper.BuildButton(mainRow.transform, "+", 20f);
            plusBtn.GetComponent<LayoutElement>().preferredHeight = 20f;

            minBtn.onClick.AddListener(new System.Action(() =>
            {
                if (capturedIdx >= _cart.Count) return;
                var ci = _cart[capturedIdx];
                if (ci.Quantity > 1)
                {
                    ci.Quantity--;
                    _cart[capturedIdx] = ci;
                    UpdateCartBadge();
                    RebuildCartView();
                }
            }));
            plusBtn.onClick.AddListener(new System.Action(() =>
            {
                if (capturedIdx >= _cart.Count) return;
                var ci = _cart[capturedIdx];
                ci.Quantity++;
                _cart[capturedIdx] = ci;
                UpdateCartBadge();
                RebuildCartView();
            }));

            // Line total
            int lineTotal = item.UnitPrice * item.Quantity;
            var totalLbl = UIHelper.BuildLabel(mainRow.transform, $"${lineTotal:N0}", 55f);
            totalLbl.fontSize = 10f; totalLbl.color = new Color(0.8f, 0.8f, 0.5f);
            totalLbl.alignment = TextAlignmentOptions.Right;

            // Remove button
            var removeBtn = UIHelper.BuildButton(mainRow.transform, "X", 22f);
            removeBtn.GetComponent<LayoutElement>().preferredHeight = 20f;
            ReusableFunctions.ChangeButtonNormalColor(removeBtn, new Color(0.35f, 0.10f, 0.10f, 1f));
            removeBtn.onClick.AddListener(new System.Action(() =>
            {
                if (capturedIdx < _cart.Count)
                {
                    _cart.RemoveAt(capturedIdx);
                    UpdateCartBadge();
                    RebuildCartView();
                }
            }));

            // SFP module sub-rows (for SFP switches)
            if (item.IsSFPSwitch && item.SFPSlots != null)
            {
                // Group selected modules by type
                var moduleCounts = new Dictionary<string, int>();
                var modulePrices = new Dictionary<string, int>();
                for (int s = 0; s < item.SFPSlots.Count; s++)
                {
                    var slot = item.SFPSlots[s];
                    if (!slot.IsSelected || !slot.HasModule) continue;
                    var mod = slot.SelectedModule;
                    string modName = mod.DisplayName;
                    if (!moduleCounts.ContainsKey(modName))
                    {
                        moduleCounts[modName] = 0;
                        modulePrices[modName] = mod.PricePerModule;
                    }
                    moduleCounts[modName]++;
                }

                // Render sub-rows
                var keys = new List<string>(moduleCounts.Keys);
                for (int k = 0; k < keys.Count; k++)
                {
                    string modName = keys[k];
                    int modCount = moduleCounts[modName];
                    int modPrice = modulePrices[modName];

                    var subRow = new GameObject($"Mod_{k}");
                    subRow.transform.SetParent(row.transform, false);
                    var subHL = subRow.AddComponent<HorizontalLayoutGroup>();
                    subHL.childControlWidth = true; subHL.childControlHeight = true;
                    subHL.childForceExpandWidth = false; subHL.spacing = 6f;
                    var subPad = new RectOffset(); subPad.left = 30; subPad.right = 6;
                    subHL.padding = subPad;
                    subRow.AddComponent<LayoutElement>().preferredHeight = 18f;

                    var modLbl = UIHelper.BuildLabel(subRow.transform, $"{modName} x{modCount}", 160f);
                    modLbl.fontSize = 9f; modLbl.color = new Color(0.6f, 0.7f, 0.8f);
                    modLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

                    var modPriceLbl = UIHelper.BuildLabel(subRow.transform, $"${modPrice * modCount:N0}", 50f);
                    modPriceLbl.fontSize = 9f; modPriceLbl.color = new Color(0.6f, 0.6f, 0.4f);
                    modPriceLbl.alignment = TextAlignmentOptions.Right;
                }
            }

            _cartRows.Add(row);
        }

        private static void RefreshCartTotal()
        {
            int grandTotal = 0;
            for (int i = 0; i < _cart.Count; i++)
                grandTotal += _cart[i].UnitPrice * _cart[i].Quantity;
            if (_cartTotalLbl != null)
                _cartTotalLbl.text = $"Total: ${grandTotal:N0}";
            if (_checkoutBtn != null)
            {
                var lbl = _checkoutBtn.GetComponentInChildren<TextMeshProUGUI>();
                if (lbl != null)
                    lbl.text = _cart.Count > 0 ? $"Checkout  ${grandTotal:N0}" : "Checkout";
            }
        }

        // ── Add SFP switch to cart ──────────────────────────────────

        private static void AddSFPToCart(ShopItemSO switchSO, string switchName, List<SFPPortSlot> slots)
        {
            if (slots == null) return;

            int perUnit = switchSO.price;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (!s.IsSelected || !s.HasModule) continue;
                perUnit += s.SelectedModule.PricePerModule;
            }

            int qty = _popupQty > 0 ? _popupQty : 1;

            var slotsCopy = new List<SFPPortSlot>(slots);

            _cart.Add(new CartItem
            {
                SO = switchSO,
                DisplayName = switchName,
                Quantity = qty,
                IsColorable = false,
                SelectedColor = Color.white,
                IsSFPSwitch = true,
                SFPSlots = slotsCopy,
                UnitPrice = perUnit
            });

            HideSFPConfigPopup();
            UpdateCartBadge();
        }

        // ── Checkout ────────────────────────────────────────────────

        private static void ExecuteCheckout()
        {
            if (_cart.Count == 0) return;

            int grandTotal = 0;
            int totalItems = 0;
            for (int i = 0; i < _cart.Count; i++)
            {
                grandTotal += _cart[i].UnitPrice * _cart[i].Quantity;
                totalItems += _cart[i].Quantity;
            }

            if (PlayerManager.instance.playerClass.money < grandTotal)
            {
                StaticUIElements.instance.AddMeesageInField("Not enough funds!");
                return;
            }

            // Close UI
            HideMiniShop();
            OnMiniShopClosed?.Invoke();
            if (_panelRoot != null && _panelRoot.activeSelf) Close();

            var cs = FloorManagerMod.ComputerShopRef;
            if (cs == null) return;

            // Clear stale game cart once at start
            ClearCart(cs);

            for (int ci = 0; ci < _cart.Count; ci++)
            {
                var item = _cart[ci];

                if (!item.IsSFPSwitch)
                {
                    // Non-SFP item
                    if (item.IsColorable && cs.flexibleColorPicker != null)
                        cs.flexibleColorPicker.SetColor(item.SelectedColor);

                    for (int q = 0; q < item.Quantity; q++)
                    {
                        ClearCart(cs);
                        cs.ButtonBuyShopItem(item.SO.itemID, item.SO.price, item.SO.itemType, item.SO.itemName ?? "Item", item.IsColorable);
                        if (item.IsColorable) cs.ButtonChosenColor();
                        cs.ButtonCheckOut();
                    }
                }
                else
                {
                    // SFP switch
                    int sfpCount = 0;
                    if (item.SFPSlots != null)
                    {
                        for (int s = 0; s < item.SFPSlots.Count; s++)
                            if (item.SFPSlots[s].IsSelected && item.SFPSlots[s].HasModule) sfpCount++;
                    }

                    for (int q = 0; q < item.Quantity; q++)
                    {
                        var switchIdsBefore = new HashSet<int>();
                        var pre = Object.FindObjectsOfType<NetworkSwitch>();
                        for (int si = 0; si < pre.Length; si++)
                            switchIdsBefore.Add(pre[si].GetInstanceID());

                        ClearCart(cs);
                        cs.ButtonBuyShopItem(item.SO.itemID, item.SO.price, item.SO.itemType, item.SO.itemName ?? "Switch", false);
                        cs.ButtonCheckOut();

                        if (sfpCount > 0 && item.SFPSlots != null)
                        {
                            NetworkSwitch newSw = null;
                            var post = Object.FindObjectsOfType<NetworkSwitch>();
                            for (int si = 0; si < post.Length; si++)
                            {
                                if (!switchIdsBefore.Contains(post[si].GetInstanceID()))
                                { newSw = post[si]; break; }
                            }
                            if (newSw != null)
                                InsertSelectedSFPs(newSw, item.SFPSlots);
                        }
                    }
                }
            }

            StaticUIElements.instance.AddMeesageInField($"Purchased {totalItems} item(s) for ${grandTotal:N0}");
            _cart.Clear();
            UpdateCartBadge();
        }

        // ── SFP Presets ─────────────────────────────────────────────

        private static void SavePresetFromPopup(int switchId)
        {
            if (_popupSlots == null) return;

            int presetNum = CountPresetsForSwitch(switchId) + 1;
            string name = $"Preset {presetNum}";
            DoSavePreset(name, switchId);
        }

        private static void DoSavePreset(string name, int switchId)
        {
            if (_popupSlots == null) return;

            // Build port data string: idx:opt,idx:opt,...
            var portParts = new List<string>();
            for (int i = 0; i < _popupSlots.Count; i++)
            {
                var s = _popupSlots[i];
                if (s.IsSelected && s.SelectedOptionIdx >= 0)
                    portParts.Add($"{i}:{s.SelectedOptionIdx}");
            }
            string portData = string.Join(",", portParts.ToArray());

            // Load existing presets, append new one
            var presets = LoadAllPresets();
            presets.Add(new PresetData { Name = name, SwitchId = switchId, PortData = portData });
            SaveAllPresets(presets);

            StaticUIElements.instance.AddMeesageInField($"Preset \"{name}\" saved");
        }

        private static void ShowPresetLoadPopup(int switchId)
        {
            if (_portDetailContent == null) return;

            // Rebuild the detail area with preset list instead
            for (int i = _portDetailContent.childCount - 1; i >= 0; i--)
                Object.Destroy(_portDetailContent.GetChild(i).gameObject);

            var allPresets = LoadAllPresets();
            var matching = new List<int>(); // indices into allPresets
            for (int i = 0; i < allPresets.Count; i++)
                if (allPresets[i].SwitchId == switchId) matching.Add(i);

            if (matching.Count == 0)
            {
                var noLbl = UIHelper.BuildLabel(_portDetailContent, "No saved presets for this switch", 300f);
                noLbl.fontSize = 10f; noLbl.color = new Color(0.45f, 0.45f, 0.45f);
                noLbl.alignment = TextAlignmentOptions.Center;
                noLbl.GetComponent<LayoutElement>().preferredHeight = 28f;

                var backBtn = UIHelper.BuildButton(_portDetailContent.gameObject.transform, "Back", 60f);
                backBtn.GetComponent<LayoutElement>().preferredHeight = 24f;
                backBtn.onClick.AddListener(new System.Action(() =>
                {
                    // Clear and show hint
                    for (int j = _portDetailContent.childCount - 1; j >= 0; j--)
                        Object.Destroy(_portDetailContent.GetChild(j).gameObject);
                    var hintLbl2 = UIHelper.BuildLabel(_portDetailContent, "Click an SFP port above to configure it", 300f);
                    hintLbl2.fontSize = 10f; hintLbl2.color = new Color(0.40f, 0.40f, 0.40f);
                    hintLbl2.alignment = TextAlignmentOptions.Center;
                    hintLbl2.GetComponent<LayoutElement>().preferredHeight = 28f;
                }));
                return;
            }

            var headerLbl = UIHelper.BuildLabel(_portDetailContent, "Load Preset", 200f);
            headerLbl.fontSize = 11f; headerLbl.fontStyle = FontStyles.Bold; headerLbl.color = Color.white;
            headerLbl.GetComponent<LayoutElement>().preferredHeight = 22f;

            for (int mi = 0; mi < matching.Count; mi++)
            {
                int presetGlobalIdx = matching[mi];
                var preset = allPresets[presetGlobalIdx];

                var pRow = new GameObject($"Preset_{mi}");
                pRow.transform.SetParent(_portDetailContent, false);
                var pImg = pRow.AddComponent<Image>();
                pImg.color = new Color(0.12f, 0.12f, 0.15f, 1f);
                var pHL = pRow.AddComponent<HorizontalLayoutGroup>();
                pHL.childControlWidth = true; pHL.childControlHeight = true;
                pHL.childForceExpandWidth = false; pHL.spacing = 6f;
                var pPad = new RectOffset(); pPad.left = 8; pPad.right = 8;
                pHL.padding = pPad;
                pRow.AddComponent<LayoutElement>().preferredHeight = 32f;

                // Preset name + summary
                string summary = GetPresetSummary(preset.PortData);
                var pNameLbl = UIHelper.BuildLabel(pRow.transform, preset.Name, 100f);
                pNameLbl.fontSize = 10f; pNameLbl.fontStyle = FontStyles.Bold; pNameLbl.color = Color.white;

                var pSumLbl = UIHelper.BuildLabel(pRow.transform, summary, 160f);
                pSumLbl.fontSize = 8f; pSumLbl.color = new Color(0.5f, 0.6f, 0.7f);
                pSumLbl.GetComponent<LayoutElement>().flexibleWidth = 1f;

                // Apply button (whole row clickable)
                string capturedPortData = preset.PortData;
                int capturedSwitchId = switchId;
                var pBtn = pRow.AddComponent<Button>();
                pBtn.targetGraphic = pImg;
                var pcb = new ColorBlock();
                pcb.normalColor = pImg.color;
                pcb.highlightedColor = new Color(0.18f, 0.22f, 0.28f, 1f);
                pcb.pressedColor = new Color(0.08f, 0.08f, 0.10f, 1f);
                pcb.selectedColor = pcb.normalColor; pcb.colorMultiplier = 1f; pcb.fadeDuration = 0.08f;
                pBtn.colors = pcb;
                var pNav = new Navigation(); pNav.mode = Navigation.Mode.None; pBtn.navigation = pNav;
                pBtn.onClick.AddListener(new System.Action(() =>
                {
                    ApplyPreset(capturedPortData);
                    // Restore hint in detail area
                    for (int j = _portDetailContent.childCount - 1; j >= 0; j--)
                        Object.Destroy(_portDetailContent.GetChild(j).gameObject);
                    var hintLbl3 = UIHelper.BuildLabel(_portDetailContent, "Preset applied — click a port to verify", 300f);
                    hintLbl3.fontSize = 10f; hintLbl3.color = new Color(0.40f, 0.65f, 0.40f);
                    hintLbl3.alignment = TextAlignmentOptions.Center;
                    hintLbl3.GetComponent<LayoutElement>().preferredHeight = 28f;
                }));

                // Delete button
                int capturedGlobalIdx = presetGlobalIdx;
                var delBtn = UIHelper.BuildButton(pRow.transform, "X", 22f);
                delBtn.GetComponent<LayoutElement>().preferredHeight = 20f;
                ReusableFunctions.ChangeButtonNormalColor(delBtn, new Color(0.35f, 0.10f, 0.10f, 1f));
                delBtn.onClick.AddListener(new System.Action(() =>
                {
                    DeletePreset(capturedGlobalIdx);
                    ShowPresetLoadPopup(capturedSwitchId);
                }));
            }

            // Back button
            var backBtn2 = UIHelper.BuildButton(_portDetailContent.gameObject.transform, "Back", 60f);
            backBtn2.GetComponent<LayoutElement>().preferredHeight = 24f;
            backBtn2.onClick.AddListener(new System.Action(() =>
            {
                for (int j = _portDetailContent.childCount - 1; j >= 0; j--)
                    Object.Destroy(_portDetailContent.GetChild(j).gameObject);
                var hintLbl4 = UIHelper.BuildLabel(_portDetailContent, "Click an SFP port above to configure it", 300f);
                hintLbl4.fontSize = 10f; hintLbl4.color = new Color(0.40f, 0.40f, 0.40f);
                hintLbl4.alignment = TextAlignmentOptions.Center;
                hintLbl4.GetComponent<LayoutElement>().preferredHeight = 28f;
            }));
        }

        private static void ApplyPreset(string portData)
        {
            if (_popupSlots == null || string.IsNullOrEmpty(portData)) return;

            // Reset all to empty first
            for (int i = 0; i < _popupSlots.Count; i++)
            {
                var s = _popupSlots[i];
                s.SelectedOptionIdx = -1;
                _popupSlots[i] = s;
            }

            // Parse portData: "idx:opt,idx:opt,..."
            var entries = portData.Split(',');
            for (int e = 0; e < entries.Length; e++)
            {
                var parts = entries[e].Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[0], out int portIdx)) continue;
                if (!int.TryParse(parts[1], out int optIdx)) continue;
                if (portIdx < 0 || portIdx >= _popupSlots.Count) continue;
                var s = _popupSlots[portIdx];
                if (s.HasModule && optIdx >= 0 && optIdx < s.Options.Count)
                {
                    s.SelectedOptionIdx = optIdx;
                    _popupSlots[portIdx] = s;
                }
            }

            RefreshPortVisuals();
            RefreshPopupTotal();
        }

        private static string GetPresetSummary(string portData)
        {
            if (string.IsNullOrEmpty(portData)) return "(empty)";
            // Count modules by option index type
            var counts = new Dictionary<int, int>();
            var entries = portData.Split(',');
            for (int e = 0; e < entries.Length; e++)
            {
                var parts = entries[e].Split(':');
                if (parts.Length != 2) continue;
                if (!int.TryParse(parts[1], out int optIdx)) continue;
                if (!counts.ContainsKey(optIdx)) counts[optIdx] = 0;
                counts[optIdx]++;
            }
            if (counts.Count == 0) return "(empty)";

            // Build summary from module names if we have popup slots
            var summaryParts = new List<string>();
            if (_popupSlots != null && _popupSlots.Count > 0)
            {
                // Try to resolve names from current popup slot options
                var keys = new List<int>(counts.Keys);
                for (int k = 0; k < keys.Count; k++)
                {
                    int optIdx = keys[k];
                    string modName = null;
                    // Find first slot that has this option
                    for (int s = 0; s < _popupSlots.Count; s++)
                    {
                        if (_popupSlots[s].HasModule && optIdx >= 0 && optIdx < _popupSlots[s].Options.Count)
                        { modName = _popupSlots[s].Options[optIdx].DisplayName; break; }
                    }
                    if (modName == null) modName = $"Opt{optIdx}";
                    summaryParts.Add($"{modName} x{counts[keys[k]]}");
                }
            }
            else
            {
                var keys = new List<int>(counts.Keys);
                for (int k = 0; k < keys.Count; k++)
                    summaryParts.Add($"Opt{keys[k]} x{counts[keys[k]]}");
            }
            return string.Join(", ", summaryParts.ToArray());
        }

        // ── Preset persistence ──────────────────────────────────────

        private struct PresetData
        {
            public string Name;
            public int SwitchId;
            public string PortData; // "idx:opt,idx:opt,..."
        }

        private static List<PresetData> LoadAllPresets()
        {
            var result = new List<PresetData>();
            if (_presetEntry == null) return result;
            string raw = _presetEntry.Value;
            if (string.IsNullOrEmpty(raw)) return result;

            // Format: name|switchId|portData;name|switchId|portData;...
            var presets = raw.Split(';');
            for (int i = 0; i < presets.Length; i++)
            {
                if (string.IsNullOrEmpty(presets[i])) continue;
                var fields = presets[i].Split('|');
                if (fields.Length < 3) continue;
                if (!int.TryParse(fields[1], out int switchId)) continue;
                result.Add(new PresetData
                {
                    Name = fields[0],
                    SwitchId = switchId,
                    PortData = fields[2]
                });
            }
            return result;
        }

        private static void SaveAllPresets(List<PresetData> presets)
        {
            if (_presetEntry == null) return;
            var parts = new List<string>();
            for (int i = 0; i < presets.Count; i++)
            {
                // Sanitize name: no pipe or semicolon
                string safeName = presets[i].Name.Replace("|", "").Replace(";", "");
                parts.Add($"{safeName}|{presets[i].SwitchId}|{presets[i].PortData}");
            }
            _presetEntry.Value = string.Join(";", parts.ToArray());
            MelonPreferences.Save();
        }

        private static int CountPresetsForSwitch(int switchId)
        {
            var all = LoadAllPresets();
            int count = 0;
            for (int i = 0; i < all.Count; i++)
                if (all[i].SwitchId == switchId) count++;
            return count;
        }

        private static void DeletePreset(int globalIndex)
        {
            var all = LoadAllPresets();
            if (globalIndex >= 0 && globalIndex < all.Count)
            {
                string name = all[globalIndex].Name;
                all.RemoveAt(globalIndex);
                SaveAllPresets(all);
                StaticUIElements.instance.AddMeesageInField($"Preset \"{name}\" deleted");
            }
        }
    }
}
