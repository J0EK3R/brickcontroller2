# Feature blocks of the brickcontroller2 fork

`main` is a GENERATED branch: `vicocz/default` + an ordered list of feature blocks (small stacked
branches) + the workflows block. Further release channels `releases/<name>` are generated the same way.
**Fixes are made in the block branch, never on `main` / `releases/*`.** Nothing is pushed by the scripts;
publishing needs the explicit approval of the product owner.

## Branches and tags
| Name | Meaning |
|---|---|
| `vicocz/default` (ref `refs/remotes/vicocz/default`) | upstream; there is no `vicocz` remote, the scripts fetch it by URL |
| `featureblock/<name>` | one block, few commits (ideally 1), based on `vicocz/default` or on the block it `needs` |
| `featureblock/workflows` | the CI workflow commit (`J0EK3R-*` files only); appended automatically, always LAST |
| `featureblock/_meta` | this branch: channel files, scripts, selftest (checked out as worktree `brickcontroller2.meta`) |
| `PR/vicocz/<name>` | pull request to vicocz: only the upstream-relevant change, no workflow commit |
| `wip/<name>` | work in progress (e.g. `wip/MKH4.0`, `wip/DISettings`). NEVER in `main`; MAY be in `releases/*` channels. When finished it becomes a `featureblock/*` or `PR/vicocz/*` |
| `backlog/<name>` | parked work, never built, in no channel file |
| `main`, `releases/<name>` | GENERATED channels |
| `backup/<branch with / -> ->-<date>[-n]` | tag of a channel branch before it was rewritten (`backup/main-2026-11-28`, `backup/releases-MKH4.0-2026-11-28`) |
| `release/*` (singular), `Release-*` tags | old snapshots, not touched by the scripts |

## Channel files `channels/<name>.conf`
`main.conf` -> branch `main`; any other `<name>.conf` -> branch `releases/<name>`. One block per line in
cherry-pick order, `#` starts a comment:
```
<branch>   [<needs>]   [<check>]
include <channel>          # inserts all entries of another channel here
```
* `<needs>`: `-` (default, based on `vicocz/default`) or a branch listed EARLIER that the block is stacked on.
* `<check>`: `test` (default) runs `$FB_TEST_CMD` (default `dotnet test` of `BrickController2.Tests`) after the
  block, `none` skips it.
* `featureblock/workflows` must NOT be listed. `backlog/*` is rejected everywhere, `wip/*` in `main`
  (also via `include`). A block may depend on at most one other block.

## Scripts (run from the meta worktree; never push)
* `scripts/update-blocks.sh [<channel>|--all] [--no-fetch] [--status]` - fetch `vicocz/default`, rebase every
  block of the channel (incl. listed `wip/*` and `PR/vicocz/*`). Independent blocks: `rebase <upstream>`
  (commits already upstream are dropped by patch-id); stacked blocks: `rebase --onto` their moved dependency.
  A block that becomes empty is reported (procedure R3). Conflict: exit 2, resolve in the printed worktree,
  `git rebase --continue`, re-run.
* `scripts/rebuild-channel.sh <channel>|--all [--no-fetch] [--no-check]` - build `rebuild/<channel>` from
  `vicocz/default`, cherry-pick block after block, run the check after each, and only when everything
  succeeded create the backup tag and move the channel branch. Conflict: exit 2, failed check: exit 3; the
  channel branch stays unchanged. `git rerere` is enabled, so a resolution is replayed next time.
* If a channel or block branch is checked out somewhere, a clean checkout is detached automatically; a dirty
  one aborts with a clear message.
* `tests/selftest.sh` - sandbox test of both scripts (no network, no dotnet).
* The scripts cannot build WinUI/MAUI: build WinUI in Visual Studio before publishing.

## Procedures
* R1 New upstream state: `update-blocks.sh --all`, then `rebuild-channel.sh main` (and every other channel).
* R2 Fix or extend a block: `git switch featureblock/<name>`, commit, fold into the block commit
  (`--amend` or `--fixup` + `rebase -i --autosquash`). Stacked dependents:
  `git rebase --onto featureblock/<dep> <old dep tip> featureblock/<dependent>`. Then rebuild.
* R3 Block landed upstream (merged PR, or reported EMPTY): first
  `git rebase --onto vicocz/default featureblock/<landed> featureblock/<dependent>` for dependents, then remove
  the line from the channel file, keep the branch for a while, rebuild.
* R4 New block: `git switch -c featureblock/<name> vicocz/default` (or the dependency), commit, add a line to
  the channel file (commit on `featureblock/_meta`), rebuild.
* R5 Upstream PR: `git switch -c PR/vicocz/<name> vicocz/default`; only the upstream change, no workflow commit.
* R6 Publish (only on approval): `git push --force-with-lease origin featureblock/<name> ...` and
  `git push --force-with-lease=<branch>:<sha of origin/<branch>> origin <branch>` (the rebuild prints it).
* R7 New channel: create `channels/<name>.conf` (usually `include main` + extra entries, `wip/*` allowed),
  commit on `featureblock/_meta`, `rebuild-channel.sh <name>`. Retire it by deleting file and branch.
