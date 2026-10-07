# Hakari 秤

A lightweight Windows taskbar meter and dashboard for **Claude Code** usage. It combines every place you use Claude Code (Windows, WSL distros, extra config dirs, other machines) and every account into one real total.

> Not affiliated with or endorsed by Anthropic. "Claude" is a trademark of Anthropic, PBC.

- Live cost, limits, and burn rate on the taskbar, in a layout you choose
- Incremental indexing: reads only new log bytes, so it stays fast with logs of several GB
- Correct cache pricing (5-minute vs 1-hour cache writes, fast mode)
- Local-only. Hakari never reads prompt or response text and sends no telemetry.

Status: **early release**. See [docs/SPEC.md](docs/SPEC.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ROADMAP.md](docs/ROADMAP.md).

## Download

Windows 10 (2004) or later, 64-bit.

1. Download `Hakari-<version>-win-x64.zip` from [Releases](https://github.com/PHUICMT/hakari/releases).
2. Unzip it where you keep apps, such as `%LOCALAPPDATA%\Programs\Hakari`.
3. Run `Hakari.exe`. The meter appears on the taskbar and a short first-run guide opens.

Nothing else to install: .NET and the Windows App SDK come inside the zip. The build is not
code-signed yet, so SmartScreen may warn on first run; choose **More info → Run anyway**.
Each release lists the zip's SHA-256 beside it.

**Update:** quit Hakari from its tray menu and replace the folder with the new zip's contents.
Settings and the usage index live in `%LOCALAPPDATA%\Hakari` and are kept.

**Uninstall:** turn off *Start with Windows* in Settings, quit from the tray menu, then delete
the app folder and `%LOCALAPPDATA%\Hakari`.

## Dev

```powershell
dotnet test
dotnet run --project src/Hakari.Cli -c Release            # scan %USERPROFILE%\.claude\projects
dotnet run --project src/Hakari.Cli -c Release -- "\\wsl.localhost\Ubuntu-24.04\home\<user>\.claude\projects"
./tools/Publish-Release.ps1 -Version 1.0.0                # portable zip in artifacts/release
```

A tag such as `v1.0.0` runs [.github/workflows/release.yml](.github/workflows/release.yml),
which tests, builds the zip and opens a draft release with it.

## Support

[GitHub Sponsors](https://github.com/sponsors/PHUICMT) · [Ko-fi](https://ko-fi.com/phuicmt)

## License

MIT
