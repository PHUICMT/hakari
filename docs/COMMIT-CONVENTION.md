# Commit Convention

[Conventional Commits 1.0](https://www.conventionalcommits.org/). The `.githooks/commit-msg` hook enforces it.

```
<type>(<scope>): <subject>

<body>

<footer>
```

## Rules

- **type** (required): `feat` `fix` `perf` `refactor` `test` `docs` `build` `ci` `chore` `style` `revert`
- **scope** (optional, but use one when possible): `core` `app` `cli` `sync` `pricing` `taskbar` `dashboard` `store` `i18n` `deps`
- **subject**: English, imperative ("add", not "added"), lowercase first letter, no trailing period, at most 72 chars for the header.
- **body**: optional. Explain *why*. Wrap at 72.
- **breaking change**: `feat(core)!: ...` plus a `BREAKING CHANGE: ...` footer.
- **issue refs**: `Refs: #12` / `Closes: #12` in the footer.
- Author email must be `icmtchannel@gmail.com`.
- No co-author trailers or tool attribution lines. The hook rejects any term that matches the local `hakari.commit.blockedPattern` git config.

## Examples

```
feat(taskbar): add two-line layout template
fix(pricing): price 1h cache writes at 2x input
perf(core): read only appended bytes from tracked logs
docs(store): add partner center checklist
```

## Setup (once per clone)

```sh
git config core.hooksPath .githooks
git config commit.template .gitmessage
git config user.email icmtchannel@gmail.com
git config hakari.commit.blockedPattern '<extended regex of blocked terms>'
```

Files listed in `.git/info/exclude` can't be committed (pre-commit check).
