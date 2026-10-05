# Hakari 秤

A lightweight Windows taskbar meter and dashboard for **Claude Code** usage. It combines every place you use Claude Code (Windows, WSL distros, extra config dirs, other machines) and every account into one real total.

> Not affiliated with or endorsed by Anthropic. "Claude" is a trademark of Anthropic, PBC.

- Live cost, limits, and burn rate on the taskbar, in a layout you choose
- Incremental indexing: reads only new log bytes, so it stays fast with logs of several GB
- Correct cache pricing (5-minute vs 1-hour cache writes, fast mode)
- Local-only. Hakari never reads prompt or response text and sends no telemetry.

Status: **pre-alpha / design**. See [docs/SPEC.md](docs/SPEC.md), [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), [docs/ROADMAP.md](docs/ROADMAP.md).

## Dev

```powershell
dotnet test
dotnet run --project src/Hakari.Cli -c Release            # scan %USERPROFILE%\.claude\projects
dotnet run --project src/Hakari.Cli -c Release -- "\\wsl.localhost\Ubuntu-24.04\home\<user>\.claude\projects"
```

## Support

[Ko-fi](https://ko-fi.com/phuicmt)

## License

MIT
