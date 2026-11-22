#!/usr/bin/env bash
# Identify which Unity package families fail to import using only /tmp copies.
# Original game Assets, scenes, packages, local changes and license are read-only.
set -Eeuo pipefail
umask 077
PROJECT=/workspaces/Unity-Game-Aris
if [[ ! -d "$PROJECT/.git" || ! -f "$PROJECT/Packages/manifest.json" ]]; then
  echo AARIS_PKG_MATRIX_RESULT=source_unavailable
  exit 20
fi
EDITOR=""
for candidate in "$HOME/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity" \
                 /workspaces/.aaris-unity-editors/6000.3.26f1/Editor/Unity \
                 /tmp/aaris-unity-runtime/editors/6000.3.26f1/Editor/Unity; do
  if [[ -x "$candidate" ]]; then EDITOR="$candidate"; break; fi
done
if [[ -z "$EDITOR" ]]; then
  echo AARIS_PKG_MATRIX_RESULT=editor_binary_missing
  exit 21
fi
if pgrep -f '[E]ditor/Unity.*-projectPath /workspaces/Unity-Game-Aris' >/dev/null 2>&1; then
  echo AARIS_PKG_MATRIX_RESULT=original_editor_busy
  exit 22
fi
available="$(df -Pk /tmp | awk 'NR==2{print $4}')"
if [[ ! "$available" =~ ^[0-9]+$ ]] || (( available < 4194304 )); then
  echo AARIS_PKG_MATRIX_RESULT=tmp_disk_insufficient
  exit 23
fi
BASE="$(mktemp -d /tmp/aaris-unity-package-matrix.XXXXXXXX)"
case "$BASE" in /tmp/aaris-unity-package-matrix.*) ;; *) echo AARIS_PKG_MATRIX_RESULT=unsafe_tmp; exit 24 ;; esac
trap 'rm -rf -- "$BASE"' EXIT
mkdir -p "$BASE/temp"
python3 - "$PROJECT/Packages/manifest.json" \
  "$PROJECT/ProjectSettings/ProjectVersion.txt" "$BASE" <<'PY'
import json
from pathlib import Path
import sys

source = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))
version = Path(sys.argv[2]).read_text(encoding="utf-8")
base = Path(sys.argv[3])
deps = source["dependencies"]
modules = {k: v for k, v in deps.items() if k.startswith("com.unity.modules.")}
cases = {
    "modules": modules,
    "ugui": dict(modules, **{"com.unity.ugui": deps["com.unity.ugui"]}),
    "urp": dict(modules, **{
        "com.unity.ugui": deps["com.unity.ugui"],
        "com.unity.render-pipelines.universal":
            deps["com.unity.render-pipelines.universal"],
    }),
    "vfx": dict(modules, **{
        "com.unity.ugui": deps["com.unity.ugui"],
        "com.unity.visualeffectgraph": deps["com.unity.visualeffectgraph"],
    }),
    "full": deps,
    "full_graphics": deps,
}
for label, selected in cases.items():
    dest = base / label
    for sub in ("Assets", "ProjectSettings", "Packages"):
        (dest / sub).mkdir(parents=True)
    (dest / "ProjectSettings/ProjectVersion.txt").write_text(version)
    (dest / "Packages/manifest.json").write_text(
        json.dumps({"dependencies": selected}, indent=2) + "\n"
    )
PY
# Only fixed labels, anonymous counts and known error families leave the machine.
report_case() {
  local name="$1" rc="$2"
  python3 - "$name" "$rc" "$BASE/$name.private.log" \
    "$BASE/$name.stdout.private" "$BASE/$name.stderr.private" <<'PY'
from pathlib import Path
import re
import sys

name, rc = sys.argv[1].upper(), int(sys.argv[2])
paths = [Path(x) for x in sys.argv[3:]]
log = "\n".join(p.read_bytes()[-3500000:].decode("utf-8", "replace")
                for p in paths if p.is_file())
signals = {
    "NATIVE_CRASH": r"segmentation fault|sigsegv|sigabrt|native crash|got a signal|signal 11",
    "LICENSE": r"no valid license|licensing client.*fail|license.*(?:expired|not activated)",
    "PACKAGE_RESOLVE": r"failed to resolve packages|upm.*(?:failed|error)|package manager.*(?:failed|error)",
    "MISSING_LIB": r"error while loading shared libraries|symbol lookup error",
    "SHADER_IMPORT": r"shader compiler.*(?:crash|fail)|shader.*import.*(?:fail|error)",
}
print("AARIS_PKG_MATRIX_" + name + "_EXIT=" + str(rc))
print("AARIS_PKG_MATRIX_" + name + "_LOG=" +
      ("yes" if paths[0].is_file() else "no"))
for key, pattern in signals.items():
    print("AARIS_PKG_MATRIX_" + name + "_" + key + "=" +
          ("yes" if re.search(pattern, log, re.I) else "no"))
errors = len(re.findall(r"\berror\s+CS\d{4}\s*:", log))
print("AARIS_PKG_MATRIX_" + name + "_CS_ERRORS=" + str(errors))
result = ("pass" if rc == 0 and errors == 0 else
          "timeout" if rc == 124 else
          "csharp_errors" if errors else
          "native_crash_signal" if re.search(signals["NATIVE_CRASH"], log, re.I) else
          "startup_or_import_failure")
print("AARIS_PKG_MATRIX_" + name + "_RESULT=" + result)
PY
}
for name in modules ugui urp vfx full; do
  rc=0
  timeout --kill-after=8s 74s env TMPDIR="$BASE/temp" \
    "$EDITOR" -batchmode -nographics -quit \
    -projectPath "$BASE/$name" \
    -logFile "$BASE/$name.private.log" \
    >"$BASE/$name.stdout.private" 2>"$BASE/$name.stderr.private" || rc=$?
  report_case "$name" "$rc"
done
# Graphics-backed control for the SAME pinned packages. The user has Unity
# installed; -nographics can mask GPU-initialization dependent import failures.
# xvfb-run picks a private display, never touches the user's :99 session.
if command -v xvfb-run >/dev/null 2>&1 && command -v Xvfb >/dev/null 2>&1; then
  rc=0
  timeout --kill-after=8s 125s xvfb-run -a \
    -s '-screen 0 1280x720x24 -nolisten tcp' \
    env TMPDIR="$BASE/temp" LIBGL_ALWAYS_SOFTWARE=1 \
    "$EDITOR" -batchmode -force-glcore -quit \
    -projectPath "$BASE/full_graphics" \
    -logFile "$BASE/full_graphics.private.log" \
    >"$BASE/full_graphics.stdout.private" \
    2>"$BASE/full_graphics.stderr.private" || rc=$?
  report_case full_graphics "$rc"
else
  echo AARIS_PKG_MATRIX_FULL_GRAPHICS_RESULT=xvfb_unavailable
fi
echo AARIS_PKG_MATRIX_RESULT=diagnostic_completed
