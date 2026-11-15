#!/usr/bin/env bash
# Ground-only real Unity pose QA in the user's existing Codespace Editor.
# Delegates flight/landing/stance authority to AarisPoseFrames; never fabricates
# walk/sprint labels from an aerial Game view. Source/scenes/Git are not edited.
set -Eeuo pipefail
umask 077
export DISPLAY=:99
ROOT=/workspaces/Unity-Game-Aris
POSE=/tmp/aaris-codespaces/motion-frames
REQ="$POSE/capture.request"
REPORT="$POSE/report.txt"
PHASE="$POSE/phase.txt"
test -d "$ROOT/.git" || { echo LIVE_REVIEW_WRONG_PROJECT; exit 11; }
cd "$ROOT"
pgrep -f '[E]ditor/Unity.*-projectPath' >/dev/null || { echo LIVE_REVIEW_EDITOR_NOT_RUNNING; exit 15; }
HEAD="$(git rev-parse --short HEAD)"
BEFORE="$(git status --porcelain=v1 --untracked-files=no | sha256sum | cut -d ' ' -f1)"
mkdir -p "$POSE"
rm -f "$REQ" "$POSE"/01_*.jpg "$POSE"/02_*.jpg "$POSE"/03_*.jpg "$POSE"/04_*.jpg "$POSE"/05_*.jpg "$POSE"/06_*.jpg
printf 'ground-pose-review\n' >"$REQ"
# AarisPoseFrames owns Play Mode, drop transition, QA-only touchdown fast-forward,
# fixed flat survey teleport and camera restoration. Do not inject movement keys.
for _ in $(seq 1 190); do
  if [[ -f "$PHASE" ]] && grep -qx '07_COMPLETE' "$PHASE" 2>/dev/null; then break; fi
  sleep .5
done
[[ -f "$PHASE" ]] && grep -qx '07_COMPLETE' "$PHASE" || {
  echo LIVE_REVIEW_POSE_TIMEOUT
  [[ -f "$PHASE" ]] && printf 'LIVE_REVIEW_LAST_PHASE=%s\n' "$(cat "$PHASE")"
  exit 31
}
[[ -f "$REPORT" ]] || { echo LIVE_REVIEW_REPORT_MISSING; exit 32; }
grep -q 'SUCCESS_18_REAL_FRAMES' "$REPORT" || {
  echo LIVE_REVIEW_POSE_FAILED
  tail -n 24 "$REPORT"
  exit 33
}
python3 - "$POSE" <<'PY'
from pathlib import Path
import re,sys
p=Path(sys.argv[1]); report=(p/'report.txt').read_text(errors='replace')
names=('01_Standing','02_Crouch','03_Prone','04_Walk','05_Sprint','06_Jump')
for name in names:
    frames=sorted(p.glob(name+'_f*.jpg'))
    if len(frames)!=3 or any(f.stat().st_size<4000 for f in frames):
        raise SystemExit('LIVE_REVIEW_FRAME_SET_INVALID_'+name)
# Ground locomotion proof must actually be post-touchdown. Jump is intentionally
# allowed airborne frames, but standing through sprint may never be aerial.
for name in names[:5]:
    lines=[x for x in report.splitlines() if ('FRAME '+name+' ') in x]
    if len(lines)!=3 or any('air=False' not in x for x in lines):
        raise SystemExit('LIVE_REVIEW_NOT_GROUNDED_'+name)
if 'EDITOR_ONLY_FAST_FORWARD_TO_TOUCHDOWN' not in report:
    raise SystemExit('LIVE_REVIEW_TOUCHDOWN_NOT_PROVEN')
if 'EDITOR_ONLY_FIXED_SURVEY_LOCATION' not in report:
    raise SystemExit('LIVE_REVIEW_SURVEY_NOT_PROVEN')
measures=[x for x in report.splitlines() if x.startswith('MEASURE stance=')]
if len(measures)<5:
    raise SystemExit('LIVE_REVIEW_PHYSICAL_MEASUREMENTS_MISSING')
print('LIVE_REVIEW_18_GROUND_POSE_FRAMES_CAPTURED')
print('LIVE_REVIEW_GROUND_STATES_VERIFIED=Standing,Crouch,Prone,Walk,Sprint')
print('LIVE_REVIEW_PHYSICAL_MEASUREMENTS='+str(len(measures)))
PY
AFTER="$(git status --porcelain=v1 --untracked-files=no | sha256sum | cut -d ' ' -f1)"
if [[ "$BEFORE" == "$AFTER" ]]; then
  echo LIVE_REVIEW_TRACKED_WORKTREE_UNCHANGED
else
  echo LIVE_REVIEW_TRACKED_WORKTREE_CHANGED_REVIEW_REQUIRED
  exit 34
fi
printf 'LIVE_REVIEW_SOURCE_HEAD=%s\n' "$HEAD"
tar -C "$POSE" -czf "$POSE/ground-pose-frames.tar.gz" ./*.jpg report.txt phase.txt
echo LIVE_REVIEW_GROUND_EVIDENCE_READY
