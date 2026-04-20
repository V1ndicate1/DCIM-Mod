using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public static class DashboardView
    {
        private static GameObject _root;
        private static Transform _contentTransform;
        private static readonly List<GameObject> _elements = new List<GameObject>();
        private static object _refreshCoroutine;

        public static void Build(GameObject root)
        {
            _root = root;
            ScrollRect sr;
            _contentTransform = UIHelper.BuildScrollView(root, out sr);
        }

        public static void Populate()
        {
            StopRefresh();
            ClearElements();
            SearchEngine.ScanAll();
            var stats = SearchEngine.LastStats;

            // ── Summary stats row ───────────────────────────────────
            var statsRow = new GameObject("StatsRow");
            statsRow.transform.SetParent(_contentTransform, false);
            var statsHL = statsRow.AddComponent<HorizontalLayoutGroup>();
            statsHL.childControlWidth = true;
            statsHL.childControlHeight = true;
            statsHL.childForceExpandWidth = false;
            statsHL.childForceExpandHeight = false;
            statsHL.spacing = 6f;
            var statsPad = new RectOffset();
            statsPad.left = 4; statsPad.right = 4; statsPad.top = 4; statsPad.bottom = 4;
            statsHL.padding = statsPad;
            var statsLE = statsRow.AddComponent<LayoutElement>();
            statsLE.preferredHeight = 30f;
            _elements.Add(statsRow);

            // Stat chips — click to filter SearchResults by type
            UIHelper.BuildFilterChip(statsRow.transform, $"{stats.ServerCount} Servers", new Color(0.4f, 0.6f, 1f),
                () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.Servers, SearchEngine.StatusFilter.All, -1));
            UIHelper.BuildFilterChip(statsRow.transform, $"{stats.SwitchCount} Switches", UIHelper.SwitchColor,
                () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.Switches, SearchEngine.StatusFilter.All, -1));
            UIHelper.BuildFilterChip(statsRow.transform, $"{stats.PatchPanelCount} PPs", UIHelper.PatchPanelColor,
                () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.PatchPanels, SearchEngine.StatusFilter.All, -1));
            UIHelper.BuildFilterChip(statsRow.transform, $"{stats.EmptySlots} Empty", UIHelper.StatusGray,
                () => FloorMapApp.SwitchToState(ViewState.FloorMap));

            // ── Alerts row (only if broken/EOL > 0) ─────────────────
            if (stats.BrokenCount > 0 || stats.EOLCount > 0)
            {
                var alertRow = new GameObject("AlertRow");
                alertRow.transform.SetParent(_contentTransform, false);
                var alertHL = alertRow.AddComponent<HorizontalLayoutGroup>();
                alertHL.childControlWidth = true;
                alertHL.childControlHeight = true;
                alertHL.childForceExpandWidth = false;
                alertHL.childForceExpandHeight = false;
                alertHL.spacing = 6f;
                var alertPad = new RectOffset();
                alertPad.left = 4; alertPad.right = 4; alertPad.top = 2; alertPad.bottom = 2;
                alertHL.padding = alertPad;
                var alertLE = alertRow.AddComponent<LayoutElement>();
                alertLE.preferredHeight = 28f;
                _elements.Add(alertRow);

                if (stats.BrokenCount > 0)
                    UIHelper.BuildFilterChip(alertRow.transform, $"Broken ({stats.BrokenCount})", UIHelper.StatusRed,
                        () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.All, SearchEngine.StatusFilter.Broken, -1));
                if (stats.EOLCount > 0)
                    UIHelper.BuildFilterChip(alertRow.transform, $"EOL ({stats.EOLCount})", UIHelper.StatusYellow,
                        () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.All, SearchEngine.StatusFilter.EOL, -1));
            }

            // ── Quick filter row (Floor Map removed — now a tab) ────────────────
            var filterRow = new GameObject("FilterRow");
            filterRow.transform.SetParent(_contentTransform, false);
            var filterHL = filterRow.AddComponent<HorizontalLayoutGroup>();
            filterHL.childControlWidth = true;
            filterHL.childControlHeight = true;
            filterHL.childForceExpandWidth = false;
            filterHL.childForceExpandHeight = false;
            filterHL.spacing = 6f;
            var filterPad = new RectOffset();
            filterPad.left = 4; filterPad.right = 4; filterPad.top = 2; filterPad.bottom = 2;
            filterHL.padding = filterPad;
            var filterLE = filterRow.AddComponent<LayoutElement>();
            filterLE.preferredHeight = 28f;
            _elements.Add(filterRow);

            UIHelper.BuildFilterChip(filterRow.transform, "Offline", new Color(0.6f, 0.6f, 0.6f),
                () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.All, SearchEngine.StatusFilter.Offline, -1));
            UIHelper.BuildFilterChip(filterRow.transform, "All Devices", Color.white,
                () => FloorMapApp.OpenSearchResults(SearchEngine.DeviceTypeFilter.All, SearchEngine.StatusFilter.All, -1));

            // ── Divider ──────────────────────────────────────────────
            BuildDivider();

            // ── Customer list header ─────────────────────────────────
            var custHeaderRow = new GameObject("CustHeader");
            custHeaderRow.transform.SetParent(_contentTransform, false);
            var custHeaderLE = custHeaderRow.AddComponent<LayoutElement>();
            custHeaderLE.preferredHeight = 22f;
            var custHeaderLbl = UIHelper.BuildLabel(custHeaderRow.transform, $"Customers ({stats.CustomerCount})", 300f);
            custHeaderLbl.fontSize = 12f;
            custHeaderLbl.fontStyle = FontStyles.Bold;
            custHeaderLbl.color = new Color(0.7f, 0.7f, 0.7f);
            _elements.Add(custHeaderRow);

            // ── Customer rows ────────────────────────────────────────
            var customers = SearchEngine.GetCustomerList();
            for (int i = 0; i < customers.Count; i++)
            {
                BuildCustomerRow(customers[i]);
            }

            if (customers.Count == 0)
            {
                var emptyRow = new GameObject("NoCustomers");
                emptyRow.transform.SetParent(_contentTransform, false);
                var emptyLE = emptyRow.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 24f;
                var emptyLbl = UIHelper.BuildLabel(emptyRow.transform, "No customers yet", 200f);
                emptyLbl.fontSize = 10f;
                emptyLbl.color = UIHelper.StatusGray;
                _elements.Add(emptyRow);
            }

            _refreshCoroutine = MelonCoroutines.Start(RefreshDashboard());
        }

        private static void StopRefresh()
        {
            if (_refreshCoroutine != null)
            {
                MelonCoroutines.Stop(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private static IEnumerator RefreshDashboard()
        {
            yield return new WaitForSeconds(30f);
            if (_root == null || !_root.activeSelf) { _refreshCoroutine = null; yield break; }
            _refreshCoroutine = null;
            Populate();
        }

        private static void BuildCustomerRow(SearchEngine.CustomerInfo cust)
        {
            var row = new GameObject($"Cust_{cust.CustomerID}");
            row.transform.SetParent(_contentTransform, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.12f, 0.12f, 0.14f, 1f);

            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 8f;
            var rowPad = new RectOffset();
            rowPad.left = 8; rowPad.right = 8; rowPad.top = 4; rowPad.bottom = 4;
            hl.padding = rowPad;

            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 32f;

            // Logo
            if (cust.Logo != null)
            {
                var logoGo = new GameObject("Logo");
                logoGo.transform.SetParent(row.transform, false);
                var logoImg = logoGo.AddComponent<Image>();
                logoImg.sprite = cust.Logo;
                logoImg.color = Color.white;
                logoImg.raycastTarget = false;
                var logoLE = logoGo.AddComponent<LayoutElement>();
                logoLE.preferredWidth = 24f;
                logoLE.preferredHeight = 24f;
            }

            // Name
            var nameLbl = UIHelper.BuildLabel(row.transform, cust.Name, 140f);
            nameLbl.fontSize = 11f;
            nameLbl.color = Color.white;
            var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>();
            nameLE.flexibleWidth = 1f;

            // Server count
            var countLbl = UIHelper.BuildLabel(row.transform, $"{cust.ServerCount} srv", 50f);
            countLbl.fontSize = 10f;
            countLbl.color = new Color(0.5f, 0.7f, 1f);

            // Revenue
            if (cust.NetRevenue != 0)
            {
                string revStr = cust.NetRevenue >= 0 ? $"${cust.NetRevenue:F0}" : $"-${-cust.NetRevenue:F0}";
                Color revColor = cust.NetRevenue >= 0 ? UIHelper.StatusGreen : UIHelper.StatusRed;
                var revLbl = UIHelper.BuildLabel(row.transform, revStr, 60f);
                revLbl.fontSize = 10f;
                revLbl.color = revColor;
            }

            // Arrow
            var arrowLbl = UIHelper.BuildLabel(row.transform, ">", 16f);
            arrowLbl.fontSize = 12f;
            arrowLbl.color = new Color(0.5f, 0.5f, 0.5f);

            // Click → CustomerIPs
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

            int capturedId = cust.CustomerID;
            btn.onClick.AddListener(new System.Action(() =>
            {
                FloorMapApp.OpenCustomerIPs(capturedId);
            }));

            _elements.Add(row);
        }

        private static void BuildDivider()
        {
            var divGo = new GameObject("Divider");
            divGo.transform.SetParent(_contentTransform, false);
            var divImg = divGo.AddComponent<Image>();
            divImg.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            var divLE = divGo.AddComponent<LayoutElement>();
            divLE.preferredHeight = 1f;
            _elements.Add(divGo);
        }

        private static void ClearElements()
        {
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i] != null)
                    Object.Destroy(_elements[i]);
            }
            _elements.Clear();
        }
    }
}
