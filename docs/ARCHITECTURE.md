# Architecture

```
hakari/
├─ src/
│  ├─ Hakari.Core/        # net9.0, no UI: parsing, index, pricing, currency, accounts, limits
│  ├─ Hakari.Taskbar/     # Win32: taskbar widgets, tray icon, motion, UI Automation layout
│  ├─ Hakari/             # Hakari.exe, always running: widgets + tray + background feed
│  ├─ Hakari.Surfaces/    # (next) WinUI 3: flyout, dashboard, settings; started on demand
│  ├─ Hakari.Cli/         # dev/verification CLI: sources, index, report, limits
│  └─ Hakari.SyncAgent/   # (later) Mac/Linux agent writing machine snapshots to a shared folder
├─ tests/Hakari.Core.Tests/, tests/Hakari.Taskbar.Tests/
├─ tools/Run-TaskbarScenarios.ps1   # Explorer restart, auto-hide, theme… with screenshots
├─ data/pricing.json      # bundled price table (also served raw from GitHub for remote update)
└─ docs/
```

## Two processes

An empty WinUI 3 window costs about 110 MB, more than the whole always-running part. So:

- **Hakari.exe** stays resident with no WinUI: widgets, tray icon, indexing and limit polling
  (idle ≈ 0.01% CPU, ≈ 95 MB).
- **Hakari.Surfaces.exe** is a WinUI 3 process started when a flyout, dashboard or settings
  window opens, and it exits a while after its last window closes. Warm start ≈ 150 ms, first
  start after boot ≈ 1.2 s. It reads the same SQLite index (WAL allows concurrent readers).

## Core pipeline

```
ISource (Windows | Wsl | ConfigDir | SharedFolderMachine)
   │  enumerates *.jsonl, knows its AccountRef
   ▼
FileTracker  ── FileSystemWatcher (NTFS) / PollingWatcher (\\wsl.localhost, 9P)
   │  per file: lastOffset, size, mtime → read only appended bytes
   ▼
UsageLineParser  (System.Text.Json Utf8JsonReader, skip non-assistant lines fast)
   │  → UsageRecord(msgId, requestId, ts, model, sessionId, cwd, branch, tokens…, speed, sidechain)
   ▼
IndexStore (SQLite, WAL)   UNIQUE(msgId, requestId) → dedupe for free
   │  + hourly rollups table (account, source, model, project, hour)
   ▼
Aggregator  → Snapshot (immutable) → IObservable<Snapshot>  (debounced 500 ms)
   ▼
App: TaskbarWidget / Flyout / Dashboard bind to Snapshot
```

- `PricingService`: merges bundled → remote (`raw.githubusercontent.com/<org>/hakari/main/data/pricing.json`, ETag cached daily) → LiteLLM fallback → user overrides. Cost is computed at query time from token columns, so price changes re-price history with no reindex.
- `LimitProvider` (opt-in): `OAuthUsageLimitProvider` with a fallback to `EstimatedLimitProvider`.
- `ExchangeRateService`: free, keyless rates. Frankfurter (ECB reference rates, with history) is the primary source and open.er-api.com (latest only) the fallback. Rates are stored in `exchange_rates` in the index database, survive index rebuilds, and are fetched only when missing or older than 4 days. Two modes: the latest rate, or the rate of each usage day (weekends and holidays use the previous business day).
- `SyncService`: writes `<shared>/Hakari/machines/<machineId>.json` (hourly rollups only) and reads the files from other machines. Snapshot schema is versioned.

## Taskbar overlay (Win11)

- Find `Shell_TrayWnd` (primary) / `Shell_SecondaryTrayWnd` (others), and `TrayNotifyWnd` for the tray rect.
- Create a WS_POPUP | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_LAYERED window. Either `SetParent` it into the taskbar (TrafficMonitor style) or keep it topmost and reposition on `EVENT_OBJECT_LOCATIONCHANGE` (SetWinEventHook). Spike both; parenting survives z-order fights better.
- Handle: `RegisterWindowMessage("TaskbarCreated")` (explorer restart), DPI change, auto-hide taskbar, taskbar alignment (centered icons can collide → compute free space), full-screen apps.
- Render via Win2D or Composition visuals for crisp text, bars, and sparklines at taskbar height.
- The widget renderer is a small layout engine: `LayoutTemplate → Slot[] → (MetricBinding, Visual, Format, ThresholdRules)`, serialized as JSON in settings so layouts can be exported and shared.

## Settings & storage

- `%LOCALAPPDATA%\Hakari\settings.json` (MSIX: `ApplicationData.Current.LocalFolder`)
- `index.db` is rebuildable (Settings → "Rebuild index").

## Risks / spikes to do first

1. Taskbar overlay reliability on current Win11 builds (24H2/25H2) and multi-monitor setups.
2. WSL 9P read throughput for 846 MB on first index. Option: run a tiny helper inside WSL via `wsl.exe -e` that streams only usage lines.
3. OAuth usage endpoint shape and stability (undocumented).
4. MSIX + `runFullTrust` + reading `\\wsl.localhost` paths from a packaged app.
5. Store certification with an overlay window and reading files outside the package.
