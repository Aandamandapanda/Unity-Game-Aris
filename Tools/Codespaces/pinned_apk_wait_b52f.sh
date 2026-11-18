#!/usr/bin/env bash
# Read-only bridge from GitHub Actions to an ALREADY RUNNING pinned Codespace build.
# Never starts a second Unity Editor or changes the live workspace.
set -Eeuo pipefail
PIN=b52f81ed0db73dd2b19a39d8484528748e9b0fde
BASE=/tmp/aaris-codespaces/android-apk
STAGE="$BASE/pinned-b52f-build-stage"
PROJECT="$BASE/native-source-$PIN"
PIDFILE="$BASE/pinned-b52f-build.pid"
APK="$BASE/Build/AarisGame.apk"
REPORT="$BASE/pinned-b52f-apk-verified.json"

test "$(git -C "$PROJECT" rev-parse HEAD)" = "$PIN" || {
  echo "::error::PINNED_SOURCE_SHA_CHANGED"; exit 21;
}
test -f "$STAGE" || { echo "::error::PINNED_BUILD_STAGE_MISSING"; exit 22; }
grep -qxF "PINNED_BUILD_SOURCE_SHA=$PIN" "$STAGE" || {
  echo "::error::PINNED_BUILD_SOURCE_SHA_MISMATCH"; exit 23;
}
echo "WATCHING_EXACT_PIN=$PIN"
for poll in $(seq 1 900); do
  if grep -qE 'PINNED_APK_.*(FAILED|INCOMPLETE|TIMEOUT|BLOCKED)' "$STAGE"; then
    echo "::error::PINNED_BUILD_FAILED"
    tail -8 "$STAGE"
    exit 24
  fi
  if grep -qxF PINNED_APK_READY_FOR_DEVICE_INSTALL_TEST "$STAGE"; then
    test -s "$APK" && test -s "$REPORT" && test -s "$BASE/Build/AarisGame.apk.sha256" || {
      echo "::error::PINNED_APK_EVIDENCE_MISSING"; exit 25;
    }
    (cd "$BASE/Build" && sha256sum -c AarisGame.apk.sha256)
    python3 - "$REPORT" "$PIN" "$APK" <<'PY'
import hashlib,json,sys
from pathlib import Path
j=json.loads(Path(sys.argv[1]).read_text())
apk=Path(sys.argv[3])
assert j["source_sha"]==sys.argv[2]
assert j["package"]=="com.aaris.game"
assert j["version_code"]==20261116
assert j["bytes"]==apk.stat().st_size
assert hashlib.sha256(apk.read_bytes()).hexdigest()==j["sha256"]
print("PINNED_APK_READ_ONLY_VERIFIED")
PY
    exit 0
  fi
  test -s "$PIDFILE" || { echo "::error::PINNED_BUILD_PIDFILE_MISSING"; exit 26; }
  pid="$(cat "$PIDFILE")"
  [[ "$pid" =~ ^[0-9]+$ ]] || { echo "::error::PINNED_BUILD_PID_INVALID"; exit 27; }
  if ! kill -0 "$pid" 2>/dev/null; then
    echo "::error::PINNED_BUILD_STOPPED_WITHOUT_VALIDATED_APK"
    tail -10 "$STAGE"
    exit 28
  fi
  if (( poll % 6 == 1 )); then
    echo "PINNED_APK_STILL_RUNNING_POLL=$poll"
    tail -4 "$STAGE"
  fi
  sleep 20
done
echo "::error::PINNED_BUILD_WAIT_LIMIT_REACHED"
exit 29
