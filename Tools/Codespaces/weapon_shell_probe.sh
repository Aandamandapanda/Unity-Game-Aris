#!/usr/bin/env bash
# Read-only Codespace SSH transport diagnosis. Never print secrets or diffs.
set -euo pipefail
echo 'WEAPON_SSH_REMOTE_BASH_ENTERED'
if test -d /workspaces/Unity-Game-Aris; then
  echo 'weapon_project_dir=present'
else
  echo 'weapon_project_dir=missing'
  exit 0
fi
cd /workspaces/Unity-Game-Aris
if git rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo 'weapon_git_repo=present'
  printf 'weapon_branch=%s\n' "$(git branch --show-current || true)"
  printf 'weapon_head=%s\n' "$(git rev-parse HEAD || true)"
  printf 'weapon_tracked_changes=%s\n' "$(git status --porcelain=v1 --untracked-files=no | wc -l | tr -d '[:space:]')"
else
  echo 'weapon_git_repo=missing'
fi
if pgrep -f '[E]ditor/Unity.*-projectPath' >/dev/null 2>&1; then
  echo 'weapon_unity_editor=running'
else
  echo 'weapon_unity_editor=stopped'
fi
echo 'WEAPON_SSH_PROBE_COMPLETE_READ_ONLY'
