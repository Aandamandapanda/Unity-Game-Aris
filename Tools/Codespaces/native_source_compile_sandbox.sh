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
# The installed Linux Unity Editor has a native graphics crash during some
# -nographics source imports even with a valid license. Native C# import was
# independently verified with xvfb-run + software OpenGL on this machine.
# This affects ONLY the disposable CI project; never change game graphics.
if ! command -v xvfb-run >/dev/null 2>&1 ||
   ! command -v Xvfb >/dev/null 2>&1; then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=xvfb_missing
  exit 28
fi
# Do not compete with another agent's temporary Unity import either.
if pgrep -f '[E]ditor/Unity' >/dev/null 2>&1; then
  echo AARIS_NATIVE_COMPILE_PREFLIGHT=other_editor_busy
  exit 29
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
# This is a C# source-only check, not an import/build of the shipping asset set.
# On the headless Linux Editor, unused Visual Effect Graph's PNG importer can
# crash Unity in native Crunch semaphore/TLS code before C# is even compiled.
# Omit VFX only from this /tmp manifest, and only if copied C# does not use it.
# Future VFX-using scripts keep their actual dependency for compilation.
if grep -R --include='*.cs' -Eq 'UnityEngine[.]VFX|UnityEditor[.]VFX|\bVisualEffect\b|\bVisualEffectAsset\b|\bVFXEventAttribute\b' "$WORK/project/Assets"; then
  echo AARIS_NATIVE_COMPILE_TEMP_VFX=retained_csharp_references
else
  python3 - "$WORK/project/Packages/manifest.json" <<'PYVFX'
import json
from pathlib import Path
import sys
path = Path(sys.argv[1])
manifest = json.loads(path.read_text())
version = manifest.get("dependencies", {}).pop("com.unity.visualeffectgraph", None)
if version is not None:
    path.write_text(json.dumps(manifest, indent=2) + "\n")
print("AARIS_NATIVE_COMPILE_TEMP_VFX=" +
      ("omitted_unused_package" if version else "not_declared"))
PYVFX
fi
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
echo AARIS_NATIVE_COMPILE_BACKEND=xvfb_software_gl
# Keep TMPDIR at the verified project parent and cap native importer workers.
# Both changes affect the /tmp C# gate only, not the actual Unity project.
# The extra VFX omission above prevents a native importer crash unrelated to C#.
timeout --kill-after=10s 490s env TMPDIR="$WORK" LIBGL_ALWAYS_SOFTWARE=1 \
  xvfb-run -a -s '-screen 0 1280x720x24' \
  "$EDITOR" -batchmode -force-glcore -job-worker-count 2 -quit \
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
