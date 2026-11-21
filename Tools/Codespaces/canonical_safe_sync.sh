#!/usr/bin/env bash
# Canonical main synchronization. Sent over SSH stdin by an owner-only Actions
# workflow; does not create a worktree, reset, clean, stash, or alter origin.
set -euo pipefail
MODE="${1:-status}"
case "$MODE" in status|sync) ;; *) echo AARIS_SYNC_ERROR=invalid_mode; exit 2 ;; esac
PROJECT=/workspaces/Unity-Game-Aris
if [[ ! -d "$PROJECT/.git" ]]; then
  echo AARIS_SYNC_ERROR=project_checkout_missing
  exit 3
fi
cd "$PROJECT"
if [[ "$(git rev-parse --show-toplevel)" != "$PROJECT" ]]; then
  echo AARIS_SYNC_ERROR=unexpected_checkout_root
  exit 4
fi
BRANCH="$(git branch --show-current)"
BEFORE="$(git rev-parse HEAD)"
echo "AARIS_SYNC_BRANCH=$BRANCH"
echo "AARIS_SYNC_BEFORE_SHA=$BEFORE"
if [[ "$MODE" == status ]]; then
  echo "AARIS_SYNC_TRACKED_CHANGES=$(git status --porcelain=v1 --untracked-files=no | wc -l | tr -d ' ')"
  echo "AARIS_SYNC_UNTRACKED_COUNT=$(git ls-files --others --exclude-standard | wc -l | tr -d ' ')"
  echo AARIS_SYNC_STATUS_COMPLETE=yes
  exit 0
fi
if [[ "$BRANCH" != main ]]; then
  echo AARIS_SYNC_ERROR=main_branch_required
  exit 5
fi
# Except for historical false positive LFS filters, ALL local staged/unstaged
# tracked changes block synchronization. No changes are stashed or overwritten.
python3 - <<'PY'
import subprocess, sys
false_positives = {
    'Assets/AarisWorld/Props/ClimbableTower/Geometry.asset',
    'Assets/AarisWorld/Props/ClimbableTower/Geometry_LOD1.asset',
    'Assets/AarisWorld/Props/ClimbableTower/Geometry_LOD2.asset',
    'Assets/AarisWorld/Props/RockCover/Geometry.asset',
    'Assets/AarisWorld/Props/RockCover/Geometry_LOD1.asset',
    'Assets/AarisWorld/Props/RockCover/Geometry_LOD2.asset',
    'Assets/AarisWorld/Props/Shared/rock_09_ARM.png',
    'Assets/AarisWorld/Props/Shared/rusty_metal_02_ARM.png',
    'Assets/AarisWorld/Props/StaticWreck/Geometry.asset',
    'Assets/AarisWorld/Props/StaticWreck/Geometry_LOD1.asset',
    'Assets/AarisWorld/Props/StaticWreck/Geometry_LOD2.asset',
}
entries = [x for x in subprocess.check_output(
    ['git', 'status', '--porcelain=v1', '--untracked-files=no', '-z']
).split(b'\\0') if x]
for entry in entries:
    status = entry[:2].decode('ascii', 'replace')
    path = entry[3:].decode('utf-8', 'replace')
    # Staged changes are ALWAYS real; never edit the index of another agent.
    if status != ' M' or path not in false_positives:
        print('AARIS_SYNC_ERROR=tracked_local_changes_preserved')
        sys.exit(6)
    row = subprocess.check_output(['git', 'ls-files', '-s', '--', path], text=True)
    if not row or '\\t' not in row:
        print('AARIS_SYNC_ERROR=index_entry_missing')
        sys.exit(6)
    indexed_sha = row.split('\\t')[0].split()[1]
    raw_sha = subprocess.check_output(
        ['git', 'hash-object', '--no-filters', '--', path], text=True
    ).strip()
    if indexed_sha != raw_sha:
        print('AARIS_SYNC_ERROR=real_local_asset_change_preserved')
        sys.exit(6)
print('AARIS_SYNC_TRACKED_WORKTREE_GUARD=pass')
PY
# Fetch the authoritative repo, not a potentially stale Codespace host fork.
# FETCH_HEAD avoids changing origin or its local remote-tracking branches.
CANONICAL=https://github.com/sundamunda289-art/Unity-Game-Aris.git
if ! git -c filter.lfs.process= -c filter.lfs.required=false \
    fetch --quiet --no-tags "$CANONICAL" main; then
  echo AARIS_SYNC_ERROR=canonical_fetch_failed
  exit 7
fi
TARGET="$(git rev-parse FETCH_HEAD)"
echo "AARIS_SYNC_TARGET_SHA=$TARGET"
if ! git merge-base --is-ancestor HEAD FETCH_HEAD; then
  echo AARIS_SYNC_ERROR=non_fast_forward_preserving_local_history
  exit 8
fi
if ! git -c filter.lfs.process= -c filter.lfs.clean=cat \
    -c filter.lfs.required=false merge --ff-only --quiet FETCH_HEAD; then
  echo AARIS_SYNC_ERROR=fast_forward_refused_preserving_worktree
  exit 9
fi
AFTER="$(git rev-parse HEAD)"
if [[ "$AFTER" != "$TARGET" ]]; then
  echo AARIS_SYNC_ERROR=head_mismatch
  exit 10
fi
echo "AARIS_SYNC_AFTER_SHA=$AFTER"
echo AARIS_SYNC_SUCCESS=yes
echo AARIS_SYNC_NO_RESET_NO_CLEAN_NO_STASH_NO_WORKTREE=yes
