#!/usr/bin/env bash
# Isolate Unity binary/licensing from game C# in a throwaway empty /tmp project.
# No writes, resets, imports or editor launches against /workspaces/Unity-Game-Aris.
set -Eeuo pipefail
umask 077
EDITOR=""
for candidate in "$HOME/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity" \
                 "/workspaces/.aaris-unity-editors/6000.3.26f1/Editor/Unity" \
                 "/tmp/aaris-unity-runtime/editors/6000.3.26f1/Editor/Unity"; do
  if [[ -x "$candidate" ]]; then EDITOR="$candidate"; break; fi
done
if [[ -z "$EDITOR" ]]; then echo AARIS_UNITY_BASELINE_RESULT=editor_missing; exit 11; fi
if pgrep -f '[E]ditor/Unity.*-projectPath /workspaces/Unity-Game-Aris' >/dev/null 2>&1; then
  echo AARIS_UNITY_BASELINE_RESULT=original_editor_busy
  exit 12
fi
ROOT="$(mktemp -d /tmp/aaris-unity-empty.XXXXXXXX)"
case "$ROOT" in /tmp/aaris-unity-empty.*) ;; *) echo AARIS_UNITY_BASELINE_RESULT=invalid_tmp; exit 13 ;; esac
trap 'rm -rf -- "$ROOT"' EXIT
mkdir -p "$ROOT/Project/Assets" "$ROOT/Project/ProjectSettings" "$ROOT/UnityTemp"
printf 'm_EditorVersion: 6000.3.26f1\n' > "$ROOT/Project/ProjectSettings/ProjectVersion.txt"
version_rc=0
timeout --kill-after=10s 40s "$EDITOR" -version \
  > "$ROOT/version-stdout.private" 2> "$ROOT/version-stderr.private" || version_rc=$?
echo "AARIS_UNITY_BASELINE_VERSION_EXIT=$version_rc"
if ((version_rc != 0)); then
  echo AARIS_UNITY_BASELINE_RESULT=editor_binary_start_failed
  exit 20
fi
empty_rc=0
timeout --kill-after=10s 170s env TMPDIR="$ROOT/UnityTemp" \
  "$EDITOR" -batchmode -nographics -quit -projectPath "$ROOT/Project" \
  -logFile "$ROOT/empty-editor.private.log" \
  > "$ROOT/empty-stdout.private" 2> "$ROOT/empty-stderr.private" || empty_rc=$?
echo "AARIS_UNITY_BASELINE_EMPTY_EXIT=$empty_rc"
python3 - "$ROOT/empty-editor.private.log" "$ROOT/empty-stdout.private" \
  "$ROOT/empty-stderr.private" "$empty_rc" <<'PY'
from pathlib import Path
import re
import sys
paths = [Path(x) for x in sys.argv[1:4]]
code = int(sys.argv[4])
s = "\n".join(p.read_bytes()[-2000000:].decode("utf-8", "replace")
              for p in paths if p.is_file())
print("AARIS_UNITY_BASELINE_LOG_FOUND=" + ("yes" if paths[0].is_file() else "no"))
# Only fixed categories, never raw lines, authentication strings or file paths.
signals = {
    "license": r"no valid license|license.*(?:invalid|expired|not activated)|activation.*(?:fail|denied)|licensing client.*fail",
    "missing_library": r"error while loading shared libraries|symbol lookup error",
    "package": r"(?:package manager|upm).*(?:failed|error)|failed to resolve packages",
    "crash": r"segmentation fault|crash handler|native crash|SIGSEGV",
}
for name, pattern in signals.items():
    found = bool(re.search(pattern, s, re.I))
    print("AARIS_UNITY_BASELINE_SIGNAL_" + name.upper() + "=" + ("yes" if found else "no"))
if code == 0:
    print("AARIS_UNITY_BASELINE_RESULT=empty_editor_pass")
    raise SystemExit(0)
print("AARIS_UNITY_BASELINE_RESULT=" +
      ("timeout" if code == 124 else "editor_crash" if code in (137, 139)
       else "empty_editor_failed"))
raise SystemExit(21)
PY
