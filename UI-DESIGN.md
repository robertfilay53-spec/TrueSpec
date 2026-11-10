# TrueSpec — UI Design (FINALIZED, Nov 10, 2026)

**Language:** English only (UI, reports, alerts). No other language in v1.
**Framework:** Avalonia UI, .NET 10. XAML like WPF, Fluent theme, builds on Linux and Windows.
**Decision:** "Command Center" dashboard. Chosen over HWiNFO-style dense tables (overwhelming for buyers) and Settings-style plain lists (too shallow). The buyer must understand the machine in 5 seconds, then drill down.

## Layout

```
+--------+--------------------------------------------------------------+
|        |  HERO STRIP: [Score ring 0-100] VERDICT | CPU temp | RAM |   |
|  LEFT  |  Disk health | Battery health | [New Laptop Check] [Export]  |
|  RAIL  +--------------------------------------------------------------+
|  icons |  COMPONENT CARDS GRID (2-3 columns, responsive)              |
|  +     |  [CPU] [RAM] [Storage] [Battery] [GPU] [Display]            |
| labels |  [Network] [Motherboard] [Security] [Drivers] ...            |
|        +--------------------------------------------------------------+
|        |  LIVE STRIP: sparkline graphs CPU temp / GPU temp / usage    |
|        +--------------------------------------------------------------+
|        |  STATUS BAR: scan time | Admin mode | version | update check |
+--------+--------------------------------------------------------------+
```

- **Window:** 1280x800 minimum, resizable, remembers size/position.
- **Left rail (200px, icons + labels):** Overview, System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security, Tests, Report, Settings. Active item highlighted. Collapsible to icons-only.
- **Hero strip (Overview only):** big score ring (0-100) + verdict word + 4 key stats + two primary buttons (New Laptop Check, Export Report).
- **Component cards:** icon, name, headline value (e.g. "16 GB DDR5-5600"), health bar with %, status dot, "Details" link. Click opens the detail page.
- **Live strip:** sparkline graphs for CPU temp, GPU temp, CPU usage, battery drain. Updates every 2 seconds.
- **Status bar:** last scan time, admin/elevated indicator, app version, update available dot.

## Detail pages (one per component)

- Header: component name + health % (or "Not measurable") + status word.
- Spec table: label/value rows. Click any value copies it. Search/filter box at top.
- Live graph where the component has sensors.
- "What this means" box: one plain-English paragraph explaining the numbers and what to do about them. No jargon.

## Buyer mode — "New Laptop Check" (dedicated view)

- Big PASS / FAIL / WARNING banner at top.
- Checklist, one row per check, each with pass/fail/warning icon:
  SSD wear must be 0%, battery cycles 5 or fewer, RAM size matches advertised,
  drive power-on hours 0, BIOS date recent, Windows install date recent,
  no critical errors in event log, all drivers signed.
- Each failed row says what it means and what to ask the seller.
- One-click report export from this view.

## Theme (tokens)

- **Dark default:** background #121417, cards #1B1E22, borders #2A2E33, text #EDEFF2, secondary text #9AA3AD.
- **Light theme:** background #F4F5F7, cards #FFFFFF, borders #E2E5E9, text #16181D.
- **Accent green (healthy):** #22C55E. **Amber (watch):** #F59E0B. **Red (problem):** #EF4444. **Grey (not measurable):** #6B7280.
- **Health bands:** 80-100 green, 60-79 amber, below 60 red, no sensor grey.
- Score ring uses the same bands. No other colors for health anywhere.

## Rules

- English only, plain words. A non-technical buyer must understand every label.
- Every number shows its source on hover/tooltip (e.g. "WMI Win32_Battery" or "NVMe SMART").
- Estimated values are labeled ESTIMATE. Unmeasurable values say "Not reported by hardware", never a fake number.
- No ads, no popups, no account prompts. One window, one job.
- Keyboard navigable. Respects Windows high-contrast setting.
- All text uses Segoe UI Variable; headings semibold, body regular.

## Screens list (v1 build order)

1. Overview (hero + cards + live strip)
2. New Laptop Check (buyer mode)
3. System, CPU, RAM, Storage, Battery, GPU, Display, Network, Motherboard, Security
4. Tests (RAM test, disk benchmark, battery estimate, WiFi speed test)
5. Report (export PDF/HTML/JSON, preview)
6. Settings (theme, refresh rate, startup, update channel, language locked to English in v1)
