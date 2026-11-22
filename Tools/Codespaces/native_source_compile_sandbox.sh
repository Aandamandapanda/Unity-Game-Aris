#!/usr/bin/env bash
# Read-only game source; compile a throwaway COPY under /tmp with installed Unity.
# Never open, migrate, import or mutate /workspaces/Unity-Game-Aris in this probe.
set -Eeuo pipefail
umask 077
SOURCE=/workspaces/Unity-Game-Aris
if [[ ! -d "$SOURCE/.git" || ! -f "$SOURCE/Packages/manifest.json" ]]; then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=source_missing
  exit 21
fi
EDITOR=""
for candidate in "$HOME/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity" \
                 "/workspaces/.aaris-unity-editors/6000.3.26f1/Editor/Unity" \
                 "/tmp/aaris-unity-runtime/editors/6000.3.26f1/Editor/Unity"; do
  if [[ -x "$candidate" ]]; then EDITOR="$candidate"; break; fi
done
if [[ -z "$EDITOR" ]]; then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=editor_missing
  exit 22
fi
# A live original Editor can be testing gameplay or importing Assets. Do not
# compete with the user's graphical session, even with a separate project copy.
if pgrep -f '[E]ditor/Unity.*-projectPath /workspaces/Unity-Game-Aris' >/dev/null 2>&1; then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=game_editor_busy
  exit 23
fi
available="$(df -Pk /tmp | awk 'NR==2 {print $4}')"
if [[ ! "$available" =~ ^[0-9]+$ ]] || ((available < 4194304)); then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=tmp_disk_low
  exit 24
fi
BASE=/tmp/aaris-native-csharp
mkdir -p "$BASE"
WORK="$(mktemp -d "$BASE/compile.XXXXXXXX")"
case "$WORK" in "$BASE"/compile.*) ;; *) echo AARIS_NATIVE_COMPILE_PREFLIGHT=unsafe_tmp; exit 25 ;; esac
cleanup() { rm -rf -- "$WORK"; }
trap cleanup EXIT
mkdir -p "$WORK/project/Assets/Editor" "$WORK/project/Packages" \
         "$WORK/project/ProjectSettings" "$WORK/UnityTemp"
# Only native C# and assembly definition inputs enter this temporary project.
# No LFS, scenes, prefabs, ProjectSettings migration, or additional worktree.
(
  cd "$SOURCE"
  find Assets -type f \( -name '*.cs' -o -name '*.asmdef' -o -name '*.asmref' -o -name '*.rsp' \) -print0 |
  while IFS= read -r -d '' path; do
    cp --parents -- "$path" "$WORK/project"
  done
)
cp "$SOURCE/ProjectSettings/ProjectVersion.txt" "$WORK/project/ProjectSettings/ProjectVersion.txt"
cp "$SOURCE/Packages/manifest.json" "$WORK/project/Packages/manifest.json"
# The same installed Editor passed an exact full-manifest package import
# when Unity generated its own temporary lock, while a previous temporary
# project importing the preexisting repository lock failed before C#.
# Do not touch or rewrite the SOURCE lock: omit it only in /tmp.
echo AARIS_NATIVE_COMPILE_TEMP_PACKAGE_LOCK=omitted
count="$(find "$WORK/project/Assets" -type f -name '*.cs' | wc -l | tr -d ' ')"
echo "AARIS_NATIVE_COMPILE_GAME_SCRIPTS=$count"
if ((count < 90)); then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=incomplete_source
  exit 26
fi
# Unity executes the method only after all Editor and runtime C# assemblies
# are imported successfully. The sentinel is proof only for this copied
# scripts-only project, never proof of full-scene PlayMode or device FPS.
cat > "$WORK/project/Assets/Editor/AarisNativeSourceCompileGate.cs" <<'CS'
using UnityEditor;
using UnityEngine;
public static class AarisNativeSourceCompileGate
{
    public static void Run()
    {
        Debug.Log("AARIS_NATIVE_SOURCE_COMPILE_METHOD_PASSED");
    }
}
CS
LOG="$WORK/unity-editor.private.log"
rc=0
timeout --kill-after=10s 490s env TMPDIR="$WORK/UnityTemp" \
  "$EDITOR" -batchmode -nographics -quit \
  -projectPath "$WORK/project" \
  -executeMethod AarisNativeSourceCompileGate.Run \
  -logFile "$LOG" > "$WORK/editor-stdout.private" \
  2> "$WORK/editor-stderr.private" || rc=$?
python3 - "$LOG" "$rc" <<'PY'
from pathlib import Path
import re
import sys

log = Path(sys.argv[1])
rc = int(sys.argv[2])
print("AARIS_NATIVE_COMPILE_EDITOR_EXIT=" + str(rc))
print("AARIS_NATIVE_COMPILE_LOG_FOUND=" + ("yes" if log.is_file() else "no"))
source = log.read_bytes()[-12000000:].decode("utf-8", "replace") if log.is_file() else ""
codes = re.findall(r"\berror\s+(CS\d{4})\s*:", source)
for code in sorted(set(codes))[:12]:
    print("AARIS_NATIVE_COMPILE_ERROR_" + code + "=yes")
print("AARIS_NATIVE_COMPILE_CSHARP_ERRORS=" + str(len(codes)))
sentinel = "AARIS_NATIVE_SOURCE_COMPILE_METHOD_PASSED" in source
print("AARIS_NATIVE_COMPILE_METHOD_RAN=" + ("yes" if sentinel else "no"))
if rc == 0 and sentinel and not codes:
    print("AARIS_NATIVE_COMPILE_RESULT=source_only_pass")
    raise SystemExit(0)
reason = (
    "timeout" if rc == 124 else
    "editor_crash" if rc in (137, 139) else
    "csharp_errors" if codes else
    "license_or_package_or_import_error"
)
print("AARIS_NATIVE_COMPILE_RESULT=" + reason)
raise SystemExit(27)
PY
