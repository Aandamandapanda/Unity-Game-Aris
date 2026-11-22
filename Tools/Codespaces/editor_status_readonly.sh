#!/usr/bin/env bash
# Owner-only exact-machine read-only diagnostics; no editor launch or project mutation.
set -Eeuo pipefail
python3 - <<'PY'
from pathlib import Path
import os
import re
import time

project = Path("/workspaces/Unity-Game-Aris")
print("AARIS_EDITOR_STATUS_PROJECT_FOUND=" + ("yes" if (project / ".git").exists() else "no"))
candidates = (
    Path.home() / ".local/share/aaris-unity-editors/6000.3.26f1/Editor/Unity",
    Path("/workspaces/.aaris-unity-editors/6000.3.26f1/Editor/Unity"),
    Path("/tmp/aaris-unity-runtime/editors/6000.3.26f1/Editor/Unity"),
)
present = any(p.is_file() and os.access(p, os.X_OK) for p in candidates)
print("AARIS_EDITOR_STATUS_BINARY_PRESENT=" + ("yes" if present else "no"))

live = 0
for proc in Path("/proc").iterdir():
    if not proc.name.isdecimal():
        continue
    try:
        if proc.stat().st_uid != os.getuid():
            continue
        argv = (proc / "cmdline").read_bytes().split(b"\0")
        argv = [item.decode("utf-8", "replace") for item in argv if item]
        if not argv or not argv[0].endswith("/Editor/Unity"):
            continue
        if "-projectPath" not in argv:
            continue
        position = argv.index("-projectPath")
        if position + 1 < len(argv) and argv[position + 1] == str(project):
            live += 1
    except (OSError, ValueError):
        continue
print("AARIS_EDITOR_STATUS_PROJECT_PROCESS_COUNT=" + str(live))

logs = (
    Path("/tmp/aaris-codespaces/Editor.log"),
    Path.home() / ".config/unity3d/Editor.log",
)
available = [p for p in logs if p.is_file()]
print("AARIS_EDITOR_STATUS_LOG_FOUND=" + ("yes" if available else "no"))
fresh = any(time.time() - p.stat().st_mtime < 1800 for p in available)
print("AARIS_EDITOR_STATUS_LOG_RECENT=" + ("yes" if fresh else "no"))
# Diagnostic only. Log age and regex cannot prove a clean *current* Unity import.
pattern = re.compile(r"\berror\s+CS\d{4}\s*:")
error_count = 0
for p in available:
    try:
        tail = p.read_bytes()[-6000000:].decode("utf-8", "replace")
        error_count += len(pattern.findall(tail))
    except OSError:
        print("AARIS_EDITOR_STATUS_LOG_READABLE=no")
        continue
print("AARIS_EDITOR_STATUS_LOG_CSHARP_ERRORS=" + str(error_count))
print("AARIS_EDITOR_STATUS_NATIVE_COMPILE_PROVEN=no")
PY
