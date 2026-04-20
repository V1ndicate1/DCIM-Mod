using Il2Cpp;
using Il2CppTMPro;
using MelonLoader;
using UnityEngine;
using System.Collections.Generic;

namespace FloorManager
{
    public static class SearchEngine
    {
        // ── Device info struct ──────────────────────────────────────
        public struct DeviceInfo
        {
            public Server Server;
            public NetworkSwitch Switch;
            public PatchPanel PatchPanel;
            public Rack Rack;
            public string RackLabel;       // "R1/3"
            public string TypeName;
            public string IP;
            public int CustomerID;
            public string CustomerName;
            public string ObjName;         // GameObject name for color
            public bool IsBroken;
            public bool IsEOL;
            public bool IsOn;
            public bool IsServer;
            public bool IsSwitch;
            public bool IsPatchPanel;
            public int EolTime;     // real-time seconds remaining (> 0 = countdown active; <= 0 = EOL reached)
        }

        // ── Rack grid info ──────────────────────────────────────────
        public struct RackInfo
        {
            public Rack Rack;
            public RackMount RackMount;
            public float WorldX;
            public float WorldZ;
            public int RowIndex;
            public int ColIndex;
            public int RowNum;   // 1-based
            public int PosNum;   // position within row, 1-based
            public string Label; // "R1/3"
            public bool IsInstalled;
        }

        // ── Stats ───────────────────────────────────────────────────
        public struct Stats
        {
            public int ServerCount;
            public int SwitchCount;
            public int PatchPanelCount;
            public int CustomerCount;
            public int BrokenCount;
            public int EOLCount;
            public int OfflineCount;
            public int UnassignedCount;
            public int EmptySlots;
        }

        // ── Cached scan results ─────────────────────────────────────
        private static List<DeviceInfo> _lastDevices = new List<DeviceInfo>();
        private static List<RackInfo> _lastRacks = new List<RackInfo>();
        private static List<RackInfo> _lastEmptyMounts = new List<RackInfo>();
        private static Stats _lastStats;
        private static readonly HashSet<int> _allCustomerIds = new HashSet<int>();

        public static List<DeviceInfo> LastDevices => _lastDevices;
        public static List<RackInfo> LastRacks => _lastRacks;
        public static List<RackInfo> LastEmptyMounts => _lastEmptyMounts;
        public static Stats LastStats => _lastStats;

        // ── Rack price (live from ShopItemSO) ──────────────────────
        public static int GetRackPrice()
        {
            try
            {
                var shopItems = Object.FindObjectsOfType<ShopItem>();
                for (int i = 0; i < shopItems.Length; i++)
                {
                    var si = shopItems[i];
                    if (si.shopItemSO != null && si.shopItemSO.itemType == PlayerManager.ObjectInHand.Rack)
                        return si.shopItemSO.price;
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] GetRackPrice failed: {ex.Message}");
            }
            return 1250; // fallback
        }

        // ── Full scan ───────────────────────────────────────────────
        public static void ScanAll()
        {
            _lastDevices.Clear();
            _lastRacks.Clear();
            _lastEmptyMounts.Clear();
            // Build rack grid
            var rackInfos = BuildRackGrid();
            _lastRacks.AddRange(rackInfos);

            // Build rack label lookup: Rack instance ID -> label string
            var rackLabelMap = new Dictionary<int, string>();
            for (int i = 0; i < rackInfos.Count; i++)
            {
                var ri = rackInfos[i];
                if (ri.Rack != null)
                    rackLabelMap[ri.Rack.GetInstanceID()] = ri.Label;
            }

            // Scan empty mounts
            ScanEmptyMounts(rackInfos);

            int serverCount = 0, brokenCount = 0, eolCount = 0, offlineCount = 0, unassignedCount = 0;

            // Scan servers — collect customer IDs from ALL servers first (rack position irrelevant for customer count)
            var allServers = Object.FindObjectsOfType<Server>();
            _allCustomerIds.Clear();
            for (int i = 0; i < allServers.Length; i++)
            {
                int cid = allServers[i].GetCustomerID();
                if (cid > 0) _allCustomerIds.Add(cid);
            }

            // Also scan active CustomerBases — catches customers whose servers return ID 0 (e.g. first customer)
            var allCustBases = Object.FindObjectsOfType<CustomerBase>();
            for (int i = 0; i < allCustBases.Length; i++)
            {
                int cid = allCustBases[i].customerID;
                if (cid < 0) continue;
                // Allow ID 0 only when the base is actively set up (customerItem not null)
                if (cid == 0 && allCustBases[i].customerItem == null) continue;
                _allCustomerIds.Add(cid);
            }

            for (int i = 0; i < allServers.Length; i++)
            {
                var srv = allServers[i];
                if (srv.currentRackPosition == null) continue;
                var rack = srv.currentRackPosition.GetComponentInParent<Rack>();

                string rackLabel = "";
                if (rack != null && rackLabelMap.ContainsKey(rack.GetInstanceID()))
                    rackLabel = rackLabelMap[rack.GetInstanceID()];

                int custId = srv.GetCustomerID();
                string custName = "";
                string serverIP = srv.IP ?? "";

                bool hasRealIP = !string.IsNullOrEmpty(serverIP) && serverIP != "0.0.0.0";

                if (custId >= 0)
                {
                    var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                    if (custItem != null) custName = custItem.customerName ?? "";
                }

                // For display purposes, servers with no real IP show as unassigned
                if (!hasRealIP)
                    custId = -1;

                bool isBroken = srv.isBroken;
                bool isEOL = !isBroken && srv.eolTime <= 0;
                bool isOn = srv.isOn;

                serverCount++;
                if (isBroken) brokenCount++;
                if (isEOL) eolCount++;
                if (!isOn && !isBroken) offlineCount++;
                if (custId < 0 || !hasRealIP) unassignedCount++;

                _lastDevices.Add(new DeviceInfo
                {
                    Server = srv,
                    Rack = rack,
                    RackLabel = rackLabel,
                    TypeName = UIHelper.GetServerTypeName(srv.gameObject.name),
                    IP = serverIP,
                    CustomerID = custId,
                    CustomerName = custName,
                    ObjName = srv.gameObject.name,
                    IsBroken = isBroken,
                    IsEOL = isEOL,
                    IsOn = isOn,
                    IsServer = true,
                    EolTime = srv.eolTime
                });
            }

            // Scan switches
            var allSwitches = Object.FindObjectsOfType<NetworkSwitch>();
            for (int i = 0; i < allSwitches.Length; i++)
            {
                var sw = allSwitches[i];
                if (sw.currentRackPosition == null) continue;
                var rack = sw.currentRackPosition.GetComponentInParent<Rack>();

                string rackLabel = "";
                if (rack != null && rackLabelMap.ContainsKey(rack.GetInstanceID()))
                    rackLabel = rackLabelMap[rack.GetInstanceID()];

                bool isBroken = sw.isBroken;
                bool isEOL = !isBroken && sw.eolTime <= 0;

                if (isBroken) brokenCount++;
                if (isEOL) eolCount++;
                if (!sw.isOn && !isBroken) offlineCount++;

                _lastDevices.Add(new DeviceInfo
                {
                    Switch = sw,
                    Rack = rack,
                    RackLabel = rackLabel,
                    TypeName = MainGameManager.instance.ReturnSwitchNameFromType(sw.switchType),
                    IP = "",
                    CustomerID = -1,
                    CustomerName = "",
                    ObjName = sw.gameObject.name,
                    IsBroken = isBroken,
                    IsEOL = isEOL,
                    IsOn = sw.isOn,
                    IsSwitch = true,
                    EolTime = sw.eolTime
                });
            }

            // Scan patch panels
            var allPPs = Object.FindObjectsOfType<PatchPanel>();
            for (int i = 0; i < allPPs.Length; i++)
            {
                var pp = allPPs[i];
                if (pp.currentRackPosition == null) continue;
                var rack = pp.currentRackPosition.GetComponentInParent<Rack>();

                string rackLabel = "";
                if (rack != null && rackLabelMap.ContainsKey(rack.GetInstanceID()))
                    rackLabel = rackLabelMap[rack.GetInstanceID()];

                _lastDevices.Add(new DeviceInfo
                {
                    PatchPanel = pp,
                    Rack = rack,
                    RackLabel = rackLabel,
                    TypeName = "Patch Panel",
                    IP = "",
                    CustomerID = -1,
                    CustomerName = "",
                    ObjName = pp.gameObject.name,
                    IsBroken = false,
                    IsEOL = false,
                    IsOn = true,
                    IsPatchPanel = true
                });
            }

            // Count all unlocked customers (not just those with servers assigned)
            int unlockedCustomerCount = _allCustomerIds.Count;
            try
            {
                var existingIds = MainGameManager.instance?.existingCustomerIDs;
                if (existingIds != null && existingIds.Count > 0)
                    unlockedCustomerCount = existingIds.Count;
            }
            catch { }

            _lastStats = new Stats
            {
                ServerCount = serverCount,
                SwitchCount = allSwitches.Length,
                PatchPanelCount = allPPs.Length,
                CustomerCount = unlockedCustomerCount,
                BrokenCount = brokenCount,
                EOLCount = eolCount,
                OfflineCount = offlineCount,
                UnassignedCount = unassignedCount,
                EmptySlots = _lastEmptyMounts.Count
            };
        }

        // ── Filter devices ──────────────────────────────────────────
        public enum DeviceTypeFilter { All, Servers, Switches, PatchPanels }
        public enum StatusFilter { All, Online, Offline, Broken, EOL, Unassigned }
        public enum ServerColorFilter { All, Blue, Green, Purple, Yellow }

        public static List<DeviceInfo> Filter(DeviceTypeFilter typeFilter, StatusFilter statusFilter, int customerFilter, ServerColorFilter colorFilter = ServerColorFilter.All)
        {
            var results = new List<DeviceInfo>();
            for (int i = 0; i < _lastDevices.Count; i++)
            {
                var d = _lastDevices[i];

                // Type filter
                if (typeFilter == DeviceTypeFilter.Servers && !d.IsServer) continue;
                if (typeFilter == DeviceTypeFilter.Switches && !d.IsSwitch) continue;
                if (typeFilter == DeviceTypeFilter.PatchPanels && !d.IsPatchPanel) continue;

                // Status filter
                if (statusFilter == StatusFilter.Broken && !d.IsBroken) continue;
                if (statusFilter == StatusFilter.EOL && !d.IsEOL) continue;
                if (statusFilter == StatusFilter.Online && (!d.IsOn || d.IsBroken)) continue;
                if (statusFilter == StatusFilter.Offline && (d.IsOn || d.IsBroken)) continue;
                if (statusFilter == StatusFilter.Unassigned)
                {
                    if (!d.IsServer) continue; // switches/patch panels never have IPs — not meaningful
                    bool hasIP = !string.IsNullOrEmpty(d.IP) && d.IP != "0.0.0.0";
                    if (hasIP) continue;
                }

                // Customer filter
                if (customerFilter >= 0 && d.CustomerID != customerFilter) continue;

                // Color filter — excludes non-servers entirely; filters servers by ObjName color
                if (colorFilter != ServerColorFilter.All)
                {
                    if (!d.IsServer) continue;
                    string colorName = colorFilter.ToString(); // "Blue", "Green", "Purple", "Yellow"
                    if (d.ObjName == null || !d.ObjName.Contains(colorName)) continue;
                }

                results.Add(d);
            }
            return results;
        }

        // ── Rack grid building (shared logic) ───────────────────────
        public static List<RackInfo> BuildRackGrid()
        {
            var allRackObjects = Object.FindObjectsOfType<Rack>();
            var installedRacks = new List<RackInfo>();

            for (int i = 0; i < allRackObjects.Length; i++)
            {
                var rack = allRackObjects[i];
                var rm = rack.GetComponentInParent<RackMount>();
                if (rm == null || !rm.isRackInstantiated) continue;
                var pos = rack.transform.position;
                installedRacks.Add(new RackInfo
                {
                    Rack = rack,
                    RackMount = rm,
                    WorldX = pos.x,
                    WorldZ = pos.z,
                    IsInstalled = true
                });
            }

            if (installedRacks.Count == 0) return installedRacks;

            // Find distinct X and Z values (snapped with 0.3 tolerance)
            var distinctX = new List<float>();
            var distinctZ = new List<float>();

            for (int i = 0; i < installedRacks.Count; i++)
            {
                var rd = installedRacks[i];
                if (!ContainsApprox(distinctX, rd.WorldX, 0.3f))
                    distinctX.Add(rd.WorldX);
                if (!ContainsApprox(distinctZ, rd.WorldZ, 0.3f))
                    distinctZ.Add(rd.WorldZ);
            }

            // Reverse X so map left = highest X in world
            distinctX.Sort();
            distinctX.Reverse();
            distinctZ.Sort();

            // Assign grid indices
            for (int i = 0; i < installedRacks.Count; i++)
            {
                var rd = installedRacks[i];
                rd.ColIndex = FindApproxIndex(distinctX, rd.WorldX, 0.3f);
                rd.RowIndex = FindApproxIndex(distinctZ, rd.WorldZ, 0.3f);
                installedRacks[i] = rd;
            }

            // Sort by row then column
            installedRacks.Sort((a, b) =>
            {
                int cmp = a.RowIndex.CompareTo(b.RowIndex);
                return cmp != 0 ? cmp : a.ColIndex.CompareTo(b.ColIndex);
            });

            // Assign labels
            var rowCounts = new Dictionary<int, int>();
            for (int i = 0; i < installedRacks.Count; i++)
            {
                var rd = installedRacks[i];
                int rowNum = rd.RowIndex + 1;
                if (!rowCounts.ContainsKey(rd.RowIndex))
                    rowCounts[rd.RowIndex] = 0;
                rowCounts[rd.RowIndex]++;
                int posNum = rowCounts[rd.RowIndex];

                rd.RowNum = rowNum;
                rd.PosNum = posNum;
                rd.Label = $"R{rowNum}/{posNum}";
                installedRacks[i] = rd;
            }

            return installedRacks;
        }

        // ── Empty mount scanning ────────────────────────────────────
        private static void ScanEmptyMounts(List<RackInfo> installedRacks)
        {
            var rackMounts = Object.FindObjectsOfType<RackMount>();

            // Collect installed RackMount instance IDs
            var installedMountIds = new HashSet<int>();
            for (int i = 0; i < installedRacks.Count; i++)
            {
                if (installedRacks[i].RackMount != null)
                    installedMountIds.Add(installedRacks[i].RackMount.GetInstanceID());
            }

            for (int i = 0; i < rackMounts.Length; i++)
            {
                var rm = rackMounts[i];
                if (rm.isRackInstantiated) continue;
                if (installedMountIds.Contains(rm.GetInstanceID())) continue;

                var pos = rm.transform.position;
                _lastEmptyMounts.Add(new RackInfo
                {
                    RackMount = rm,
                    WorldX = pos.x,
                    WorldZ = pos.z,
                    IsInstalled = false,
                    Label = "Empty"
                });
            }
        }

        // ── Get customer list with server counts ────────────────────
        public struct CustomerInfo
        {
            public int CustomerID;
            public string Name;
            public Sprite Logo;
            public int ServerCount;
            public float Revenue;
            public float Penalties;
            public float NetRevenue;
        }

        public static List<CustomerInfo> GetCustomerList()
        {
            var orderedIds = new List<int>();
            var customerMap = new Dictionary<int, CustomerInfo>();

            // Seed from existingCustomerIDs — all unlocked customers in purchase order
            try
            {
                var existingIds = MainGameManager.instance?.existingCustomerIDs;
                if (existingIds != null)
                {
                    for (int i = 0; i < existingIds.Count; i++)
                    {
                        int custId = existingIds[i];
                        if (custId < 0 || customerMap.ContainsKey(custId)) continue;
                        var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                        customerMap[custId] = new CustomerInfo
                        {
                            CustomerID = custId,
                            Name = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}",
                            Logo = custItem != null ? custItem.logo : null,
                            ServerCount = 0
                        };
                        orderedIds.Add(custId);
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] existingCustomerIDs read failed: {ex.Message}");
            }

            // Also include any customers from _allCustomerIds not already captured
            // (_allCustomerIds includes IDs from servers + CustomerBase objects, populated in ScanAll)
            foreach (int custId in _allCustomerIds)
            {
                if (custId < 0 || customerMap.ContainsKey(custId)) continue;
                var custItem = MainGameManager.instance.GetCustomerItemByID(custId);
                customerMap[custId] = new CustomerInfo
                {
                    CustomerID = custId,
                    Name = custItem != null ? (custItem.customerName ?? $"Customer {custId}") : $"Customer {custId}",
                    Logo = custItem != null ? custItem.logo : null,
                    ServerCount = 0
                };
                orderedIds.Add(custId);
            }

            // Fill server counts
            for (int i = 0; i < _lastDevices.Count; i++)
            {
                var d = _lastDevices[i];
                if (!d.IsServer) continue;

                int realCustId = d.CustomerID; // uses -1 override for servers without real IPs
                if (realCustId < 0 || !customerMap.ContainsKey(realCustId)) continue;

                var ci = customerMap[realCustId];
                ci.ServerCount++;
                customerMap[realCustId] = ci;
            }

            // Add revenue data from BalanceSheet
            try
            {
                var bs = BalanceSheet.instance;
                if (bs != null && bs.currentRecords != null)
                {
                    var entries = bs.currentRecords._entries;
                    for (int ei = 0; ei < entries.Length; ei++)
                    {
                        if (entries[ei].hashCode >= 0)
                        {
                            int custId = entries[ei].key;
                            var record = entries[ei].value;
                            if (customerMap.ContainsKey(custId))
                            {
                                var ci = customerMap[custId];
                                ci.Revenue = record.revenue;
                                ci.Penalties = record.penalties;
                                ci.NetRevenue = record.Total;
                                customerMap[custId] = ci;
                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                MelonLogger.Warning($"[DCIM] BalanceSheet read failed: {ex.Message}");
            }

            // Return in unlock/purchase order
            var result = new List<CustomerInfo>();
            for (int i = 0; i < orderedIds.Count; i++)
                result.Add(customerMap[orderedIds[i]]);
            return result;
        }

        // ── Rack tooltip data ───────────────────────────────────────
        public struct RackTooltipData
        {
            public string Label;
            public int ServerCount;
            public int SwitchCount;
            public int BrokenCount;
            public int EOLCount;
            public List<string> CustomerNames;
        }

        public static RackTooltipData GetRackTooltip(Rack rack)
        {
            var data = new RackTooltipData
            {
                CustomerNames = new List<string>()
            };
            if (rack == null) return data;

            int rackId = rack.GetInstanceID();
            var customerSet = new HashSet<string>();

            for (int i = 0; i < _lastDevices.Count; i++)
            {
                var d = _lastDevices[i];
                if (d.Rack == null || d.Rack.GetInstanceID() != rackId) continue;

                if (d.IsServer) data.ServerCount++;
                if (d.IsSwitch) data.SwitchCount++;
                if (d.IsBroken) data.BrokenCount++;
                if (d.IsEOL) data.EOLCount++;
                if (!string.IsNullOrEmpty(d.CustomerName) && !customerSet.Contains(d.CustomerName))
                {
                    customerSet.Add(d.CustomerName);
                    data.CustomerNames.Add(d.CustomerName);
                }
            }

            // Find label
            for (int i = 0; i < _lastRacks.Count; i++)
            {
                if (_lastRacks[i].Rack != null && _lastRacks[i].Rack.GetInstanceID() == rackId)
                {
                    data.Label = _lastRacks[i].Label;
                    break;
                }
            }

            return data;
        }

        // ── Utility functions ───────────────────────────────────────
        public static bool ContainsApprox(List<float> list, float value, float tolerance)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Mathf.Abs(list[i] - value) <= tolerance)
                    return true;
            }
            return false;
        }

        public static int FindApproxIndex(List<float> sortedList, float value, float tolerance)
        {
            for (int i = 0; i < sortedList.Count; i++)
            {
                if (Mathf.Abs(sortedList[i] - value) <= tolerance)
                    return i;
            }
            return 0;
        }
    }
}
