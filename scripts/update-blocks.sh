#!/usr/bin/env bash
# update-blocks.sh - bring the blocks of one channel (or of all channels) onto the current
# vicocz/default.
#
# Usage: update-blocks.sh [<channel>|--all] [--no-fetch] [--status]
#   <channel>   channel to update (default: main); --all = the union of all channels
#   --no-fetch  do not fetch vicocz/default first
#   --status    only print how far every block is behind, change nothing
#
# Every block is rebased in manifest order: an independent block (needs `-`) onto
# vicocz/default, a stacked block with `rebase --onto` onto its (already moved) dependency.
# wip/* and PR/vicocz/* branches listed in a channel are moved too.  Commits that are already
# upstream (same patch-id) are dropped by git, so a block that landed upstream becomes empty.
# Exit codes: 0 ok, 1 error, 2 conflict (resolve, then re-run), see README.md.
# Never pushes.

# shellcheck source=lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

CHANNEL=main; ALL=0; FETCH=1; STATUS=0
for arg in "$@"; do
  case "$arg" in
    --all) ALL=1 ;;
    --no-fetch) FETCH=0 ;;
    --status) STATUS=1 ;;
    -h|--help) sed -n '2,15p' "${BASH_SOURCE[0]}"; exit 0 ;;
    -*) die "unknown option $arg" ;;
    *) CHANNEL="$arg" ;;
  esac
done

# build_union: fill U_REFS[] and U_NEEDS[] with the deduplicated blocks of the selected channels.
build_union() {
  local -a channels=() ; local ch i j found
  if (( ALL )); then mapfile -t channels < <(list_channels); else channels=("$CHANNEL"); fi
  U_REFS=(); U_NEEDS=()
  for ch in "${channels[@]}"; do
    read_manifest "$ch"
    for i in "${!REFS[@]}"; do
      found=-1
      for j in "${!U_REFS[@]}"; do [[ "${U_REFS[$j]}" == "${REFS[$i]}" ]] && found=$j; done
      if (( found >= 0 )); then
        [[ "${U_NEEDS[$found]}" == "${NEEDS[$i]}" ]] \
          || die "${REFS[$i]} has different 'needs' in two channels (${U_NEEDS[$found]} vs ${NEEDS[$i]})"
      else
        U_REFS+=("${REFS[$i]}"); U_NEEDS+=("${NEEDS[$i]}")
      fi
    done
  done
}

# base_of_union <i>: base ref of union entry i.
base_of_union() {
  if [[ "${U_NEEDS[$1]}" == - ]]; then echo "$FB_UP"; else echo "refs/heads/${U_NEEDS[$1]}"; fi
}

# print_status: how far behind its base every block is.
print_status() {
  local i base
  for i in "${!U_REFS[@]}"; do
    base="$(base_of_union "$i")"
    printf '%-40s behind base: %s   own commits: %s\n' "${U_REFS[$i]}" \
      "$(g rev-list --count "refs/heads/${U_REFS[$i]}..$base")" \
      "$(g rev-list --count "$base..refs/heads/${U_REFS[$i]}")"
  done
  say "vicocz/default is $(g rev-parse --short "$FB_UP")"
}

# rebase_block <worktree> <args...>: run `git rebase`; whenever it stops with everything
# resolved (rerere) continue it.  Returns 1 if a real conflict is left.
rebase_block() {
  local wt="$1"; shift
  git -C "$wt" rebase "$@" || true
  while rebase_in_progress "$wt"; do
    # a conflict remains if files are unmerged or changes are not staged
    if has_unmerged "$wt" || ! git -C "$wt" diff --quiet; then return 1; fi
    say "  conflict resolved by rerere, continuing"
    GIT_EDITOR=true git -C "$wt" rebase --continue || return 1
  done
}

# update_block <i>: move block i onto its base (see header).
update_block() {
  local i="$1" ref base wt old
  ref="${U_REFS[$i]}"; base="$(base_of_union "$i")"
  wt="$FB_WORK/rebase-${ref//\//_}"
  # a worktree left over from an earlier, interrupted run
  if [[ -d "$wt" ]]; then
    if rebase_in_progress "$wt"; then
      die "a rebase of $ref is still in progress in $wt: resolve, 'git add', 'git rebase --continue' there, then re-run"
    fi
    g worktree remove --force "$wt"
  fi
  if g merge-base --is-ancestor "$base" "refs/heads/$ref"; then
    say "up to date: $ref"; return 0
  fi
  # remember the old tip (needed to rebase blocks stacked on this one)
  g show-ref --verify --quiet "refs/featureblock-old/$ref" \
    || g update-ref "refs/featureblock-old/$ref" "refs/heads/$ref"
  free_branch "$ref"
  mkdir -p "$FB_WORK"
  g worktree add -q "$wt" "$ref"
  say "rebasing $ref onto $base ..."
  if [[ "${U_NEEDS[$i]}" == - ]]; then
    rebase_block "$wt" "$base" || conflict "$ref" "$wt"
  else
    old="refs/featureblock-old/${U_NEEDS[$i]}"
    g show-ref --verify --quiet "$old" \
      || die "cannot rebase $ref: no saved old tip of ${U_NEEDS[$i]} ($old); rebase it manually with 'git rebase --onto ${U_NEEDS[$i]} <old tip> $ref'"
    rebase_block "$wt" --onto "$base" "$old" || conflict "$ref" "$wt"
  fi
  g worktree remove --force "$wt"
  if [[ "$(g rev-list --count "$base..refs/heads/$ref")" == 0 ]]; then
    say "*** $ref is now EMPTY - probably landed upstream. Retire it (README procedure R3). ***"
  fi
}

# conflict <ref> <worktree>: report a rebase conflict and stop with exit code 2.
conflict() {
  say ""
  say "CONFLICT while rebasing $1."
  say "Worktree: $2"
  say "Conflicting files:"; git -C "$2" diff --name-only --diff-filter=U | sed 's/^/  /'
  say "Resolve it there, 'git add' the files, 'git rebase --continue', then run update-blocks.sh again"
  say "(it continues). rerere remembers the resolution for the next time."
  exit 2
}

build_union
enable_rerere
OLD_UP="$(g rev-parse --verify -q "$FB_UP" || true)"
(( FETCH )) && fetch_upstream
g rev-parse --verify -q "$FB_UP" >/dev/null || die "$FB_UP does not exist (run without --no-fetch)"
if (( STATUS )); then print_status; exit 0; fi

for i in "${!U_REFS[@]}"; do update_block "$i"; done

# all blocks moved: the saved old tips are no longer needed
g for-each-ref --format='%(refname)' refs/featureblock-old/ | while read -r r; do g update-ref -d "$r"; done
NEW_UP="$(g rev-parse "$FB_UP")"
if [[ -n "$OLD_UP" && "$OLD_UP" != "$NEW_UP" ]]; then
  say ""; say "New commits in vicocz/default:"; g log --oneline "$OLD_UP..$NEW_UP"
fi
say ""; say "Done. Next: rebuild-channel.sh <channel> (not pushed anything)."
