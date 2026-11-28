#!/usr/bin/env bash
# selftest.sh - tests update-blocks.sh and rebuild-channel.sh in throw-away sandbox repositories
# (an "upstream" repo and a "fork" repo with a meta worktree that holds a copy of the scripts).
# No network, no dotnet: the build check is replaced by FB_TEST_CMD=true/false.
# Usage: tests/selftest.sh      Prints PASS/FAIL per assertion, exits non-zero on any failure.

set -uo pipefail
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unset FB_REPO FB_WORK
export GIT_AUTHOR_NAME=t GIT_AUTHOR_EMAIL=t@t GIT_COMMITTER_NAME=t GIT_COMMITTER_EMAIL=t@t
export GIT_CONFIG_GLOBAL=/dev/null GIT_CONFIG_SYSTEM=/dev/null   # no user config leaks into the tests
export FB_TEST_CMD=true
FAILS=0
ROOT="$(mktemp -d)"
trap 'rm -rf "${ROOT:?}"' EXIT

# ok <description> <command...>: run the command, print PASS or FAIL.
ok() {
  local desc="$1"; shift
  if "$@" >/dev/null 2>&1; then echo "PASS: $desc"; else echo "FAIL: $desc"; FAILS=$((FAILS + 1)); fi
}
# run <command...>: run it, keep exit code in RC and combined output in OUT.
run() { OUT="$("$@" 2>&1)" && RC=0 || RC=$?; }
# has <text>: the last OUT contains the text.
has() { [[ "$OUT" == *"$1"* ]]; }
# rc_is <n>: the last exit code equals n.
rc_is() { [[ "$RC" == "$1" ]]; }

# --- sandbox helpers -----------------------------------------------------------------------
# commit_file <dir> <file> <content> <message>: write a file and commit it in the checked-out branch of <dir>.
commit_file() {
  mkdir -p "$(dirname "$1/$2")"; printf '%s' "$3" >"$1/$2"
  git -C "$1" add -A; git -C "$1" commit -q -m "$4"
}
# set_line <dir> <file> <n> <text> <message>: replace line n of a file and commit.
set_line() {
  sed -i "${3}s/.*/$4/" "$1/$2"; git -C "$1" commit -q -am "$5"
}
# block <branch> <base> <file> <content> <message>: create a one-commit branch in the fork.
block() {
  git -C "$F" checkout -q -b "$1" "$2"; commit_file "$F" "$3" "$4" "$5"
}
# new_sandbox: fresh upstream + fork with blocks a, c (stacked on a), b, workflows and channel main.
new_sandbox() {
  SB="$(mktemp -d -p "$ROOT")"; U="$SB/upstream"; F="$SB/fork"
  git init -q -b default "$U"
  seq -f 'line%02g' 1 20 >"$U/base.txt"; git -C "$U" add -A; git -C "$U" commit -q -m "upstream 1"
  git init -q -b main "$F"
  git -C "$F" fetch -q "$U" default:refs/remotes/vicocz/default
  git -C "$F" checkout -q -B main refs/remotes/vicocz/default
  UP=refs/remotes/vicocz/default
  # a: new file + change of line 10; c (stacked on a): extends A.txt; b: new file + change of line 15
  block featureblock/a "$UP" A.txt $'a1\n' "tmp"; set_line "$F" base.txt 10 A-10 "tmp"
  git -C "$F" reset -q --soft HEAD~2; git -C "$F" commit -q -m "block a"
  block featureblock/c featureblock/a A.txt $'a1\nc1\n' "block c"
  block featureblock/b "$UP" B.txt $'b1\n' "tmp"; set_line "$F" base.txt 15 B-15 "tmp"
  git -C "$F" reset -q --soft HEAD~2; git -C "$F" commit -q -m "block b"
  block featureblock/workflows "$UP" .github/workflows/J0EK3R-ci.yml $'ci\n' "workflows"
  git -C "$F" worktree add -q --orphan -b featureblock/_meta "$SB/fork.meta"
  mkdir -p "$SB/fork.meta/channels"; cp -r "$HERE/scripts" "$SB/fork.meta/"
  printf 'featureblock/a\nfeatureblock/c featureblock/a\nfeatureblock/b # comment\n' >"$SB/fork.meta/channels/main.conf"
  git -C "$F" checkout -q --detach
  export FB_VICOCZ_URL="$U"
  UPD="$SB/fork.meta/scripts/update-blocks.sh"; REB="$SB/fork.meta/scripts/rebuild-channel.sh"
}
# up_commit <file> <content> <message>: new commit on the sandbox upstream.
up_commit() { commit_file "$U" "$1" "$2" "$3"; }
# subjects <range>: commit subjects of a range in the fork, oldest first, joined by '|'.
subjects() { git -C "$F" log --reverse --format=%s "$1" | paste -sd'|'; }
# all_on_top <branch...>: every branch contains the current upstream tip.
all_on_top() {
  local b
  for b in "$@"; do git -C "$F" merge-base --is-ancestor "$UP" "refs/heads/$b" || return 1; done
}
# resolve <file> <drop-regex>: remove conflict markers and every line matching drop-regex
# (resolves a conflict by keeping the other side).
resolve() { sed -i "/^<<<<<<<\|^=======\|^>>>>>>>\|$2/d" "$1"; }

# --- scenarios ------------------------------------------------------------------------------
echo "# 1: update + rebuild with independent, stacked and workflows blocks"
new_sandbox
OLD_MAIN="$(git -C "$F" rev-parse main)"
up_commit up1.txt $'up\n' "upstream 2"
run "$UPD" main; ok "update exits 0" rc_is 0
ok "all blocks on new upstream" all_on_top featureblock/a featureblock/c featureblock/b featureblock/workflows
ok "stacked block c sits on a" git -C "$F" merge-base --is-ancestor featureblock/a featureblock/c
run "$REB" main --no-fetch; ok "rebuild exits 0" rc_is 0
ok "main = blocks in order, workflows last" [ "$(subjects "$UP..main")" = "block a|block c|block b|workflows" ]
ok "main contains the new upstream commit" git -C "$F" cat-file -e main:up1.txt
ok "backup tag points to the old main" [ "$(git -C "$F" rev-parse "backup/main-$(date +%F)")" = "$OLD_MAIN" ]
ok "no rebuild leftovers" bash -c "! git -C '$F' show-ref --verify --quiet refs/heads/rebuild/main"
ok "no refs/featureblock-old left" [ -z "$(git -C "$F" for-each-ref refs/featureblock-old)" ]

echo "# 2: a block lands upstream (identical patch)"
new_sandbox
up_commit A.txt $'a1\n' "upstream: A.txt like block a"
# the identical patch of block a is only A.txt; make block a identical by dropping its base.txt change
OLD_A="$(git -C "$F" rev-parse featureblock/a)"
git -C "$F" checkout -q featureblock/a; git -C "$F" checkout -q "$UP" -- base.txt; git -C "$F" commit -q --amend --no-edit
git -C "$F" checkout -q featureblock/c; git -C "$F" rebase -q --onto featureblock/a "$OLD_A" featureblock/c; git -C "$F" checkout -q --detach
run "$UPD" main; ok "update exits 0" rc_is 0; ok "block a reported EMPTY" has "featureblock/a is now EMPTY"
ok "block a equals upstream" [ "$(git -C "$F" rev-parse featureblock/a)" = "$(git -C "$F" rev-parse $UP)" ]
printf 'featureblock/c -\nfeatureblock/b\n' >"$SB/fork.meta/channels/main.conf"
run "$REB" main --no-fetch; ok "rebuild after removing a exits 0" rc_is 0
ok "main = c, b, workflows" [ "$(subjects "$UP..main")" = "block c|block b|workflows" ]

echo "# 3: conflict with upstream in a stacked base block"
new_sandbox
up_commit x.txt $'x\n' "upstream 2"; set_line "$U" base.txt 10 UP-10 "upstream changes line 10"
run "$UPD" main; ok "update exits 2" rc_is 2; ok "message names the block" has "CONFLICT while rebasing featureblock/a"
WT="$SB/fork.fbwork/rebase-featureblock_a"
resolve "$WT/base.txt" '^UP-10$'
git -C "$WT" add base.txt; GIT_EDITOR=true git -C "$WT" rebase --continue >/dev/null 2>&1
run "$UPD" main; ok "re-run exits 0" rc_is 0
ok "a and dependent c sit on new upstream" all_on_top featureblock/a featureblock/c
ok "c is stacked on a" git -C "$F" merge-base --is-ancestor featureblock/a featureblock/c
ok "c still has exactly its own commit" [ "$(git -C "$F" rev-list --count featureblock/a..featureblock/c)" = 1 ]

echo "# 4: conflict between two independent blocks at rebuild time, rerere replay"
new_sandbox
block featureblock/x "$UP" base.txt "$(sed '15s/.*/X-15/' "$U/base.txt")"$'\n' "block x"
git -C "$F" checkout -q --detach
printf 'featureblock/b\nfeatureblock/x\n' >"$SB/fork.meta/channels/main.conf"
BEFORE="$(git -C "$F" rev-parse main)"
run "$REB" main --no-fetch; ok "rebuild exits 2" rc_is 2; ok "message says fix in block branch" has "IN THE BLOCK BRANCH"
ok "main unchanged" [ "$(git -C "$F" rev-parse main)" = "$BEFORE" ]
WT="$SB/fork.fbwork/rebuild-main"
resolve "$WT/base.txt" '^B-15$'; sed -i 's/^X-15$/RES-15/' "$WT/base.txt"
git -C "$WT" add base.txt; GIT_EDITOR=true git -C "$WT" cherry-pick --continue >/dev/null 2>&1
run "$REB" main --no-fetch; ok "second rebuild exits 0 (rerere)" rc_is 0
ok "resolution applied" bash -c "git -C '$F' show main:base.txt | grep -qx RES-15"

echo "# 5: failing check"
new_sandbox
BEFORE="$(git -C "$F" rev-parse main)"
FB_TEST_CMD=false run "$REB" main --no-fetch; ok "rebuild exits 3" rc_is 3; ok "message CHECK FAILED" has "CHECK FAILED after block featureblock/a"
ok "main unchanged" [ "$(git -C "$F" rev-parse main)" = "$BEFORE" ]

echo "# 6: checked-out branches"
new_sandbox
git -C "$F" checkout -q main
run "$REB" main --no-fetch; ok "clean checked-out main: exit 0" rc_is 0; ok "main was detached" has "detaching it there"
ok "HEAD detached afterwards" bash -c "! git -C '$F' symbolic-ref -q HEAD"
git -C "$F" checkout -q main; echo dirt >>"$F/base.txt"; BEFORE="$(git -C "$F" rev-parse main)"
run "$REB" main --no-fetch; ok "dirty checked-out main: exit 1" rc_is 1; ok "clear message" has "checked out and has changes"
ok "main unchanged" [ "$(git -C "$F" rev-parse main)" = "$BEFORE" ]
git -C "$F" checkout -q -- base.txt; git -C "$F" checkout -q --detach
up_commit up1.txt $'u\n' "upstream 2"
git -C "$F" checkout -q featureblock/b
run "$UPD" main; ok "clean checked-out block: update exit 0" rc_is 0; ok "block was detached" has "featureblock/b is checked out"
up_commit up2.txt $'u\n' "upstream 3"
git -C "$F" checkout -q featureblock/b; echo dirt >>"$F/B.txt"; git -C "$F" add B.txt; BEFORE="$(git -C "$F" rev-parse featureblock/b)"
run "$UPD" main; ok "dirty checked-out block: exit 1" rc_is 1; ok "block unchanged" [ "$(git -C "$F" rev-parse featureblock/b)" = "$BEFORE" ]
git -C "$F" reset -q --hard; git -C "$F" checkout -q --detach

echo "# 7: channels"
new_sandbox
git -C "$F" checkout -q -b wip/m "$UP"; commit_file "$F" M.txt $'m\n' "wip m"; git -C "$F" checkout -q --detach
C="$SB/fork.meta/channels"
printf 'include main\nwip/m\n' >"$C/X.conf"
run "$REB" X --no-fetch; ok "releases/X builds" rc_is 0
ok "order: main blocks, wip, workflows last" [ "$(subjects "$UP..releases/X")" = "block a|block c|block b|wip m|workflows" ]
printf 'wip/m\n' >"$C/Y.conf"; cp "$C/main.conf" "$SB/main.conf.orig"
printf 'featureblock/a\nwip/m\n' >"$C/main.conf"
run "$REB" main --no-fetch; ok "wip/* in main.conf rejected" rc_is 1; ok "reason shown" has "never be part of main"
printf 'include Y\n' >"$C/main.conf"
run "$REB" main --no-fetch; ok "wip/* via include in main rejected" rc_is 1
cp "$SB/main.conf.orig" "$C/main.conf"
git -C "$F" branch backlog/z "$UP"; printf 'backlog/z\n' >"$C/bl.conf"
run "$REB" bl --no-fetch; ok "backlog/* rejected" rc_is 1; ok "reason shown" has "parked work"
printf 'featureblock/workflows\n' >"$C/wf.conf"
run "$REB" wf --no-fetch; ok "listed workflows rejected" rc_is 1
printf 'include c2\n' >"$C/c1.conf"; printf 'include c1\n' >"$C/c2.conf"
run "$REB" c1 --no-fetch; ok "include cycle rejected" rc_is 1; ok "reason shown" has "include cycle"
for n in Y bl wf c1 c2; do rm "${C:?}/$n.conf"; done
up_commit up1.txt $'u\n' "upstream 2"
run "$UPD" --all; ok "update --all exits 0" rc_is 0
ok "a rebased exactly once" [ "$(grep -c 'rebasing featureblock/a ' <<<"$OUT")" = 1 ]
ok "wip/m and b on new upstream" all_on_top wip/m featureblock/b

echo
if (( FAILS )); then echo "$FAILS FAILURE(S)"; exit 1; fi
echo "ALL PASSED"
