using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

namespace FloorManager
{
    public static class CustomerIPView
    {
        private static GameObject _root;
        private static Transform _contentTransform;
        private static readonly List<GameObject> _rows = new List<GameObject>();

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
        }

        public static void InitPopup(GameObject appRoot)
        {
            BuildAddServerPopup(appRoot);
        }

        public static void Populate(int customerID)
        {
            // Clear old rows
            for (int i = 0; i < _rows.Count; i++)
            {
                if (_rows[i] != null)
                    Object.Destroy(_rows[i]);
            }
            _rows.Clear();

            // Get customer info
            var custItem = MainGameManager.instance.GetCustomerItemByID(customerID);
            string custName = custItem != null ? custItem.customerName : $"Customer {customerID}";

            // Summary row with logo
            var summaryRow = new GameObject("SummaryRow");
            summaryRow.transform.SetParent(_contentTransform, false);
            var summaryImg = summaryRow.AddComponent<Image>();
            summaryImg.color = new Color(0.10f, 0.10f, 0.14f, 1f);
            var summaryHL = summaryRow.AddComponent<HorizontalLayoutGroup>();
            summaryHL.childControlWidth = true;
            summaryHL.childControlHeight = true;
            summaryHL.childForceExpandWidth = false;
            summaryHL.childForceExpandHeight = false;
            summaryHL.spacing = 8f;
            var summaryPad = new RectOffset();
            summaryPad.left = 8; summaryPad.right = 8; summaryPad.top = 6; summaryPad.bottom = 6;
            summaryHL.padding = summaryPad;
            var summaryLE = summaryRow.AddComponent<LayoutElement>();
            summaryLE.preferredHeight = 36f;

            // Logo in summary
            if (custItem != null && custItem.logo != null)
            {
                var logoGo = new GameObject("Logo");
                logoGo.transform.SetParent(summaryRow.transform, false);
                var logoImg = logoGo.AddComponent<Image>();
                logoImg.sprite = custItem.logo;
                logoImg.color = Color.white;
                var logoLE = logoGo.AddComponent<LayoutElement>();
                logoLE.preferredWidth = 28f;
                logoLE.preferredHeight = 28f;
            }

            var nameLbl = UIHelper.BuildLabel(summaryRow.transform, custName, 200f);
            nameLbl.fontSize = 13f;
            nameLbl.fontStyle = FontStyles.Bold;
            nameLbl.color = Color.white;
            _rows.Add(summaryRow);

            // Server type → subnet info row
            BuildSubnetInfo(customerID);

            // Revenue summary row
            BuildRevenueSummary(customerID);

            _rows.Add(UIHelper.BuildDivider(_contentTransform));

            // Column header
            var headerRow = new GameObject("HeaderRow");
            headerRow.transform.SetParent(_contentTransform, false);
            var headerHL = headerRow.AddComponent<HorizontalLayoutGroup>();
            headerHL.childControlWidth = true;
            headerHL.childControlHeight = true;
            headerHL.childForceExpandWidth = false;
            headerHL.childForceExpandHeight = false;
            headerHL.spacing = 6f;
            var headerPad = new RectOffset();
            headerPad.left = 6; headerPad.right = 6; headerPad.top = 2; headerPad.bottom = 2;
            headerHL.padding = headerPad;
            var headerLE = headerRow.AddComponent<LayoutElement>();
            headerLE.preferredHeight = 20f;

            var hdrType = UIHelper.BuildLabel(headerRow.transform, "Server", 160f);
            hdrType.fontSize = 9f;
            hdrType.color = new Color(0.6f, 0.6f, 0.6f);
            var hdrTypeLE = hdrType.gameObject.GetComponent<LayoutElement>();
            hdrTypeLE.flexibleWidth = 1f;

            var hdrIP = UIHelper.BuildLabel(headerRow.transform, "IP Address", 130f);
            hdrIP.fontSize = 9f;
            hdrIP.color = new Color(0.6f, 0.6f, 0.6f);

            var hdrStatus = UIHelper.BuildLabel(headerRow.transform, "Status", 50f);
            hdrStatus.fontSize = 9f;
            hdrStatus.color = new Color(0.6f, 0.6f, 0.6f);
            _rows.Add(headerRow);

            // Find all servers belonging to this customer
            var entries = new List<ServerEntry>();
            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.GetCustomerID() != customerID) continue;
                string ip = srv.IP;
                if (string.IsNullOrEmpty(ip) || ip == "0.0.0.0") continue; // skip unassigned
                string typeName = UIHelper.GetServerTypeName(srv.gameObject.name);
                entries.Add(new ServerEntry
                {
                    Server = srv,
                    IP = ip,
                    TypeName = typeName,
                    ObjName = srv.gameObject.name
                });
            }

            // Sort by IP
            entries.Sort((a, b) => string.Compare(a.IP, b.IP, System.StringComparison.Ordinal));

            if (entries.Count == 0)
            {
                var emptyRow = new GameObject("EmptyRow");
                emptyRow.transform.SetParent(_contentTransform, false);
                var emptyLE = emptyRow.AddComponent<LayoutElement>();
                emptyLE.preferredHeight = 28f;
                var emptyLbl = UIHelper.BuildLabel(emptyRow.transform, "No active IPs", 200f);
                emptyLbl.fontSize = 10f;
                emptyLbl.color = UIHelper.StatusGray;
                emptyLbl.alignment = TextAlignmentOptions.Center;
                _rows.Add(emptyRow);
                return;
            }

            // Count label
            var countLbl = UIHelper.BuildLabel(summaryRow.transform, $"{entries.Count} active", 80f);
            countLbl.fontSize = 10f;
            countLbl.color = UIHelper.StatusGreen;

            for (int i = 0; i < entries.Count; i++)
            {
                BuildIPRow(entries[i]);
            }
        }

        private struct ServerEntry
        {
            public Server Server;
            public string IP;
            public string TypeName;
            public string ObjName;
        }

        private static void BuildSubnetInfo(int customerID)
        {
            // Scan all servers for this customer, group by serverType to find subnets
            var allServers = Object.FindObjectsOfType<Server>();
            var typeSubnets = new Dictionary<int, SubnetEntry>();

            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.GetCustomerID() != customerID) continue;
                string ip = srv.IP;
                if (string.IsNullOrEmpty(ip) || ip == "0.0.0.0") continue;

                int sType = srv.serverType;
                if (typeSubnets.ContainsKey(sType)) continue;

                // Extract subnet prefix
                int lastDot = ip.LastIndexOf('.');
                if (lastDot <= 0) continue;
                string prefix = ip.Substring(0, lastDot);

                string typeName = UIHelper.GetServerTypeName(srv.gameObject.name);
                string objName = srv.gameObject.name;
                typeSubnets[sType] = new SubnetEntry
                {
                    TypeName = typeName,
                    Subnet = prefix,
                    ObjName = objName
                };
            }

            if (typeSubnets.Count == 0) return;

            var infoRow = new GameObject("SubnetInfoRow");
            infoRow.transform.SetParent(_contentTransform, false);
            var infoHL = infoRow.AddComponent<HorizontalLayoutGroup>();
            infoHL.childControlWidth = true;
            infoHL.childControlHeight = true;
            infoHL.childForceExpandWidth = false;
            infoHL.childForceExpandHeight = false;
            infoHL.spacing = 16f;
            var infoPad = new RectOffset();
            infoPad.left = 8; infoPad.right = 8; infoPad.top = 4; infoPad.bottom = 4;
            infoHL.padding = infoPad;
            var infoLE = infoRow.AddComponent<LayoutElement>();
            infoLE.preferredHeight = 22f;

            foreach (var kvp in typeSubnets)
            {
                var entry = kvp.Value;
                Color typeColor = UIHelper.GetDeviceTypeColor(entry.ObjName);

                // Color dot
                var dotGo = new GameObject("Dot");
                dotGo.transform.SetParent(infoRow.transform, false);
                var dotImg = dotGo.AddComponent<Image>();
                dotImg.color = typeColor;
                var dotLE = dotGo.AddComponent<LayoutElement>();
                dotLE.preferredWidth = 8f;
                dotLE.preferredHeight = 8f;

                // "TypeName: subnet"
                var lbl = UIHelper.BuildLabel(infoRow.transform, $"{entry.TypeName}: {entry.Subnet}.x", 180f);
                lbl.fontSize = 9f;
                lbl.color = typeColor;
            }

            _rows.Add(infoRow);
        }

        private struct SubnetEntry
        {
            public string TypeName;
            public string Subnet;
            public string ObjName;
        }

        private static void BuildRevenueSummary(int customerID)
        {
            try
            {
                var bs = BalanceSheet.instance;
                if (bs == null)
                {
                    MelonLogger.Warning("[DCIM] BalanceSheet.instance is null — revenue unavailable");
                    return;
                }
                if (bs.currentRecords == null)
                {
                    MelonLogger.Warning("[DCIM] BalanceSheet.currentRecords is null — revenue unavailable");
                    return;
                }

                // Match by record.customerID instead of dictionary key (IL2CPP key access is broken)
                var entries = bs.currentRecords._entries;
                bool found = false;
                for (int ei = 0; ei < entries.Length; ei++)
                {
                    if (entries[ei].hashCode >= 0 && entries[ei].value != null && entries[ei].value.customerID == customerID)
                    {
                        found = true;
                        var record = entries[ei].value;
                        var revRow = new GameObject("RevenueRow");
                        revRow.transform.SetParent(_contentTransform, false);
                        var revHL = revRow.AddComponent<HorizontalLayoutGroup>();
                        revHL.childControlWidth = true;
                        revHL.childControlHeight = true;
                        revHL.childForceExpandWidth = false;
                        revHL.childForceExpandHeight = false;
                        revHL.spacing = 8f;
                        var revPad = new RectOffset();
                        revPad.left = 8; revPad.right = 8; revPad.top = 2; revPad.bottom = 2;
                        revHL.padding = revPad;
                        var revLE = revRow.AddComponent<LayoutElement>();
                        revLE.preferredHeight = 20f;

                        var revLbl = UIHelper.BuildLabel(revRow.transform, $"Revenue: ${record.revenue:F0}", 120f);
                        revLbl.fontSize = 9f;
                        revLbl.color = UIHelper.StatusGreen;

                        var penLbl = UIHelper.BuildLabel(revRow.transform, $"Penalties: ${record.penalties:F0}", 120f);
                        penLbl.fontSize = 9f;
                        penLbl.color = UIHelper.StatusRed;

                        float net = record.Total;
                        var netLbl = UIHelper.BuildLabel(revRow.transform, $"Net: ${net:F0}", 100f);
                        netLbl.fontSize = 9f;
                        netLbl.color = net >= 0 ? UIHelper.StatusGreen : UIHelper.StatusRed;
                        netLbl.fontStyle = FontStyles.Bold;

                        _rows.Add(revRow);
                        break;
                    }
                }
                if (!found)
                    MelonLogger.Warning($"[DCIM] No BalanceSheet record for custID={customerID}");
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] Revenue read failed: {ex.Message}");
            }
        }

        // ── Add Server Popup ──────────────────────────────────────────

        private static GameObject _addServerPopup;
        private static Transform _addServerPopupContent;
        private static readonly List<GameObject> _addServerPopupRows = new List<GameObject>();
        private static readonly List<Server> _addServerSelected = new List<Server>();
        private static readonly Dictionary<Server, Image> _addServerRowImages = new Dictionary<Server, Image>();
        private static TextMeshProUGUI _addServerConfirmLabel;
        private static Button _addServerConfirmBtn;
        private static int _addServerCustomerID = -1;

        private static void BuildAddServerPopup(GameObject root)
        {
            _addServerPopup = new GameObject("AddServerPopup");
            _addServerPopup.transform.SetParent(root.transform, false);
            var overlayRT = _addServerPopup.AddComponent<RectTransform>();
            overlayRT.anchorMin = Vector2.zero;
            overlayRT.anchorMax = Vector2.one;
            overlayRT.sizeDelta = Vector2.zero;
            overlayRT.offsetMin = Vector2.zero;
            overlayRT.offsetMax = Vector2.zero;
            _addServerPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.6f);

            var panel = new GameObject("Panel");
            panel.transform.SetParent(_addServerPopup.transform, false);
            var panelRT = panel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0.05f, 0.08f);
            panelRT.anchorMax = new Vector2(0.95f, 0.92f);
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
            titleRow.AddComponent<LayoutElement>().preferredHeight = 30f;

            var titleLbl = UIHelper.BuildLabel(titleRow.transform, "Add Unassigned Server", 250f);
            titleLbl.fontSize = 14f;
            titleLbl.fontStyle = FontStyles.Bold;
            titleLbl.color = Color.white;
            titleLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var cancelBtn = UIHelper.BuildButton(titleRow.transform, "Cancel", 80f);
            cancelBtn.onClick.AddListener(new System.Action(() => { _addServerPopup.SetActive(false); }));

            // Scroll area
            var scrollArea = new GameObject("ScrollArea");
            scrollArea.transform.SetParent(panel.transform, false);
            scrollArea.AddComponent<LayoutElement>().flexibleHeight = 1f;

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
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            _addServerPopupContent = content.transform;

            // Bottom row
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
            bottomRow.AddComponent<LayoutElement>().preferredHeight = 36f;

            var spacer = new GameObject("Spacer");
            spacer.transform.SetParent(bottomRow.transform, false);
            spacer.AddComponent<RectTransform>();
            spacer.AddComponent<LayoutElement>().flexibleWidth = 1f;

            _addServerConfirmBtn = UIHelper.BuildButton(bottomRow.transform, "Add Server", 140f);
            _addServerConfirmLabel = _addServerConfirmBtn.GetComponentInChildren<TextMeshProUGUI>();
            _addServerConfirmBtn.interactable = false;
            ReusableFunctions.ChangeButtonNormalColor(_addServerConfirmBtn, new Color(0.1f, 0.4f, 0.1f));
            _addServerConfirmBtn.onClick.AddListener(new System.Action(OnAddServerConfirm));

            _addServerPopup.SetActive(false);
        }

        public static void ShowAddServerPopup(int customerID)
        {
            _addServerCustomerID = customerID;
            _addServerSelected.Clear();
            _addServerRowImages.Clear();
            _addServerConfirmBtn.interactable = false;
            _addServerConfirmLabel.text = "Add Server";

            for (int i = 0; i < _addServerPopupRows.Count; i++)
            {
                if (_addServerPopupRows[i] != null)
                    Object.Destroy(_addServerPopupRows[i]);
            }
            _addServerPopupRows.Clear();

            // "Unused" = no IP provisioned yet (game default customerID is 0, not -1, so IP is the reliable sentinel)
            var allServers = Object.FindObjectsOfType<Server>();
            var unused = new List<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                string ip = srv.IP;
                if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0") continue; // already provisioned
                unused.Add(srv);
            }

            unused.Sort((a, b) => string.Compare(
                UIHelper.GetServerTypeName(a.gameObject.name),
                UIHelper.GetServerTypeName(b.gameObject.name),
                System.StringComparison.Ordinal));

            if (unused.Count == 0)
            {
                var emptyRow = new GameObject("EmptyRow");
                emptyRow.transform.SetParent(_addServerPopupContent, false);
                emptyRow.AddComponent<LayoutElement>().preferredHeight = 30f;
                var emptyLbl = UIHelper.BuildLabel(emptyRow.transform, "No unassigned servers", 300f);
                emptyLbl.fontSize = 11f;
                emptyLbl.color = UIHelper.StatusGray;
                emptyLbl.alignment = TextAlignmentOptions.Center;
                _addServerPopupRows.Add(emptyRow);
            }
            else
            {
                for (int i = 0; i < unused.Count; i++)
                    BuildAddServerRow(unused[i]);
            }

            _addServerPopup.SetActive(true);
        }

        private static void BuildAddServerRow(Server server)
        {
            var row = new GameObject("AddSrvRow");
            row.transform.SetParent(_addServerPopupContent, false);

            var rowImg = row.AddComponent<Image>();
            rowImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);

            var rowHL = row.AddComponent<HorizontalLayoutGroup>();
            rowHL.childControlWidth = true;
            rowHL.childControlHeight = true;
            rowHL.childForceExpandWidth = false;
            rowHL.childForceExpandHeight = false;
            rowHL.spacing = 8f;
            var rowPad = new RectOffset();
            rowPad.left = 10; rowPad.right = 10; rowPad.top = 5; rowPad.bottom = 5;
            rowHL.padding = rowPad;
            row.AddComponent<LayoutElement>().preferredHeight = 34f;

            Color typeColor = UIHelper.GetDeviceTypeColor(server.gameObject.name);
            string typeName = UIHelper.GetServerTypeName(server.gameObject.name);

            string statusStr;
            Color statusColor;
            if (server.isBroken) { statusStr = "BRK"; statusColor = UIHelper.StatusRed; }
            else if (server.isOn) { statusStr = "ON";  statusColor = UIHelper.StatusGreen; }
            else                  { statusStr = "OFF"; statusColor = UIHelper.StatusGray; }

            var typeLbl = UIHelper.BuildLabel(row.transform, typeName, 160f);
            typeLbl.fontSize = 11f;
            typeLbl.color = typeColor;
            typeLbl.gameObject.GetComponent<LayoutElement>().flexibleWidth = 1f;

            var statusLbl = UIHelper.BuildLabel(row.transform, statusStr, 40f);
            statusLbl.fontSize = 10f;
            statusLbl.color = statusColor;

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

            _addServerRowImages[server] = rowImg;

            Server capturedSrv = server;
            Image capturedImg = rowImg;
            btn.onClick.AddListener(new System.Action(() =>
            {
                if (_addServerSelected.Contains(capturedSrv))
                {
                    _addServerSelected.Remove(capturedSrv);
                    capturedImg.color = new Color(0.14f, 0.14f, 0.16f, 1f);
                }
                else
                {
                    _addServerSelected.Add(capturedSrv);
                    capturedImg.color = new Color(0.12f, 0.28f, 0.12f, 1f);
                }
                int count = _addServerSelected.Count;
                _addServerConfirmBtn.interactable = count > 0;
                _addServerConfirmLabel.text = count > 0 ? $"Add ({count})" : "Add Server";
            }));

            _addServerPopupRows.Add(row);
        }

        private static void OnAddServerConfirm()
        {
            if (_addServerSelected.Count == 0 || _addServerCustomerID < 0) return;

            var assignedIPs = DeviceConfigPanel.GetAllUsedIPs();
            for (int i = 0; i < _addServerSelected.Count; i++)
            {
                var srv = _addServerSelected[i];
                srv.UpdateCustomer(_addServerCustomerID);
                DeviceListView.AutoFillIP(srv, assignedIPs);
            }

            var custItem = MainGameManager.instance.GetCustomerItemByID(_addServerCustomerID);
            string custName = custItem != null ? custItem.customerName : $"Customer {_addServerCustomerID}";
            StaticUIElements.instance.AddMeesageInField($"Added {_addServerSelected.Count} server(s) to {custName}");

            _addServerPopup.SetActive(false);
            FloorMapApp.OpenCustomerIPs(_addServerCustomerID);
        }

        private static void BuildIPRow(ServerEntry entry)
        {
            var row = new GameObject("IPRow");
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
            le.preferredHeight = 24f;

            Color typeColor = UIHelper.GetDeviceTypeColor(entry.ObjName);

            // Server type
            var typeLbl = UIHelper.BuildLabel(row.transform, entry.TypeName, 160f);
            typeLbl.fontSize = 10f;
            typeLbl.color = typeColor;
            var typeLE = typeLbl.gameObject.GetComponent<LayoutElement>();
            typeLE.flexibleWidth = 1f;

            // IP
            var ipLbl = UIHelper.BuildLabel(row.transform, entry.IP, 130f);
            ipLbl.fontSize = 10f;
            ipLbl.color = Color.white;

            // Status
            string statusStr;
            Color statusColor;
            if (entry.Server.isBroken)
            {
                statusStr = "X BRK";
                statusColor = UIHelper.StatusRed;
            }
            else if (entry.Server.isOn)
            {
                statusStr = "* ON";
                statusColor = UIHelper.StatusGreen;
            }
            else
            {
                statusStr = "O OFF";
                statusColor = UIHelper.StatusGray;
            }

            var statusLbl = UIHelper.BuildLabel(row.transform, statusStr, 50f);
            statusLbl.fontSize = 10f;
            statusLbl.color = statusColor;

            // Make row clickable → DeviceConfig
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

            Server capturedServer = entry.Server;
            btn.onClick.AddListener(new System.Action(() =>
            {
                FloorMapApp.OpenDeviceFromCustomer(capturedServer);
            }));

            _rows.Add(row);
        }
    }
}
