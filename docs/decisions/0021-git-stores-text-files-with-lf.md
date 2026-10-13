# 0021. Git stores text files with LF

- **Status:** Accepted
- **Date:** 2026-10-13

## Context
Git for Windows defaults to `core.autocrlf=true`, which turns CRLF into LF on commit, so every text file in this
repo's index is LF while a Windows checkout is CRLF (the root `.editorconfig` asks editors for CRLF) and a Linux or
macOS checkout, CI's included, is LF. That rests on each committer's git: a machine with `core.autocrlf=false` commits
CRLF as it is, nothing notices, and the repair is a renormalizing commit over every affected file. Two kinds of files
also need LF in every checkout, whatever the setting: the git hooks and shell scripts, which bash runs on every OS and
which trip over a CR, and deploy files read by Linux tools in containers (Docker, Caddy). Projects built from this
template ran into both (UnifiedPackageTracker ADR 0066/0067, Podium).

## Decision
A root `.gitattributes`:
- `* text=auto` (no `eol`): git decides which files are text, as `core.autocrlf` already did, and stores them with
  LF whatever the committer's settings, while each checkout keeps its machine's line endings.
- `eol=lf` for `.githooks/*`, `*.sh`, `deploy/**` and `.dockerignore`: LF in every checkout.

Added while every text file in the index is LF, so `git add --renormalize .` stages nothing and no checkout needs a
refresh. The hooks are also stored with the executable bit (`git add --chmod=+x .githooks/*`): Windows never records
it, and git on Linux or macOS skips a hook stored without it.

## Consequences
- LF in the index no longer depends on anyone's settings, for the whole repo, the .NET files included.
- Checkouts with `core.autocrlf=true` (CRLF) and on Linux and macOS (LF) stay as they are. A Windows checkout with
  `core.autocrlf=false` turns CRLF as git rewrites its files (`core.eol` defaults to `native`), which the build accepts.
- Anything that compares files byte for byte must accept both endings: a `web/` Prettier config sets
  `"endOfLine": "auto"` for that reason (UnifiedPackageTracker ADR 0066).
- Git's own detection decides what is text; a binary file (an icon) is left alone. A file that must be CRLF in every
  checkout, Linux and macOS included (a `.cmd` script), needs a line of its own, `*.cmd text eol=crlf`; a binary file
  git would take for text needs `binary`.

## Alternatives considered
- **Keep relying on `core.autocrlf`** — rejected: nothing would notice a CRLF commit, and the repair then is a
  renormalizing commit over every affected file, where adding `* text=auto` now changes nothing.
- **`* text=auto eol=lf`** — LF checkouts everywhere; rejected: `.editorconfig` asks for CRLF, so it would contradict
  the checkout and have to change as well, and every existing checkout would need every file checked out again.
- **A line per file type** (`*.cs text`, `*.ts text`, …) — rejected: a list to keep up with every new kind of file,
  for what git's detection already does; explicit lines are for exceptions.
