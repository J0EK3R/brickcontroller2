#!/usr/bin/env bash
# lib.sh - shared helpers of the feature-block scripts (sourced by update-blocks.sh,
# rebuild-channel.sh and the selftest; never executed directly).
#
# Concepts:
#   * A "channel" is a generated branch: `main`, or `releases/<name>` for every other channel.
#   * A channel file `channels/<name>.conf` lists the blocks (branches) of the channel in
#     cherry-pick order.  `featureblock/workflows` is appended implicitly and is always last.
#   * Nothing in this file (or in the scripts that use it) ever pushes.

set -euo pipefail

# Directory of the meta worktree (parent of scripts/), where channels/ lives.
META_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# The git repository the meta worktree belongs to: parent of the common git dir
# (<repo>/.git).  FB_REPO may override it (used by tests).
if [[ -z "${FB_REPO:-}" ]]; then
  _common="$(git -C "$META_DIR" rev-parse --path-format=absolute --git-common-dir)"
  FB_REPO="$(dirname "$_common")"
fi
REPO="$FB_REPO"

# URL of the upstream repository and the local ref it is fetched into
# (there is deliberately no `vicocz` remote).
FB_VICOCZ_URL="${FB_VICOCZ_URL:-https://github.com/vicocz/brickcontroller2}"
FB_UP="refs/remotes/vicocz/default"

# Directory for temporary worktrees (next to the repository by default).
FB_WORK="${FB_WORK:-$(dirname "$REPO")/$(basename "$REPO").fbwork}"

# Build check run inside the worktree after each block that has check `test`.
# -clp:ErrorsOnly keeps the output short (warnings of the test projects are noisy).
FB_TEST_CMD="${FB_TEST_CMD:-dotnet test BrickController2/BrickController2.Tests/BrickController2.Tests.csproj --nologo --verbosity quiet -clp:ErrorsOnly}"

# Make a user-level dotnet installation usable when it is not on PATH.
if ! command -v dotnet >/dev/null 2>&1 && [[ -x "$HOME/.dotnet/dotnet" ]]; then
  export DOTNET_ROOT="$HOME/.dotnet"
  export PATH="$PATH:$HOME/.dotnet"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1

# Name of the block that holds the CI workflow commit; always the last block.
FB_WORKFLOWS="featureblock/workflows"

# g: run git in the main repository, whatever the current directory is.
g() { git -C "$REPO" "$@"; }

# say: progress message on stdout.
say() { printf '%s\n' "$*"; }

# die: print an error on stderr and abort with exit code 1.
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

# channel_branch <channel>: branch generated for a channel.
channel_branch() {
  if [[ "$1" == main ]]; then echo main; else echo "releases/$1"; fi
}

# list_channels: names of all channel files, `main` first.
list_channels() {
  local f n
  [[ -f "$META_DIR/channels/main.conf" ]] && echo main
  for f in "$META_DIR"/channels/*.conf; do
    [[ -e "$f" ]] || continue
    n="$(basename "$f" .conf)"
    [[ "$n" == main ]] || echo "$n"
  done
}

# branch_exists <branch>: true if refs/heads/<branch> exists.
branch_exists() { g show-ref --verify --quiet "refs/heads/$1"; }

# _load_channel <channel> <stack>: append the entries of one channel file (recursing into
# `include` lines) to the global arrays REFS, NEEDS and CHECKS.  <stack> lists the channels
# currently being loaded and is used to detect include cycles.
_load_channel() {
  local channel="$1" stack="$2" file line
  local -a words
  file="$META_DIR/channels/$channel.conf"
  [[ -f "$file" ]] || die "channel file not found: $file"
  case "$stack:" in
    *":$channel:"*) die "include cycle: ${stack#:} -> $channel" ;;
  esac
  stack="$stack:$channel"
  while IFS= read -r line || [[ -n "$line" ]]; do
    line="${line%%#*}"                      # strip comments
    words=()
    read -r -a words <<<"$line" || true     # split on whitespace
    (( ${#words[@]} > 0 )) || continue      # skip blank lines
    if [[ "${words[0]}" == include ]]; then
      (( ${#words[@]} == 2 )) || die "$file: 'include' needs exactly one channel name"
      _load_channel "${words[1]}" "$stack"
    else
      REFS+=("${words[0]}")
      NEEDS+=("${words[1]:--}")
      CHECKS+=("${words[2]:-test}")
    fi
  done <"$file"
}

# read_manifest <channel>: resolve a channel into the global arrays REFS[] NEEDS[] CHECKS[]
# (blocks in cherry-pick order, workflows appended last) and validate it.
read_manifest() {
  local channel="$1" i j ref needs found
  REFS=(); NEEDS=(); CHECKS=()
  _load_channel "$channel" ""
  for i in "${!REFS[@]}"; do
    ref="${REFS[$i]}"; needs="${NEEDS[$i]}"
    case "$ref" in
      "$FB_WORKFLOWS") die "$ref must not be listed: it is appended automatically as the last block" ;;
      backlog/*) die "$ref: backlog/* branches are parked work and must not be listed in any channel" ;;
      featureblock/_meta) die "$ref is the meta branch, not a block" ;;
      featureblock/*|PR/vicocz/*) ;;
      wip/*)
        [[ "$channel" != main ]] || die "$ref: wip/* branches must never be part of main (only of releases/* channels)" ;;
      *) die "$ref: unsupported branch name (expected featureblock/*, PR/vicocz/* or, in releases/* channels, wip/*)" ;;
    esac
    branch_exists "$ref" || die "branch $ref (channel $channel) does not exist"
    case "${CHECKS[$i]}" in test|none) ;; *) die "$ref: unknown check '${CHECKS[$i]}' (use test or none)" ;; esac
    for (( j = 0; j < i; j++ )); do
      [[ "${REFS[$j]}" != "$ref" ]] || die "$ref is listed twice in channel $channel"
    done
    if [[ "$needs" != - ]]; then
      found=0
      for (( j = 0; j < i; j++ )); do [[ "${REFS[$j]}" == "$needs" ]] && found=1; done
      (( found )) || die "$ref needs $needs, which must be listed EARLIER in channel $channel"
    fi
  done
  branch_exists "$FB_WORKFLOWS" || die "branch $FB_WORKFLOWS does not exist"
  REFS+=("$FB_WORKFLOWS"); NEEDS+=(-); CHECKS+=(none)
}

# base_of <i>: the commit/ref block i is stacked on (upstream, or the block it needs).
base_of() {
  if [[ "${NEEDS[$1]}" == - ]]; then echo "$FB_UP"; else echo "refs/heads/${NEEDS[$1]}"; fi
}

# fetch_upstream: update the local ref of vicocz/default by URL.
fetch_upstream() {
  say "Fetching upstream $FB_VICOCZ_URL (default) ..."
  g fetch --no-tags -q "$FB_VICOCZ_URL" "+default:$FB_UP"
}

# enable_rerere: remember conflict resolutions (and apply them) in this clone.
enable_rerere() {
  g config rerere.enabled true
  g config rerere.autoupdate true
}

# git_dir_has <worktree> <name>: true if the worktree's git dir contains <name>
# (e.g. rebase-merge, CHERRY_PICK_HEAD).
git_dir_has() {
  [[ -e "$(git -C "$1" rev-parse --path-format=absolute --git-path "$2")" ]]
}

# rebase_in_progress <worktree>: true while a rebase is stopped in that worktree.
rebase_in_progress() { git_dir_has "$1" rebase-merge || git_dir_has "$1" rebase-apply; }

# has_unmerged <worktree>: true if there are files with unresolved conflicts.
has_unmerged() { [[ -n "$(git -C "$1" diff --name-only --diff-filter=U)" ]]; }

# worktree_busy <worktree>: true if it has uncommitted changes to tracked files or an
# operation (rebase, merge, cherry-pick, revert) in progress.  Untracked files are ignored
# because detaching HEAD does not touch them.
worktree_busy() {
  local wt="$1" name
  [[ -z "$(git -C "$wt" status --porcelain --untracked-files=no)" ]] || return 0
  for name in rebase-merge rebase-apply MERGE_HEAD CHERRY_PICK_HEAD REVERT_HEAD; do
    ! git_dir_has "$wt" "$name" || return 0
  done
  return 1
}

# free_branch <branch>: make sure <branch> is not checked out anywhere so that it can be
# moved.  A clean checkout is detached automatically; otherwise abort with a clear message.
free_branch() {
  local branch="$1" line wt="" holder=""
  while IFS= read -r line; do
    case "$line" in
      "worktree "*) wt="${line#worktree }" ;;
      "branch refs/heads/$branch") holder="$wt" ;;
    esac
  done < <(g worktree list --porcelain)
  [[ -n "$holder" ]] || return 0
  if worktree_busy "$holder"; then
    die "$branch is checked out and has changes (or an operation in progress) in $holder: commit/stash/finish there, then re-run"
  fi
  say "$branch is checked out in $holder (clean): detaching it there"
  git -C "$holder" checkout -q --detach
}
