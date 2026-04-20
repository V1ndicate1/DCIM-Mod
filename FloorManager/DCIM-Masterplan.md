# DCIM — Master Plan

(formerly "Floor Manager", renamed to DCIM — Data Center Infrastructure Management)

**Version:** v1.0.2 — **LIVE ON NEXUS (2026-04-19)**
**Deploy path:** `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll`
**Nexus zip:** `C:\Users\Jacob\Desktop\DCIM-1.0.2.zip`
**Fix zip:** `C:\Users\Jacob\Desktop\DCIM_MelonLoader_Fix_1.0.2.zip` — uploaded as optional file, pending false-positive appeal
**Next work:** See open TODO items — Hacking System is ON HOLD, stripped from all builds until explicitly resumed

---

## Test History

### v1.0.1 — Compatibility rebuild (2026-04-17)

**Nexus release.** No functionality changes.

- Rebuilt against game update 1.0.47.2 (Unity 6000.4.2f1) to address potential load issues.
- Version bump only: `MelonInfo` and startup log updated to `"1.0.1"`.
- `FixCoreModule` tool confirmed CoreModule already clean (0 duplicates) on dev machine prior to rebuild.

**Known issue documented:**
- **Buy Rack (In-Rack Purchase)** — the Buy button inside the rack diagram panel is not functioning as expected. Under investigation. Attempting to use it will not cause problems, but it is currently unclear whether funds are deducted.

---

### Fresh Installer Fix — MelonLoader assembly workaround (2026-04-17)

**Problem:** MelonLoader 0.7.2 Open-Beta has a bug (issue #1142) where the Il2CppInterop unstripper writes a duplicate `<>O` TypeDef into the regenerated `UnityEngine.CoreModule.dll`. On a fresh install, ML generates assemblies from scratch, hits the bug, and crashes before loading any mod. Affects ALL mods for Data Center, not just DCIM. Dev machine was unaffected because its `Il2CppAssemblies/` were already cached from a pre-bug generation.

**Root cause confirmed:** Third-party log showed five unrelated mods all failing with the identical stack trace. DCIM.dll is clean — does not contain `<>O` and does not ship CoreModule.

**Fix:** Distribute pre-generated assemblies + the generator hash record so ML skips generation entirely.

Two files are needed:
- `MelonLoader\Il2CppAssemblies\` — 158 pre-generated DLLs (valid for game version 1.0.47.2)
- `MelonLoader\Dependencies\Il2CppAssemblyGenerator\Config.cfg` — contains `GameAssemblyHash` so ML sees "Assembly is up to date. No Generation Needed." and skips regeneration

**Tested:** Deleted both from dev machine, followed README, launched — confirmed "Assembly is up to date" in log, DCIM loaded clean.

**Confirmed on real fresh install (2026-04-18):** Third user with a brand new machine hit the identical `Duplicate type '<>O'` error + `No Support Module Loaded`. Applied fix zip, issue resolved. Fix confirmed working end-to-end on a genuine fresh install.

**Artifact:** `C:\Users\Jacob\Desktop\DCIM_ML_Fix_1.0.2.zip` (21 MB) — uploaded as optional file on Nexus page.

**Lifecycle:** Fix is version-locked to game 1.0.47.2. If the game updates, re-zip fresh assemblies + updated Config.cfg and replace the Nexus file. When MelonLoader fixes issue #1142, remove the notice from the Nexus page — the zip becomes irrelevant but causes no harm.

**Replies sent:** beex82 and Darbunderscore pointed to the optional fix file on Nexus. Third reporter resolved on 2026-04-18.

---

### v0.0.2 (2026-04-13) — First in-game test

Logs: `Logs\FloorManager\0.0.2.txt`

**Working:** Mod loads, app button injected, floor map renders, legend visible, discovery (84/512 installed, 0/92 walls opened).

**Bugs found:**
- Back button not working
- Back button text clipped
- Background not opaque (game HUD bled through)
- All racks showing gray (no status colors)
- Rack rows flipped
- App button didn't match game style

### v0.0.3–v0.0.4 — UI fixes

**Fixed:**
- Back button: replaced `ButtonReturnMainScreen()` with direct `mainScreen.SetActive(true)` + manual anchoring, wider "< Back" button
- Background: alpha 1.0, dark viewport background (wireframe look adopted)
- Rack rows: removed Z-reversal
- App button: gray background with grid icon, matches game style
- Legend: moved to top-right to avoid overlapping rack squares
- Cyan button highlight: custom ColorBlock + Navigation.Mode.None on rack buttons

### v0.0.5 — Status color investigation

**Key discovery:** Devices live under `Objects > UsableObjects > Server.XXX(Clone)` — NOT as children of Rack hierarchy. `GetComponentsInChildren<Server>()` on Rack/RackMount returns 0. `rack.positions` has 47 items (47U rack) but positions have no device children.

### v0.0.6 — Status colors working

Logs: `Logs\FloorManager\0.0.6.txt`

**Fixed:** Rewrote status detection to scan all `Server`/`NetworkSwitch` via `FindObjectsOfType`, trace each to its Rack via `device.currentRackPosition` → `GetComponentInParent<Rack>()`. Status map: 60/84 racks with devices (328 servers, 67 switches). Green/yellow/red borders now display correctly.

**Also fixed:** Rack numbering changed from global counter to `row/position` format (e.g. `1/1`, `1/2`).

**Still broken:** Device list view — clicking a rack showed empty because DeviceListView still used `rack.positions[i].GetComponentInChildren<Server>()`.

### v0.0.7 — Device list rewrite

**Changed:** DeviceListView rewritten to use same scene-scan approach. Builds `HashSet` of `rack.positions[]` instance IDs, matches devices by checking if `device.currentRackPosition` ID is in the set.

**Also added:** Clock overlay diagnostics (coroutine logging).

### v0.0.7 — Device list + config tested (2026-04-13)

Logs: `Logs\FloorManager\0.0.7.txt`
Screenshots: `Screenshot 2026-04-13 0131*.png`, `0132*.png`

**Newly confirmed working:**
- Device list view — devices found via position ID matching (7 servers + 1 switch matched for rack 1/1)
- Device config panel — switch config shows connected ports, server config shows power/IP/customer/EOL/processing
- Switch connected ports list (GetConnectedDevices works)
- Navigation: floor map → device list → device config → back → back → back to laptop

### v0.0.8 — Bug fixes + LACP rework (2026-04-13)

Logs: `Logs\FloorManager\0.0.8.txt`

**Fixed all 7 v0.0.7 bugs** (see list above) plus 5 additional issues found during testing:
- LACP button in header, renders on top of laptop via ScreenSpaceOverlay trick
- Device list matching unified to hierarchy-walk (fixes empty device lists on some racks)
- Button minWidth prevents text wrapping in layout groups
- App screen fits laptop frame (transparent root, backgrounds on header+content only)
- ESC key not handled (conflicts with game's own handler — use Back button)

**Known from testing:** LACP canvas detected as `Canvas_SwitchSeting - OFF`. 60/84 racks have devices (328 servers, 67 switches). Some racks showed 0 matched devices with old position-ID matching — fixed with hierarchy walk.

### v0.0.9 — Deep scan fixes + customer features (2026-04-13)

**Deep scan bug fixes (3 IL2CPP/logic crashes found and fixed):**

1. **IL2CPP foreach crash** — `foreach` on `FindObjectsOfType<RackMount>()` (returns `Il2CppArrayBase`) replaced with indexed `for` loops throughout `FloorMapView.cs`. IL2CPP arrays do not support `GetEnumerator()`.

2. **IL2CPP Dictionary enumerator crash** — `AutoFillIP()` in `DeviceConfigPanel.cs` used `subnetsPerApp.GetEnumerator()` / `MoveNext()` on an IL2CPP `Dictionary<int, string>`. Replaced with direct `_entries` array access with `hashCode >= 0` validity check.

3. **Row/column parameter swap** — `FloorMapView` passed `(capturedCol, capturedRow)` to `OpenRack()` but header displayed them as `R{col}/{row}`. Fixed: now passes 1-based `(rowNum, posNum)` matching the display labels.

**Rack layout fixes:**

4. **Rack mirroring fix** — Racks were horizontally flipped (1/1 on app = physical 1/24). Root cause: distinctX was sorted ascending but physical layout reads right-to-left. Fixed by reversing the X sort order (descending).

5. **Aisle gaps disappeared after X-sort reversal** — `distinctX[i] - distinctX[i-1]` produced negative values with descending sort. Fixed with `Mathf.Abs()`.

6. **Rack discovery changed** — Switched from `FindObjectsOfType<RackMount>()` → `GetComponentInChildren<Rack>()` to `FindObjectsOfType<Rack>()` directly with `GetComponentInParent<RackMount>()` validation. More reliable 1:1 rack matching.

**Server color fix:**

7. **Purple servers showing as yellow** — `ReturnServerNameFromType(2)` returns product name "RISC 3U 5000 IOPS" which triggered the RISC→yellow keyword fallback. Fixed: `GetDeviceTypeColor()` now uses `server.gameObject.name` (e.g. "Server.Purple2(Clone)") which contains the true color keyword. Removed serverType tier fallback entirely.

**Customer features (new):**

8. **Customer logo + name in device config** — Replaced `"Customer 6"` text with customer logo sprite + `customerName` from `MainGameManager.instance.GetCustomerItemByID()`. Logo hidden when null. Updates dynamically when cycling customers with `<`/`>` buttons.

9. **Customer squares on floor map** — Sequential row of 40×40 squares at top of floor map, one per customer. Each shows the customer's logo sprite. Clickable → opens Customer IP View.

10. **Customer IP View (new file: CustomerIPView.cs)** — New `ViewState.CustomerIPs` added to navigation. Shows customer logo + name header with active server count, then a scrollable table of all servers belonging to that customer with columns: Server type (color-coded by object name), IP Address, Status (ON/OFF/BRK/EOL). Servers sorted by IP. Back returns to floor map.

**All confirmed working in-game.**

### v0.1.0 — Customer dropdown, batch assign, clock fix (2026-04-13)

**Clock overlay fix:**
- Clock was invisible because TMP font wasn't loaded during early init. Coroutine now waits for both `TimeController.instance` AND a TMP font from scene before creating UI. Font explicitly assigned to TextMeshProUGUI.
- **PERF FIX:** Initial font search used `FindObjectOfType<TextMeshProUGUI>()` every frame (`yield return null`). With 227k+ loaded objects, this caused heavy per-frame overhead for the entire session because the font was never found. Fixed: polls once per second (`WaitForSeconds(1f)`), gives up after 30 attempts and creates clock anyway.
- **Clock font still not found** — `FindObjectOfType<TextMeshProUGUI>()` never returned a result even after scene fully loaded. Likely an IL2CPP type resolution issue. Next fix: grab font from ComputerShop UI in the `ComputerShopAwakePatch` (where TMP text is confirmed working) and pass it to ClockOverlay.

**Customer dropdown in server config:**
- Replaced `< >` cycling buttons with inline scrollable list of all customers (logo + name + selection indicator)
- Clicking a customer calls `server.UpdateCustomer(id)` directly (no cycling)
- Current customer highlighted green, auto-fills IP on change
- Filters out invalid customers (customerID < 0)

**Batch customer assignment (new — device list):**
- Server rows in device list now have checkboxes for multi-select
- "Select All" / "Deselect All" toggle button at top of list
- "Assign Customer" button in header bar (right side), shows selected count
- Clicking "Assign Customer" opens popup overlay with scrollable customer list (logos + names)
- Click a customer to highlight, click "Select" to confirm
- Batch assigns customer + auto-fills IP for all selected servers
- After confirm, device list refreshes to show updated IPs

**Crash investigation (16:41):**
- Game crashed ~1 hour after last FloorManager activity (batch assign at 15:43, crash at 16:41)
- No managed exceptions in MelonLoader or Player logs — native C++ crash (dump: `Data Center.exe.27848.dmp`)
- Prior crash dumps exist from Apr 10 and Apr 12 (before v0.1.0) — game has pre-existing native crash issues
- Only AutoDispatcher was active at crash time (tech dispatch/reset loop)
- **Not caused by FloorManager**, but the per-frame FindObjectOfType polling may have contributed to memory pressure

### v0.1.1 — Bug fixes + UI improvements (2026-04-13)

**Bugs fixed:**

1. **Batch IP auto-fill duplicate IPs** — `AutoFillIP()` now takes a `HashSet<string>` of already-assigned IPs and skips them. Each server in a batch gets a unique IP.

2. **IP auto-fill assigned .1 (gateway)** — Both single-server and batch AutoFillIP now skip .0 (network) and .1 (gateway) addresses. First assigned IP is .2 or higher.

3. **SetIP.ShowCanvas() broken from laptop** — Removed non-functional `TMP_InputField` (doesn't work in IL2CPP). Replaced with:
   - "Auto IP" button — picks next available IP from customer subnet (skips .0/.1)
   - "Clear" button — removes IP assignment
   - Manual IP editor row — shows subnet prefix + last octet picker with -/+ buttons (cycles 2-254) + Set button

4. **Cyan/blue selection box artifact** — Unity EventSystem was drawing selection highlights on buttons with default `Automatic` navigation mode. Fixed by adding `Navigation.Mode.None` to `UIHelper.BuildButton()` (all mod buttons) and `BuildAppButton()` (laptop app button). Also added `EventSystem.current.SetSelectedGameObject(null)` on all view transitions.

5. **FM screen rendered as tiny box** — `FloorManagerScreen` RectTransform was copied from `mainScreen` during `Awake` when values were still default/zero. Fixed with `CopyRTNextFrame` coroutine that waits 2 frames before copying RT values.

6. **Clock overlay removed** — Scrapped entirely. `ClockOverlay.cs` deleted, clock prefs and coroutine removed from `FloorManagerMod.cs`. TMP font never loaded correctly in IL2CPP context.

**UI improvements:**

7. **EOL Timer format** — Changed from `MMM:SS` to `H.M.S` format matching in-game display style.

8. **Processing display** — Was showing `server.currentProcessingSpeed / server.maxProcessingSpeed` which returned 0.1/0.1 for a 5000 IOPS server (wrong fields). Now parses rated IOPS from server type name (e.g. "System X 3U **5000** IOPS" → "5000 IOPS").

9. **Rack click highlight** — Clicking a rack on the floor map now shows a cyan border highlight on the selected rack before navigating to device list. Highlight hides on refresh.

**Confirmed working (user-tested):**
- Power ON and OFF both work remotely
- Back button navigation fixed (no blue box artifact)
- Blue box killed globally via OnLateUpdate EventSystem deselect (also applied to AutoDispatcher)
- Auto IP assigns correct addresses (.2+)
- Customer dropdown + batch assignment still working

**New bug found:**
- **[PRIORITY] LACP config exit breaks laptop** — closing LACP config boots player out of laptop but FM screen stays floating. Must spam E to re-find laptop.

**7 bugs found in v0.0.7 — all fixed in v0.0.8:**

1. ~~**Clock not showing**~~ **FIXED** — Coroutine now waits for `TimeController.instance` to exist before creating UI. Was starting before scene load.

2. ~~**Cyan square still appears in device list**~~ **FIXED** — Added `Navigation.Mode.None` to device row buttons to prevent Unity focus highlight.

3. ~~**Empty slot spam**~~ **FIXED** — Removed 47-slot loop. Only occupied positions are rendered. Empty rack shows "No devices installed".

4. ~~**Server config buttons broken**~~ **FIXED** — Widened ON/OFF (80px), Change IP (100px), customer `<`/`>` (40px). Added `minWidth` to all buttons via LayoutElement so layout groups cannot squish them.

5. ~~**Device type colors missing for non-keyword names**~~ **FIXED** — Added "RISC" as yellow keyword. Added fallback: `serverType` index mapped to color tiers (0-3=Blue, 4-7=Green, 8-11=Purple, 12+=Yellow).

6. ~~**Legend overlapping device list rows**~~ **FIXED** — Legend now hidden when not on floor map view.

7. ~~**Device list header uses old format**~~ **FIXED** — Changed to "R{row}/{pos}" format.

**Additional fixes in v0.0.8:**

8. **LACP button moved to header** — "Configure LACP" now appears as a header action button (right side) on switch config views, not in scroll content.

9. **LACP renders on top of laptop** — LACP canvas (`Canvas_SwitchSeting - OFF`) temporarily switched to ScreenSpaceOverlay with sortingOrder=999 while open. Original settings restored on close. Coroutine polls for close and returns to FM screen.

10. **Device list matching unified** — Device list now uses same `GetComponentInParent<Rack>()` hierarchy walk as floor map status detection. Previous approach (matching position IDs from `rack.positions[]` array) produced mismatches — some racks showed 0 devices in the list despite being populated on the map.

11. **App screen fits laptop** — FM screen copies `mainScreen`'s RectTransform (10px inset). Root has no background Image — only header and content areas have backgrounds, so the laptop frame is visible through the inset edges.

12. **ESC key** — Not handled by the mod. ESC follows the game's native behavior (closes laptop). Use the "< Back" button for in-app navigation. Attempted ESC interception conflicted with the game's own handler.

### v0.2.0 — UI Overhaul (2026-04-13)

**Major feature addition — all code written, needs in-game testing.**

**New navigation structure (hub-and-spoke from Dashboard):**
```
Dashboard (NEW home screen)
  ├── FloorMap (spatial view — existing, now secondary)
  │     └── DeviceList → DeviceConfig
  ├── SearchResults (NEW — filtered device access)
  │     └── DeviceConfig (back returns to SearchResults)
  └── CustomerIPs (from Dashboard customer list)
```

**New files added:**
- `SearchEngine.cs` — centralized device scanning, filter logic (type/status/customer), rack grid numbering (shared utility), empty mount discovery, rack price lookup, customer revenue from BalanceSheet, rack tooltip data
- `DashboardView.cs` — home screen: summary stat chips (servers/switches/PPs/empty), alert row (broken/EOL counts with red/yellow), quick filter chips (offline/all devices/floor map), scrollable customer list with logos + server count + revenue
- `SearchResultsView.cs` — filter toolbar with dropdown popups (Type/Status/Customer), result list with status dots + type color + IP + customer + rack label, multi-select checkboxes on servers, bulk Power ON/OFF buttons
- `RackLabelManager.cs` — world-space canvas labels ("R1/3") on each rack in the 3D world, created on save load via `SaveSystem.onLoadingDataLater`, refreshed on new rack placement
- `Patches/RackInstantiatePatch.cs` — Harmony postfix on `RackMount.InstantiateRack()` triggers `RackLabelManager.RefreshAllLabels()` for consistent numbering

**Modifications to existing files:**

1. **FloorMapApp.cs** — `ViewState` enum expanded with `Dashboard` + `SearchResults`. Added `_previousState` field so DeviceConfig back returns to origin view (DeviceList or SearchResults). App now opens to Dashboard instead of FloorMap. Buy rack popup (price lookup from ShopItemSO, balance check, InstantiateRack call, money deduction). New navigation methods: `OpenDeviceFromSearch()`, `OpenSearchResults()`, `ShowBuyRackPopup()`.

2. **FloorMapView.cs** — Empty rack mount positions rendered as dashed-outline squares with "+" icon (clickable → buy rack popup). Rack tooltip overlay (nested Canvas with overrideSorting, shows device counts/broken/EOL/customer names, "Details >" button drills into DeviceList, "Close" dismisses). Rack click now shows tooltip first instead of immediately navigating. Uses `SearchEngine.BuildRackGrid()` for shared grid logic.

3. **DeviceListView.cs** — Power ON and Power OFF buttons added to toolbar (next to Select All). Iterate selected servers, call `PowerButton()` for each, HUD message confirms count.

4. **DeviceConfigPanel.cs** — Rack label row ("Rack: R1/3") added to server and switch config views. Switch port count parsed from type name (e.g. "8 Port Switch" → "3 / 8 used"), display capped at actual port count.

5. **CustomerIPView.cs** — Revenue/penalty/net summary row at top (from `BalanceSheet.currentRecords`). IP rows now clickable → navigate to DeviceConfig via `OpenDeviceFromSearch()`.

6. **UIHelper.cs** — `BuildFilterChip()` (colored pill button with dot + label), `BuildStatusDot()` (small color circle), `BuildScrollView()` (reusable ScrollRect+Viewport+Content+VLG+CSF builder).

7. **FloorManagerMod.cs** — v0.2.0, calls `RackLabelManager.RefreshAllLabels()` in `OnSaveLoaded()`.

**Critical implementation rules followed:**
- NO foreach on IL2CPP collections — indexed for-loops everywhere
- NO TMP_InputField — all filters are click-based dropdown popups
- `_entries` pattern for BalanceSheet.currentRecords dictionary
- Navigation.Mode.None on all new buttons
- Font grabbed from existing TextMeshProUGUI before creating 3D labels
- Nested Canvas with overrideSorting for tooltip above ScrollRect mask

**Post-build fixes:**
- **Renamed to DCIM** — all user-facing strings (mod name, app button, header, log prefixes, GO names) changed from "Floor Manager" to "DCIM"
- **Unprovisioned server fix** — servers with no IP or `0.0.0.0` were showing the game's default customer assignment. Now treated as unassigned (custId forced to -1) so customer column shows blank for unprovisioned servers.

**All v0.2.0 features confirmed working (2026-04-14).**

### v0.9.2 — Warn removed, Customer IP Add Server, multi-select (2026-04-15)

**Warn functionality removed:**
- `IsWarnEnabled`, `SetWarnEnabled`, `_prefWarnEnabled`, `_prefCat`, `_pollTimer`, `_warnServers`, `_warnSwitches` all removed from `FloorManagerMod.cs`
- Entire `OnUpdate()` removed (no remaining per-frame logic in mod)
- Warn button build block, `_warnBtn`, `_warnLabel`, `RefreshWarnButton()` removed from `FloorMapApp.cs`
- Header action button repositioned flush to right edge (`anchoredPosition.x = -6f`), title `offsetMax.x` adjusted from -240 to -142
- **Reason:** Base game's Command Center auto-repair covers warning suppression; feature was redundant

**Customer IP View — Add Server:**
- "Add Server" header action button added to CustomerIPs state in `FloorMapApp.cs`
- `CustomerIPView.ShowAddServerPopup(int customerID)` — popup listing all servers with no IP assigned (IP = `null` / `"0.0.0.0"` is the reliable "unused" sentinel since game default customerID is 0, not -1)
- Multi-select toggle rows (green highlight = selected, click again to deselect)
- Confirm button shows "Add (N)" with count, disabled until ≥1 selected
- On confirm: `UpdateCustomer()` + `AutoFillIP()` for all selected, refreshes CustomerIPs view
- Popup parented to canvas (fmScreen's parent) so it renders above header
- `DeviceListView.AutoFillIP` changed to `internal static` so CustomerIPView can reuse it

---

### v1.0.1 — Screen fit fix + floor map vertical-scroll rework (2026-04-16)

**Screen fit bug (FIXED — confirmed in-game 2026-04-16):**
- Root cause (final, confirmed): `DCIMScreen` was parented to `canvas.transform` (the canvas root, which is full-screen). `mainScreen` and all other native app screens live inside a nested container one level below the canvas root that defines the actual laptop panel bounds. Parenting to the canvas root bypassed this container entirely.
- Fix: `fmScreen.transform.SetParent(mainScreen.transform.parent, false)` — parent to the same container as mainScreen, not the canvas root. `CopyRTNextFrame` then copies mainScreen's RT values (anchor-fill + 10px inset) after one frame. `OnAppOpened` also re-syncs on every open.
- Canvas hierarchy confirmed via runtime logging: canvas renderMode=WorldSpace, rect=1200×675. mainScreen parent has no Canvas component — it is the screens container between canvas root and the individual screens.

**Floor map rework — vertical-only scroll (confirmed in-game 2026-04-16):**
- `scrollRect.horizontal = false` — horizontal scroll disabled entirely.
- Content `anchorMax` changed from `(0,1)` to `(1,1)` and `sizeDelta.x = 0` — content now stretches full viewport width automatically.
- Dynamic rack sizing: `rackSize = Clamp((vpW - ROW_LABEL_W - PADDING - gapTotal) / numCols, 14f, 40f)` — all columns always fit in viewport regardless of data center size.
- Position labels, count badges, plus text, highlight square all scale with `rackSize`.
- Aisle vs. rack gap detection preserved (`AISLE_THRESHOLD = 1.5f`).

---

### v1.1.0 — Floor Map Overhaul (2026-04-17)

**Major overhaul of FloorMapView + new RackDiagramPanel + multi-rack device list.**

**Files changed:**
- `FloorMapView.cs` — full rewrite
- `RackDiagramPanel.cs` — new file (~750 lines)
- `FloorMapApp.cs` — `OpenMultiRackDevices()`, multi-rack header, updated color legend
- `DeviceListView.cs` — `PopulateMultiRack()`
- `FloorManagerMod.cs` — EOL threshold constants added

---

**5-level EOL status system (FloorMapView + RackDiagramPanel):**

- Status levels: `0=empty(gray)`, `1=healthy(green)`, `2=approaching 4h(amber)`, `3=warning 2h/expired(yellow)`, `4=broken(red)`
- Thresholds added to `FloorManagerMod`:
  ```csharp
  public const int EOL_WARN_SECONDS     = 7200;   // within this → yellow warning
  public const int EOL_APPROACH_SECONDS = 14400;  // within this → amber approaching
  ```
- Matches the game's built-in Command Center auto-repair dropdown levels (Off / Repair Broken / Repair EOL 4h+ / Repair EOL 2h+)
- `BuildAllRackData()` now classifies each rack to 5 levels across both server and switch device scans
- EOL filter chip (value 2) matches both level 2 (approaching) and level 3 (warning) via special-case in `ApplyFilters()`
- `StatusLevelToColor()` updated with amber `new Color(0.95f, 0.72f, 0.08f, 1f)` for level 2
- Stats strip EOL count: `level == 2 || level == 3` both increment `eolRacks`
- Color legend updated: added `"~EOL <4h"` amber entry, "EOL" relabeled `"EOL <2h"`, legend height 80f → 96f

---

**Stats strip (FloorMapView):**

- Fixed `TextMeshProUGUI` label anchored at top: `"Racks: N  Devices: N  Broken: N  EOL: N"`
- Broken count in red via TMP rich text; counts from `BuildAllRackData()` at end of `Refresh()`

---

**Rack utilization fill bars (FloorMapView):**

- `RackEntry` gains `TotalOccupiedU` field — accumulated from `server.sizeInU` / `sw.sizeInU` during `BuildAllRackData()`
- Each rack square gets a 4px bottom fill bar: `fillRatio = TotalOccupiedU / rack.positions.Length`, color `(0.65, 0.65, 0.65, 0.9)`
- `CanvasGroup` added to each rack square root for the filter dim system

---

**Filter bar (FloorMapView):**

- 4 toggle chips: Broken (red), EOL (yellow), Healthy (green), Empty (gray)
- `_activeStatusFilters` HashSet; toggle adds/removes, calls `ApplyFilters()`
- `ApplyFilters()` dims non-matching racks to `alpha = 0.2f` via `CanvasGroup`; no `Refresh()` needed
- Dictionary iteration uses `_rackIdOrder List<int>` for IL2CPP safety
- Customer filter: logo click now toggles `_customerFilter` (filters rack visibility) instead of navigating to `CustomerIPView`. Navigation to CustomerIPView removed from logo click handler.

---

**Multi-select mode (FloorMapView):**

- Toggle button in fixed header (top-right, outside scroll): label `"Select"` / `"Selecting (N)"`
- Rack click in select mode adds/removes orange overlay: fill `(1, 0.55, 0, 0.35)` + border `(1, 0.65, 0, 1f)` 2px
- Selection overlays are **siblings** of rack squares in `_contentRT` (not children), so `CanvasGroup` dimming does not affect them
- Does not open `RackDiagramPanel` when `_selectModeActive`

---

**Action bar (FloorMapView):**

Fixed to bottom, visible when `_selectModeActive && _selectedRackIds.Count > 0`:
- **View Devices** → `FloorMapApp.OpenMultiRackDevices(selectedRackList)` → `DeviceListView.PopulateMultiRack(racks)`
- **Assign Customer** → `_customerAssignPanel` modal overlay; assigns `UpdateCustomer()` to unassigned servers in selected racks
- **Clear** → clears selection, hides action bar, updates button label

---

**Rack Diagram Panel (`RackDiagramPanel.cs` — new file):**

- Canvas with `overrideSorting=true, sortingOrder=100`; dim overlay click-to-close
- Panel: `anchorMin=(0.5f,0.075f), anchorMax=(0.5f,0.925f), sizeDelta=(380f,0f)` — 380px wide, 85% screen height
- Header: rack label, device count, broken/EOL badges, ✕ button
- U-slot layout:
  - `Dictionary<int, Server> serverMap` + `Dictionary<int, NetworkSwitch> switchMap` keyed by `positionIndex`
  - While-loop 0..totalSlots; occupied rows span `sizeInU * SLOT_H` (SLOT_H = 18f); multi-U continuation slots skipped
  - Slot label "U{n}", device type badge "SRV"/"SW", customer color dot, status badge
  - Status color: broken=red, `eolTime <= EOL_WARN_SECONDS`=yellow, `<= EOL_APPROACH_SECONDS`=amber, isOn=green, else=gray
  - Status badge: `"BROKEN"` / `"EOL"` / `"~EOL"` / `"ON"` / `"OFF"`
  - Click occupied slot → `FloorMapApp.OpenDevice()` (navigates to DeviceConfigPanel, closes diagram)
- Empty slots: "— empty —" label + "Buy" button → opens MiniShopPanel

---

**Mini Shop Panel (inside RackDiagramPanel):**

- Nested Canvas `sortingOrder=10`, overlay inside diagram panel
- Item filter: `Server1U, Server2U, Server3U, Switch` only, `isUnlocked == true`
- 2-column scrollable grid: icon, name, price label
- Non-colorable items: single "Buy $X" button → `cs.ButtonBuyShopItem(id, price, type, name, false)`
- Colorable items (`isCustomColor == true`): row of 8 color swatches + "Buy with Color" + "★ Save" button
  - Per-item swatch state via **closure-captured arrays** (`int[]`, `Color[]`, `Image[]`) — no shared static fields
  - Active swatch gets white border ring; "Buy with Color" pre-sets `cs.flexibleColorPicker.SetColor(selColor[0])` then calls `ButtonBuyShopItem(…, true)`
  - "★ Save" writes current picker color (`cs.flexibleColorPicker.color`) as hex to `MelonPreferences`, calls `MelonPreferences.Save()`

---

**Favorite colors (MelonPreferences):**

- Category `"DCIM_Colors"`, entries `"fav_0"` – `"fav_7"` (hex strings)
- Defaults: `#4A90D9, #2ECC71, #E74C3C, #F39C12, #9B59B6, #1ABC9C, #F0F0F0, #808080`
- Persist across sessions and save files

---

**`FloorMapApp.cs` additions:**

- `OpenMultiRackDevices(List<Rack> racks)` — sets `CurrentRack=null, CurrentRackColumn=-1`, calls `SwitchToState(ViewState.DeviceList)` then `DeviceListView.PopulateMultiRack(racks)`
- `SwitchToState` DeviceList case: shows `"Multiple Racks"` header when `CurrentRackColumn < 0`, only calls `DeviceListView.Populate()` when `CurrentRack != null`

---

**`DeviceListView.cs` additions:**

- `PopulateMultiRack(List<Rack> racks)` — scans `FindObjectsOfType<Server/NetworkSwitch/PatchPanel>`, filters by `HashSet<int> rackIds`, builds aggregate device list
- Header row: `"{racks.Count} racks selected  —  {devices.Count} devices"`
- Reuses `BuildToolbar()` and `BuildDeviceRow()` unchanged; starts live refresh coroutine if any live rows

---

**Behavioral changes vs. prior versions:**

- **Rack click:** Previously (v0.2.0–v1.0.1) clicking a rack square showed a tooltip overlay first; clicking "Details >" in the tooltip navigated to DeviceList. Now (v1.1.0) clicking a rack directly opens `RackDiagramPanel`. The tooltip is gone.
- **Customer logo click:** Previously navigated to CustomerIPView. Now toggles `_customerFilter` (dims non-matching racks). Navigation to CustomerIPView is still accessible from within the rack diagram or device list.

---

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.1 — Rack custom color picker + persistence + crash fix (2026-04-17)

**Files changed:** `FloorMapApp.cs`, `RackDiagramPanel.cs`, `FloorManagerMod.cs`

---

**Rack color picker in Buy Rack popup (`FloorMapApp.cs`):**

- 8 favorite swatch squares at top — click to select and sync sliders to that color
- RGB sliders (R/G/B channels, 0–255, `UnityEngine.UI.Slider` components) — drag for any color, live preview
  - Built via `BuildChannelSlider()` helper: fill rect + handle rect + `Slider` component + tinted channel label
  - `onValueChanged.AddListener(new System.Action<float>(...))` — updates preview image + value labels + clears swatch border
- Color preview square shows currently selected color live
- **★ Save slots 1–8** — overwrites that favorite slot with current slider color via `RackDiagramPanel.SetFavoriteColor()`, saves to MelonPreferences immediately, updates swatch visuals, shows HUD message
- **✕ No Color (default)** button — clears `_buyRackSelectedColor`, resets preview to gray, clears all borders
- Clicking a swatch syncs sliders to match (fine-tuning from a saved favorite)
- Panel height extended: anchors `(0.15, 0.05)` → `(0.85, 0.95)` to accommodate added rows

**Rack color persistence (`FloorMapApp.cs`):**

- `SaveRackColor(Vector3 worldPos, Color color)` — stores `{x:F1},{z:F1},{HEX}` entries in MelonPreferences
  - Category `"DCIM_RackColors"`, single entry `"colors"` (semicolon-separated list)
  - Keyed by rack mount world X/Z position (stable across save/load since racks don't move)
  - Rebuilds the entry string each save, removing any prior entry for that position
- `RestoreRackColors()` — called from `FloorManagerMod.OnSaveLoaded()`:
  - Parses all stored entries into `Dictionary<string, Color>`
  - Scans `FindObjectsOfType<Rack>()`, matches each to its `RackMount` parent position
  - Applies `renderer.material.color` to all `MeshRenderer` components on the matching rack
- `ApplyRackColorDelayed(RackMount, Color)` coroutine (3-frame wait) — waits for `InstantiateRack()` to finish, then applies color and calls `SaveRackColor()`

**`RackDiagramPanel.cs` additions:**

- `GetFavoriteColor(int index)` — reads hex from `_colorEntries[index]`, parses with `ColorUtility.TryParseHtmlString`, returns `Color`
- `SetFavoriteColor(int index, Color color)` — writes hex to `_colorEntries[index].Value`, calls `MelonPreferences.Save()`
- Both shared with `FloorMapApp` so the buy rack popup and mini shop use the same 8 favorite slots

**Bug fix — `CreateEntry` crash on second laptop open:**

- **Root cause:** `RackDiagramPanel.Build()` and `FloorMapApp.Build()` are called on every laptop open (re-runs `ComputerShopAwakePatch.Postfix`). Both called `MelonPreferences_Category.CreateEntry()` each time, which throws `System.Exception: Calling CreateEntry for fav_0 when it Already Exists` on the second open.
- **Fix — `RackDiagramPanel`:** Added `static bool _prefsInitialized = false` guard; `CreateEntry` block only runs on first `Build()` call.
- **Fix — `FloorMapApp`:** Rack colors entry guarded with `if (_rackColorsEntry == null)` — `CreateEntry` only called when field is null (first build).

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.2 — EOL threshold fix + rack diagram server info (2026-04-17)

**Files changed:** `FloorMapView.cs`, `RackDiagramPanel.cs`

---

**EOL threshold fix (`FloorMapView.cs`):**

- **Root cause:** Floor map was flagging racks as EOL when `eolTime <= 7200` (within 2h) or `eolTime <= 14400` (within 4h), but the game's own DeviceConfigPanel only turns the EOL field yellow at `eolTime <= 0` (fully expired). So servers the game considered healthy were showing EOL warnings on the floor map.
- **Fix:** `BuildAllRackData()` — server and switch classification now uses `eolTime <= 0` only for level 3 (EOL expired). The amber level-2 "approaching" tier is removed entirely from the floor map grid.
- **Fix:** Customer health map — same change: only `eolTime <= 0` elevates a customer's health to level 3.
- **Fix:** `StatusLevelToColor()` — removed case 2 (amber). Level 3 = yellow (expired), level 1 = green, default = gray.
- **Fix:** Filter chip `statusLevels` — changed from `{4, 2, 1, 0}` to `{4, 3, 1, 0}` so the EOL chip directly targets level 3.
- **Fix:** `ApplyFilters()` — removed the special case that made EOL chip match levels 2 and 3. Now direct match only.
- **Fix:** Stats strip EOL count — changed from `level == 3 || level == 2` to `level == 3` only.
- **`RackEntry.StatusLevel` values:** 0 = no devices, 1 = healthy, 3 = EOL expired, 4 = broken (level 2 removed).

**Rack diagram extra server info (`RackDiagramPanel.cs`):**

- **EOL logic updated:** `eolWarn = eolTime <= 0` (truly expired), `eolAppr = eolTime > 0 && eolTime <= EOL_WARN_SECONDS` (within 2h — shown amber `~EOL`).
- **IP address:** Server rows now show the server's IP in dim blue below the device name (only if server has a non-empty IP).
- **EOL countdown:** When `eolAppr`, shows a countdown `Xh Xm` or `Xm Xs` in amber in the sub-info line.
- **Layout:** Device name + sub-info wrapped in a `VerticalLayoutGroup` (`flexibleWidth=1`). Sub-info only added when IP or countdown is present.

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.3 — Live EOL timers everywhere + color fav persistence + rack strip removed (2026-04-17)

**Files changed:** `UIHelper.cs`, `RackDiagramPanel.cs`, `DeviceListView.cs`, `SearchResultsView.cs`, `FloorMapApp.cs`, `FloorManagerMod.cs`, `FloorMapView.cs`

---

**Shared EOL time helpers (`UIHelper.cs`):**

- `StatusAmber = new Color(0.95f, 0.72f, 0.08f)` and `StatusOrange = new Color(1.0f, 0.5f, 0.1f)` added as shared constants.
- `FormatEolTime(int eolTime)` — `eolTime > 0` → `"HH:MM:SS"` (counting down); `eolTime <= 0` → `"+HH:MM:SS"` (overdue, using `Math.Abs`).
- `EolTimeColor(int eolTime)` — amber when counting down, orange when expired/overdue.
- `ApplyEolLabel(TextMeshProUGUI, int)` — sets both text and color in one call. Use this in any live-refresh coroutine.
- Adding live EOL to a new view: add `TextMeshProUGUI EolLbl` to its `LiveRow` struct, call `UIHelper.ApplyEolLabel(lr.EolLbl, eolTime)` in the coroutine.

**Live EOL in RackDiagramPanel (`RackDiagramPanel.cs`):**

- Added `LiveRow` struct (`Server`, `Switch`, `Image BorderImg`, `TextMeshProUGUI StatusLbl`), `_liveRows` list, `_refreshCoroutine` field.
- `Open()` clears `_liveRows` and starts `RefreshLiveRows()` coroutine after building slot rows.
- `BuildOccupiedRow()`: status badge (widened to 56px) shows initial `FormatEolTime` / `EolTimeColor` values; `borderImg` and `statusLbl` refs stored in `_liveRows`.
- Sub-info row now only shows IP (countdown removed — status badge handles it live).
- `RefreshLiveRows()` coroutine: `WaitForSeconds(1f)` loop while panel is active; updates `BorderImg.color` and `StatusLbl` text/color each second via `UIHelper.ApplyEolLabel`.
- Expired devices show `+HH:MM:SS` counting up; approaching devices show `HH:MM:SS` counting down; healthy show `ON`/`OFF`.

**Live EOL countdown in DeviceListView (`DeviceListView.cs`):**

- `LiveRow` struct: added `TextMeshProUGUI EolLbl` field.
- `BuildDeviceRow()`: adds a 56px EOL countdown label after the detail label for server/switch rows. Initial value set with `FormatEolTime`/`EolTimeColor`.
- `RefreshLiveRows()` coroutine: now reads `eolTime` and calls `UIHelper.ApplyEolLabel(lr.EolLbl, eolTime)` each tick. Clears label for broken/no-EOL devices.

**SearchResultsView updated (`SearchResultsView.cs`):**

- Inline EOL formatting replaced with `UIHelper.FormatEolTime` / `UIHelper.EolTimeColor` / `UIHelper.ApplyEolLabel` — no behavior change, DRY.

**Color favorite persistent saves fixed (`FloorMapApp.cs`, `RackDiagramPanel.cs`, `FloorManagerMod.cs`):**

- **Root cause:** `MelonPreferences.CreateCategory` / `CreateEntry` for `DCIM_Colors` and `DCIM_RackColors` was called lazily inside `Build()` (first laptop open). If MelonLoader's config file was written before the first laptop open in a session, the values loaded from disk couldn't bind to the in-memory entries until that point.
- **Fix:** `RackDiagramPanel.InitPrefs()` — public static method (guarded by `_prefsInitialized`) that creates the category and 8 entries.
- **Fix:** `FloorMapApp.InitPrefs()` — public static method (guarded by `_rackColorsEntry != null`) that creates `DCIM_RackColors` category and `"colors"` entry.
- Both called from `FloorManagerMod.OnInitializeMelon()` — prefs now registered at game start, values load from disk immediately. Lazy init removed from `Build()`.

**Customer color strip removed from rack squares (`FloorMapView.cs`):**

- The segmented color bar in the top ~22% of each rack's inner area (showing customer colors) has been removed. It was visually confusing alongside the status color border and utilization fill bar.
- The rack square visual is now: status color border (2px ring) + dark inner background + utilization fill bar (4px, bottom).

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.4 — Rack diagram multi-select: Power ON + Assign Customer (2026-04-17)

**Files changed:** `RackDiagramPanel.cs`

---

**Multi-select in RackDiagramPanel:**

- Each occupied slot row now has a **checkbox** (14×14px square, leftmost child in the HLG) with a green fill that appears when selected.
- Clicking a checkbox toggles the device in/out of the selection. Uses `_checkboxClick` static bool flag to prevent the parent row's `onClick` from firing when only the checkbox was clicked (IL2CPP button propagation: child fires before parent synchronously).
- **Action bar** (36px, hidden by default) appears at the bottom of the panel VLG when ≥1 device is selected. Shows count label + 3 action buttons:
  - **Power ON** — calls `PowerButton()` on all selected servers/switches that are off and not broken. Shows HUD message with count.
  - **Assign Customer** — opens the customer assignment overlay.
  - **Clear** — deselects all, hides fills, hides action bar.
- **Customer assignment overlay** (`_assignCustPanel`) — full-screen dim + centered inner panel (Canvas sortingOrder=200, parented to `_panelRoot`). Scrollable list of all customers (logo + name buttons). Clicking a customer calls `server.UpdateCustomer(custId)` for all selected servers. Shows HUD message with assigned count. Switches are excluded (no customer API on `NetworkSwitch`). Dim backdrop click also dismisses.
- Selection state is cleared on every `Open()` call (fresh state for each rack opened).
- `_checkFillImages` dictionary (instanceID → fill Image) is keyed to current rack's slot rows; cleared on `Open()`.

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.5 — Status = power only + ON/OFF accuracy fix (2026-04-17)

**Files changed:** `DeviceListView.cs`, `SearchResultsView.cs`, `RackDiagramPanel.cs`, `CustomerIPView.cs`

---

**Status labels now only show BRK / ON / OFF — never EOL:**

- **Root cause of "shows OFF but is actually ON":** `on` was computed as `!broken && !eol && isOn` in every coroutine and initial build. An EOL server that was powered on had `eol = true` so `on = false`, causing the badge to show "EOL" (or "OFF" after coroutine update) even though the device was running.
- **Fix applied to all four views:** `on = !broken && isOn` — power state is independent of EOL state.
- Status dot/badge/label in `DeviceListView`, `SearchResultsView`, `RackDiagramPanel`, and `CustomerIPView` all updated. EOL countdown label (separate column) is unaffected and still displays correctly.
- `CustomerIPView` live-refresh coroutine rows removed EOL check from both static build and live-update paths.

**`UIHelper.GetDeviceState()` helper added (`UIHelper.cs`):**

- Two overloads: `GetDeviceState(Server, ...)` and `GetDeviceState(NetworkSwitch, ...)`.
- Outputs: `broken`, `on`, `eol`, `eolTime` — single source of truth so this class of bug cannot recur.
- All three coroutines (`DeviceListView`, `SearchResultsView`, `RackDiagramPanel`) now call `UIHelper.GetDeviceState()` instead of computing state inline.
- `using Il2Cpp;` added to `UIHelper.cs` to allow `Server` / `NetworkSwitch` parameter types.

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.6 — Back nav fix + checkbox visibility + Power OFF (2026-04-17)

**Files changed:** `FloorMapApp.cs`, `RackDiagramPanel.cs`

---

**Back navigation from device config fixed (`FloorMapApp.cs`):**

- **Root cause:** Clicking a device row in `RackDiagramPanel` called `OpenDevice()` which set `_previousState = DeviceList`. Back from DeviceConfig then rendered an empty device list ("R0/0") because `CurrentRack` was never set.
- **Fix:** Added `OpenDeviceFromDiagram(Server, NetworkSwitch)` — sets `_previousState = FloorMap`. Back handler updated with `else if (_previousState == ViewState.FloorMap) → SwitchToState(FloorMap)`.
- `RackDiagramPanel` row click updated to call `OpenDeviceFromDiagram` instead of `OpenDevice`.

**Checkbox visibility fixed (`RackDiagramPanel.cs`):**

- Checkbox background was `(0.08, 0.08, 0.10)` — nearly identical to the row background, invisible.
- Changed to `(0.30, 0.30, 0.38)` — visible slate gray square. Green fill appears when selected.

**Power OFF added to action bar (`RackDiagramPanel.cs`):**

- `PowerOffSelected()` method added — calls `PowerButton()` on all selected devices that are currently on.
- Action bar now: `[N selected] [Power ON] [Power OFF] [Assign Customer] [Clear]`

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.7 — Rack diagram: EOL timer + row alignment + larger logos + IP fix (2026-04-17)

**Files changed:** `RackDiagramPanel.cs`

---

**EOL countdown in rack diagram rows:**

- Added `TextMeshProUGUI EolLbl` to `LiveRow` struct.
- EOL label sits inside the `infoGo` VLG below the IP address (or device name if no IP).
- Shows when `eolTime <= 0` (expired, orange `+HH:MM:SS`) or `eolTime <= EOL_WARN_SECONDS` (approaching, cyan `HH:MM:SS`). Hidden for healthy devices.
- Live-refreshed by the existing `RefreshLiveRows()` coroutine via `UIHelper.ApplyEolLabel()`.

**IP label overlap fixed:**

- Previous: IP label was wrapped in a `subGo` container with no layout group, causing it to float with default center anchors and overlap the device name.
- Fix: IP label added directly to `infoGo.transform` as a VLG child. Width widened from 80px to 120px. Added `0.0.0.0` guard so unprovisioned servers don't show a placeholder IP.

**Multi-U row content alignment:**

- Row HLG now has `childAlignment = TextAnchor.MiddleLeft` — content (slot label, type badge, info, logo, status) is vertically centered within tall multi-U rows instead of hugging the top.

**Customer logos enlarged:**

- Logo size in rack diagram rows increased from 14×14px to 22×22px.

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.1.8 — Floor map EOL threshold: correct semantics + live refresh (2026-04-17)

**Files changed:** `FloorMapView.cs`

---

**eolTime semantics (confirmed via debug logging):**

- `eolTime` counts **DOWN** from a positive value. Positive = time remaining in seconds. Negative = past EOL deadline (still running, not yet repaired). Zero = just expired.
- Servers with `eolTime = 0` are NOT healthy defaults — all installed servers have a non-zero eolTime (either positive countdown or negative overdue).
- `isBroken` is a separate flag set when a server physically breaks down (independent of EOL expiry).

**autoRepairMode values (confirmed via debug logging):**

| Mode int | Game setting |
|---|---|
| 0 | Off |
| 1 | Repair Broken Only |
| 2 | Repair EOL 2h+ |
| 3 | Repair EOL 4h+ |

**Floor map EOL coloring rule (corrected):**

"Repair EOL Xh+" means the game auto-repairs servers that have been **past their EOL deadline for X hours** (eolTime < -threshold). The floor map matches this: racks/customers turn yellow only when the server has been expired *longer* than the auto-repair window — i.e., auto-repair should have handled it but hasn't yet.

```csharp
// threshold = 7200 (2h) or 14400 (4h) depending on autoRepairMode
bool srvEolFlag = eolThreshold > 0
    ? srv.eolTime < -eolThreshold   // expired past auto-repair window → warn
    : srv.eolTime < 0;              // no auto-repair: warn all expired
```

- **Green**: healthy (eolTime > 0) OR recently expired but still within auto-repair window
- **Yellow**: expired past the threshold (auto-repair missed it / needs manual attention)
- **Red**: `isBroken`
- Switching from **2h → 4h** reduces yellow (servers expired 2–4h go back to green because auto-repair has more time)

**Customer border colors (same logic):**
- Red: any server `isBroken`
- Yellow: any server `srvEolFlag` (expired past threshold)
- Green: all servers healthy or recently expired within window

**Live refresh coroutine added (`FloorMapView.cs`):**

- `_liveRefreshCoroutine` started at end of `Refresh()`, stopped at start of next `Refresh()`.
- `LiveRefreshCoroutine()`: `WaitForSeconds(3f)` loop while `_root.activeSelf`. Updates rack border `Image.color` and customer health `Image.color` in-place — no UI rebuild.
- `_rackBorderImages` (rackId → Image) and `_customerHealthImages` (custId → Image) tracked during build.
- Also updates stats strip (broken/EOL counts) each tick.
- Coroutine stops automatically when floor map view is hidden (`_root.activeSelf = false`).

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-17).

---

### v1.0.2 — Tab bar nav + bulk power + row/aisle checkboxes + mass buy (2026-04-19)

**Files changed:** `FloorMapApp.cs`, `DashboardView.cs`, `FloorMapView.cs`

---

**Tab bar navigation (`FloorMapApp.cs`):**

- Persistent 30px tab bar added between the header and content area — tabs: **Dashboard | Floor Map | Search**.
- Active tab: white text + 2px cyan underline indicator. Inactive: muted gray text.
- Tab bar shows on top-level views (Dashboard, FloorMap, SearchResults), hidden on drill-down views (DeviceList, DeviceConfig, CustomerIPs). Content area offsets dynamically between `-66px` (tab visible) and `-36px` (tab hidden) via `_contentAreaRT.offsetMax`.
- `UpdateTabBar(ViewState)` method drives show/hide + active highlight. Called at start of every `SwitchToState`.
- Fields added: `_tabBar`, `_contentAreaRT`, `_tabIndicators[3]`, `_tabLbls[3]`.
- Existing `< Back` back-stack behavior unchanged — tabs are an additional shortcut.

**Dashboard cleanup (`DashboardView.cs`):**

- Removed "Floor Map" chip from the filter row — now a tab.
- Kept "Offline" and "All Devices" chips as useful quick-filter shortcuts.

**Bulk power from floor map multi-select (`FloorMapView.cs`):**

- **Power ON** (dark green) and **Power OFF** (dark red) buttons added to the multi-select action bar.
- `BulkPower(bool on)` — collects selected rack `GetInstanceID()` set, scans all `Server` and `NetworkSwitch` objects, walks up each device's transform hierarchy with `GetComponentInParent<Rack>()` to test rack membership, calls `PowerButton()` on matching devices in the correct state.
- Servers: skips `isBroken`. Switches: no broken guard (switches don't have `isBroken`).
- Shows `StaticUIElements.instance.AddMeesageInField` HUD message with device count.

**Row/aisle inline checkboxes (`FloorMapView.cs`):**

- When **Select** is active, "R1"/"R2" row labels and "Aisle A"/"Aisle B" aisle labels swap to 10×10 checkboxes in-place. When Select is deactivated, labels reappear.
- Checkbox fill: dark = nothing selected, amber = partial, green = all selected (considers both racks and empty mount slots in the group).
- Clicking a row or aisle checkbox toggles ALL racks AND empty mount slots in that group. Standard toggle: if all selected → deselect all, else select all.
- Fields added: `_rowLabelTexts`, `_rowCheckboxGos`, `_aisleLabelTexts`, `_aisleCheckboxGos`, `_rowCheckboxFills`, `_aisleCheckboxFills` (all List/Dictionary, rebuilt each Refresh).

**Mass buy from floor map (`FloorMapView.cs`, `FloorMapApp.cs`):**

- Empty mount tiles ("+" slots) are now selectable in select mode. Clicking a "+" tile in select mode toggles selection rather than opening the single-buy popup. Blue overlay (vs orange for racks) to distinguish.
- Row/aisle checkboxes also select/deselect all empty mount slots in that group alongside racks.
- Action bar shows **"Buy Slots (N)"** button (dark blue) when one or more empty mounts are selected. Appears and disappears dynamically.
- Count label updates to show `"3r/2s"` (racks/slots) when both types selected, or just `"3 racks"` / `"2 slots"` when only one type.
- `ShowMassBuyPopup(List<RackMount>)` — new public method in `FloorMapApp`. Reuses the existing HSV color picker popup with title "Build N racks? Cost: $X  Balance: $Y" and confirm button "Build All".
- `OnMassBuyConfirm()` — checks total cost vs balance (fails with message if insufficient), closes popup, starts `MassBuyCoroutine`. Coroutine builds one rack per frame (`UpdateCoin(-price)` + `InstantiateRack(null)` + `ApplyRackColorDelayed`) to avoid frame stutter. Single HUD message on completion: "Built N rack(s) for $X". Calls `RefreshAllLabels` + `FloorMapView.Refresh()`.
- Color selection: user picks one color that applies to ALL purchased racks. Choosing no color clears stale saved entries (same as single buy).
- Fields added: `_massBuyMode` (bool), `_pendingBuyMounts` (List<RackMount>).

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-19).

---

### v1.0.1 — HSV color picker + recolor existing racks + buy fix + aisle fix (2026-04-19)

**Files changed:** `FloorMapApp.cs`, `RackDiagramPanel.cs`, `FloorMapView.cs`

---

**HSV color picker (replaces RGB sliders) (`FloorMapApp.cs`):**

- All three RGB slider fields (`_rSlider`, `_gSlider`, `_bSlider`, labels) replaced with HSV fields: `_hSlider` (0–360), `_sSlider` (0–100), `_vSlider` (0–100), gradient background images (`_satBgImg`, `_valBgImg`), value labels.
- `BuildHSVSlider()` — creates a labeled slider with a procedural gradient texture behind the track. Hue track is a static rainbow gradient (`BuildHueGradientTexture()`). Saturation and Value tracks update dynamically (`UpdateGradientTexture()`) whenever hue or the other component changes, so the gradient always shows the current color context.
- `SetSliderHSV(h, s, v)` — programmatically sets all three sliders while suppressing feedback loops via `_suppressHSVUpdate` flag.
- `OnHSVSliderChanged()` — reads H/S/V, converts to RGB via `Color.RGBToHSV`, updates preview square and hex input field.
- `UpdateHSVDerivedVisuals()` — updates saturation gradient (`black → full-hue`) and value gradient (`black → full-sat-hue`) every time H or S changes.
- Hex input field (`TMP_InputField`) added below sliders — displays `RRGGBB` (no `#`), editable. On submit (`OnHexInputConfirmed`) parses the hex string, converts to HSV, calls `SetSliderHSV`.
- `ShowBuyRackPopup()` resets to `SetSliderHSV(0,0,0)` and sets confirm button label to "Confirm".

**Recolor existing racks (single) (`FloorMapApp.cs`, `RackDiagramPanel.cs`):**

- `ShowRecolorPopup(Rack rack)` — public static method. Sets `_recolorMode = true`, `_pendingRecolorRack = rack`. Pre-loads the picker with the rack's current color (converts RGB → HSV via `Color.RGBToHSV`). Sets confirm button label to "Recolor". Opens the popup.
- "Recolor" button added to the rack diagram panel header (blue, left of the "Close" button). Clicks: `Close()` → `FloorMapApp.ShowRecolorPopup(_currentRack)`.
- `OnBuyRackConfirm()` — branches on `_recolorMode`. If recolor mode: calls `OnRecolorConfirm()`. Otherwise: existing buy flow.
- `OnRecolorConfirm()` — converts picker color to `Color32` → hex string → applies to all `MeshRenderer` components on the rack via `mat.color`. Calls `SaveRackColor` (single) or `SaveRackColor` per rack (mass). Resets `_recolorMode`, `_pendingRecolorRack`, `_pendingRecolorRacks`.
- Cancel button: also clears `_pendingRecolorRack`, `_pendingRecolorRacks`, `_recolorMode`.

**Mass recolor from floor map multi-select (`FloorMapApp.cs`, `FloorMapView.cs`):**

- `ShowMassRecolorPopup(List<Rack> racks)` — public static method. Sets `_recolorMode = true`, `_pendingRecolorRacks = racks`. Pre-loads with black (no meaningful "current" color across multiple racks). Sets confirm button label to "Recolor N Racks".
- "Recolor" button added to the floor map multi-select action bar. Calls `FloorMapApp.ShowMassRecolorPopup(selectedRacks)`.

**Buy device fix (`RackDiagramPanel.cs`):**

- **Root cause:** `ButtonBuyShopItem` only adds to the cart; it does NOT buy. The shop requires `ButtonCheckOut()` to actually spawn the item and deduct money. For colorable items, `ButtonChosenColor()` must also be called in between to confirm the pre-set color.
- Fix: buy button lambda now calls full sequence:
  ```csharp
  cs.ButtonBuyShopItem(id, price, type, name, isColorable);
  if (isColorable) cs.ButtonChosenColor();
  cs.ButtonCheckOut();
  ```

**Aisle label z-order fix (`FloorMapView.cs`):**

- **Root cause (z-order):** `BuildAisleLabels()` was called before the rack tile and empty mount loops, so aisle label GameObjects were added to `_contentRT` first and rendered behind all tiles (Unity UI renders children in sibling order). Fix: moved `BuildAisleLabels()` call to after both loops complete so aisle labels are added last and render on top of all tiles.
- **Root cause (y-position):** Label was placed at `firstRackY - 4f` with a top-anchored pivot, so the 12px label extended 8px into the rack tile. Fixed: `firstRackY - 17f` centers the 12px label in the 22px gap between the customer row bottom and the first rack row top (5px margin each side).

**Demolish + rebuild color persistence fix (`FloorMapApp.cs`):**

- **Root cause:** Buying a rack with no color selected did not call `RemoveRackColor`, leaving a stale entry in `DCIM_RackColors` prefs keyed to that mount position. After demolish + rebuild + reload, the color was restored from the stale entry.
- New method: `RemoveRackColor(Vector3 worldPos)` — finds and removes the entry for that position from the prefs string.
- Fix in `OnBuyRackConfirm()`: if `_selectedRackColor == Color.clear` (no color), calls `RemoveRackColor(_pendingBuyMount.transform.position)` instead of `SaveRackColor`.

**Confirmed deployed:** Built Release → `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll` (2026-04-19).

---

### v1.0.0 — Hacking System Chunks 1–3 + UI security labels (2026-04-16)

**Chunk 1 — HackingSystem core + wave scheduling (tested in-game):**

- `HackingSystem.cs` created with full static fields: firewall chain, IDS list, honeypot set, wave state, lockdown, counter-trace, offsite, hacker profiles, rep, ransom, attack log, MelonPreferences
- 4 hacker profiles: Script Kiddie, Hacktivist, Cybercriminal, APT-9 (DPS/frequency/persistence/disengage multipliers)
- `OnSaveLoaded()` — loads prefs, resolves switch/server refs, restores dormant state, reschedules stale wave triggers
- `OnLateUpdateTick()` — advances `_elapsedSeconds`, detects day change, runs activation check (4 customers OR 4 days), dormant expiry, wave trigger check, EOL drain accumulators for firewalls (3× during active wave), lockdown timer, ransom timer, periodic prefs save
- `OnDayChanged()` — rep recovery (+2/day), offsite HP regen, revenue penalty (rep deficit × daily revenue), disengagement check
- Activation threshold confirmed working: after 4 in-game days, `_hackingEnabled = true`, wave scheduled
- `SecondsPerDay = 300f` (5 real minutes per in-game day)
- **Bugs fixed during Chunk 1 testing:**
  1. Disengagement fired on Day 2 even though wave was pending at Day 5.7 — fixed by adding `wavePending` guard to disengage check
  2. Wave trigger silently did nothing — `_currentProfile` was null after `EnterDormant()` cleared it and no restore on load — fixed: `OnSaveLoaded()` always calls `SelectNewHackerProfile()` when hacking active; `EnterDormant()` now clears `_waveTriggerAt`; stale triggers rescheduled on load
  3. Stale `TimeController.onEndOfTheDayCallback` reference removed from FloorManagerMod (replaced by in-code clock)

**Chunk 2 — DeviceConfigPanel designation buttons (tested in-game):**

- Switch config security role section: "Set as Firewall" / "Remove Firewall" button, "Set as IDS" / "Remove IDS" button (mutually exclusive), HP bar with fill image, "Patch ($N)" button
- Server config security role section: "Set as Honeypot" / "Remove Honeypot" button (powers server off on designate, on on remove)
- Firewall HP cap: `FirewallMaxHP = 200` (2× base 100) — `ApplySecurityPatch` boosts MaxHP 20% per apply, hard cap enforced, button shows "MAX HP" and is disabled at cap
- `RefreshHPBar()` helper: fill image anchor updated to HP%, color thresholds (red < 30%, yellow < 60%, white ≥ 60%), cost label dynamic
- Harmony patches added to FloorManagerMod: `NetworkSwitchRepairPatch` (restores HP to full), `ServerRepairPatch` (clears ransomed state)
- `GetSwitchLabel(NetworkSwitch sw)` helper added (DeviceConfigPanel + DeviceListView) — looks up label from `SwitchSaveData` via `SaveData._current.networkData.switches[i].switchID` match (game update 6000.4.2f1 removed `NetworkSwitch.label`)
- Designations persist via MelonPreferences, survive session restart
- **Confirmed in-game:** HP bar visible at 100/100 after firewall designation; "Remove Honeypot" persists after restart; Patch button correctly disabled at max HP

**Chunk 3 — Wave execution: HP damage, breach, EOL acceleration, wave end (tested in-game):**

- `TickActiveWaves(deltaTime)` — ticks all active waves: 45-second timer countdown (paused during lockdown), calls `DealWaveDamage`, checks end conditions, logs events, schedules next wave on timer expiry
- `DealWaveDamage(wave, deltaTime)` — float accumulator per switch (`_waveHpAccumulators`) converts DPS to integer HP damage; offsite absorbs first if online, then frontline physical chain entry; skips broken switches (sets their `CurrentHP = 0` on encounter)
- `IsChainFullyBreached()` — returns true if offsite is down AND all physical chain entries are at 0 HP (or broken); empty chain = immediate breach
- `OnChainBreached(wave)` — rep loss by type (Probe −5, DataExfil −15, others −10); triggers `ApplyRansomware()` for Ransomware wave type; sets `_chainBreachedThisWave` flag to prevent double-breach
- `OnWaveDefeated()` — increments successive defenses, restores 10% HP to all chain switches, unlocks counter-trace at 3 successive defenses
- `ApplyRansomware()` — ransoms 1–3 random servers (stored in `_ransomed` HashSet by instance ID), starts 2-minute ransom timer
- Server EOL acceleration during breach: 2 extra eolTime seconds drained per real-second via `_serverEolAccum`
- EOL drain fix: when `eolTime` hits 0, sets `entry.Switch.isBroken = true` AND `entry.CurrentHP = 0` (both must sync)
- `OnSaveLoaded()` order fixed: transient reset (including `_currentProfile = null`) now happens BEFORE profile restoration so profile isn't wiped after being set
- **Bugs fixed during code review (pre-deploy):**
  1. `_currentProfile = null` in transient reset happened AFTER `SelectNewHackerProfile()` on reload — profile was immediately nulled; waves bailed silently. Fix: swap order — reset transient state first, restore profile last.
  2. Broken switches (EOL → `isBroken = true`) didn't zero `CurrentHP` — `DealWaveDamage` continued treating them as valid frontline. Fix: zero `CurrentHP` when setting `isBroken` in EOL drain, and guard in `DealWaveDamage` loop.
- **Confirmed in-game:** Wave triggered → HP depleted to 0 → breach detected → rep −10 → server EOL accelerating → next wave scheduled. Security Patch mid-wave correctly restored HP (100 → 200 with patch). Removing firewall mid-wave correctly triggered breach (chain became empty). All log entries accurate.

**UI — Security role labels in DeviceListView and SearchResultsView (hacking dev build only):**

- `DeviceListView` redesigned to match `SearchResultsView` style: status dot + colored badge (BRK/EOL/ON/OFF) instead of plain text; security role tags added after type name
- `SearchResultsView` security role tags added after type name column
- Role tags: `FW` (orange `#FF8019`) for firewalls, `IDS` (cyan `#1ACCCC`) for IDS switches, `HP` (purple `#B233E5`) for honeypot servers
- Both views call `HackingSystem.IsFirewall()`, `HackingSystem.IsIDS()`, `HackingSystem.IsHoneypot()` — live-read from HackingSystem state
- **These tags are stripped from the Nexus release DLL** — role tag blocks, H_Role spacer, and srTag block removed from copies of DeviceListView/SearchResultsView

**Polish fixes (SearchResultsView, DeviceListView, FloorMapApp):**

- **Double divider** — `SearchResultsView.BuildResultRow` had an unconditional `BuildColDivider` before the `if (_showCustomerCol)` block, which also added a divider. Removed the unconditional one.
- **Role tag column misalignment** — Conditional 28px role tag (only present for FW/IDS/HP devices) caused variable-width rows and a header with no matching column. Fixed by always rendering a 28px element (blank text if no role) in both header and data rows.
- **Power badge "OFF" for broken devices** — Badge logic checked `device.IsOn` before `device.isBroken`, so broken devices fell to the else branch and showed "OFF". Fixed: `isBroken` checked first → shows "BRK" in red.
- **Stale SearchResults after back-navigation** — When returning from DeviceConfig (where honeypot/IDS/FW designations are changed) to SearchResults, only scroll position was restored — role tag changes were not reflected. Fixed: `FloorMapApp.OnBackPressed()` now calls `SwitchToState(ViewState.SearchResults)` (full repopulate) followed by `SearchResultsView.RestoreView()` (scroll restore).

**Real-time data stream (live refresh via coroutines):**

- Architecture: in-place label updates via `MelonCoroutines`. No full repopulate — only `.text` and `.color` assignments on already-held refs. No GC, safe at any list size.
- Coroutine lifecycle: `while (_root != null && _root.activeSelf)` — exits automatically when view is hidden. `StopRefresh()` called at top of `Populate()` / `ClearElements()` to kill stale coroutine before rebuilding rows.
- `LiveRow` struct stores live IL2CPP object refs (`Server`/`NetworkSwitch`) + UI component refs (`Image DotImg`, `TextMeshProUGUI BadgeLbl`). Single badge label handles all states (BRK/EOL/ON/OFF) — not separate pwrLbl/eolLbl.

| View | Interval | Updates |
|---|---|---|
| `SearchResultsView` | 1s | Status dot color, power badge (BRK/ON/OFF), EOL countdown |
| `DeviceListView` | 1s | Status dot color, badge (BRK/EOL/ON/OFF) |
| `DeviceConfigPanel` | 1s | EOL timer label (H.M.S for servers, MMM:SS for switches); HP bar if switch is firewall |
| `DashboardView` | 30s | Full `Populate()` repopulate (cheap, small content) |

- Dashboard self-stop problem solved: `RefreshDashboard()` nulls `_refreshCoroutine` before calling `Populate()` so `StopRefresh()` inside `Populate()` doesn't kill the new coroutine.

**Floor map improvements (2026-04-16, deployed in Nexus release DCIM_v1.0.0.zip):**

- **Customer color strips** — top 22% of each rack's inner square split into up to 5 colored segments, one per unique customer with devices in that rack. Colors are deterministic: golden-angle hue spacing `((custId * 137.508f) % 360f) / 360f` → `Color.HSVToRGB(hue, 0.60f, 0.90f)`.
- **Device count badge** — count of all devices in the rack displayed bottom-right (7pt, 75% alpha) so utilization is visible at a glance.
- **Customer health borders** — customer squares in the top row now have border color reflecting their server fleet health: red = any broken, yellow = any EOL, green = all healthy, blue = no servers yet.
- **Aisle labels** — if more than one column group exists (columns separated by a gap > `AISLE_THRESHOLD = 1.5f` world units), labels "Aisle A", "Aisle B", etc. render just above the first rack row, centered over each group.
- **Quadrant-based tooltip** — tooltip spawns in the quadrant opposite to the clicked rack (top-left rack → bottom-right corner, etc.) using stored `_totalCols`/`_totalRows` and `colIdx`/`rowIdx` per-click. No more `ScanAll()` inside the tooltip handler.
- **Single-pass data scan** — `BuildAllRackData(out Dictionary<int,int> custHealthOut)` replaces `BuildRackStatusMap()`. One `FindObjectsOfType<Server>` + one `FindObjectsOfType<NetworkSwitch>` produces both the per-rack status/count/customer data AND the per-customer health map simultaneously.
- **`BuildCustomerSquare` helper** — extracted from two duplicate 80-line blocks in `BuildCustomerSquares`. Avoids IL2CPP type-casting issues.
- `CUSTOMER_ROW_HEIGHT` increased to `RACK_SIZE + RACK_GAP + 18f = 62f` to make room for aisle labels.

**Nexus release workflow (2026-04-16):**

- Two-DLL build workflow established: main dev project (with HackingSystem) and Nexus release (stripped copy).
- Nexus stripped files: `FloorManagerMod.cs` (HackingSystem tick + save hooks + two Harmony patches removed), `DeviceConfigPanel.cs` (security section + RefreshHPBar removed, RefreshSwitchConfig signature simplified), `DeviceListView.cs` (role tag block removed), `SearchResultsView.cs` (H_Role spacer + srTag block removed), `HackingSystem.cs` (deleted from copy).
- Binary verified clean: PowerShell UTF8 string scan of built DLL — zero HackingSystem references.
- Nexus release: `C:\Users\Jacob\Desktop\DCIM_v1.0.0.zip` (DLL only).

---

### v0.9.1 — Game update compatibility fix (2026-04-15)

**Trigger:** Data Center updated Unity engine to 6000.4.2f1 (Unity 6.4). IL2CppInterop 1.5.1 (shipped with MelonLoader 0.7.2) has a bug where it writes duplicate TypeDef entries into the generated `UnityEngine.CoreModule.dll` at the PE binary level. .NET's runtime loader catches this and throws `BadImageFormatException: Duplicate type with name '<>O'`, preventing DCIM (and all mods) from loading.

**Fix:** Mono.Cecil normalizes duplicate TypeDef entries when reading a DLL. Round-tripping `UnityEngine.CoreModule.dll` through Mono.Cecil (read → write with no changes) produces a clean PE without binary-level duplicates. The patched DLL loads correctly.

**Tool created:** `C:\Users\Jacob\Desktop\data center mods\Tools\FixCoreModule\` — small .NET 6 console app using Mono.Cecil. Run after any game update that causes `Duplicate type '<>O'` errors. The original is backed up as `UnityEngine.CoreModule.dll.bak` each run.

**Mods rebuilt and redeployed** against the clean interop assemblies. No source code changes — version bump only.

**Note on game's built-in Command Center / Auto Repair:** The game update added a built-in auto-repair system (Asset Management → Technician Job Queue → Auto Repair dropdown: Off / Repair Broken / Repair EOL 4h+ & Broken / Repair EOL 2h+ & Broken). This fully supersedes AutoDispatcher. AutoDispatcher is retired. The Warn toggle (warning suppression) from AutoDispatcher has been migrated into DCIM as part of this release.

**Warning suppression migrated from AutoDispatcher:** "Warn: ON/OFF" toggle added to the DCIM header (always visible, far right). When ON, clears device warning signs and broken error signs every 2s via background poll in `FloorManagerMod.OnUpdate`. Saved in MelonPreferences under `DCIM.WarnEnabled`. AutoDispatcher.dll removed from Mods folder.

---

### v0.9.0 — Nexus pre-release polish (2026-04-14)

**Version bump to 0.9.0** — first public Nexus release candidate. Signifies near-complete feature set.

**MelonInfo version fixed** — was still set to `"0.2.0"` since initial dev build; updated to `"0.9.0"` in both `MelonInfo` attribute and startup log.

**Remaining diagnostic logs removed:**
- `ComputerShopPatch` — removed "Laptop app injected." (one-time startup noise)
- `RackInstantiatePatch` — removed "New rack placed — refreshing 3D labels" (per-rack-placement noise)
- `RackLabelManager.RefreshAllLabels()` — removed "3D rack labels created: N" (fires on save load + rack placement)
- `FloorMapApp.OnBuyRackConfirm()` — removed "Rack placed at mount, cost $N" (HUD message already covers this)

**User-facing typo fixed (UIHelper.cs):**
- `"Mainframe 3U 5000 IOPs"` → `"IOPS"` and `"Mainframe 7U 12000 IOPs"` → `"IOPS"` — now consistent with all other server type strings

**Dead code removed (SearchEngine.cs):**
- `_lastScanTime` field — set on every `ScanAll()` call but never read anywhere; removed field and assignment

**Search Results broken server display fixed (SearchResultsView.cs):**
- EOL column: broken servers now show `BRK` in red instead of blank
- PWR column: broken servers now show `OFF` in gray instead of blank

**Server count stat fixed (SearchEngine.cs):**
- `ServerCount` was using `allServers.Length` (all Server objects in scene, including unracked). Now only counts servers where `currentRackPosition != null` (rack-mounted only), matching the data displayed in customer and device breakdowns. Dashboard stat changed from 328 → 324 on one test map (4 servers were loose objects not in any rack).

**Customer server count fixed (SearchEngine.cs + CustomerIPView.cs):**
- `GetCustomerList()` was calling `d.Server.GetCustomerID()` which bypasses the `-1` override applied to servers with no real IP. Fixed to use `d.CustomerID` directly, which respects the override. Prevents unprovisioned servers from being counted under real customers.

**DeviceListView batch assign log removed (DeviceListView.cs):**
- Removed `MelonLogger.Msg($"[DCIM] Batch assigned {assigned} servers to customer...")` — HUD message already confirms this action.

**DLL renamed from FloorManager.dll to DCIM.dll (FloorManager.csproj):**
- Changed `<AssemblyName>FloorManager</AssemblyName>` → `<AssemblyName>DCIM</AssemblyName>`. Old `FloorManager.dll` deleted from Mods folder. Deploy path is now `DCIM.dll`.

**Auto IP subnet lookup fixed (DeviceConfigPanel.cs):**
- `FindSubnetForServerType` was deriving the subnet by scanning all scene servers to find one already assigned to the same customer + server type, then extracting its IP prefix. This failed for brand new customers with no servers yet (always fell back to the first subnet regardless of server type). Replaced with a direct `subnetsPerApp.TryGetValue(serverType, out subnet)` call — the dictionary key IS the server type int. IL2CPP direct key access works; only enumeration crashes. `_entries` fallback retained as safety net. Result: Auto IP now correctly picks the right subnet for any server type, including on a customer with zero servers assigned.

**Assign Customer added to Search Results (SearchResultsView.cs):**
- "Assign Customer" button added to the power row (next to Power OFF). Button label shows selected server count in brackets when servers are selected. Switches excluded (can't be customer-assigned). Clicking opens a popup overlay with scrollable customer list (logo + name), click to highlight selection, Assign + Cancel buttons. On confirm: calls `UpdateCustomer()` + `AutoFillIP()` for each selected server, seeds `assignedIPs` with `GetAllUsedIPs()` so no duplicates within the batch or with existing IPs, shows HUD message, refreshes the results list. Same batch assign pattern as DeviceListView.

**Confirmed working in-game.**

---

### v0.2.7 — Pre-release cleanup (2026-04-14)

**Diagnostic logging removed:**
- `SearchEngine.ScanAll()` — removed `existingCustomerIDs` dump block and `ScanAll` count log (also removed dead `noRackIds` StringBuilder that only fed the removed log)
- `SearchEngine.GetCustomerList()` — removed fallback customer log line
- `FloorMapView.Refresh()` — removed floor map installed/empty count log
- `FloorMapView.BuildCustomerSquares()` — removed CustomerBase fallback log line

**Divider memory leak fixed (UIHelper.cs + DeviceConfigPanel.cs + CustomerIPView.cs):**
- `UIHelper.BuildDivider()` changed from `void` to `GameObject` return
- `DeviceConfigPanel` — all 7 `UIHelper.BuildDivider()` calls now add the returned GameObject to `_elements`, so they are properly destroyed on every `ClearElements()` call
- `CustomerIPView` — 1 `UIHelper.BuildDivider()` call now adds to `_rows`, so it is properly destroyed on every `Populate()` call
- `DashboardView` and `SearchResultsView` were already correct — no changes needed

**Note:** `FloorMapView._rackSquares` and `_customerButtons` confirmed NOT dead — they are actively used for cleanup on every `Refresh()`.

---

### v0.2.6 — Fix customer ID 0 (first customer invisible) (2026-04-14)

**Root cause:** All customer ID checks used `<= 0` as "invalid/unassigned", but the game assigns customer IDs starting at 0 (zero-based index into `customerItems[]`). The first customer unlocked always gets ID 0, meaning it was silently skipped everywhere.

**Diagnosed via:** Added `MelonLogger` dump of `existingCustomerIDs` — output was `[0 1 2 3 5 4 6 7 8 9 10 11 12]`. ID 0 present in the list, confirmed as a real customer (Bermuda Triangle Backup with 4 active servers).

**Fixes (SearchEngine.cs + FloorMapView.cs):**
1. All `custId <= 0` / `realCustId <= 0` guards changed to `< 0` — allows ID 0 as valid
2. Server scan customer name lookup: `custId > 0` → `custId >= 0` so ID-0 servers display the correct customer name
3. `unassignedCount` check: `custId <= 0` → `custId < 0`
4. CustomerBase scan in `ScanAll()`: allows ID 0 only when `customerItem != null` (active base), to avoid treating 34 empty/inactive bases as customer 0
5. CustomerBase fallback in `BuildCustomerSquares()`: same ID-0 guard

**Key learning:** Game customer IDs are 0-based. Never use `<= 0` or `> 0` as valid/invalid sentinel — always use `< 0` / `>= 0`.

---

### v0.2.5 — All unlocked customers shown in floor map + dashboard (2026-04-14)

**Customer list now sourced from `existingCustomerIDs` (SearchEngine.cs + FloorMapView.cs):**

1. **All unlocked customers shown** — Previously both the floor map header row and dashboard customer list only showed customers that had at least one server assigned. Now both read from `MainGameManager.instance.existingCustomerIDs` — the game's authoritative list of purchased/unlocked customers — so customers appear as soon as they're unlocked, even before any servers are assigned to them.

2. **Purchase order preserved** — `existingCustomerIDs` is a `List<int>` that preserves insertion order (the order customers were purchased). Both views now iterate it in order using an indexed for-loop (IL2CPP-safe). The old dashboard sorted by server count descending; that sort is removed.

3. **`CustomerCount` stat updated** — `ScanAll()` now sets `CustomerCount` from `existingCustomerIDs.Count` (all unlocked) instead of `_allCustomerIds.Count` (only those with servers). The dashboard "Customers (N)" header reflects the true unlocked count.

4. **Fallback preserved** — `GetCustomerList()` still appends any customers found in `_allCustomerIds` (from server scanning) that aren't in `existingCustomerIDs`, as a safety net.

5. **Floor map slot indexing fixed** — `BuildCustomerSquares` now uses a separate `slotIndex` counter (not loop index `i`) so squares are always contiguous even if any ID entry is ≤ 0.

---

### v0.2.3 — Search Results column layout + dividers (2026-04-14)

**Search Results — proportional column layout (SearchResultsView.cs):**
1. **All data columns now proportional** — replaced fixed/single-flexible approach with weighted `flexibleWidth` on every data column: Type=3x, IP=2x, EOL=1.2x, Customer=4x, Rack=0.8x. Columns share the full row width relative to their weights — no blank void in any one column
2. **Vertical column dividers** — 1px `Image` dividers (`BuildColDivider`) between every data column in both header and data rows; subtle `(0.22, 0.22, 0.26)` color
3. **Customer names untruncated** — Customer column has the highest flex weight (4x) so full names always fit without truncation
4. **Rack column proportional** — Rack now also shares flex space (0.8x) instead of fixed, scales naturally with screen width

### v0.2.4 — Navigation fixes + Floor map empty-mount filter + Legend cleanup (2026-04-14)

**Navigation fixes (FloorMapApp.cs + CustomerIPView.cs):**

1. **CustomerIPs → DeviceConfig → Back now returns to CustomerIPs** — Root cause: `CustomerIPView.BuildIPRow()` was calling `OpenDeviceFromSearch()`, which hardcoded `_previousState = SearchResults`. Fixed by adding `OpenDeviceFromCustomer(Server)` method that sets `_previousState = ViewState.CustomerIPs`. `CustomerIPView.BuildIPRow()` now calls this method instead.

2. **SearchResults → DeviceConfig → Back: scroll position preserved** — `SwitchToState(SearchResults)` always called `Populate()`, which wiped the list and reset scroll. Fixed with a separate restore path in `OnBackPressed()` (DeviceConfig case) that manually activates the SearchResults view and calls `SearchResultsView.RestoreView()` instead of going through `SwitchToState`. `SearchResultsView.SaveScrollPosition()` is called in `OpenDeviceFromSearch()` before navigating away.

**SearchResults column visibility (SearchResultsView.cs):**

3. **IP Address and Customer columns hidden when no servers in results** — When searching switches only, the IP and Customer columns are meaningless. `Populate()` now computes `hasServers` from the result set and sets `_showIPCol`/`_showCustomerCol` flags. Header and data rows both gate on these flags. Switch-only searches show a clean Type / EOL / Rack layout.

**Floor map empty mount filter (FloorMapView.cs):**

4. **Empty rack mounts now filtered to installed rack bounding box** — Previously all `RackMount` objects were rendered including mounts in wall areas and unpurchased zones (428 empty mounts on one test map). There is no usable flag to distinguish purchased vs. unpurchased floor areas (`Wall.isWallOpened` always returns `false`). Fix: compute the bounding box of all installed racks (min/max X and Z world positions), then skip any empty mount whose position falls outside that box with a 2.5f expansion buffer. This keeps one row/column of expansion slots visible around the installed zone. Summary log: `[DCIM] Floor map: N installed, M empty mounts in rack zone`.

**Color legend cleanup (FloorMapApp.cs):**

5. **Legend moved to bottom-right** — Was anchored top-right, which blocked the Buy Rack popup and clickable rack targets. Changed `anchorMin/Max` to `(1,0)` (bottom-right), `anchoredPosition` to `(-6f, 6f)`, `sizeDelta` to `(110f, 80f)`.

6. **Legend entries trimmed to Broken / EOL / Healthy / Empty** — Removed six entries: Blue Server, Green Server, Purple Server, Yellow Server, Switch, Patch Panel (and the divider between the server colors and device types). The legend now only shows the four status meanings that are not obvious from color alone.

**Log cleanup:**
- Removed per-mount diagnostic log from FloorMapView (Y/parent research complete)
- Removed RT-copy noise from `ComputerShopPatch` (fired every laptop open)
- Upgraded 3 `MelonLogger.Msg` null/missing checks in CustomerIPView to `MelonLogger.Warning`

---

### v0.2.2 — Search Results polish + Color filter + Tooltip improvements (2026-04-14)

**Rack tooltip improvements (FloorMapView.cs):**
1. **Grammar fix** — "1 switches" → "1 switch", "1 servers" → "1 server" (singular/plural)
2. **Auto-sizing panel** — replaced fixed anchors `(0.05,0.30)→(0.60,0.65)` with `ContentSizeFitter` anchored bottom-left; panel now shrinks to content instead of taking 55% of screen
3. **Side-by-side buttons** — Details and Close now in a `HorizontalLayoutGroup` row instead of stacked full-width
4. **Rack label shown** — `data.Label` (e.g. "R2/6") now displayed in gray next to the rack ID header if set
5. **One customer per line** — customer list changed from comma-separated to one name per line with "Customers:" header

**Search Results — Color filter (SearchEngine.cs + SearchResultsView.cs):**
6. **`ServerColorFilter` enum added** — `All`, `Blue`, `Green`, `Purple`, `Yellow`
7. **Color filter button + popup** — "Color: All" toolbar button opens a popup with colored status dots next to each option (Blue/System X, Green/GPU, Purple/Mainframe, Yellow/RISC)
8. **Color filter logic** — checks `ObjName.Contains(colorName)` on servers only; when a color is selected, non-servers (switches, patch panels) are excluded entirely

**Search Results — Unassigned filter (SearchEngine.cs + SearchResultsView.cs):**
9. **`StatusFilter.Unassigned` added** — shows servers with IP `0.0.0.0` or empty only; switches and patch panels excluded (they never have IPs so the filter is meaningless for them)

**Search Results — Row polish (SearchResultsView.cs):**
10. **Column headers** — sticky header row above results showing: Pwr / Type / IP Address / EOL / Customer / Rack
11. **Alternating row shading** — even rows `(0.12,0.12,0.14)`, odd rows `(0.10,0.10,0.115)` for readability
12. **Checkbox border style** — outer border image `(0.35,0.35,0.38)` with dark inner fill; fill turns green on select (was a plain gray square)
13. **ON/OFF power badges** — "ON" in green for powered-on, "OFF" in gray for powered-off non-broken, nothing for broken (red dot is sufficient)
14. **EOL time column** — `EolTime int` added to `DeviceInfo`, populated in `ScanAll()` from `srv.eolTime` / `sw.eolTime` (real-time seconds); displays as `HH:MM:SS` when countdown active, "EOL" in orange when expired
15. **Customer column widened** — truncation increased from 10→13 chars, column from 70→90px
16. **Fresh scan on populate** — `SearchEngine.ScanAll()` now called at the top of `SearchResultsView.Populate()` so EOL times and device states are always current (previously only Dashboard triggered a scan)

**Bug fixes:**
17. **EOL time not showing** — was checking `EolTime <= 30f` (seconds), which almost never matched. Now checks `EolTime > 0` (any active countdown)
18. **EOL format** — changed from `HH.MM.SS` (dots) to `HH:MM:SS` (colons)

### v0.2.1 — Bug fixes + testing (2026-04-13/14)

**Fixed:**
1. **LACP config exit no longer breaks cursor/camera** — After closing the native LACP config canvas, cursor is now forced visible (`Cursor.visible = true`, `Cursor.lockState = CursorLockMode.Confined`) and player movement/mouse look are disabled (`enabledMouseMovement = false`, `enabledPlayerMovement = false`). Previously the game's `CloseAnyCanvas()` called `LockedCursorForPlayerMovement()` which locked the cursor and re-enabled camera movement while still in the laptop UI.

2. **Switch select all now works** — Checkboxes added to switches (not just servers) in both DeviceListView and SearchResultsView. Select All/Deselect All toggles both servers and switches. Power ON/OFF bulk actions now call `sw.PowerButton()` for selected switches in addition to servers. DeviceListView uses parallel tracking (`_selectedSwitches`, `_switchCheckboxImages`, `_allSwitchesInRack`) since the existing data structures were keyed by `Server`.

3. **Filter dropdown popups now show** — SearchResultsView filter popups (Type/Status/Customer) were invisible because VerticalLayoutGroup absorbed the overlay. Fixed with `LayoutElement.ignoreLayout = true`.

4. **Revenue row now shows in CustomerIPView** — IL2CPP dictionary key access returned garbage pointers for all entries. Fixed by matching `entries[ei].value.customerID == customerID` instead of comparing keys.

5. **3D rack labels positioned correctly** — Iterative positioning: labels sit at `localPosition = new Vector3(-0.33f, 2.20f, 0.40f)` with scale 0.002 and 180° Y rotation. Top-right corner of rack front face, just above the door. No background, cyan text.

6. **Rack price now live** — `GetRackPrice()` no longer caches — reads `ShopItemSO.price` every call so it reflects any game price changes. Fallback updated from $500 to $1250.

7. **TMP outline crash avoided** — `outlineWidth`/`outlineColor` on TextMeshProUGUI crash in IL2CPP. Not used.

8. **Auto IP duplicate prevention** — Single-server auto-fill now scans all existing server IPs (`GetAllUsedIPs()`) before picking one. Batch auto-fill seeds its tracking set with all currently-used IPs instead of starting empty. No more duplicate IP assignments.

9. **Auto IP correct subnet per server type** — `FindSubnetForServerType()` finds an existing server of the same `serverType` + same customer that already has a valid IP, extracts its subnet prefix, and uses it. Avoids IL2CPP dictionary key access issues with `appIdToServerType`. Blue servers get blue subnet, yellow get yellow, etc.

10. **Server type names now correct (3U vs 7U)** — `ReturnServerNameFromType(serverType)` returned wrong names (all showing 5000 IOPS). New `UIHelper.GetServerTypeName()` derives the correct name from game object name (e.g. "Server.Blue2(Clone)" → "System X 7U 12000 IOPS"). Fixed across all files: CustomerIPView, DeviceListView, DeviceConfigPanel, SearchEngine, FloorMapApp.

11. **Customer count now includes all customers** — Previously customers whose servers all lacked IPs were invisible (customerID forced to -1). Now `customerIds` is populated from the real `GetCustomerID()` before the IP check override. `GetCustomerList()` also uses `Server.GetCustomerID()` directly.

12. **Customer subnet info row** — CustomerIPView now shows a row below the customer name with color dots + type name + subnet for each server type the customer uses. Groups by `serverType` so both 3U and 7U of the same color show separately.

13. **ESC closes DCIM screen** — `OnLateUpdate` detects when `canvasComputerShop` is inactive (laptop closed via ESC) and hides DCIMScreen automatically.

**Confirmed working (user-tested):**
- LACP config exit: cursor stays visible, camera locked, DCIM screen re-appears correctly
- Switch select all: checkboxes appear on switches, Select All includes them, bulk power works
- Filter dropdowns: Type/Status/Customer popups appear and filter correctly
- SearchResults row click: navigates to DeviceConfig
- Back navigation from DeviceConfig: returns to correct origin (SearchResults or DeviceList)
- 3D rack labels: visible on racks, correct position, correct numbering
- Revenue row: shows in CustomerIPView with revenue/penalties/net
- CustomerIPView rows: clickable, navigate to DeviceConfig
- Rack purchase: correct live price, placement works
- Status colors on floor map: working correctly
- Buying racks from FloorMap: price correct, rack placed
- Auto IP: no duplicates, correct subnet per server type
- Server type names: correctly show 3U/5000 vs 7U/12000
- Customer count: all 12 customers visible
- Customer subnet info row: shows type + subnet per server color
- ESC closes DCIM screen properly, re-opening laptop works

---

## What Works (confirmed)

- Mod loads (v0.1.1), coexists with AutoDispatcher
- App button injected into laptop GridLayoutGroup, matches game style
- Floor map renders with dark wireframe look, correct physical orientation (no mirroring)
- Rack squares show correct status colors (green/amber/yellow/red/gray — 5-level EOL system as of v1.1.0)
- Rack click highlight — cyan border on selected rack
- Rack numbering (row/position format: R1/1, R1/2...) — matches physical layout
- Aisle gaps render correctly between rack groups
- Color legend (top-right, hidden outside floor map view)
- Back button (all four levels + back to laptop, no selection artifacts)
- Row labels (R1–R5)
- Device list — finds devices via hierarchy-walk matching (unified with floor map)
- Device list — multi-select checkboxes on servers and switches, Select All toggle
- Device list — "Assign Customer" header button opens customer popup
- Device config — switch ports, server power/IP/customer/EOL/processing all display
- Device config — customer dropdown list with logos, names, selection indicators
- Device config — Power ON and OFF both confirmed working remotely
- Device config — Auto IP assigns correct IPs (.2+, skips gateway), no duplicates, correct subnet per server type
- Device config — Manual IP editor (subnet prefix + octet picker + Set)
- Device config — EOL timer in H.M.S format matching game style
- Device config — Processing shows rated IOPS from server type name
- Device type names correct — derived from game object name (Blue1=System X 3U, Blue2=System X 7U, etc.)
- Device type colors correct — uses game object name (Blue/Green/Purple/Yellow keywords)
- Customer squares on floor map — logo sprites, health-colored borders, clickable to toggle customer rack filter (v1.1.0: no longer navigates to CustomerIPView)
- Floor map — customer color strips on rack squares (up to 5 segments per rack)
- Floor map — device count badge bottom-right of each rack square
- Floor map — aisle labels above column groups when multiple aisles exist
- Floor map — tooltip in opposite quadrant to clicked rack (quadrant-based positioning)
- Floor map — single-pass BuildAllRackData scan (no redundant FindObjectsOfType)
- Customer IP View — all active IPs for a customer with server type, IP, status columns
- Customer IP View — subnet info row shows server types + subnets per customer
- Batch customer assignment — popup overlay, unique IPs per server (no duplicates), skips .0/.1
- Customer list shows all customers (including those with servers that have no IPs yet)
- ESC key closes DCIM screen when exiting laptop
- Remote API: `GetConnectedDevices()` works
- LACP config opens on top of laptop, returns to DCIM screen on close with cursor/camera state correctly restored
- No cyan/blue selection box artifacts (Navigation.Mode.None on all buttons + EventSystem deselect)
- IL2CPP safe: no foreach on Il2CppArrayBase, no GetEnumerator on IL2CPP dictionaries

## All Features Tested (v1.0.1, 2026-04-19)

v1.0.1 features (deployed locally, not yet on Nexus):
- HSV color picker: H/S/V sliders with gradient track backgrounds (hue rainbow, sat/val dynamic gradients), hex input field
- Recolor button in rack diagram panel header — opens picker pre-loaded with rack's current color, applies immediately
- Mass recolor in floor map multi-select action bar — one color to all selected racks
- Buy device from rack diagram mini shop now works (full `ButtonBuyShopItem` + `ButtonChosenColor` + `ButtonCheckOut` sequence)
- Aisle labels now render on top of rack tiles (z-order fix) and positioned in the gap between customer squares and rack rows (y-offset fix)
- Demolish + rebuild rack: old color no longer restored on reload (`RemoveRackColor` called on no-color buy)

## All Features Tested (v1.1.3, 2026-04-17)

v1.1.3 features (deployed, not yet confirmed in-game):
- `UIHelper.FormatEolTime` / `EolTimeColor` / `ApplyEolLabel` — shared helpers; any future view uses these for live EOL
- Rack diagram: live EOL per slot — counts down `HH:MM:SS`, flips to `+HH:MM:SS` (overdue) when expired; border color also live
- DeviceListView: live EOL countdown label per row (56px, updates every second)
- SearchResultsView: refactored to use shared UIHelper EOL helpers (behavior unchanged)
- Color favorites: now persist correctly — prefs registered at game start (`OnInitializeMelon`) not lazily on first laptop open
- Floor map rack squares: customer color strip (top bar) removed

v1.1.2 features (deployed, not yet confirmed in-game):
- Floor map EOL threshold: only racks with fully expired EOL (`eolTime <= 0`) show yellow — no more false warnings for healthy servers
- EOL filter chip and stats strip EOL count updated to match (level 3 only)
- Rack diagram: server IP shown in dim blue below device name
- Rack diagram: EOL approaching — status badge shows `HH:MM:SS` countdown in amber

v1.1.1 features (deployed, not yet confirmed in-game):
- Rack buy popup: RGB sliders for fully custom color, live preview square
- Rack buy popup: click swatch → syncs sliders to that color for fine-tuning
- ★ Save slots 1–8 in buy popup: overwrites favorite slot with slider color, persists immediately
- ✕ No Color button clears selection
- Rack colors persist across saves: stored by X/Z position in `DCIM_RackColors` prefs, restored on save load
- `CreateEntry` crash on second laptop open: fixed with `_prefsInitialized` guard in `RackDiagramPanel` and null-check guard in `FloorMapApp`

v1.1.0 features (deployed, not yet confirmed in-game):
- Stats strip: rack/device/broken/EOL counts at top of floor map
- Filter bar: Broken/EOL/Healthy/Empty chip toggles, dim to 20% on non-matching racks
- Customer logo click: filters racks (toggles `_customerFilter`), no longer navigates to CustomerIPView
- Rack colors: green/yellow/red/gray (expired EOL = yellow, broken = red)
- Rack utilization fill bar (4px bottom bar, gray, fill = occupiedU / totalU)
- Multi-select mode: orange overlays on selected racks, count badge on toggle button
- Action bar: View Devices / Assign Customer / Clear buttons
- View Devices → DeviceListView shows aggregated list with "N racks — M devices" header
- Assign Customer panel: assigns to unassigned servers in selected racks
- Rack diagram panel: U-slot layout, multi-U device rows, status badges, customer dot
- Click occupied slot in diagram → DeviceConfigPanel
- Click empty slot → Mini shop panel (unlocked servers/switches only)
- Colorable items: 8 color swatches, "Buy with Color" pre-sets picker, game picker opens
- Favorite colors saved to MelonPreferences `DCIM_Colors`, persist across sessions
- Rack click: directly opens RackDiagramPanel (tooltip removed)
- Customer logo click: toggles customer filter (no longer navigates to CustomerIPView)

## All Features Tested (v1.0.0 Nexus, 2026-04-17)

All features confirmed working as of 2026-04-17 (Nexus release — no HackingSystem):

**Core app:**
- Power ON/OFF (single + bulk in DeviceList and SearchResults)
- Manual IP editor (-/+ octet picker + Set)
- Auto IP (no duplicates, correct subnet per server type)
- Dashboard, floor map, device list, device config, customer IPs, search results
- Rack label in DeviceConfig panels ("Rack: R1/12", "Rack: R4/5")
- Switch port count + connected ports list
- 3D rack labels on save load
- Bulk power ON/OFF in SearchResults
- ESC closes DCIM screen
- Customer dropdown with all 12 customers
- Search Results Color filter (Blue/Green/Purple/Yellow — servers only, switches excluded)
- Search Results Unassigned filter (servers with 0.0.0.0 — switches excluded)
- Search Results column headers, alternating rows, checkbox border, ON/OFF badges, EOL HH:MM:SS column
- Search Results proportional column layout, vertical dividers, full customer names untruncated
- CustomerIPs → DeviceConfig → Back returns to CustomerIPs
- SearchResults → DeviceConfig → Back preserves scroll position
- Real-time refresh coroutines: SearchResultsView (1s), DeviceListView (1s), DeviceConfigPanel (1s EOL), DashboardView (30s)
- Live EOL countdown in DeviceListView, SearchResultsView, RackDiagramPanel (counting down amber / overdue orange)
- Power status (ON/OFF) accurate independent of EOL state

**Floor map (v1.1.x):**
- Stats strip (Racks / Devices / Broken / EOL counts)
- Filter bar: Broken / EOL / Healthy / Empty toggle chips (dims non-matching racks)
- Customer logo squares with health-colored outlines (green/yellow/red) — click to filter racks by customer
- Floor map live refresh every 3 seconds — rack and customer colors update without navigation
- EOL colors tied to game's Command Center auto-repair setting (2h/4h threshold)
- Rack utilization fill bars (occupied U / total U)
- Multi-select mode with action bar (View Devices / Assign Customer / Clear)
- Color legend bottom-right (Broken/EOL/Healthy/Empty)
- Floor map empty mounts filtered to installed rack bounding box (2.5f expansion)
- Aisle labels above column groups

**Rack Diagram Panel (v1.1.x):**
- Opens on rack click — U-slot layout, multi-U rows span correctly, vertically centered content
- Live status badges (BRK/ON/OFF) and live EOL countdown per slot, updating every second
- IP address shown per server row (hidden if 0.0.0.0)
- Customer logo (22×22px) per row
- Checkbox multi-select with action bar: Power ON / Power OFF / Assign Customer
- Buy button on empty slots → mini shop (servers/switches only, color swatches for colorable items)
- Back from DeviceConfig (opened via diagram) returns to floor map

**Rack color picker (v1.0.1):**
- Buy Rack popup: 8 favorite swatches + HSV sliders (H/S/V with gradient tracks) + hex input field + live preview
- ★ Save to slot persists favorites to MelonPreferences
- Rack colors persist across sessions and save/load via world position keying
- Recolor existing rack via button in rack diagram panel header (no purchase required)
- Mass recolor all selected racks from floor map multi-select action bar
- Demolish + rebuild rack: old color correctly cleared (no stale prefs entry)

## TODO

- ~~**Switch individual power control**~~ — **DONE (v0.2.1)**
- ~~**Warn button removal**~~ — **DONE (v0.9.2)**
- ~~**Customer IP Add Server button**~~ — **DONE (v0.9.2)**
- ~~**Floor map customer color strips + device count + health borders + aisle labels + quadrant tooltip**~~ — **DONE (v1.0.0, 2026-04-16)**
- ~~**Nexus release DCIM_v1.0.0.zip (no HackingSystem)**~~ — **DONE (2026-04-16)** — deployed and tested in-game
- ~~**DCIM screen does not fit laptop frame**~~ — **FIXED (v1.0.1, confirmed 2026-04-16)**: Root cause was parenting `DCIMScreen` to the canvas root (full-screen) instead of `mainScreen.transform.parent` (the screens container that defines the laptop panel bounds). Fix: parent to `mainScreen.transform.parent`, copy mainScreen RT after one frame, re-sync in `OnAppOpened`.
- ~~**Floor Map Overhaul**~~ — **DONE (v1.1.0, 2026-04-17)**: Stats strip, 5-level EOL status (2h/4h thresholds), filter bar, customer filter on logo click, utilization fill bars, multi-select + action bar, RackDiagramPanel (U-slot layout + mini shop + color swatches + favorite colors), PopulateMultiRack in DeviceListView.
- ~~**Rack custom color picker + persistence**~~ — **DONE (v1.1.1, 2026-04-17)**: RGB sliders, 8 saved favorite swatches, rack colors persist across sessions via MelonPreferences.
- ~~**EOL threshold fix (floor map)**~~ — **DONE (v1.1.2–v1.1.8, 2026-04-17)**: eolTime semantics confirmed — counts down, negative = past deadline. EOL warning uses `eolTime < -threshold` to match game's auto-repair window. autoRepairMode values confirmed (2=2h, 3=4h). Floor map live refresh coroutine (3s tick).
- ~~**Live EOL timers everywhere**~~ — **DONE (v1.1.3, 2026-04-17)**: UIHelper EOL helpers, live countdown in RackDiagramPanel + DeviceListView + SearchResultsView.
- ~~**Rack diagram multi-select power + assign**~~ — **DONE (v1.1.4, 2026-04-17)**: Checkboxes per slot row, Power ON/OFF/Assign Customer action bar.
- ~~**ON/OFF status accuracy fix**~~ — **DONE (v1.1.5, 2026-04-17)**: Power state independent of EOL state. UIHelper.GetDeviceState() added.
- ~~**Back nav + Power OFF + checkbox visibility**~~ — **DONE (v1.1.6, 2026-04-17)**.
- ~~**Rack diagram EOL timer + layout polish**~~ — **DONE (v1.1.7, 2026-04-17)**: EOL label in diagram rows, multi-U alignment, larger logos, IP overlap fix.
- ~~**v1.0.0 published to Nexus**~~ — **DONE (2026-04-17)**: `DCIM-1.0.0.zip` live. Hacking system stripped via `STRIP_HACKING` preprocessor define in Release build config.
- ~~**HSV color picker upgrade**~~ — **DONE (v1.0.1, 2026-04-19)**: H/S/V sliders with gradient track backgrounds, hex input field, replaces RGB sliders.
- ~~**Recolor existing rack (single)**~~ — **DONE (v1.0.1, 2026-04-19)**: "Recolor" button in rack diagram header, pre-loads picker with current color, applies immediately without purchase.
- ~~**Mass recolor (multi-select)**~~ — **DONE (v1.0.1, 2026-04-19)**: "Recolor" button in floor map multi-select action bar, applies one color to all selected racks.
- ~~**Buy device from rack diagram mini shop**~~ — **FIXED (v1.0.1, 2026-04-19)**: Was missing `ButtonChosenColor()` + `ButtonCheckOut()` after `ButtonBuyShopItem`. Full cart+checkout flow now called.
- ~~**Aisle labels hidden behind racks**~~ — **FIXED (v1.0.1, 2026-04-19)**: Moved `BuildAisleLabels()` call to after all tile loops; z-order now correct.
- ~~**Demolish + rebuild restores old color**~~ — **FIXED (v1.0.1, 2026-04-19)**: `RemoveRackColor()` called when buying rack with no color; stale prefs entry no longer survives rebuild.
- **[OPEN] Rack naming** — Custom label per rack stored in `DCIM_RackNames` MelonPreferences. Shown on floor map tile (center text), rack diagram panel header, and 3D rack label. Editable from rack diagram header (inline TMP_InputField). NOT STARTED.
- **[OPEN] Revenue per rack** — Show estimated revenue on rack tile and rack diagram header. Computed by scanning assigned customers' BalanceSheet data and distributing by server count per rack. NOT STARTED.
- ~~**Bulk power from floor map**~~ — **DONE (v1.0.2, 2026-04-19)**: Power ON/OFF buttons in multi-select action bar. Scans devices by rack transform hierarchy membership.
- ~~**Tab bar navigation**~~ — **DONE (v1.0.2, 2026-04-19)**: Persistent Dashboard | Floor Map | Search tab bar below header. Active tab has cyan underline. Shows on top-level views, hidden on drill-downs.
- ~~**Row/aisle inline checkboxes**~~ — **DONE (v1.0.2, 2026-04-19)**: R1/R2 and Aisle A/B labels swap to in-place checkboxes when select mode is active. Green/amber/dark fill for all/partial/none selected. Removed bottom quick-select bar.
- ~~**Mass buy from floor map**~~ — **DONE (v1.0.2, 2026-04-19)**: Empty mount slots selectable in select mode (blue overlay), "Buy Slots (N)" action bar button opens existing HSV picker popup for color + confirm. All selected slots built in one confirm; color applied to each via `ApplyRackColorDelayed`.
- **[OPEN] UI design system + polish pass** — Dark terminal, polished, game-native aesthetic. Design tokens in UIHelper, consistent components across all views, contextual navigation links between screens. NOT STARTED.
- **Hacking System (`#if !STRIP_HACKING`)** — **ON HOLD. Stripped from all builds.** Backend logic complete. What remains is entirely the UI layer. To resume: remove `STRIP_HACKING` from the unconditional PropertyGroup in `FloorManager.csproj`.
  - **Chunks 1–3** — DONE and tested in-game (wave scheduling, HP damage, breach, EOL acceleration, designation buttons in DeviceConfigPanel, Harmony repair patches)
  - **Backend complete (implemented in HackingSystem.cs, no UI yet):**
    - Hacker profiles × 4 (Script Kiddie / Hacktivist / Cybercriminal / APT-9) with DPS/freq/persistence/disengage multipliers
    - IDS DPS reduction (each IDS switch reduces incoming DPS by 30%, capped at 60%)
    - Counter-trace (`UseCounterTrace()` — unlocks at 3 successive defenses, reveals hacker name/specialty)
    - Reputation system (`_rep` 0–100, `OnRepLoss()`, +2 rep/day recovery, `OnRepLoss` on breach)
    - Revenue penalty (`ApplyRevenuePenalty()` — scales daily revenue loss with rep deficit)
    - Target attractiveness (`CalculateAttractiveness()` = daily revenue × 0.5 + rep × 10)
    - Disengagement + dormant (`EnterDormant()` — 3–7 day random window, re-engages if attractiveness > 1200)
    - Offsite firewall (`SubscribeOffsite(tier)` / `CancelOffsite()` — absorbs first, overloads when depleted, regen per day)
    - Lockdown (`ActivateLockdown()` — 120s duration, $15/sec cost, 300s cooldown, pauses wave damage)
    - Ransomware (`ApplyRansomware()` — ransoms 1–3 servers, 120s timer, `PayRansom()`, `OnRansomExpired()`)
    - Data Exfiltration wave type (fires for APT-9 profile at 40% chance)
    - Threat level string (`GetThreatLevel()` → None/Dormant/Low/Elevated/Active/Critical)
    - Attack log (last 50 entries, `[DateString] msg` format)
    - All state persisted via MelonPreferences (firewalls, IDS, honeypots, offsite, rep, elapsed time, wave timer, dormant timer, hacking enabled flag)
  - **UI NOT STARTED — full list:**
    - `SecurityView.cs` — new file, entire security tab (NOT DONE)
    - `ViewState.Security` in FloorMapApp + nav button + Back navigation (NOT DONE)
    - Dashboard security widget — rep chip, threat level display (NOT DONE)
    - Red badge on DCIM app button when attack active (NOT DONE)
    - Ransom indicators in DeviceConfigPanel, CustomerIPView, SearchResultsView (NOT DONE)
    - Offsite subscription UI (NOT DONE)
    - Lockdown button UI (NOT DONE)
    - IDS early-warning display / counter-trace reveal button (NOT DONE — go in SecurityView)
- **Returning to hacking dev build** — All UI work above targets the dev build. All Nexus improvements go into both builds when resumed.

## Known Issues

- ~~**DCIM screen does not fit laptop frame**~~ — **FIXED (v1.0.1, 2026-04-16)** — See v1.0.1 test history.

- **LACP config exit breaks laptop — FIXED (v0.2.1)** — Root cause: game's `CloseAnyCanvas()` calls `LockedCursorForPlayerMovement()` which locks cursor and re-enables camera movement. Fix: after detecting LACP canvas close, call `InputManager.ConfinedCursorforUI()` + force `Cursor.visible = true` + `Cursor.lockState = CursorLockMode.Confined` + disable `enabledMouseMovement`/`enabledPlayerMovement` on PlayerManager before re-showing DCIM screen.

- **Switch select all not working — FIXED (v0.2.1)** — Checkboxes were only created for servers (`device.IsServer`). Switches had no checkboxes, so Select All and bulk power only affected servers. Fix: added checkboxes for switches in both DeviceListView and SearchResultsView, added parallel tracking lists (`_selectedSwitches`, `_switchCheckboxImages`, `_allSwitchesInRack`), updated power ON/OFF handlers to also call `sw.PowerButton()`.

- **Blue box — FIXED** — Killed globally via `OnLateUpdate` clearing `EventSystem.current.selectedGameObject` every frame. Also fixes base game buttons.

- **Dividers not tracked for cleanup** — `UIHelper.BuildDivider()` creates elements that aren't added to `_elements` (DeviceConfigPanel) or `_rows` (CustomerIPView). On repeated `Populate()` calls, dividers accumulate. Minor visual/memory issue.

- **Clock feature removed** — Scrapped due to IL2CPP TMP font loading issues.

---

## What This Mod Does

Adds a **DCIM** (Data Center Infrastructure Management) laptop app as a standalone mod (runs alongside AutoDispatcher):
   - **Dashboard home screen** — summary stats, alert chips, quick filters, customer list with revenue
   - **Floor map** — top-down wireframe with rack squares (5-level status colors: green/amber/yellow/red/gray matching 2h/4h EOL thresholds), stats strip, filter bar, customer filter toggle on logo click, utilization fill bars, multi-select with action bar (View Devices / Assign Customer), rack click opens diagram panel (U-slot layout + mini shop for empty slots), customer squares (logos), empty positions ("+" to buy)
   - **Search/filter** — filter devices by type/status/customer/color, bulk power ON/OFF, column headers, alternating rows, ON/OFF badges, EOL countdown
   - **Device config** — remote power, IP, customer, LACP, rack label, port count
   - **Customer IPs** — all active IPs per customer with revenue/penalty summary
   - **3D rack labels** — physical world-space labels on every rack matching FloorMap numbering
   - **Buy racks** — click empty positions on FloorMap to purchase and place racks

No dispatch logic. No technician interaction. Pure remote visibility and control.

---

## Save Analysis — Confirmed Scale

Extracted from `AutoSave_2026-04-11_23-28-20.save` (1.2 MB):

- **335 servers** across 8 types (Blue1, Blue2, Green1, Green2, Purple1, Purple2, Yellow1, Yellow2)
- **83 switches** (74x Switch4xQSXP16xSFP, 9x Switch32xQSFP)
- **27 active subnets**
- **Room footprint** (device positions): X ~= -12 to +16, Z ~= -18 to 0
- **Rack rows** at approximately Z = -14, -13, -10, -9 (parallel aisles)
- **UI implication**: Floor map must be scrollable/pannable. 418 devices across ~52+ racks.

---

## Feature 1 — Clock Overlay

### Data Source — Confirmed

```csharp
TimeController.instance.CurrentTimeInHours()   // float, e.g. 14.5 = 14:30
TimeController.instance.day                    // int, current day number
TimeController.instance.secondsInFullDay       // float, total seconds per game day
```

`TimeController` is a singleton (`public static TimeController instance`). Available after scene load.

### Display Format

```
Day 4    14:32
```

Updates every second via coroutine.

### Implementation

- Screen-space overlay canvas (`RenderMode.ScreenSpaceOverlay`, `sortingOrder = 100`)
- `DontDestroyOnLoad` — persists across scene loads
- Anchored **top-right corner**, `TextMeshProUGUI` label with a semi-transparent dark background panel
- Created once in `OnInitializeMelon`. Coroutine runs an infinite `while(true)` loop with a 1-second wait — if `TimeController.instance == null` (main menu), it skips the update but keeps looping. **Never use `yield break`** — that kills the coroutine permanently and the clock stops after the first scene change.

```csharp
private IEnumerator ClockUpdateCoroutine() {
    while (true) {
        if (TimeController.instance != null) {
            _clockCanvas.SetActive(true);
            float hours = TimeController.instance.CurrentTimeInHours();
            int day = TimeController.instance.day;
            int h = (int)hours;
            int m = (int)((hours - h) * 60f);
            _clockLabel.text = $"Day {day}    {h:00}:{m:00}";
        } else {
            _clockCanvas.SetActive(false);
        }
        yield return new WaitForSeconds(1f);
    }
}
```
- Coroutine started via `MelonCoroutines.Start()` in `OnInitializeMelon`. Runs for the lifetime of the mod — no cleanup needed since MelonLoader mods persist until game exit.
- Toggle via `MelonPreferences` key `FloorManager.ClockEnabled`

---

## Feature 2 — Floor Manager App

### Visual Style — Wireframe

- **Dark background** (near-black, e.g. `Color(0.08, 0.08, 0.10, 0.97)`)
- **Wireframe outlines** for walls, rack groupings, and rack squares
- **Device type indicated by text color** in the device list (yellow server = yellow text, etc.)
- Clean, schematic look

### Navigation Flow — 7 Views (v1.0.0)

```
Laptop Main Screen
  +-- [DCIM] app button  [red badge when attack active]
       +-- Dashboard (HOME)              <-- stats, alerts, filters, customer list, security widget
            +-- Floor Map                <-- top-down wireframe map + empty positions + tooltips
            |    +-- Device List         <-- click rack tooltip "Details >" -> multi-select + power
            |    |    +-- Device Config  <-- click device row -> remote control panel
            |    +-- [Buy Rack]          <-- click empty "+" position -> confirmation popup
            +-- Search Results           <-- filtered device list, bulk power ON/OFF
            |    +-- Device Config       <-- click result row -> remote control panel
            +-- Customer IP View         <-- click customer row -> IPs + revenue summary
            |    +-- [Add Server popup]  <-- add unassigned servers to customer
            +-- Security                 <-- [NOT YET IMPLEMENTED] threat level, firewall chain, IDS,
                                             honeypot, lockdown, counter-trace, attack log
```

Back navigation:
- Dashboard -> [Back] -> Laptop Main Screen
- Floor Map -> [Back] -> Dashboard
- Device List -> [Back] -> Floor Map
- Device Config -> [Back] -> Device List OR Search Results (whichever originated the navigation)
- Search Results -> [Back] -> Dashboard
- Customer IP View -> [Back] -> Dashboard
- Security -> [Back] -> Dashboard

### Color Legend

A color reference panel in the **top-right corner** of the Floor Manager screen (visible only on floor map view, hidden on other views).

**Device Type Colors (used as text color in device list):**
| Type Name Contains | Color | RGB |
|---|---|---|
| "Blue" | Blue | `(0.3, 0.5, 1.0)` |
| "Green" | Green | `(0.3, 0.8, 0.3)` |
| "Purple" | Purple | `(0.6, 0.3, 0.9)` |
| "Yellow" | Yellow | `(1.0, 0.85, 0.2)` |
| Switch (any type) | Cyan | `(0.0, 0.8, 0.8)` |
| Patch Panel | Gray | `(0.5, 0.5, 0.5)` |
| Unknown/fallback | White | `(1.0, 1.0, 1.0)` |

**Status Colors (used on rack squares in floor map):**
| Status | Color |
|---|---|
| Broken device present | Red `(0.9, 0.2, 0.2)` |
| EOL device present | Yellow/Orange `(1.0, 0.7, 0.1)` |
| All healthy | Green `(0.2, 0.8, 0.2)` |
| Empty rack | Gray `(0.4, 0.4, 0.4)` |

Color is derived at runtime from the **game object name** (e.g. `Server.Purple2(Clone)`):
```csharp
string objName = server.gameObject.name;
// Contains "Blue" -> blue, "Green" -> green, "Purple" -> purple, "Yellow" -> yellow
// DO NOT use ReturnServerNameFromType() — returns product names like "RISC 3U 5000 IOPS"
// which do NOT contain color keywords
```

### Adding the App to the Laptop — Confirmed Pattern

Patch `ComputerShop.Awake` (postfix):
1. Create `floorManagerScreen` GameObject as child of `canvasComputerShop`
2. RectTransform: copied from `mainScreen` (anchor fill with 10px inset: offsetMin=10,10 offsetMax=-10,-10)
3. Add icon button to `mainScreen` layout group
4. Patch `ButtonReturnMainScreen` to hide `floorManagerScreen` on any back navigation

**Timing note:** `canvasComputerShop` and `mainScreen` are assigned in `Awake` (they're serialized Unity fields), so a postfix on `Awake` is safe — they're already populated. If testing reveals they're null in the postfix, move the patch to `Start` instead.

---

## View 1 — Floor Map (Top-Down Wireframe)

### What It Shows

A top-down wireframe map of the data center:
- **Dark background** — near-black content area
- **Customer squares** at top — sequential row of logo sprites, clickable → Customer IP View
- **Rack squares** below — placed at world (X, Z) positions with direct coordinate mapping
- **Rack square border color** = worst-case device status (Red/Yellow/Green/Gray)
- **Aisle gaps** appear naturally from world-space distance between rack groups
- **Row labels** (R1–R5) on the left side
- **Scrollable/pannable** — ScrollRect with both horizontal and vertical enabled
- Click a rack square → opens Device List for that rack
- Click a customer square → opens Customer IP View for that customer

### Wall System — Confirmed from Research

```csharp
// Wall class: extends Interact (clickable world object)
// Key field: bool isWallOpened — true = purchased/expanded, false = not yet bought

var walls = Object.FindObjectsOfType<Wall>();
foreach (var wall in walls) {
    Vector3 pos = wall.transform.position;
    bool opened = wall.isWallOpened;
}

// MainGameManager wall-related fields:
MainGameManager.instance.walls          // GameObject — parent container for all walls
MainGameManager.onBuyingWallEvent       // delegate -> fires when player buys a wall
```

### Wall Geometry — Runtime Discovery (Step 0)

Wall dimensions, orientation, and how they form room boundaries **cannot be determined from decompiled source** (method bodies are IL2CPP stubs). We need a one-time runtime discovery pass.

**Discovery logs ALL spatial data — walls AND rack mounts:**
```csharp
// Wall discovery
var walls = Object.FindObjectsOfType<Wall>();
MelonLogger.Msg($"=== WALL DISCOVERY: {walls.Length} walls ===");
foreach (var wall in walls) {
    var pos = wall.transform.position;
    var rot = wall.transform.eulerAngles;
    var scl = wall.transform.localScale;
    var col = wall.GetComponent<Collider>();
    string boundsStr = col != null
        ? $"bounds center={col.bounds.center} size={col.bounds.size}"
        : "no collider";
    MelonLogger.Msg($"Wall pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                    $"rot=({rot.x:F0},{rot.y:F0},{rot.z:F0}) " +
                    $"scale=({scl.x:F2},{scl.y:F2},{scl.z:F2}) " +
                    $"opened={wall.isWallOpened} {boundsStr}");
}

// RackMount discovery
var rackMounts = Object.FindObjectsOfType<RackMount>();
MelonLogger.Msg($"=== RACKMOUNT DISCOVERY: {rackMounts.Length} mounts ===");
foreach (var rm in rackMounts) {
    var pos = rm.transform.position;
    MelonLogger.Msg($"RackMount pos=({pos.x:F2},{pos.y:F2},{pos.z:F2}) " +
                    $"installed={rm.isRackInstantiated}");
}
```

**Fallback plan:** If wall geometry is too complex or irregular, fall back to:
- Show all RackMount positions (racks only exist in opened space)
- Draw a bounding region around them
- Dark out everything outside

### Visibility Masking

- **Opened area** = visible on the map (rack positions shown)
- **Unopened area** = blacked out / hidden
- Refreshed each time the floor map is opened

**How masking works:** RackMounts only exist in opened/purchased areas — the game doesn't spawn them behind unpurchased walls. So the simplest approach is: only draw racks where `RackMount` objects exist. No need to cross-reference wall state with rack positions. Walls are used only for drawing room boundary outlines (if wall geometry is clean enough from discovery). If wall geometry is too irregular, skip wall outlines entirely and just show rack groupings floating on the dark background.

### Rack Layout — Direct Coordinate Mapping

~~Column grouping algorithm was tried and abandoned~~ — racks are placed at their actual (X, Z) world positions.

**Algorithm (implemented):**
1. `FindObjectsOfType<Rack>()` directly — validate each has a `RackMount` parent via `GetComponentInParent<RackMount>()`
2. Find distinct X and Z values (snapped with 0.3 tolerance to handle float imprecision)
3. Sort distinct X values **descending** (reversed — matches physical right-to-left layout), distinct Z values front-to-back
4. Map each distinct X to a screen X position, each distinct Z to a screen Y position
5. Where `Mathf.Abs()` of world-space gap between adjacent X or Z values exceeds 1.5 units, insert an aisle gap (16px instead of 4px)
6. Place each rack square at its grid (X index, Z index) screen position
7. Z is reversed: highest Z (furthest back) renders at the top of the map
8. Customer squares rendered in sequential row at top, offset rack grid down by CUSTOMER_ROW_HEIGHT

**Rack square size:** 40×40 pixels. Content area grows to fit all racks + padding. ScrollRect handles overflow.

### Rack Square Status Color

```csharp
// CONFIRMED by AutoDispatcher (deployed, working):
// eolTime <= 0 = EOL expired, eolTime > 0 = time remaining
// existingWarningSigns is PRIVATE — do not access
var status = Gray;  // assume empty
for (int i = 0; i < rack.positions.Length; i++)  // use .Length, not hardcoded 8
{
    var rp = rack.positions[i];
    var srv = rp.GetComponentInChildren<Server>();
    if (srv != null)
    {
        if (status == Gray) status = Green;
        if (srv.isBroken)     { status = Red; break; }
        if (srv.eolTime <= 0) { status = Yellow; }
    }
    var sw = rp.GetComponentInChildren<NetworkSwitch>();
    if (sw != null)
    {
        if (status == Gray) status = Green;
        if (sw.isBroken)     { status = Red; break; }
        if (sw.eolTime <= 0) { status = Yellow; }
    }
}
```

### Refresh Strategy

All data is pulled **synchronously when the view is opened**:
1. `FindObjectsOfType<RackMount>()` — returns ~52 objects, instant
2. For each rack, read `rack.positions` and check device status — ~416 component lookups, instant
3. `FindObjectsOfType<Wall>()` — wall visibility masking
4. Build/update UI elements

No background timer. No async loading. Everything completes within one frame.

**Known limitation:** If the player installs a new rack or a device breaks while the floor map is already open, the view won't update until they navigate away and back. Acceptable for v1. Future improvement: add optional timer-based refresh while the view is open.

---

## View 2 — Device List

### What It Shows

When the player clicks a rack square on the floor map, a scrollable list of all devices in that rack appears, ordered top-to-bottom matching physical slot order.

```
+-------------------------------------------+
|  Column 3 > Rack 2         [Back]         |
+-------------------------------------------+
|  [1] Server.Blue2    192.168.1.10    * ON  |  <- blue text
|  [2] Server.Yellow2  10.2.13.5       * ON  |  <- yellow text
|  [3] Server.Yellow2  10.2.13.6       X BRK |  <- yellow text, red status
|  [4]  -- empty --                          |  <- gray text
|  [5] Switch4xQSXP    Core-SW-01     * ON  |  <- cyan text
|  [6]  -- empty --                          |
|  [7]  -- empty --                          |
|  [8]  -- empty --                          |
+-------------------------------------------+
```

### Device Row

- **Text color** = device type color (Blue server = blue text, Yellow = yellow, Switch = cyan, Patch Panel = gray)
- Shows: slot number, device type name, IP (server) or label (switch), power/status indicator
- **Status indicators:** `*` = on (green), `O` = off (red dot), `X` = broken (red), `!` = EOL (orange)
- **Empty slots:** gray text "-- empty --", not clickable
- **Click a device row** -> opens Device Config panel (server/switch) or read-only info panel (patch panel)
- **2U/3U devices** span their slots but show as a single row (skip spanned slots)

### Data Source

```csharp
rack.positions[]           // RackPosition[] — use .Length for slot count
rack.isPositionUsed[]      // int[] — occupation flags

rackPosition.GetComponentInChildren<Server>()
rackPosition.GetComponentInChildren<NetworkSwitch>()
rackPosition.GetComponentInChildren<PatchPanel>()
```

### 2U/3U Slot Spanning

```csharp
for (int i = 0; i < rack.positions.Length; i++)
{
    var rp = rack.positions[i];
    var device = rp.GetComponentInChildren<Server>()
              ?? (UsableObject)rp.GetComponentInChildren<NetworkSwitch>()
              ?? (UsableObject)rp.GetComponentInChildren<PatchPanel>();
    if (device != null)
    {
        // Render device row — text color based on type
        i += device.sizeInU - 1;  // skip spanned slots
        continue;
    }
    if (rack.isPositionUsed[i] > 0)
        continue;  // spanned by multi-U device above
    // Empty slot — render gray "-- empty --" row
}
```

---

## View 3 — Device Config Panel

The device config panel mirrors the **physical walk-up interaction UI** — the same controls you get when clicking a server or switch in person, but accessed through the laptop.

### Server Controls — All APIs Confirmed

| Control | API | Notes |
|---|---|---|
| Power on/off | `server.PowerButton()` | Toggles `server.isOn`. **Always call with NO arguments.** Disable button when `isBroken == true`. |
| Read power state | `server.isOn` | bool, public |
| Read IP | `server.IP` | string, public |
| Set IP (manual) | `Object.FindObjectOfType<SetIP>().ShowCanvas(server)` | Opens the game's native numeric keypad overlay. Proven to work, no custom text input needed. |
| Set IP (direct) | `server.SetIP(string ip)` | Direct write. Used by auto-fill. |
| Read customer | `server.GetCustomerID()` | int |
| Change customer | `server.ButtonClickChangeCustomer(bool forward)` | Cycle buttons (prev/next), matches physical UI. `true` = next, `false` = prev. |
| Status | `server.isBroken`, `server.eolTime`, `server.isOn` | Read-only display |

**Auto-Fill IP on Customer Selection:**

When the player changes the customer, auto-assign a valid IP from that customer's subnet:

```csharp
// 1. Find the CustomerBase for the selected customer
var customerBases = MainGameManager.instance.customerBases;
CustomerBase targetBase = null;
for (int i = 0; i < customerBases.Length; i++) {
    if (customerBases[i].customerID == selectedCustomerID) {
        targetBase = customerBases[i];
        break;
    }
}

// 2. Get subnets for this customer's apps
// IL2CPP Dictionary — use _entries array, NOT GetEnumerator() (crashes in IL2CPP)
var subnetsPerApp = targetBase.GetSubnetsPerApp();  // Dictionary<int, string>
if (subnetsPerApp == null || subnetsPerApp.Count == 0) return;
string subnet = null;
var entries = subnetsPerApp._entries;
for (int ei = 0; ei < entries.Length; ei++) {
    if (entries[ei].hashCode >= 0) {
        subnet = entries[ei].value;  // pick first valid entry
        break;
    }
}

// 3. Get available IPs in that subnet
var setIP = Object.FindObjectOfType<SetIP>();
string[] usableIPs = setIP.GetUsableIPsFromSubnet(subnet);

// 4. Assign first available IP
if (usableIPs != null && usableIPs.Length > 0) {
    server.SetIP(usableIPs[0]);
    StaticUIElements.instance.AddMeesageInField($"IP auto-assigned: {usableIPs[0]}");
}
```

**Note:** `existingCustomerIDs` is an Il2Cpp `List<int>` — use index loop (`for i < Count`), NOT foreach.

**Server Config Layout:**
```
+--- Server.Blue2 --- 2U Blue ---- * Online ---+
|                                                |
|  Power:      [ ON ]  [ OFF ]                   |
|  IP:         192.168.1.10    [Change IP]       |
|  Customer:   [ < ]  Customer 3  [ > ]          |
|              (auto-assigns IP on change)        |
|                                                |
|  EOL Timer:  240:00 (MM:SS)                    |
|  Processing: 125.0 / 200.0 IOPS               |
+------------------------------------------------+
```

- [Change IP] opens the game's native SetIP keypad overlay
- Customer [<] [>] buttons cycle via `ButtonClickChangeCustomer`
- Changing customer auto-fills a valid IP from the new customer's subnet

Text color matches device type.

### Switch Controls — All APIs Confirmed

| Control | API | Notes |
|---|---|---|
| Power toggle | **DISABLED in v1** | Cascade risk — `DisconnectCablesWhenSwitchIsOff()` severs all connections |
| Read power state | `networkSwitch.isOn` | Read-only status indicator |
| Read label | `networkSwitch.label` | string, public |
| Set label | Write `networkSwitch.label` directly | Persists via SwitchSaveData.label |
| Open game config UI | `MainGameManager.instance.ShowNetworkConfigCanvas(networkSwitch)` | **Test early — may fail remotely. Fallback: message telling player to visit switch physically.** |
| Connected devices | `networkSwitch.GetConnectedDevices()` | Returns `List<(string, int)>`. Use index loop (`for i < list.Count`), NOT foreach. `.Item1`=deviceName, `.Item2`=cableId. |
| Status | `networkSwitch.isBroken`, `networkSwitch.eolTime`, `networkSwitch.isOn` | Read-only display |

**v1 Design Decision — Switch Power Disabled:**
- **v1:** No power buttons. Power state shown as read-only indicator.
- **v2 (future):** Add power toggle with confirmation dialog showing active port count.

**LACP — v1 strategy:** Open game's native config UI via `MainGameManager.instance.ShowNetworkConfigCanvas(networkSwitch)`. Inline LACP management is v2.

**NetworkSwitchConfiguration public API (confirmed for v2):**
```csharp
NetworkSwitchConfiguration.OpenConfig(NetworkSwitch)
NetworkSwitchConfiguration.ClickPort(int)
NetworkSwitchConfiguration.CreateLACP()
NetworkSwitchConfiguration.RemoveLACP()
NetworkSwitchConfiguration.ButtonEditLabel()
NetworkSwitchConfiguration.CloseConfig()
```

**Switch Config Layout:**
```
+--- Switch4xQSXP16xSFP --- Core-SW-01 --- * Online ---+
|                                                        |
|  Status:   * Online  (power control disabled in v1)    |
|  Label:    [ Core-SW-01 ______________ ]   [Apply]     |
|                                                        |
|  Connected Ports:                                      |
|    Port  1 -> Server.Blue2 (192.168.1.10)              |
|    Port  2 -> Server.Blue2 (192.168.1.11) [LACP Grp 2] |
|    Port  3 -> PatchPanel-A                             |
|    Port  4 -> (empty)                                  |
|                                                        |
|  [ Configure LACP... ]   <- opens game's native UI     |
+---------------------------------------------------------+
```

Text color: Cyan.

### Patch Panel — Read-Only Info Panel

Patch panels have no configurable state. Clicking a patch panel row opens a simple read-only panel:

```
+--- PatchPanel --- 1U --- Slot 3 ---+
|                                     |
|  Type:   Patch Panel                |
|  Size:   1U                         |
|  Slot:   3                          |
|                                     |
|  (No configurable options)          |
+-------------------------------------+
```

Text color: Gray. No buttons, no actions — just identification info so the player knows what's in the slot.

### Early Test Risks

These APIs must be tested early (step 8) because they may behave differently when called remotely from the laptop vs. at the physical device:

1. **`server.PowerButton()`** — may trigger animations or proximity checks. If it fails remotely, fallback to directly toggling `server.isOn` + calling any required side-effect methods.
2. **`SetIP.ShowCanvas(server)`** — the native keypad overlay may render in world-space near the server rather than on top of the laptop screen. If it fails, fallback to `server.SetIP()` with a simple text input field built into the config panel.
3. **`MainGameManager.instance.ShowNetworkConfigCanvas(networkSwitch)`** — same world-space concern. Fallback: message "visit switch physically to configure LACP."
4. **`GetConnectedDevices()` ValueTuple** — `.Item1`/`.Item2` may not resolve in IL2CPP. If it fails, use reflection or skip port list display.

### HUD Feedback

```csharp
// Message log (bottom-left) — note the typo is in the game
StaticUIElements.instance.AddMeesageInField("Server powered off");

// Notification banner (top center)
StaticUIElements.instance.SetNotification(0, null, "IP updated to 10.2.0.5");
```

---

## Architecture — Files

| File | Purpose |
|---|---|
| `FloorManager.csproj` | Project file. net6.0, references MelonLoader + game assemblies + PhysicsModule. |
| `FloorManagerMod.cs` | MelonLoader entry. Prefs, clock coroutine start, `SaveSystem.onLoadingDataLater` discovery, mainScreen hierarchy dump. |
| `ClockOverlay.cs` | Screen-space overlay canvas (DontDestroyOnLoad). `TimeController` coroutine, 1s update. |
| `UIHelper.cs` | Shared UI builders (button, label, divider) + device type/status color constants. |
| `FloorMapApp.cs` | 4-state navigation manager (FloorMap / DeviceList / DeviceConfig / CustomerIPs). Color legend (top-right). Header bar with back button + action button. |
| `FloorMapView.cs` | Top-down wireframe map. Direct coordinate mapping (world X→screen X, world Z→screen Y). Reversed X sort for correct orientation. Aisle gaps from world-space distance. Customer squares row at top. |
| `DeviceListView.cs` | Scrollable device list for a single rack. Type-colored text rows (color from game object name). |
| `DeviceConfigPanel.cs` | Server config (power, IP via SetIP, customer cycle with logo/name + auto-fill). Switch config (label, port list, LACP). Patch panel (read-only info). |
| `CustomerIPView.cs` | Customer IP list — shows all active server IPs for a customer with type, IP, status columns. Customer logo + name header. |
| `Patches/ComputerShopPatch.cs` | Patches `ComputerShop.Awake` (postfix — injects screen + button into GridLayoutGroup) and `ButtonReturnMainScreen` (hides FM screen). |

---

## Navigation State Machine

4-state enum — no stack needed (fixed hierarchy):

```csharp
enum ViewState { FloorMap, DeviceList, DeviceConfig, CustomerIPs }

// On state change:
// 1. Hide all view GameObjects
// 2. Show the target view GameObject
// 3. If entering DeviceList: populate with rack data
// 4. If entering DeviceConfig: populate with device data
// 5. If entering CustomerIPs: populate with customer's servers/IPs
// Back: DeviceConfig -> DeviceList -> FloorMap -> Laptop
//       CustomerIPs -> FloorMap -> Laptop
```

---

## Confirmed API Reference

### Clock
```csharp
TimeController.instance.CurrentTimeInHours()   // float -> format as HH:mm
TimeController.instance.day                    // int -> "Day N"
TimeController.instance.secondsInFullDay       // float -> real-time seconds per game day
```

### Walls
```csharp
Object.FindObjectsOfType<Wall>()               // all wall objects in scene
wall.isWallOpened                              // bool -> true = purchased, false = not yet bought
wall.transform.position                        // Vector3 -> world position
MainGameManager.instance.walls                 // GameObject -> parent container
MainGameManager.onBuyingWallEvent              // delegate -> fires on wall purchase
```

### Room Layout
```csharp
Object.FindObjectsOfType<RackMount>()           // all rack locations in scene
rackMount.isRackInstantiated                    // bool -> rack installed here?
rackMount.transform.position                    // Vector3 -> world X,Z for floor plan
rackMount.GetComponentInChildren<Rack>()        // get installed rack
```

### Rack Contents
```csharp
rack.positions[]                                // RackPosition[] — use .Length for slot count
rack.isPositionUsed[]                           // int[] — occupation flags

rackPosition.GetComponentInChildren<Server>()          // null if not a server
rackPosition.GetComponentInChildren<NetworkSwitch>()   // null if not a switch
rackPosition.GetComponentInChildren<PatchPanel>()      // null if not a patch panel

rackPosition.rackPosGlobalUID    // int — global UID
rackPosition.positionIndex       // int — slot index (0-based)
rackPosition.rack                // Rack — back-reference
```

### All Devices
```csharp
// Use FindObjectsOfType for bulk iteration (returns Il2CppArrayBase — use indexed for-loop, NOT foreach):
var servers  = Object.FindObjectsOfType<Server>();
var switches = Object.FindObjectsOfType<NetworkSwitch>();

// Single lookup by ID:
NetworkMap.instance.GetServer(string serverId);
NetworkMap.instance.GetSwitchById(string switchId);

// DO NOT foreach on: GetAllServers(), GetAllNetworkSwitches() (Il2Cpp IEnumerable unsafe)
```

### Server Config
```csharp
server.ServerID                   // string — unique ID
server.serverType                 // int — type index
server.IP                         // string, public
server.appID                      // int — assigned application
server.maxProcessingSpeed         // float
server.currentProcessingSpeed     // float
server.timeToBrake                // int — countdown to breakdown
server.eolTime                    // int — EOL countdown in real seconds.
                                  // Counts DOWN. Positive = seconds remaining. Negative = seconds
                                  // past EOL deadline (still running, not yet repaired). Zero = just expired.
                                  // CONFIRMED via debug logging (2026-04-17): all installed servers have
                                  // non-zero eolTime. No "healthy default = 0" state exists.
server.isBroken                   // bool
server.isOn                       // bool
server.sizeInU                    // int — rack unit height (from UsableObject)
server.SetIP(string ip)           // set IP directly (used by auto-fill)
server.GetCustomerID()            // -> int
server.UpdateCustomer(int id)     // reassign customer
server.ButtonClickChangeCustomer(bool forward)  // cycle customer (true=next, false=prev)
server.currentRackPosition        // RackPosition — direct slot reference
MainGameManager.instance.IsSubnetValid(string subnet)
MainGameManager.instance.existingCustomerIDs             // List<int> — IL2CPP, use index loop NOT foreach
MainGameManager.instance.customerBases                   // CustomerBase[] — find base by customerID
MainGameManager.instance.GetCustomerItemByID(int)        // CustomerItem — has .customerName (string) and .logo (Sprite)
MainGameManager.instance.ReturnServerNameFromType(int)   // display name (product name, NOT color — e.g. "RISC 3U 5000 IOPS")
MainGameManager.instance.ReturnSwitchNameFromType(int)   // display name
```

### Customer & IP Auto-Fill
```csharp
// CustomerBase — find by customer ID
MainGameManager.instance.customerBases                   // CustomerBase[] — iterate to find by customerID
customerBase.customerID                                  // int — matches server.GetCustomerID()
customerBase.GetSubnetsPerApp()                          // Dictionary<int, string> — appID -> subnet (public)
customerBase.IsIPPresent(string ip)                      // bool — check if IP already in use
customerBase.GetAppIDForIP(string ip)                    // int — reverse lookup

// SetIP — game's IP management system
Object.FindObjectOfType<SetIP>()                         // singleton-like, one in scene
setIP.ShowCanvas(Server server)                          // open native numeric keypad for manual IP entry
setIP.GetUsableIPsFromSubnet(string subnet)              // string[] — available IPs in subnet (public)
```

### Switch Config
```csharp
networkSwitch.GetSwitchId()       // string — unique ID (switchId field is PRIVATE)
networkSwitch.switchType          // int — type index
networkSwitch.label               // string, public — persisted in SwitchSaveData
networkSwitch.eolTime             // int — EOL countdown in real seconds.
                                  // Counts DOWN. Same semantics as server.eolTime (see above).
networkSwitch.timeToBrake         // int — breakdown countdown
networkSwitch.isBroken            // bool
networkSwitch.isOn                // bool
networkSwitch.sizeInU             // int — rack unit height (from UsableObject)
networkSwitch.GetConnectedDevices()  // List<(string, int)> — no IL2CPP mangling.
                                     // Use index loop (for i < Count), NOT foreach.
                                     // .Item1=deviceName, .Item2=cableId
networkSwitch.currentRackPosition    // RackPosition — direct slot reference
MainGameManager.instance.ShowNetworkConfigCanvas(networkSwitch)  // open native LACP config UI
// DO NOT use networkSwitch.ButtonShowNetworkSwitchConfig() — physical object context only
```

### LACP
```csharp
// lacpGroups field is PRIVATE — use these public methods:
NetworkMap.instance.GetAllLACPGroups()                             // Dictionary<int, LACPGroup>
NetworkMap.instance.GetLACPGroupBetween(string devA, string devB) // LACPGroup
NetworkMap.instance.CreateLACPGroup(string devA, string devB, List<int> cableIds)  // -> int groupId
NetworkMap.instance.RemoveLACPGroup(int groupId)

// LACPGroup fields (VERIFIED from decompiled source):
group.groupId    // int (NOT string)
group.deviceA    // string — device identifier (NOT NetworkMap.Device)
group.deviceB    // string — device identifier (NOT NetworkMap.Device)
group.cableIds   // List<int>
```

### HUD Feedback
```csharp
StaticUIElements.instance.AddMeesageInField("message");  // typo is in the game
StaticUIElements.instance.SetNotification(0, null, "message");
```

---

## MelonPreferences

| Key | Type | Default | Description |
|---|---|---|---|
| `FloorManager.ClockEnabled` | bool | true | Show/hide clock overlay |

---

## Build & Deploy

**Source:** `C:\Users\Jacob\Desktop\data center mods\FloorManager\`
**Project file:** `FloorManager.csproj`
**Deploy:** `D:\SteamLibrary\steamapps\common\Data Center\Mods\DCIM.dll`
**Nexus zip:** `C:\Users\Jacob\Desktop\DCIM-1.0.0.zip`

**All builds (Debug and Release — hacking always stripped):**
```
dotnet build
dotnet build -c Release
```
`STRIP_HACKING` is defined in the unconditional `<PropertyGroup>` in `FloorManager.csproj` — applies to every configuration. All `#if !STRIP_HACKING` blocks (HackingSystem tick, save hooks, Harmony repair patches, security UI) are excluded from every output DLL until the hacking feature is ready to resume.

**To resume hacking work:** Remove the `<DefineConstants>STRIP_HACKING</DefineConstants>` line from the unconditional PropertyGroup in `FloorManager.csproj`.

---

## Implementation Order & Progress

Build in this order to keep each step testable:

1. **`FloorManagerMod.cs`** + **`FloorManager.csproj`** — MelonLoader entry, prefs, static refs, wall + rack discovery logging — **DONE**
2. **`ClockOverlay.cs`** — clock only — **DONE** (awaiting visual confirmation)
3. **DEPLOY + TEST** — **DONE** (two rounds of testing, see Findings below)
4. **`Patches/ComputerShopPatch.cs`** — inject app into laptop — **DONE** (button appears, app opens)
5. **`FloorMapApp.cs`** + **`UIHelper.cs`** — 4-state navigation manager, dark background, color legend — **DONE**
6. **`FloorMapView.cs`** — top-down wireframe map with direct coordinate mapping — **DONE** (rewritten from column-grouping to direct mapping after test 2)
7. **`DeviceListView.cs`** — scrollable device list with type-colored text — **DONE** (code complete, awaiting in-game test)
8. **`DeviceConfigPanel.cs`** — server config (power + IP + customer + auto-fill), switch config (label + ports + LACP), patch panel (read-only) — **DONE** (code complete, awaiting in-game test)

### Current Status — DEPLOYED & WORKING (v0.0.9, 2026-04-13)

All core features working and confirmed in-game:
- Floor map with correct physical orientation, rack colors, aisle gaps, customer squares
- Device list with type-colored rows, hierarchy-walk matching
- Device config with customer logo/name, power buttons, IP controls, LACP
- Customer IP view with all active IPs per customer
- All IL2CPP safety issues resolved (no foreach on arrays, no dict enumerators)

## Runtime Findings (from test deploys)

### Test 1 — v0.0.1 (initial deploy)
- **Discovery timing:** `OnSceneWasInitialized` fires before save data loads — all 512 RackMounts showed `installed=False`. **Fix:** moved discovery to `SaveSystem.onLoadingDataLater` callback.
- **Button injection:** `mainScreen` uses `GridLayoutGroup`, not H/V LayoutGroup. **Fix:** broadened search to `GetComponentInChildren<LayoutGroup>()`.
- **Wall data:** All 306 walls showed `opened=False` even on a save with most rooms open. Wall `isWallOpened` may load after `onLoadingDataLater` or may work differently than expected. **Decision:** skip wall outlines entirely — use rack-only approach as planned in fallback.

### Test 2 — v0.0.2 (post-fix deploy)
- **Save load timing fixed:** 84/512 RackMounts now show `installed=True`. 0/92 walls opened (wall state still not loaded — confirmed: skip wall outlines).
- **Button injection works:** `Btn_FloorManager` visible in `GridLayoutGroup` on mainScreen. App opens correctly.
- **Floor map renders but layout wrong:** Column-grouping algorithm produced vertical columns. Physical layout has racks in horizontal rows at fixed Z positions.
- **Column clustering bug:** Comparing each rack to the first rack's X in the column caused columns spanning >1.0 unit to split incorrectly. (Bug was fixed but approach was abandoned.)
- **Decision:** Rewrote FloorMapView to use **direct coordinate mapping** — each rack placed at its actual (X, Z) position. No grouping algorithm. Aisles appear naturally from world-space gaps > 1.5 units.

### Test 3 — v0.0.3 (direct coordinate mapping deploy)
- **Back button does not work.** Clicking Back on the Floor Manager screen does nothing — navigation is broken. Must investigate and fix.
- **Z-axis layout is inverted.** Top-to-bottom ordering on the floor map is backwards compared to the physical room. The Z reversal logic needs to be flipped.
- **UI needs major polish.** The overall look and feel is rough — needs significant visual upgrades across all views (floor map, device list, device config). This is a blocking issue before release.

### Confirmed Layout Data (from test 2 logs)
- **84 installed racks** across 512 mount points
- **Distinct X positions (sorted):** -11.97, -11.16, -10.36, -9.55, -8.75, -1.97, -1.16, -0.36, 0.45, 1.25, 3.03, 3.84, 4.64, 5.45, 8.03, 8.84, 9.64, 10.45, 11.25, 13.03, 13.84, 14.64, 15.45, 16.25
- **Distinct Z positions:** -6.52, -8.52, -10.52, -12.52, -14.52 (5 rows, spaced 2.0 units apart)
- **X groupings by aisle gaps:** Group A (-11.97 to -8.75), Group B (-1.97 to 1.25), Group C (3.03 to 5.45), Group D (8.03 to 11.25), Group E (13.03 to 16.25)
- **Aisle gaps:** A↔B = 6.78, B↔C = 1.78, C↔D = 2.58, D↔E = 1.78 (1.78 = facing rack rows with aisle between)
- **mainScreen hierarchy:** `MainPage > Button Grid (GridLayoutGroup) > [Icon buttons + Btn_FloorManager]`

---

## Test Cases

| Test | What to Confirm | Status |
|---|---|---|
| Clock visible | Overlay appears top-right on scene load, persists across scenes | PENDING |
| Clock updates | Time advances, day increments at midnight | PENDING |
| Clock hides on main menu | Canvas hidden when `TimeController.instance == null` | PENDING |
| Discovery log | All wall + rack mount positions logged to MelonLoader log | PASS — 84/512 installed, 0/92 walls opened |
| App button appears | "Floor Manager" button in laptop main screen | PASS |
| Floor map renders | Dark background, rack squares placed at world positions | PASS (v0.0.2), layout rewritten for v0.0.3 |
| Horizontal layout | Racks in horizontal rows matching physical data center | PASS — reversed X sort matches physical layout |
| Aisle gaps | Visible gaps between facing rack rows | PASS |
| Status colors | Red = broken, Yellow = EOL, Green = healthy, Gray = empty | PASS |
| Color legend | Top-right corner shows device type color reference | PASS |
| Click rack | Opens device list for that specific rack | PASS |
| Device list order | Devices listed top-to-bottom matching slot order | PASS |
| Type-colored text | Blue server = blue text, Yellow = yellow, Switch = cyan | PASS — uses game object name for color |
| Server power toggle | `isOn` toggles, status updates | PENDING |
| Server IP change (manual) | [Change IP] opens game's SetIP keypad, IP updates correctly | **FAIL** — keypad doesn't open from laptop context |
| Server IP auto-fill | Changing customer auto-assigns valid IP from new customer's subnet | PENDING |
| Server customer cycle | [<] [>] buttons cycle via `ButtonClickChangeCustomer` | PASS — with customer logo + name |
| Patch panel read-only | Click shows type/size/slot, no buttons | PASS |
| ValueTuple access | `GetConnectedDevices()` `.Item1`/`.Item2` work in IL2CPP | PASS |
| Switch power disabled | No power button, read-only status | PASS |
| LACP button | `ShowNetworkConfigCanvas` opens native UI | PASS |
| Back navigation (4 levels) | Config -> List -> Map -> Laptop + CustomerIPs -> Map | PASS |
| Both mods active | No errors, no conflicts with AutoDispatcher | PASS |
| Refresh on open | All data re-pulled each time a view is opened | PASS |
| Performance | 84+ racks render without lag | PASS |
| Customer squares | Logo squares on floor map, clickable | PASS |
| Customer IP view | Shows all active IPs for customer with type/IP/status | PASS |
| Rack orientation | 1/1 on app matches 1/1 physically | PASS — reversed X sort |
| Header coordinates | Clicking rack shows correct R{row}/{pos} | PASS |

---

## Known Issues & TODO (v0.1.0)

### Bugs
- **Clock overlay invisible** — `FindObjectOfType<TextMeshProUGUI>()` never finds IL2CPP TMP types, so font is never resolved and text renders invisible. Fix: grab font from ComputerShop UI in `ComputerShopAwakePatch`.
- **Batch IP auto-fill assigns same IP to all servers** — `GetUsableIPsFromSubnet()` returns same first-available IP each call because the game's IP pool doesn't update between synchronous `SetIP()` calls. Fix: re-query after each assignment, track assigned IPs locally, or yield between assignments.
- **SetIP.ShowCanvas() doesn't work from laptop** — native keypad doesn't open in laptop context. Fix: replace with inline input field + Apply button.

### Still Untested
- `PowerButton()` — does clicking ON/OFF actually toggle server power remotely?

### Future (v2)
- Switch power toggle with confirmation dialog
- Inline LACP management (instead of opening game's native UI)
- Timer-based auto-refresh while floor map is open
- Rack hover effects / tooltips
- **Auto-failover / hot spare system** — Full lifecycle standby pool:
  1. **On break:** detect broken server, find an idle spare, auto-reassign to affected customer + auto-fill IP, notify via HUD
  2. **On recovery:** when the original server is repaired and back online, automatically reassign customer + IP back to the original, return the spare to idle/standby pool
  3. Spares sit idle waiting for the next break — no manual intervention needed
  - Requires: break/repair event detection (poll `isBroken` or hook game events), spare server registry (track which servers are designated standby vs production), customer+IP snapshot before failover (to restore original assignment on recovery), state machine per failover (broken → spare active → original repaired → spare released)

---

## Feature 3 — Security / Hacking System (v1.0.0 IN PROGRESS)

> **Status:** Chunks 1–3 complete and verified in-game. UI security labels built, awaiting deploy. Chunks 4–8 remaining.
>
> **Build chunks:**
> - ✅ Chunk 1 — HackingSystem core + wave scheduling + FloorManagerMod hooks (tested 2026-04-16)
> - ✅ Chunk 2 — DeviceConfigPanel designation buttons + HP bar + repair patches (tested 2026-04-16)
> - ✅ Chunk 3 — Wave execution: HP damage, breach, server EOL accel, wave end (tested 2026-04-16)
> - ⏳ UI labels — FW/IDS/HP role tags in DeviceListView + SearchResultsView (built, awaiting deploy)
> - 🔲 Chunk 4 — SecurityView.cs + FloorMapApp.cs ViewState.Security
> - 🔲 Chunk 5 — Hacker profiles + IDS reveal + counter-trace
> - 🔲 Chunk 6 — Reputation system + revenue deduction + Dashboard widget
> - 🔲 Chunk 7 — Ransomware wave type + ransom demand UI
> - 🔲 Chunk 8 — Target attractiveness + disengagement + offsite firewall + Data Exfil ransom

### Implementation Notes — Deviations from Original Design (2026-04-16)

- **`SecondsPerDay = 300f`** (5 real minutes per in-game day) — original design said 60s; changed to 300s for better pacing
- **`Server.ServerID` is `string`** (not int) — honeypots use `server.ServerID` as stable key; `GetInstanceID()` is transient
- **`NetworkSwitch.label` REMOVED** in game update 6000.4.2f1 — label now in `SwitchSaveData.label`, looked up via `SaveData._current.networkData.switches[i].switchID == sw.GetSwitchId()`
- **`TimeController.onEndOfTheDayCallback`** no longer used — replaced with in-code clock (`_elapsedSeconds`) and day-change detection in `OnLateUpdateTick`
- **Firewall HP cap = 200** (2× base 100) — encourages chaining multiple switches instead of over-patching one; `ApplySecurityPatch` boosts `MaxHP` by 20% per apply (5 patches = full cap)
- **Wave base DPS = 10 × profile.DpsMultiplier** (not `3 + customerCount × 0.8` from design doc) — simpler and cleaner; IDS reduces by 30% per switch (max 60%)
- **No end-of-day hook** — day boundary detected from `_elapsedSeconds / SecondsPerDay` crossing a new integer in `OnLateUpdateTick`

---

### Overview

Adds an attacker simulation that creates ongoing threat pressure scaling with business size. The player defends using a layered firewall chain and multiple deterrence tools. If all defenses fail, servers are damaged. The system hooks into the existing technician repair loop for firewall recovery.

---

### Attack Trigger

- **Threshold:** Attack waves begin after **4 active customers OR 4 in-game days**, whichever comes first
- Hooks into `TimeController.onEndOfTheDayCallback` (end-of-day tick)
- On each day-end tick: check trigger conditions, then roll wave schedule

---

### Wave Frequency

Random, customer-count scaled:

```
interval (in-game days) = max(0.5, 3.0 - (customerCount × 0.4)) ± Random(0.0, 1.0)
```

Examples:
- 4 customers → 1.4 ± 1.0 days (~0.4–2.4 days between waves)
- 8 customers → 0.5 ± 1.0 days (min-clamped to 0.5)

Result: at scale, attacks are nearly daily. Early game has breathing room.

**With active Honeypot:** wave interval multiplied by 1.67× (40% fewer waves). At 4 customers: ~2.3 days between waves instead of ~1.4.

---

### Wave Damage Rate

How fast a wave depletes firewall HP (applied per real-time second while the wave is active):

```
damage per second = (3 + (customerCount × 0.8)) × hackerDPSModifier
```

| Customers | DPS | Small (80HP) survives | Medium (140HP) survives | Large (220HP) survives |
|---|---|---|---|---|
| 4 | ~6.2 | ~13s | ~23s | ~35s |
| 8 | ~9.4 | ~8s | ~15s | ~23s |
| 12 | ~12.6 | ~6s | ~11s | ~17s |

Damage applies to the frontline firewall only. Next firewall in chain begins taking damage only after frontline HP hits 0.

---

### Wave Duration

A wave lasts a maximum of **45 real-time seconds** regardless of outcome. After 45 seconds the wave ends automatically — firewalls that survived keep their remaining HP, servers stop taking EOL acceleration. If all firewalls are breached before 45 seconds the wave continues dealing server damage until the timer expires.

**Lockdown pauses the wave timer.** 30 seconds of lockdown means the wave only has 15 seconds left when lockdown ends.

**No firewalls designated:** Wave immediately breaches to servers — no chain to deplete, instant punishment.

---

### Overlapping Waves

At high customer counts, multiple waves can run simultaneously. All active waves deal DPS to the same frontline firewall simultaneously — DPS stacks:

| Customers | Max simultaneous waves |
|---|---|
| 4–7 | 1 |
| 8–11 | 2 |
| 12+ | 3 |

Each simultaneous wave is scheduled independently, rolls its own attack type and target customer. Each wave has its own 45-second timer running independently.

---

### Attack Types (random per wave)

| Type | Target | Consequence on breach |
|---|---|---|
| **Service Disruption** | Random customer's servers | EOL accelerates 4× while breach active; sustained breach causes `isBroken`; **−5 rep** |
| **Data Exfiltration** | Random customer | Ransom demand issued (2-minute timer); outcome depends on player response; **rep loss on bad outcome** |
| **Ransomware** | Random customer's servers | 1–3 servers flagged `isRansomed` — online but generating zero revenue; pay ransom per server ($600–$1,400) or assign tech to clean; **−10 rep** |

Targeting is random per wave — not always the highest-revenue customer. Data Breach penalty (`$500 + customerRevenue × 0.15`) only applies **on breach** — if firewalls hold, no penalty.

---

### Data Exfiltration — Ransom Demand

When a Data Exfiltration wave fully breaches all firewalls, the hacker sends a ransom demand ("pay us or we release the stolen data"):

- A **2-minute real-time countdown** appears in the Security tab and as a HUD alert
- Player can **pay the ransom** at any time before the timer expires:
  - Money penalty deducted (`$500 + customerRevenue × 0.15`)
  - Rep saved — no rep loss
  - Log: `"Ransom paid — data suppressed, reputation protected"`
- If the **timer expires** without payment, outcome is random based on active hacker profile:

| Outcome | Description | Rep loss |
|---|---|---|
| Worst case | Money penalty + rep loss both hit | −15 rep |
| Moderate | Money penalty only (hacker takes payment, doesn't release data) | −8 rep |
| Bluff | Nothing — hacker moves on | −0 rep |

**Random outcome weights by hacker profile:**

| Profile | Worst case | Moderate | Bluff |
|---|---|---|---|
| Script Kiddie | 25% | 35% | 40% |
| Data Broker | 50% | 30% | 20% |
| RansomCrew | 65% | 25% | 10% |
| APT-9 | 80% | 15% | 5% |

Hacker profile is only known if counter-traced — otherwise the odds are unknown to the player, adding genuine tension to the decision.

---

### Reputation System

**Rep** is a 0–100 score representing the data center's trustworthiness. Visible as a stat on the Dashboard. Starts at 100.

**Rep loss per breach:**

| Event | Rep loss |
|---|---|
| Service Disruption breach | −5 |
| Ransomware breach | −10 |
| Data Exfiltration — ransom paid | 0 |
| Data Exfiltration — timer expired worst case | −15 |
| Data Exfiltration — timer expired moderate | −8 |
| Data Exfiltration — timer expired bluff | 0 |

**Rep recovery:** +2 per clean in-game day (no successful breaches).

**Mechanical effect (Option C — both):**
- **Below 80 rep:** Revenue penalty applied at end of each day — `penalty = totalDailyRevenue × (1f - rep/100f)` deducted via `UpdateCoin(-penalty)`. At 70 rep: 30% of revenue lost. Applied after normal revenue is added.
- **Rep ≤ 10:** Crisis threshold — active hacker ignores disengagement attractiveness check and keeps attacking regardless of target value. Revenue multiplier already makes income near-zero at this point; the real punishment is no relief from attacks.
- **No rev-freeze crisis event** — the multiplier handles near-zero revenue naturally. Crisis = hacker persistence, not an extra freeze.

---

### Hacker Profiles

Hackers are threat actors with distinct styles. The active hacker is **unknown** until identified through IDS + counter-trace. All logs and Security tab show "Unknown Threat" until revealed.

**Identification progression:**
- **No IDS:** "Unknown Threat" — attack type and target hidden until wave hits firewalls
- **IDS active:** Attack type + target customer revealed in log before wave hits firewalls
- **Counter-trace used:** Hacker profile revealed — name, specialty, persistence shown in Security tab

**Four profiles:**

| Profile | Specialty | Wave frequency | DPS modifier | Persistence | Target preference |
|---|---|---|---|---|---|
| Script Kiddie | Service Disruption | ×0.7 (less frequent) | ×0.8 | 2 defenses | Random |
| Data Broker | Data Exfiltration | ×1.0 | ×1.0 | 3 defenses | Highest revenue customer |
| RansomCrew | Ransomware | ×1.0 | ×1.2 | 3 defenses | Random |
| APT-9 | Mixed (all types) | ×1.4 (more frequent) | ×1.4 | 5 defenses | Weakest firewall chain |

**Balanced progression:**
- Attacks always begin with **Script Kiddie** — teaches the system
- After Script Kiddie is counter-traced or disengages, next profile is weighted by customer count:

| Customers | Profile pool |
|---|---|
| 4–7 | Script Kiddie, Data Broker |
| 8–11 | Script Kiddie, Data Broker, RansomCrew |
| 12+ | All four — APT-9 included |

**Persistence:** "Successful defense" = wave ended without reaching servers (at least one firewall held HP > 0). Lockdown-assisted survival counts. Offsite-only defense counts. Consecutive counter resets to 0 if a wave fully breaches all firewalls.

---

### Target Attractiveness — Attacker Disengagement

When a data center has been damaged enough, the active hacker disengages — there is no longer enough value to justify the effort.

```
attractiveness = (dailyRevenue × 0.5) + (rep × 10)
```

**Disengagement thresholds:**

| Profile | Disengages below | Recovery window |
|---|---|---|
| Script Kiddie | 800 | 3 days |
| Data Broker | 600 | 4 days |
| RansomCrew | 500 | 4 days |
| APT-9 | 300 | 6 days |

During the recovery window: **no waves scheduled**. Security tab threat level shows **"Dormant"** with a day counter ("Next threat in ~3 days").

**Re-entry threshold:** attractiveness above 1,200 — a new hacker notices the recovering data center and starts targeting it. New profile selected from pool weighted by customer count.

**Log entries:**
- `"Unknown Threat has disengaged — insufficient target value"` (unidentified hacker)
- `"[Profile name] has disengaged — target no longer profitable"` (identified hacker)
- `"New threat actor detected — attacks resuming"` (re-entry)

---

### Firewall Chain — Sequential

**Any network switch can be designated as a firewall.** Switches designated as firewalls form a **sequential chain** in designation order:
- Attacker must fully deplete firewall 1 HP before reaching firewall 2
- This makes **ordering matter** — outermost firewall takes most punishment
- Player can **stack multiple switches** as a layered defense

**Firewall HP base values (scales with switch type):**

| Switch Type | Base HP |
|---|---|
| Small (4/8 port) | 80 |
| Medium (16 port) | 140 |
| Large (32+ port) | 220 |

- HP determined at designation time from switch type name (parse port count from name)
- **No HP auto-regeneration** — damaged HP persists between waves
- **HP only restored by technician repair** (`RepairDevice()` → full HP restored)
- When HP hits 0: switch becomes `isBroken = true`, fully offline, no protection until repaired
- A broken firewall is skipped in the chain (chain shrinks until repaired)

**Firewall Switch EOL Degradation (two-tier):**

Being designated as a firewall puts the switch under constant extra load — it degrades faster than an ordinary switch:

| State | EOL Drain Rate |
|---|---|
| Normal switch (not a firewall) | 1× baseline |
| Firewall switch — idle (no attack) | 2× baseline |
| Firewall switch — under active attack | 4× baseline |

- Degradation applied per `OnLateUpdate()` tick — `switch.currentEOL` decremented proportionally
- The frontline firewall (position 1 in chain) takes 4× during a wave; deeper firewalls take 2× (idle) until their turn
- When firewall switch hits EOL 0 it becomes broken, same as HP=0 — technician required
- **Player tradeoff:** Firewall protection costs switch lifespan. Cheap switches burn out fast; investing in larger switches buys more protection time.

---

### Punishment

**When a firewall is breached and the wave reaches servers:**
- Targeted customer's servers: EOL countdown runs at **4× normal speed**
- If EOL hits 0 during breach: server becomes `isBroken = true`
- EOL acceleration stops when wave ends (firewall repaired, wave deterred, or wave timer expires)

**Data Exfiltration waves:** No EOL effect — SLA penalty only.

---

### Deterrence Methods

Six methods, each with distinct benefit and tradeoff:

#### 1. Honeypot Server
- **Designate** any server as bait (new button in DeviceConfigPanel server section)
- **Benefit:** Reduces wave frequency by 40% — attacker wastes time on the decoy, lengthening the interval between waves. Also provides early warning (honeypot is targeted before firewalls are hit, giving the player advance notice in the attack log). Effect does not stack with multiple honeypots — one honeypot is sufficient.
- **Tradeoff:** Server is powered off while active (cannot serve customers). Implemented via `server.PowerButton()` on activation; reversed on deactivation.
- **Visual:** Server row in CustomerIPView shows "HONEYPOT" tag; cable nearest to it turns amber before firewalls are hit
- **Log entry:** "Honeypot triggered — wave interval extended, early warning active"

#### 2. Security Patch
- **Buy** per-firewall in the Security tab or DeviceConfigPanel switch section — usable anytime including during an active wave
- **Benefit:** Instantly restores firewall to full HP + 20% bonus HP (120% total)
- **Pricing:** General funds — `$800 + (maxHP × 5)` (small firewall ~$1,200, large ~$1,900). Competes with rack/tech spending.
- **Log entry:** "Security patch applied to [switch name] — HP restored to 120%"

#### 3. Lockdown Mode
- **Toggle button** in Security tab header (always visible when Security tab is open)
- **Benefit:** Completely halts current wave progression — attack is frozen
- **Tradeoff:** Costs **$15/second** while active (simulated lost revenue from throttled traffic); cables turn blue during lockdown
- **Hard limit:** Max 30 seconds per activation; 60-second cooldown before reuse
- **Log entry:** "Lockdown active — wave suspended ($15/s drain)"

#### 4. IDS Switch (Intrusion Detection System)
- **Designate** any switch as IDS instead of firewall (mutually exclusive — one or the other)
- **Does NOT block** — detection and mitigation only
- **Benefits (applies every wave, not just the first):**
  - Early warning: logs attack type + target customer before wave hits firewalls (~5 second window to react)
  - Reduces incoming DPS by 30% for Service Disruption waves
  - Reduces financial penalty by 30% for Data Exfiltration waves on breach
  - Reduces ransom demand cost by 30% for Ransomware waves on breach
- **Multiple IDS switches:** each adds 15% additional reduction, stacking up to 60% total
- **Log entry:** "IDS alert — [Attack type] wave incoming, targeting [Customer]"
- **Identity:** IDS is required to reveal attack type — without it, wave type is unknown until servers are hit

#### 5. Counter-trace
- **Unlocked** automatically after 3 consecutive successful defenses (wave ends without fully breaching all firewalls)
- **Trigger:** Button appears in Security tab when counter reaches 3; player clicks "Counter-Trace" to use
- **Benefit:** Pauses the next scheduled wave for 1 full in-game day
- **Resets** if a wave fully breaches all firewalls (counter drops to 0)
- **Log entry:** "Counter-trace deployed — next wave delayed by 1 day"

#### 6. Offsite Firewall Service
- **Subscribe** from the Security tab — choose a tier, pay a daily fee deducted at end of each in-game day
- **Position:** Automatically placed at **chain position 0** (outermost) — attacks hit it before any physical switch
- **Auto-regen:** HP heals at end of each day with no technician required
- **Overload:** If HP hits 0 mid-wave the service is "overloaded" for that wave — no protection, but recovers automatically next day (does NOT become `isBroken`; this is its key advantage over physical firewalls)
- **Cancel anytime:** Subscription stops at end of current day; removed from chain
- **Tradeoff:** Continuous money drain vs physical firewalls which are free to run but need tech attention when broken

| Tier | Daily Cost | HP | Daily HP Regen |
|---|---|---|---|
| Basic | $300/day | 100 | 25 |
| Advanced | $600/day | 200 | 50 |
| Enterprise | $1,200/day | 350 | 100 |

- Displayed in Security tab firewall chain panel as "Offsite [Tier]" with a distinct color (cyan) separate from physical switch entries
- **Log entries:**
  - "Offsite firewall (Advanced) subscribed — chain position 0"
  - "Day 9 — Offsite firewall overloaded — wave passed through to physical chain"
  - "Offsite firewall HP restored to 200 (end of day regen)"

---

### Cable Attack Balls (3D world)

**Research confirmed:** The game's travelling cable balls are DOTS/ECS entities (`PacketComponent`). Manipulating ECS entities from IL2CPP managed code is unreliable. Instead, hacker attack traffic is shown using **managed GameObjects animated along cable waypoints**.

**Implementation:**
- `CablePositions.cables[cableId]` returns `List<Vector3>` — the smooth waypoint path for any cable
- `NetworkSwitch.GetConnectedDevices()` returns `List<(string, int cableId)>` — finds cable IDs for firewall switches
- `CablePositions` is NOT a singleton — access via `Object.FindObjectOfType<CablePositions>()`
- On wave start: spawn attack ball GameObjects parented to scene root, animate along waypoints via coroutine
- On wave end: destroy all active attack ball GameObjects

**Attack ball appearance:**
- Small black sphere (created from `GameObject.CreatePrimitive(PrimitiveType.Sphere)`, scale ~0.04)
- Child `Canvas` (world space) with a red "✕" `TextMeshProUGUI` label centered on the sphere
- Travels from switch toward server following waypoint path

**Ball states by wave condition:**

| State | Ball behavior |
|---|---|
| Wave active, firewall holding | Black + red ✕ balls travel toward firewall switch at normal speed |
| Firewall breached | Balls speed up, travel past switch toward servers |
| Multiple simultaneous waves | Higher spawn frequency of attack balls (more balls on cables) |
| Lockdown active | Attack balls pause (coroutine suspended while lockdown active) |
| Honeypot triggered | Attack balls appear on honeypot server's cables first (pre-firewall warning) |
| Wave deterred / ended | Attack balls destroyed, brief green normal ball burst on firewall cables |
| No attack | No attack balls — normal game packets continue unaffected |

---

### Laptop Warning Indicator

Red pulsing badge added to the DCIM app button on the laptop main screen when a wave is active. Implemented as a small red `Image` child of the app button GameObject, toggled by `HackingSystem` via a static event/callback.

---

### Attack Log

- **Location:** Security tab in DCIM (new `ViewState.Security`)
- **Scope:** Session-only (cleared on game load) — log entries are not persisted
- **Entries include:** timestamp (Day N), attack type, targeted customer, wave outcome (held / breached / deterred), deterrence used, penalty applied
- **Format varies by knowledge state:**
  ```
  // No IDS — attack type and target hidden:
  Day 7 — UNKNOWN ATTACK → [Target Hidden] — BREACHED

  // IDS active — type and target revealed:
  Day 8 — SERVICE DISRUPTION → Union Busters — HELD (Firewall 2 at 34 HP)

  // Counter-traced — hacker profile also shown:
  Day 9 — DATA EXFILTRATION [Data Broker] → Bermuda Triangle Backup — BREACHED — $1,240 penalty
  Day 10 — SERVICE DISRUPTION [Script Kiddie] → Union Busters — DETERRED (Counter-trace)
  ```

---

### Security Tab (new DCIM view — `ViewState.Security`)

New tab accessible from Dashboard. Contains:

1. **Threat status bar** — current threat level (Low / Elevated / Active / Critical / Dormant); wave timer countdown to next attack; "Dormant — Next threat in ~N days" when disengaged; active ransom countdown timer (2-min, shown when ransom demand is live)
2. **Hacker profile panel** — "Unknown Threat" until counter-traced; after identification shows name, specialty, persistence counter, disengagement threshold
3. **Offsite Firewall panel** — subscribe/cancel buttons, tier selector (Basic / Advanced / Enterprise), current HP bar with regen display, daily cost, "Overloaded" badge if HP=0
4. **Firewall chain panel** — ordered list of all designated firewall switches (physical) with HP bars, breach status, and "Security Patch" button per switch; Offsite entry shown at top of chain if subscribed
5. **IDS panel** — list of IDS-designated switches with detection status
6. **Honeypot panel** — list of honeypot servers with active/inactive toggle
7. **Lockdown button** — in header (shown only in Security tab)
8. **Counter-trace button** — appears when 3 consecutive defenses achieved
9. **Attack log** — scrollable history of wave events this session (format varies by knowledge state — see Attack Log section)

---

### New Files Required

| File | Purpose |
|---|---|
| `HackingSystem.cs` | Core system: wave scheduling, hacker profiles, firewall HP tracking, EOL acceleration, attack ball spawning/animation, rep system, ransom demand, disengagement logic, deterrence, save/load via MelonPreferences |
| `SecurityView.cs` | Security tab UI: threat bar, hacker profile panel, offsite/firewall/IDS/honeypot panels, ransom countdown, lockdown/counter-trace buttons, attack log |

### Modified Files

| File | Changes |
|---|---|
| `FloorMapApp.cs` | Add `ViewState.Security`, Security nav button on Dashboard, wire Lockdown button |
| `FloorManagerMod.cs` | Hook `TimeController.onEndOfTheDayCallback` for wave scheduling + rep/revenue deduction + offsite regen; restore `OnLateUpdate` for EOL drain tick |
| `DeviceConfigPanel.cs` | Switch: "Set Firewall / Remove Firewall", "Set IDS / Remove IDS" buttons + HP bar; Server: "Set Honeypot / Remove Honeypot" button + RANSOMED status + "Pay Ransom" button |
| `DashboardView.cs` | Add security status widget (threat level chip + rep score + last attack summary); rep shown on Dashboard |
| `CustomerIPView.cs` | Show "RANSOMED" tag on ransomed server rows |
| `SearchResultsView.cs` | Show RANSOMED status in EOL/status column for ransomed servers |
| `FloorManagerMod.cs` | Harmony postfix on `NetworkSwitch.RepairDevice()` — restore firewall HP to full on tech repair; Harmony postfix on `Server.RepairDevice()` — clear ransomed state if server is in ransomed set |

### Implementation Notes (from source research)

- **Money deduction:** `PlayerManager.instance.playerClass.UpdateCoin(-amount)` — pass negative float to deduct
- **HUD toast:** `StaticUIElements.instance.AddMeesageInField(string message)` — exact spelling with double-e
- **End-of-day hook:** `TimeController.onEndOfTheDayCallback += MyMethod` — `OnEndOfTheDay` delegate
- **Switch ID for persistence:** `switch.GetSwitchId()` returns stable string — use as MelonPreferences key instead of rackLabel approach
- **EOL is int:** `eolTime` is `int` (seconds) on both Server and NetworkSwitch. Track float accumulator in HackingSystem per device, subtract whole seconds from `eolTime` when accumulator ≥ 1
- **isRansomed:** Does not exist in game. Track as `static HashSet<Server> _ransomed` in HackingSystem. Not persisted — clears on load (acceptable)
- **Revenue multiplier:** No built-in hook. At end-of-day: calculate `penalty = totalRevenue × (1f - rep/100f)` and call `UpdateCoin(-penalty)` to apply rep damage to income
- **Ransomware tech cleanup:** Harmony postfix on `Server.RepairDevice()` — if server is in `_ransomed`, remove it. Player uses normal game tech dispatch; our hook clears ransomed state on completion. No custom RepairJob needed.
- **Rep storage:** Tracked in `HackingSystem` as static float, persisted via MelonPreferences. Do NOT use `PlayerManager.instance.playerClass.reputation` — unknown native behavior.
- **Cable attack balls:** Spawn managed `GameObject` (black sphere + red ✕ TextMeshProUGUI child) and animate via coroutine along `CablePositions.cables[cableId]` waypoints. `CablePositions` via `FindObjectOfType<CablePositions>()`.
- **Customer count:** `MainGameManager.instance.existingCustomerIDs.Count`

---

### Save / Load Behavior

- **Firewall/IDS designations:** Persisted via `MelonPreferences` — semicolon-delimited string of `switchId:type:maxHP` using `switch.GetSwitchId()` as stable key (e.g. `SW_abc123:firewall:140`)
- **Honeypot designations:** Persisted via `MelonPreferences` — semicolon-delimited server IDs (`server.ServerID`)
- **Rep score:** Persisted via `MelonPreferences` — float value restored on load
- **Offsite Firewall subscription:** Persisted via `MelonPreferences` — tier (0–3) + current HP
- **Firewall HP (physical switches):** Lost on load — resets to full (natural session cooldown)
- **Ransomed servers:** Lost on load — `HashSet<Server>` cleared (acceptable)
- **Attack wave state:** Lost on load — wave timer resets
- **Attack log:** Session-only, cleared on load
- **Why:** Avoids complex game-save integration; most transient state resets cleanly between sessions

---

### ✅ Confirmed Design Decisions

- **Firewall switch eligibility:** Any network switch can be designated as a firewall — confirmed 2026-04-16
- **Firewall EOL degradation:** 2× baseline passive (idle), 4× under active attack — confirmed 2026-04-16
- **Firewall switch EOL = break condition:** When EOL hits 0 on a firewall switch, it breaks same as HP=0 — confirmed 2026-04-16
- **Wave damage rate:** `3 + (customerCount × 0.8)` DPS — confirmed 2026-04-16
- **Wave duration:** 45 real-time seconds max per wave — confirmed 2026-04-16
- **Server EOL acceleration when breached:** 4× normal rate — confirmed 2026-04-16
- **Data theft penalty:** `$500 + (customerRevenue × 0.15)` — confirmed 2026-04-16
- **HUD toast on wave start:** Yes — `StaticUIElements.AddMeesageInField()` in addition to cable glow + laptop badge — confirmed 2026-04-16
- **Attack intensity escalation:** Frequency only scales with customer count; per-hit damage is fixed — confirmed 2026-04-16
- **Lockdown cost:** $15/s, max 30s per activation ($450 max), 60s cooldown — confirmed 2026-04-16
- **Firewall chain ordering:** First-designated = frontline (chain position 1); order visible in Security tab — confirmed 2026-04-16
- **Honeypot effect:** Reduces wave frequency by 40% (interval ×1.67); does not stack with multiple honeypots; server powered off while active — confirmed 2026-04-16
- **Counter-trace with offsite firewall:** Offsite-only successful defense counts toward the 3-defense counter — confirmed 2026-04-16
- **Attack trigger:** 4 active customers OR 4 in-game days, whichever comes first — confirmed 2026-04-16
- **Offsite firewall auto-cancel:** If daily fee cannot be paid at end of day, subscription cancels automatically with log warning — confirmed 2026-04-16

- **Security Patch pricing:** Option A — general funds, `$800 + (maxHP × 5)` per patch (small firewall ~$1,200, large ~$1,900). Competes with rack/tech spending — confirmed 2026-04-16
- **Security Patch mid-wave:** Usable during active waves — confirmed 2026-04-16
- **Lockdown timer:** Pauses the 45s wave timer while active — confirmed 2026-04-16
- **Overlapping waves:** 1 wave at 4–7 customers, 2 at 8–11, 3 at 12+ — confirmed 2026-04-16
- **IDS:** Per-wave mitigation (not first-wave only); reduces DPS 30% for Service Disruption, penalty 30% for Data Exfiltration, ransom 30% for Ransomware; stacks to 60% — confirmed 2026-04-16
- **Third wave type:** Ransomware — servers flagged `isRansomed`, zero revenue until paid or tech cleans — confirmed 2026-04-16
- **Hacker profiles:** 4 profiles (Script Kiddie, Data Broker, RansomCrew, APT-9); identity hidden until IDS + counter-trace — confirmed 2026-04-16
- **Reputation system:** 0–100 score; revenue multiplier below 80 rep; crisis at 0 rep (revenue frozen 3 days); +1 rep per clean day — confirmed 2026-04-16
- **Data Exfiltration ransom:** 2-minute timer; pay to save rep; timer expiry = random outcome weighted by hacker profile — confirmed 2026-04-16
- **Target attractiveness:** `(dailyRevenue × 0.5) + (rep × 10)`; hacker disengages below profile threshold; Security tab shows "Dormant" with day counter; re-entry above 1,200 — confirmed 2026-04-16
- **No firewalls = immediate breach** — confirmed 2026-04-16
- **Successful defense definition:** Wave didn't reach servers (≥1 firewall held HP > 0); lockdown-assisted and offsite-only both count — confirmed 2026-04-16
- **Cable attack balls:** Managed GameObject coroutines along `CablePositions.cables[cableId]` waypoints; black sphere + red ✕ label; not ECS manipulation — confirmed via source research 2026-04-16
- **eolTime is int:** Float accumulator per device in HackingSystem; subtract whole seconds from `eolTime` — confirmed via source research 2026-04-16
- **Switch ID persistence:** `switch.GetSwitchId()` as stable MelonPreferences key — confirmed via source research 2026-04-16
- **isRansomed:** `static HashSet<Server>` in HackingSystem; not a game field — confirmed via source research 2026-04-16
- **Revenue multiplier:** Daily `UpdateCoin(-penalty)` at end-of-day; no built-in hook — confirmed via source research 2026-04-16
- **Ransomware cleanup:** Harmony postfix on `Server.RepairDevice()` clears ransomed state — no custom RepairJob needed — confirmed via source research 2026-04-16
- **Rep storage:** HackingSystem static float + MelonPreferences; do NOT use `PlayerManager.playerClass.reputation` — confirmed via source research 2026-04-16
- **Rep recovery:** +2/day — confirmed 2026-04-16
- **Rep crisis:** At rep ≤ 10, hacker ignores disengagement threshold (keeps attacking); no separate revenue freeze — confirmed 2026-04-16
- **Rep crisis threshold change:** Crisis triggers at rep ≤ 10 not 0 — confirmed 2026-04-16
- **Simultaneous ransom demands:** Second breach on same customer extends active timer by +60s — confirmed 2026-04-16
- **Ransomed server cap:** 3 per customer max — confirmed 2026-04-16
- **Ransomware revenue:** `(ransomed/total customer servers) × customerRevenue` deducted daily via `UpdateCoin` — confirmed 2026-04-16
- **DPS formula includes hacker modifier:** `(3 + customerCount×0.8) × hackerDPSModifier` — confirmed 2026-04-16
