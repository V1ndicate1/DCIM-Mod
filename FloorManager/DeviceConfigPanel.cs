using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

namespace FloorManager
{
    public static class DeviceConfigPanel
    {
        private static GameObject _root;
        private static Transform _contentTransform;
        private static readonly List<GameObject> _elements = new List<GameObject>();
        private static object _refreshCoroutine;

        public static void Build(GameObject root)
        {
            _root = root;

            // ScrollRect for overflow
            var scrollRect = root.AddComponent<ScrollRect>();
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
            vl.spacing = 6f;
            var pad = new RectOffset();
            pad.left = 16; pad.right = 16; pad.top = 12; pad.bottom = 12;
            vl.padding = pad;
            var csf = content.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scrollRect.content = contentRT;

            _contentTransform = content.transform;
        }

        public static void Populate(Server server, NetworkSwitch sw, PatchPanel pp)
        {
            ClearElements();

            if (server != null)
                BuildServerConfig(server);
            else if (sw != null)
                BuildSwitchConfig(sw);
            else if (pp != null)
                BuildPatchPanelConfig(pp);
        }

        private static void BuildServerConfig(Server server)
        {
            string typeName = UIHelper.GetServerTypeName(server.gameObject.name);
            Color typeColor = UIHelper.GetDeviceTypeColor(server.gameObject.name);

            // Title
            string statusStr = server.isBroken ? "BROKEN" : (server.isOn ? "Online" : "Offline");
            var title = AddLabel($"{typeName} --- {server.sizeInU}U --- {statusStr}", typeColor, 14f);
            title.fontStyle = FontStyles.Bold;

            // Rack label
            string rackLabel = GetRackLabel(server.currentRackPosition);
            if (!string.IsNullOrEmpty(rackLabel))
            {
                var rackRow = AddRow();
                var rackLbl = UIHelper.BuildLabel(rackRow.transform, "Rack:", 80f);
                rackLbl.color = typeColor;
                UIHelper.BuildLabel(rackRow.transform, rackLabel, 100f).color = Color.white;
            }

            // Network connection indicator
            var netRow = AddRow();
            var netLbl = UIHelper.BuildLabel(netRow.transform, "Network:", 80f);
            netLbl.color = typeColor;
            bool serverCabled = server.IsAnyCableConnected();
            BuildEthernetIcon(netRow.transform, serverCabled);
            var netStatus = UIHelper.BuildLabel(netRow.transform, serverCabled ? "Connected" : "No Cable", 120f);
            netStatus.color = serverCabled ? UIHelper.StatusGreen : UIHelper.StatusRed;

            _elements.Add(UIHelper.BuildDivider(_contentTransform));

            // Power controls
            var powerRow = AddRow();
            var powerLbl = UIHelper.BuildLabel(powerRow.transform, "Power:", 80f);
            powerLbl.color = typeColor;

            var onBtn = UIHelper.BuildButton(powerRow.transform, "ON", 80f);
            var offBtn = UIHelper.BuildButton(powerRow.transform, "OFF", 80f);

            // Disable power buttons when broken
            if (server.isBroken)
            {
                onBtn.interactable = false;
                offBtn.interactable = false;
            }

            // Highlight current state
            UpdatePowerButtons(onBtn, offBtn, server.isOn);

            onBtn.onClick.AddListener(new System.Action(() =>
            {
                if (!server.isOn)
                {
                    server.PowerButton();
                    UpdatePowerButtons(onBtn, offBtn, server.isOn);
                    StaticUIElements.instance.AddMeesageInField("Server powered on remotely");
                }
            }));
            offBtn.onClick.AddListener(new System.Action(() =>
            {
                if (server.isOn)
                {
                    server.PowerButton();
                    UpdatePowerButtons(onBtn, offBtn, server.isOn);
                    StaticUIElements.instance.AddMeesageInField("Server powered off remotely");
                }
            }));

            // IP display row
            var ipRow = AddRow();
            var ipLbl = UIHelper.BuildLabel(ipRow.transform, "IP:", 80f);
            ipLbl.color = typeColor;
            var ipVal = UIHelper.BuildLabel(ipRow.transform, server.IP ?? "(none)", 150f);
            ipVal.color = Color.white;
            var ipValLE = ipVal.gameObject.GetComponent<LayoutElement>();
            ipValLE.flexibleWidth = 1f;

            // Auto-assign IP button
            var autoIpBtn = UIHelper.BuildButton(ipRow.transform, "Auto IP", 80f);
            autoIpBtn.onClick.AddListener(new System.Action(() =>
            {
                AutoFillIP(server, ipVal);
            }));

            // Clear IP button
            var clearIpBtn = UIHelper.BuildButton(ipRow.transform, "Clear", 60f);
            clearIpBtn.onClick.AddListener(new System.Action(() =>
            {
                server.SetIP("");
                ipVal.text = "(none)";
                StaticUIElements.instance.AddMeesageInField("IP cleared");
            }));

            // Manual IP editor row — subnet prefix + last octet picker
            var manualRow = AddRow();
            var manualLbl = UIHelper.BuildLabel(manualRow.transform, "Set IP:", 80f);
            manualLbl.color = typeColor;

            // Parse current IP or customer subnet to get the prefix
            string currentIP = server.IP ?? "";
            string subnetPrefix = "";
            int currentOctet = 2;

            if (!string.IsNullOrEmpty(currentIP) && currentIP.Contains("."))
            {
                int lastDot = currentIP.LastIndexOf('.');
                subnetPrefix = currentIP.Substring(0, lastDot + 1);
                int.TryParse(currentIP.Substring(lastDot + 1), out currentOctet);
                if (currentOctet < 2) currentOctet = 2;
            }
            else
            {
                // Try to get subnet from customer
                subnetPrefix = GetCustomerSubnetPrefix(server);
                if (string.IsNullOrEmpty(subnetPrefix)) subnetPrefix = "0.0.0.";
            }

            var prefixLbl = UIHelper.BuildLabel(manualRow.transform, subnetPrefix, 100f);
            prefixLbl.color = Color.white;

            // Octet value label
            int[] octetVal = { currentOctet };
            var octetLbl = UIHelper.BuildLabel(manualRow.transform, octetVal[0].ToString(), 36f);
            octetLbl.color = Color.white;
            octetLbl.alignment = TextAlignmentOptions.Center;
            octetLbl.fontStyle = FontStyles.Bold;

            // - button
            var minusBtn = UIHelper.BuildButton(manualRow.transform, "-", 30f);
            minusBtn.onClick.AddListener(new System.Action(() =>
            {
                octetVal[0]--;
                if (octetVal[0] < 2) octetVal[0] = 254;
                octetLbl.text = octetVal[0].ToString();
            }));

            // + button
            var plusBtn = UIHelper.BuildButton(manualRow.transform, "+", 30f);
            plusBtn.onClick.AddListener(new System.Action(() =>
            {
                octetVal[0]++;
                if (octetVal[0] > 254) octetVal[0] = 2;
                octetLbl.text = octetVal[0].ToString();
            }));

            // Set button — applies the manual IP
            string capturedPrefix = subnetPrefix;
            var setBtn = UIHelper.BuildButton(manualRow.transform, "Set", 50f);
            setBtn.onClick.AddListener(new System.Action(() =>
            {
                string newIP = capturedPrefix + octetVal[0].ToString();
                server.SetIP(newIP);
                ipVal.text = newIP;
                StaticUIElements.instance.AddMeesageInField($"IP set to {newIP}");
            }));

            // Customer — dropdown list of all customers
            var custHeader = AddRow();
            var custLbl = UIHelper.BuildLabel(custHeader.transform, "Customer:", 80f);
            custLbl.color = typeColor;

            // Current customer display (logo + name) — updated when selection changes
            var currentLogoGo = new GameObject("CustLogo");
            currentLogoGo.transform.SetParent(custHeader.transform, false);
            var currentLogoImg = currentLogoGo.AddComponent<Image>();
            var currentLogoLE = currentLogoGo.AddComponent<LayoutElement>();
            currentLogoLE.preferredWidth = 24f;
            currentLogoLE.preferredHeight = 24f;

            var currentCustLabel = UIHelper.BuildLabel(custHeader.transform, "", 180f);
            currentCustLabel.color = Color.white;
            currentCustLabel.fontStyle = FontStyles.Bold;
            var currentCustLE = currentCustLabel.gameObject.GetComponent<LayoutElement>();
            currentCustLE.flexibleWidth = 1f;

            int activeCustId = server.GetCustomerID();

            // Helper to update the current customer display
            System.Action updateCurrentDisplay = () =>
            {
                int id = server.GetCustomerID();
                var ci = MainGameManager.instance.GetCustomerItemByID(id);
                if (ci != null)
                {
                    currentCustLabel.text = ci.customerName ?? $"Customer {id}";
                    if (ci.logo != null)
                    {
                        currentLogoImg.sprite = ci.logo;
                        currentLogoImg.color = Color.white;
                    }
                    else
                        currentLogoImg.color = new Color(0, 0, 0, 0);
                }
                else
                {
                    currentCustLabel.text = $"Customer {id}";
                    currentLogoImg.color = new Color(0, 0, 0, 0);
                }
            };
            updateCurrentDisplay();

            // Build list of all available customers
            var custBases = MainGameManager.instance.customerBases;
            var customerRows = new List<GameObject>();

            for (int ci = 0; ci < custBases.Length; ci++)
            {
                var cb = custBases[ci];
                int custId = cb.customerID;
                if (custId < 0) continue;
                var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                string custName = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}";

                var custRow = new GameObject($"CustOption_{custId}");
                custRow.transform.SetParent(_contentTransform, false);
                var rowImg = custRow.AddComponent<Image>();
                bool isSelected = custId == activeCustId;
                rowImg.color = isSelected
                    ? new Color(0.15f, 0.25f, 0.15f, 1f)
                    : new Color(0.10f, 0.10f, 0.12f, 1f);

                var rowHL = custRow.AddComponent<HorizontalLayoutGroup>();
                rowHL.childControlWidth = true;
                rowHL.childControlHeight = true;
                rowHL.childForceExpandWidth = false;
                rowHL.childForceExpandHeight = false;
                rowHL.spacing = 8f;
                var rowPad = new RectOffset();
                rowPad.left = 16; rowPad.right = 16; rowPad.top = 4; rowPad.bottom = 4;
                rowHL.padding = rowPad;

                var rowLE = custRow.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 32f;

                // Selection indicator
                var indicator = UIHelper.BuildLabel(custRow.transform, isSelected ? ">" : " ", 16f);
                indicator.fontSize = 12f;
                indicator.color = isSelected ? UIHelper.StatusGreen : new Color(0.3f, 0.3f, 0.3f);

                // Logo
                if (custItem != null && custItem.logo != null)
                {
                    var logoGo = new GameObject("Logo");
                    logoGo.transform.SetParent(custRow.transform, false);
                    var logoImg = logoGo.AddComponent<Image>();
                    logoImg.sprite = custItem.logo;
                    logoImg.color = Color.white;
                    logoImg.raycastTarget = false;
                    var logoLE2 = logoGo.AddComponent<LayoutElement>();
                    logoLE2.preferredWidth = 22f;
                    logoLE2.preferredHeight = 22f;
                }

                // Name
                var nameLbl = UIHelper.BuildLabel(custRow.transform, custName, 200f);
                nameLbl.fontSize = 11f;
                nameLbl.color = isSelected ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                var nameLE = nameLbl.gameObject.GetComponent<LayoutElement>();
                nameLE.flexibleWidth = 1f;

                // Click handler
                var btn = custRow.AddComponent<Button>();
                btn.targetGraphic = rowImg;
                var btnCb = new ColorBlock();
                btnCb.normalColor = rowImg.color;
                btnCb.highlightedColor = new Color(0.18f, 0.22f, 0.28f, 1f);
                btnCb.pressedColor = new Color(0.08f, 0.12f, 0.08f, 1f);
                btnCb.selectedColor = rowImg.color;
                btnCb.colorMultiplier = 1f;
                btnCb.fadeDuration = 0.1f;
                btn.colors = btnCb;
                var nav = new Navigation();
                nav.mode = Navigation.Mode.None;
                btn.navigation = nav;

                int capturedCustId = custId;
                // Capture refs for highlight update
                var capturedRowImg = rowImg;
                var capturedIndicator = indicator;
                var capturedNameLbl = nameLbl;

                btn.onClick.AddListener(new System.Action(() =>
                {
                    server.UpdateCustomer(capturedCustId);

                    // Update all row highlights
                    for (int r = 0; r < customerRows.Count; r++)
                    {
                        if (customerRows[r] == null) continue;
                        bool sel = customerRows[r] == custRow;
                        var img = customerRows[r].GetComponent<Image>();
                        if (img != null)
                            img.color = sel
                                ? new Color(0.15f, 0.25f, 0.15f, 1f)
                                : new Color(0.10f, 0.10f, 0.12f, 1f);
                        // Update indicator (first TMP child) and name (second TMP child)
                        var tmps = customerRows[r].GetComponentsInChildren<TextMeshProUGUI>();
                        for (int t = 0; t < tmps.Length; t++)
                        {
                            if (t == 0)
                            {
                                tmps[t].text = sel ? ">" : " ";
                                tmps[t].color = sel ? UIHelper.StatusGreen : new Color(0.3f, 0.3f, 0.3f);
                            }
                            else if (t == 1)
                            {
                                tmps[t].color = sel ? Color.white : new Color(0.7f, 0.7f, 0.7f);
                            }
                        }
                    }

                    updateCurrentDisplay();
                    AutoFillIP(server, ipVal);
                }));

                customerRows.Add(custRow);
                _elements.Add(custRow);
            }

            _elements.Add(UIHelper.BuildDivider(_contentTransform));

            // EOL timer
            var eolRow = AddRow();
            var eolLbl = UIHelper.BuildLabel(eolRow.transform, "EOL Timer:", 80f);
            eolLbl.color = typeColor;
            string eolStr;
            if (server.eolTime <= 0)
                eolStr = "EXPIRED";
            else
            {
                int hours = server.eolTime / 3600;
                int mins = (server.eolTime % 3600) / 60;
                int secs = server.eolTime % 60;
                eolStr = $"{hours}.{mins}.{secs}";
            }
            var eolVal = UIHelper.BuildLabel(eolRow.transform, eolStr, 100f);
            eolVal.color = server.eolTime <= 0 ? UIHelper.StatusYellow : Color.white;

            // Processing speed — parse rated IOPS from server type name
            var procRow = AddRow();
            var procLbl = UIHelper.BuildLabel(procRow.transform, "Processing:", 80f);
            procLbl.color = typeColor;
            string ratedIops = "?";
            var iopsMatch = System.Text.RegularExpressions.Regex.Match(typeName, @"(\d+)\s*IOPS");
            if (iopsMatch.Success)
                ratedIops = iopsMatch.Groups[1].Value;
            var procVal = UIHelper.BuildLabel(procRow.transform,
                $"{ratedIops} IOPS", 200f);
            procVal.color = Color.white;

#if !STRIP_HACKING
            // ── Security designation ───────────────────────────────────
            _elements.Add(UIHelper.BuildDivider(_contentTransform));
            var srvSecHdr = AddLabel("Security Role:", typeColor, 12f);
            srvSecHdr.fontStyle = FontStyles.Bold;

            bool isHoneypot = HackingSystem.IsHoneypot(server);
            var honeypotRow = AddRow();
            UIHelper.BuildLabel(honeypotRow.transform, "Honeypot:", 80f).color = typeColor;
            var honeypotBtn = UIHelper.BuildButton(honeypotRow.transform,
                isHoneypot ? "Remove Honeypot" : "Set as Honeypot", 140f);
            ReusableFunctions.ChangeButtonNormalColor(honeypotBtn,
                isHoneypot ? new Color(0.6f, 0.1f, 0.1f) : new Color(0.35f, 0.1f, 0.35f));

            honeypotBtn.onClick.AddListener(new System.Action(() =>
            {
                if (HackingSystem.IsHoneypot(server))
                {
                    HackingSystem.RemoveHoneypot(server);
                    honeypotBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Set as Honeypot";
                    ReusableFunctions.ChangeButtonNormalColor(honeypotBtn, new Color(0.35f, 0.1f, 0.35f));
                }
                else
                {
                    HackingSystem.DesignateHoneypot(server);
                    honeypotBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Remove Honeypot";
                    ReusableFunctions.ChangeButtonNormalColor(honeypotBtn, new Color(0.6f, 0.1f, 0.1f));
                }
            }));

            AddLabel("Honeypot powers server off and slows attack frequency.", new Color(0.5f, 0.5f, 0.5f), 9f);
#endif

            _refreshCoroutine = MelonCoroutines.Start(RefreshServerConfig(server, eolVal));
        }

        private static void BuildSwitchConfig(NetworkSwitch sw)
        {
            string typeName = MainGameManager.instance.ReturnSwitchNameFromType(sw.switchType);
            Color typeColor = UIHelper.SwitchColor;

            // Title
            string statusStr = sw.isBroken ? "BROKEN" : (sw.isOn ? "Online" : "Offline");
            var title = AddLabel($"{typeName} --- {GetSwitchLabel(sw)} --- {statusStr}", typeColor, 14f);
            title.fontStyle = FontStyles.Bold;

            // Rack label
            string rackLabel = GetRackLabel(sw.currentRackPosition);
            if (!string.IsNullOrEmpty(rackLabel))
            {
                var rackRow = AddRow();
                var rackLbl = UIHelper.BuildLabel(rackRow.transform, "Rack:", 80f);
                rackLbl.color = typeColor;
                UIHelper.BuildLabel(rackRow.transform, rackLabel, 100f).color = Color.white;
            }

            // Network connection indicator
            var swNetRow = AddRow();
            var swNetLbl = UIHelper.BuildLabel(swNetRow.transform, "Network:", 80f);
            swNetLbl.color = typeColor;
            bool swCabled = sw.IsAnyCableConnected();
            BuildEthernetIcon(swNetRow.transform, swCabled);
            var swNetStatus = UIHelper.BuildLabel(swNetRow.transform, swCabled ? "Connected" : "No Cable", 120f);
            swNetStatus.color = swCabled ? UIHelper.StatusGreen : UIHelper.StatusRed;

            _elements.Add(UIHelper.BuildDivider(_contentTransform));

            // Power controls
            var powerRow = AddRow();
            var swPowerLbl = UIHelper.BuildLabel(powerRow.transform, "Power:", 80f);
            swPowerLbl.color = typeColor;

            var swOnBtn = UIHelper.BuildButton(powerRow.transform, "ON", 80f);
            var swOffBtn = UIHelper.BuildButton(powerRow.transform, "OFF", 80f);

            if (sw.isBroken)
            {
                swOnBtn.interactable = false;
                swOffBtn.interactable = false;
            }

            UpdatePowerButtons(swOnBtn, swOffBtn, sw.isOn);

            swOnBtn.onClick.AddListener(new System.Action(() =>
            {
                if (!sw.isOn)
                {
                    sw.PowerButton();
                    UpdatePowerButtons(swOnBtn, swOffBtn, sw.isOn);
                    StaticUIElements.instance.AddMeesageInField("Switch powered on remotely");
                }
            }));
            swOffBtn.onClick.AddListener(new System.Action(() =>
            {
                if (sw.isOn)
                {
                    sw.PowerButton();
                    UpdatePowerButtons(swOnBtn, swOffBtn, sw.isOn);
                    StaticUIElements.instance.AddMeesageInField("Switch powered off remotely");
                }
            }));

            // Label
            var labelRow = AddRow();
            var labelLbl = UIHelper.BuildLabel(labelRow.transform, "Label:", 80f);
            labelLbl.color = typeColor;

            // Label input field (simple text display + apply button)
            var labelVal = UIHelper.BuildLabel(labelRow.transform, GetSwitchLabel(sw), 150f);
            labelVal.color = Color.white;
            var labelValLE = labelVal.gameObject.GetComponent<LayoutElement>();
            labelValLE.flexibleWidth = 1f;

            _elements.Add(UIHelper.BuildDivider(_contentTransform));

            // Connected ports
            var portsHeader = AddLabel("Connected Ports:", typeColor, 12f);
            portsHeader.fontStyle = FontStyles.Bold;

            // Parse port count from switch type name (e.g. "8 Port Switch" → 8)
            int maxPorts = 0;
            var portMatch = System.Text.RegularExpressions.Regex.Match(typeName, @"(\d+)\s*[Pp]ort");
            if (portMatch.Success)
                int.TryParse(portMatch.Groups[1].Value, out maxPorts);

            try
            {
                var connected = sw.GetConnectedDevices();
                int connectedCount = connected != null ? connected.Count : 0;
                if (maxPorts > 0)
                {
                    var portCountRow = AddRow();
                    UIHelper.BuildLabel(portCountRow.transform, "Ports:", 80f).color = typeColor;
                    UIHelper.BuildLabel(portCountRow.transform, $"{connectedCount} / {maxPorts} used", 200f).color = Color.white;
                }

                if (connected != null && connectedCount > 0)
                {
                    int displayCount = maxPorts > 0 ? System.Math.Min(connectedCount, maxPorts) : connectedCount;
                    for (int i = 0; i < displayCount; i++)
                    {
                        var entry = connected[i];
                        string deviceName = entry.Item1;
                        int cableId = entry.Item2;
                        var portRow = AddRow();
                        var portLbl = UIHelper.BuildLabel(portRow.transform,
                            $"  Port {i + 1} -> {deviceName}", 350f);
                        portLbl.fontSize = 10f;
                        portLbl.color = new Color(0.7f, 0.7f, 0.7f);
                    }
                }
                else
                {
                    var noneLbl = AddLabel("  (no connections)", new Color(0.5f, 0.5f, 0.5f), 10f);
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] GetConnectedDevices failed: {ex.Message}");
                AddLabel("  (port list unavailable)", new Color(0.5f, 0.5f, 0.5f), 10f);
            }

            // EOL timer
            _elements.Add(UIHelper.BuildDivider(_contentTransform));
            var eolRow = AddRow();
            var eolLbl = UIHelper.BuildLabel(eolRow.transform, "EOL Timer:", 80f);
            eolLbl.color = typeColor;
            string eolStr;
            if (sw.eolTime <= 0)
                eolStr = "EXPIRED";
            else
            {
                int mins = sw.eolTime / 60;
                int secs = sw.eolTime % 60;
                eolStr = $"{mins:D3}:{secs:D2}";
            }
            var eolVal = UIHelper.BuildLabel(eolRow.transform, eolStr, 100f);
            eolVal.color = sw.eolTime <= 0 ? UIHelper.StatusYellow : Color.white;

#if !STRIP_HACKING
            // ── Security designation ───────────────────────────────────
            _elements.Add(UIHelper.BuildDivider(_contentTransform));
            var secHdr = AddLabel("Security Role:", typeColor, 12f);
            secHdr.fontStyle = FontStyles.Bold;

            bool isFirewall = HackingSystem.IsFirewall(sw);
            bool isIDS      = HackingSystem.IsIDS(sw);

            // Build all UI elements first so lambdas can capture them all
            var fwRow = AddRow();
            UIHelper.BuildLabel(fwRow.transform, "Firewall:", 80f).color = typeColor;
            var fwBtn = UIHelper.BuildButton(fwRow.transform, isFirewall ? "Remove Firewall" : "Set as Firewall", 140f);
            ReusableFunctions.ChangeButtonNormalColor(fwBtn, isFirewall ? new Color(0.6f, 0.1f, 0.1f) : new Color(0.1f, 0.35f, 0.1f));

            var idsSecRow = AddRow();
            UIHelper.BuildLabel(idsSecRow.transform, "IDS:", 80f).color = typeColor;
            var idsBtn = UIHelper.BuildButton(idsSecRow.transform, isIDS ? "Remove IDS" : "Set as IDS", 140f);
            ReusableFunctions.ChangeButtonNormalColor(idsBtn, isIDS ? new Color(0.6f, 0.1f, 0.1f) : new Color(0.1f, 0.35f, 0.1f));

            // HP bar row — only visible when switch is a firewall
            var hpRow = AddRow();
            hpRow.SetActive(isFirewall);
            UIHelper.BuildLabel(hpRow.transform, "FW HP:", 80f).color = typeColor;

            var hpBarBg = new GameObject("HPBarBg");
            hpBarBg.transform.SetParent(hpRow.transform, false);
            hpBarBg.AddComponent<Image>().color = new Color(0.15f, 0.15f, 0.15f);
            var hpBarBgLE = hpBarBg.AddComponent<LayoutElement>();
            hpBarBgLE.preferredWidth = 100f;
            hpBarBgLE.preferredHeight = 14f;

            var hpFillGo = new GameObject("HPFill");
            hpFillGo.transform.SetParent(hpBarBg.transform, false);
            var hpFillRT = hpFillGo.AddComponent<RectTransform>();
            hpFillRT.anchorMin = Vector2.zero;
            hpFillRT.anchorMax = Vector2.one;
            hpFillRT.offsetMin = Vector2.zero;
            hpFillRT.offsetMax = Vector2.zero;
            hpFillGo.AddComponent<Image>().color = new Color(0.15f, 0.65f, 0.15f);

            var hpLbl = UIHelper.BuildLabel(hpRow.transform, "", 70f);
            hpLbl.color = Color.white;

            var patchBtn = UIHelper.BuildButton(hpRow.transform, "", 110f);

            if (isFirewall) RefreshHPBar(hpFillRT, hpLbl, patchBtn, sw);

            // Click handlers — all UI elements now declared
            fwBtn.onClick.AddListener(new System.Action(() =>
            {
                if (HackingSystem.IsFirewall(sw))
                {
                    HackingSystem.RemoveFirewall(sw);
                    fwBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Set as Firewall";
                    ReusableFunctions.ChangeButtonNormalColor(fwBtn, new Color(0.1f, 0.35f, 0.1f));
                    hpRow.SetActive(false);
                }
                else
                {
                    HackingSystem.DesignateFirewall(sw);
                    fwBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Remove Firewall";
                    ReusableFunctions.ChangeButtonNormalColor(fwBtn, new Color(0.6f, 0.1f, 0.1f));
                    // DesignateFirewall calls RemoveIDS internally
                    idsBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Set as IDS";
                    ReusableFunctions.ChangeButtonNormalColor(idsBtn, new Color(0.1f, 0.35f, 0.1f));
                    hpRow.SetActive(true);
                    RefreshHPBar(hpFillRT, hpLbl, patchBtn, sw);
                }
            }));

            idsBtn.onClick.AddListener(new System.Action(() =>
            {
                if (HackingSystem.IsIDS(sw))
                {
                    HackingSystem.RemoveIDS(sw);
                    idsBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Set as IDS";
                    ReusableFunctions.ChangeButtonNormalColor(idsBtn, new Color(0.1f, 0.35f, 0.1f));
                }
                else
                {
                    HackingSystem.DesignateIDS(sw);
                    idsBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Remove IDS";
                    ReusableFunctions.ChangeButtonNormalColor(idsBtn, new Color(0.6f, 0.1f, 0.1f));
                    // DesignateIDS calls RemoveFirewall internally
                    fwBtn.GetComponentInChildren<TextMeshProUGUI>().text = "Set as Firewall";
                    ReusableFunctions.ChangeButtonNormalColor(fwBtn, new Color(0.1f, 0.35f, 0.1f));
                    hpRow.SetActive(false);
                }
            }));

            patchBtn.onClick.AddListener(new System.Action(() =>
            {
                HackingSystem.ApplySecurityPatch(sw);
                RefreshHPBar(hpFillRT, hpLbl, patchBtn, sw);
            }));

            _refreshCoroutine = MelonCoroutines.Start(RefreshSwitchConfig(sw, eolVal, hpFillRT, hpLbl, patchBtn));
#else
            _refreshCoroutine = MelonCoroutines.Start(RefreshSwitchConfig(sw, eolVal));
#endif
        }

        private static void StopRefresh()
        {
            if (_refreshCoroutine != null)
            {
                MelonCoroutines.Stop(_refreshCoroutine);
                _refreshCoroutine = null;
            }
        }

        private static IEnumerator RefreshServerConfig(Server server, TextMeshProUGUI eolLbl)
        {
            while (_root != null && _root.activeSelf)
            {
                yield return new WaitForSeconds(1f);
                if (eolLbl == null) yield break;
                if (server.eolTime <= 0)
                {
                    eolLbl.text = "EXPIRED";
                    eolLbl.color = UIHelper.StatusYellow;
                }
                else
                {
                    int h = server.eolTime / 3600;
                    int m = (server.eolTime % 3600) / 60;
                    int s = server.eolTime % 60;
                    eolLbl.text = $"{h}.{m}.{s}";
                    eolLbl.color = Color.white;
                }
            }
            _refreshCoroutine = null;
        }

        private static IEnumerator RefreshSwitchConfig(NetworkSwitch sw, TextMeshProUGUI eolLbl, RectTransform hpFillRT = null, TextMeshProUGUI hpLbl = null, Button patchBtn = null)
        {
            while (_root != null && _root.activeSelf)
            {
                yield return new WaitForSeconds(1f);
                if (eolLbl == null) yield break;
                if (sw.eolTime <= 0)
                {
                    eolLbl.text = "EXPIRED";
                    eolLbl.color = UIHelper.StatusYellow;
                }
                else
                {
                    int mins = sw.eolTime / 60;
                    int secs = sw.eolTime % 60;
                    eolLbl.text = $"{mins:D3}:{secs:D2}";
                    eolLbl.color = Color.white;
                }
#if !STRIP_HACKING
                if (HackingSystem.IsFirewall(sw) && hpFillRT != null)
                    RefreshHPBar(hpFillRT, hpLbl, patchBtn, sw);
#endif
            }
            _refreshCoroutine = null;
        }

        private static void BuildEthernetIcon(Transform parent, bool connected)
        {
            var container = new GameObject("EthernetIcon");
            container.transform.SetParent(parent, false);
            var cLE = container.AddComponent<LayoutElement>();
            cLE.preferredWidth = 22f;
            cLE.preferredHeight = 18f;

            Color bodyColor = connected ? new Color(0.25f, 0.55f, 0.25f) : new Color(0.45f, 0.45f, 0.45f);

            // Port housing body
            var housing = new GameObject("Housing");
            housing.transform.SetParent(container.transform, false);
            var hRT = housing.AddComponent<RectTransform>();
            hRT.anchorMin = hRT.anchorMax = new Vector2(0.5f, 0.5f);
            hRT.sizeDelta = new Vector2(20f, 12f);
            hRT.anchoredPosition = new Vector2(0f, 3f);
            housing.AddComponent<Image>().color = bodyColor;

            // Inner port hole
            var hole = new GameObject("Hole");
            hole.transform.SetParent(housing.transform, false);
            var hoRT = hole.AddComponent<RectTransform>();
            hoRT.anchorMin = hoRT.anchorMax = new Vector2(0.5f, 0.5f);
            hoRT.sizeDelta = new Vector2(13f, 7f);
            hoRT.anchoredPosition = new Vector2(0f, 1f);
            hole.AddComponent<Image>().color = new Color(0.08f, 0.08f, 0.10f);

            // Gold contact pins inside hole
            for (int p = 0; p < 4; p++)
            {
                var pin = new GameObject("Pin");
                pin.transform.SetParent(hole.transform, false);
                var pRT = pin.AddComponent<RectTransform>();
                pRT.anchorMin = pRT.anchorMax = new Vector2(0.5f, 0.5f);
                pRT.sizeDelta = new Vector2(2f, 5f);
                pRT.anchoredPosition = new Vector2(-4.5f + p * 3f, -0.5f);
                pin.AddComponent<Image>().color = new Color(0.75f, 0.65f, 0.20f);
            }

            // Locking tab below housing
            var tab = new GameObject("Tab");
            tab.transform.SetParent(container.transform, false);
            var tabRT = tab.AddComponent<RectTransform>();
            tabRT.anchorMin = tabRT.anchorMax = new Vector2(0.5f, 0.5f);
            tabRT.sizeDelta = new Vector2(7f, 4f);
            tabRT.anchoredPosition = new Vector2(0f, -5f);
            tab.AddComponent<Image>().color = bodyColor;

            // Red diagonal slash when not connected
            if (!connected)
            {
                var slash = new GameObject("Slash");
                slash.transform.SetParent(container.transform, false);
                var sRT = slash.AddComponent<RectTransform>();
                sRT.anchorMin = sRT.anchorMax = new Vector2(0.5f, 0.5f);
                sRT.sizeDelta = new Vector2(3f, 26f);
                sRT.anchoredPosition = new Vector2(0f, 0f);
                sRT.localRotation = Quaternion.Euler(0f, 0f, 45f);
                slash.AddComponent<Image>().color = new Color(0.85f, 0.15f, 0.15f);
            }
        }

        private static void BuildPatchPanelConfig(PatchPanel pp)
        {
            Color typeColor = UIHelper.PatchPanelColor;

            var title = AddLabel($"Patch Panel --- {pp.sizeInU}U", typeColor, 14f);
            title.fontStyle = FontStyles.Bold;

            _elements.Add(UIHelper.BuildDivider(_contentTransform));

            var typeRow = AddRow();
            UIHelper.BuildLabel(typeRow.transform, "Type:", 80f).color = typeColor;
            UIHelper.BuildLabel(typeRow.transform, "Patch Panel", 200f).color = Color.white;

            var sizeRow = AddRow();
            UIHelper.BuildLabel(sizeRow.transform, "Size:", 80f).color = typeColor;
            UIHelper.BuildLabel(sizeRow.transform, $"{pp.sizeInU}U", 200f).color = Color.white;

            // Slot info
            if (pp.currentRackPosition != null)
            {
                var slotRow = AddRow();
                UIHelper.BuildLabel(slotRow.transform, "Slot:", 80f).color = typeColor;
                UIHelper.BuildLabel(slotRow.transform, $"{pp.currentRackPosition.positionIndex + 1}", 200f).color = Color.white;
            }

            _elements.Add(UIHelper.BuildDivider(_contentTransform));
            AddLabel("(No configurable options)", new Color(0.5f, 0.5f, 0.5f), 10f);
        }

        private static void AutoFillIP(Server server, TextMeshProUGUI ipLabel)
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

                string subnet = FindSubnetForServerType(targetBase, server.serverType);
                if (subnet == null) return;

                var setIP = Object.FindObjectOfType<SetIP>();
                if (setIP == null) return;

                // Build set of all IPs currently in use
                var usedIPs = GetAllUsedIPs();

                string[] usableIPs = setIP.GetUsableIPsFromSubnet(subnet);
                if (usableIPs != null && usableIPs.Length > 0)
                {
                    string chosenIP = null;
                    for (int ui = 0; ui < usableIPs.Length; ui++)
                    {
                        string ip = usableIPs[ui];
                        int lastDot = ip.LastIndexOf('.');
                        if (lastDot >= 0)
                        {
                            string lastOctet = ip.Substring(lastDot + 1);
                            if (lastOctet == "0" || lastOctet == "1") continue;
                        }
                        if (usedIPs.Contains(ip)) continue;
                        chosenIP = ip;
                        break;
                    }
                    if (chosenIP != null)
                    {
                        server.SetIP(chosenIP);
                        ipLabel.text = chosenIP;
                        StaticUIElements.instance.AddMeesageInField($"IP auto-assigned: {chosenIP}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] Auto-fill IP failed: {ex.Message}");
            }
        }

        internal static string FindSubnetForServerType(CustomerBase cb, int serverType)
        {
            var subnetsPerApp = cb.GetSubnetsPerApp();
            if (subnetsPerApp == null || subnetsPerApp.Count == 0) return null;

            // Direct key lookup — works in IL2CPP (only enumeration crashes, not TryGetValue)
            try
            {
                string subnet;
                if (subnetsPerApp.TryGetValue(serverType, out subnet) && subnet != null)
                    return subnet;
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] Subnet lookup failed: {ex.Message}");
            }

            // Fallback: first available subnet
            var entries = subnetsPerApp._entries;
            for (int ei = 0; ei < entries.Length; ei++)
            {
                if (entries[ei].hashCode >= 0)
                    return entries[ei].value;
            }
            return null;
        }

        internal static HashSet<string> GetAllUsedIPs()
        {
            var used = new HashSet<string>();
            var allServers = Object.FindObjectsOfType<Server>();
            for (int i = 0; i < allServers.Length; i++)
            {
                string ip = allServers[i].IP;
                if (!string.IsNullOrEmpty(ip) && ip != "0.0.0.0")
                    used.Add(ip);
            }
            return used;
        }

        private static string GetCustomerSubnetPrefix(Server server)
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
                if (targetBase == null) return null;

                var subnetsPerApp = targetBase.GetSubnetsPerApp();
                if (subnetsPerApp == null || subnetsPerApp.Count == 0) return null;

                var entries = subnetsPerApp._entries;
                for (int ei = 0; ei < entries.Length; ei++)
                {
                    if (entries[ei].hashCode >= 0)
                    {
                        string subnet = entries[ei].value;
                        if (subnet != null && subnet.Contains("."))
                        {
                            int lastDot = subnet.LastIndexOf('.');
                            return subnet.Substring(0, lastDot + 1);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] GetCustomerSubnetPrefix failed: {ex.Message}");
            }
            return null;
        }

#if !STRIP_HACKING
        private static void RefreshHPBar(RectTransform fill, TextMeshProUGUI lbl, Button patchBtn, NetworkSwitch sw)
        {
            int hp    = HackingSystem.GetFirewallHP(sw);
            int maxHp = HackingSystem.GetFirewallMaxHP(sw);
            if (hp < 0) { lbl.text = ""; return; }
            float pct = maxHp > 0 ? (float)hp / maxHp : 0f;
            fill.anchorMax = new Vector2(Mathf.Clamp01(pct), 1f);
            fill.offsetMax = Vector2.zero;
            lbl.text = $"{hp}/{maxHp}";
            lbl.color = pct < 0.3f ? UIHelper.StatusRed : pct < 0.6f ? UIHelper.StatusYellow : Color.white;
            bool atCap = maxHp >= HackingSystem.FirewallMaxHP;
            int cost = 500 + (HackingSystem.OffsiteTier * 200);
            patchBtn.GetComponentInChildren<TextMeshProUGUI>().text = atCap ? "MAX HP" : $"Patch (${cost})";
            patchBtn.interactable = !atCap;
        }
#endif

        private static void UpdatePowerButtons(Button onBtn, Button offBtn, bool isOn)
        {
            ReusableFunctions.ChangeButtonNormalColor(onBtn,
                isOn ? new Color(0.1f, 0.55f, 0.1f) : new Color(0.2f, 0.2f, 0.2f));
            ReusableFunctions.ChangeButtonNormalColor(offBtn,
                !isOn ? new Color(0.55f, 0.1f, 0.1f) : new Color(0.2f, 0.2f, 0.2f));
        }

        private static TextMeshProUGUI AddLabel(string text, Color color, float fontSize)
        {
            var lbl = UIHelper.BuildLabel(_contentTransform, text, 400f);
            lbl.fontSize = fontSize;
            lbl.color = color;
            var le = lbl.gameObject.GetComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            _elements.Add(lbl.gameObject);
            return lbl;
        }

        private static GameObject AddRow()
        {
            var row = new GameObject("Row");
            row.transform.SetParent(_contentTransform, false);
            var hl = row.AddComponent<HorizontalLayoutGroup>();
            hl.childControlWidth = true;
            hl.childControlHeight = true;
            hl.childForceExpandWidth = false;
            hl.childForceExpandHeight = false;
            hl.spacing = 6f;
            var le = row.AddComponent<LayoutElement>();
            le.preferredHeight = 26f;
            _elements.Add(row);
            return row;
        }

        private static string GetRackLabel(RackPosition rackPosition)
        {
            if (rackPosition == null) return null;
            var rack = rackPosition.GetComponentInParent<Rack>();
            if (rack == null) return null;

            int rackId = rack.GetInstanceID();
            var lastRacks = SearchEngine.LastRacks;
            for (int i = 0; i < lastRacks.Count; i++)
            {
                if (lastRacks[i].Rack != null && lastRacks[i].Rack.GetInstanceID() == rackId)
                    return lastRacks[i].Label;
            }
            return null;
        }

        private static void ClearElements()
        {
            StopRefresh();
            for (int i = 0; i < _elements.Count; i++)
            {
                if (_elements[i] != null)
                    Object.Destroy(_elements[i]);
            }
            _elements.Clear();
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
