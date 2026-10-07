# Hakari privacy policy

Last updated: 7 October 2026

Hakari is a Windows taskbar meter and dashboard for Claude Code usage, made by PHUICMT. This page says what it reads, what it keeps, and what it sends.

## What Hakari reads on your PC

- **Claude Code's local log files** (the `.claude` folders in your user folder, in WSL, and in any folder you add). From each response it reads only usage fields: token counts, model name, time, session id, working folder and git branch. It never reads or stores the text of your prompts or of the replies.
- **Session titles**, only if you turn on *Session titles* in Settings. These are the short titles Claude Code writes for each session.
- **Whether a response included thinking**, only if you turn on *Count thinking*. Hakari looks at the kind of each part of a response, never at what it says, and keeps only yes or no.
- **Your Claude Code sign-in**, only for accounts where you turn on usage limits. Hakari reads the sign-in token Claude Code keeps on your PC to ask Anthropic for that account's 5-hour and weekly usage. The token is used for that request only; Hakari does not copy it or store it anywhere else. If you turn on *Renew sign-in automatically*, Hakari renews an expired sign-in with Anthropic and writes the new one back into Claude Code's own sign-in file, only while Claude Code is not running.

## What Hakari keeps

Everything stays on your PC, in `%LOCALAPPDATA%\Hakari`: your settings, an index of the usage fields above, limit readings, and exchange rates. Nothing is uploaded. Deleting that folder removes it all; in the Microsoft Store version, uninstalling Hakari removes it.

## What Hakari sends over the network

Hakari makes only these requests. None of them carries your usage, prompts, replies or anything that identifies you beyond what any web request does (such as your IP address).

| Request | When | Sent to |
|---|---|---|
| Your account's usage limits | For accounts where you turned limits on | Anthropic (`api.anthropic.com`), with that account's own sign-in |
| Renewing that sign-in | When it expires, if *Renew sign-in automatically* is on | Anthropic (`console.anthropic.com`) |
| Public exchange rates | When you show costs in a currency other than US dollars | Frankfurter (`api.frankfurter.dev`) or ExchangeRate-API (`open.er-api.com`) |
| Newer model prices | When you press *Check now* under Settings, Prices | GitHub (`raw.githubusercontent.com`) |
| The public release list | Once a day, in the GitHub download only, unless you turn off *Check for new versions* | GitHub (`api.github.com`) |

Hakari has no analytics, no telemetry, no ads and no accounts of its own.

## Children

Hakari is a developer tool and is not directed at children.

## Changes

If this policy changes, the new version will be posted on this page with a new date.

## Contact

Questions: open an issue at https://github.com/PHUICMT/hakari/issues

Hakari is not affiliated with or endorsed by Anthropic. "Claude" is a trademark of Anthropic, PBC.
