#!/usr/bin/env bash
# rebuild-channel.sh - regenerate a channel branch (main or releases/<name>) from its channel
# file: vicocz/default + the listed blocks (cherry-picked in order) + the workflows block.
#
# Usage: rebuild-channel.sh <channel>|--all [--no-fetch] [--no-check]
#   --no-fetch  do not fetch vicocz/default first
#   --no-check  skip the build/test check after each block
#
# The channel branch is only moved when EVERYTHING succeeded, after a backup tag
# backup/<branch with / -> ->-<date> was created.  Fix problems in the block branch,
# never on the channel branch.  Exit codes: 0 ok, 1 error, 2 cherry-pick conflict,
# 3 failed check.  Never pushes.

# shellcheck source=lib.sh
source "$(dirname "${BASH_SOURCE[0]}")/lib.sh"

CHANNEL=""; ALL=0; FETCH=1; CHECK=1
for arg in "$@"; do
  case "$arg" in
    --all) ALL=1 ;;
    --no-fetch) FETCH=0 ;;
    --no-check) CHECK=0 ;;
    -h|--help) sed -n '2,14p' "${BASH_SOURCE[0]}"; exit 0 ;;
    -*) die "unknown option $arg" ;;
    *) CHANNEL="$arg" ;;
  esac
done
[[ -n "$CHANNEL" || $ALL == 1 ]] || die "usage: rebuild-channel.sh <channel>|--all [--no-fetch] [--no-check]"

# pick <worktree> <sha>: cherry-pick one commit; returns 1 on a real conflict.
# Handles the cases git reports as errors: a commit that became empty (already upstream)
# is skipped, a conflict fully resolved by rerere is committed.
pick() {
  local wt="$1" sha="$2"
  git -C "$wt" cherry-pick "$sha" >/dev/null 2>&1 && return 0
  git_dir_has "$wt" CHERRY_PICK_HEAD || return 1
  if has_unmerged "$wt" || ! git -C "$wt" diff --quiet; then return 1; fi
  if git -C "$wt" diff --cached --quiet; then
    say "  dropped $(git -C "$wt" show -s --format='%h %s' "$sha") (already in the base)"
    git -C "$wt" cherry-pick --skip >/dev/null
  else
    say "  conflict in $(git -C "$wt" show -s --format='%h %s' "$sha") resolved by rerere, please review"
    GIT_EDITOR=true git -C "$wt" cherry-pick --continue >/dev/null
  fi
}

# backup_tag <branch>: create backup/<branch>-<date>[-n] at the branch tip and print its name.
backup_tag() {
  local base tag n=1
  base="backup/${1//\//-}-$(date +%F)"; tag="$base"
  while g show-ref --verify --quiet "refs/tags/$tag"; do n=$((n + 1)); tag="$base-$n"; done
  g tag "$tag" "refs/heads/$1"
  echo "$tag"
}

# rebuild <channel>: the whole procedure for one channel.
rebuild() {
  local channel="$1" branch tmp wt i ref base sha oldtip="" tag=""
  branch="$(channel_branch "$channel")"; tmp="rebuild/$channel"
  wt="$FB_WORK/rebuild-${channel//\//_}"
  say "=== Rebuilding channel $channel -> $branch ==="
  read_manifest "$channel"
  # pre-checks: every block must already sit on its base
  for i in "${!REFS[@]}"; do
    base="$(base_of "$i")"
    g merge-base --is-ancestor "$base" "refs/heads/${REFS[$i]}" \
      || die "${REFS[$i]} is not based on ${base#refs/heads/}: run update-blocks.sh first"
    [[ "$(g rev-list --count "$base..refs/heads/${REFS[$i]}")" != 0 ]] \
      || say "WARNING: ${REFS[$i]} has no own commits (landed upstream? see README R3)"
  done
  free_branch "$branch"
  # a worktree left over from an interrupted run is a throw-away: start from scratch
  if [[ -d "$wt" ]]; then g worktree remove --force "$wt"; fi
  mkdir -p "$FB_WORK"
  g worktree add -q -B "$tmp" "$wt" "$FB_UP"
  for i in "${!REFS[@]}"; do
    ref="${REFS[$i]}"; base="$(base_of "$i")"
    say "--- block $ref"
    g cherry "$FB_UP" "refs/heads/$ref" | sed -n 's/^- /  already upstream (patch-id): /p'
    while read -r sha; do
      [[ -n "$sha" ]] || continue
      pick "$wt" "$sha" || stop_conflict "$ref" "$sha" "$wt"
    done < <(g rev-list --reverse --no-merges "$base..refs/heads/$ref")
    if [[ "${CHECKS[$i]}" == test ]] && (( CHECK )); then
      say "  running check: $FB_TEST_CMD"
      (cd "$wt" && bash -c "$FB_TEST_CMD") || {
        say ""; say "CHECK FAILED after block $ref. Fix it in the block branch (never on $branch), then re-run."
        say "Worktree kept: $wt ($branch is unchanged)"; exit 3; }
    fi
  done
  # success: backup, then move the channel branch
  if branch_exists "$branch"; then
    oldtip="$(g rev-parse "refs/heads/$branch")"; tag="$(backup_tag "$branch")"
    say "Backup tag $tag -> ${oldtip:0:7}"
  fi
  g branch -f "$branch" "$tmp"
  g worktree remove --force "$wt"
  g branch -D "$tmp" >/dev/null
  report "$branch" "$oldtip" "$tag"
}

# stop_conflict <block> <sha> <worktree>: explain a cherry-pick conflict and exit 2.
stop_conflict() {
  say ""
  say "CONFLICT in block $1 at commit $(git -C "$3" show -s --format='%h %s' "$2")."
  say "Conflicting files:"; git -C "$3" diff --name-only --diff-filter=U | sed 's/^/  /'
  say "Fix it IN THE BLOCK BRANCH (never on the channel branch): usually make the later block"
  say "'need' the earlier one or adapt it, then re-run rebuild-channel.sh (it starts from scratch;"
  say "rerere replays resolutions you committed in $3). Worktree kept at $3."
  say "The channel branch is unchanged."
  exit 2
}

# report <branch> <old-tip> <tag>: summary and the push command for the product owner.
report() {
  local branch="$1" oldtip="$2" tag="$3" mb remote_sha
  say ""; say "Result: git log --oneline vicocz/default..$branch"
  g log --oneline "$FB_UP..refs/heads/$branch"
  if [[ -n "$tag" ]]; then
    mb="$(g merge-base "$FB_UP" "$tag")"
    say ""; say "range-diff old ($tag) vs new:"
    g range-diff "$mb..$tag" "$FB_UP..refs/heads/$branch" || true
  fi
  say ""
  say "NOT verified here: the WinUI/MAUI build. Build it in Visual Studio before publishing."
  remote_sha="$(g rev-parse --verify -q "refs/remotes/origin/$branch" || true)"
  if [[ -n "$remote_sha" ]]; then
    say "Publish (only on approval): git push --force-with-lease=$branch:$remote_sha origin $branch"
  else
    say "Publish (only on approval): git push origin $branch"
  fi
}

enable_rerere
(( FETCH )) && fetch_upstream
if (( ALL )); then
  mapfile -t ALL_CHANNELS < <(list_channels)
  for c in "${ALL_CHANNELS[@]}"; do rebuild "$c"; done
else
  rebuild "$CHANNEL"
fi
