#!/usr/bin/env bash
# Fresh-import immutable APK from newer commit 1f13e49. Never clone a prior commit's Unity Library.
# Reuses only the official Android toolchain and verified Git LFS assets; native player script binding must come from a fresh Library.
set -Eeuo pipefail
umask 077
PIN=1f13e49f9e43011b13b525c3a4e17b2fa3231188
BASE=/tmp/aaris-codespaces/android-apk
PROJECT="$BASE/native-source-$PIN"
EDITOR="$BASE/managed-editors/6000.3.26f1/Editor/Unity"
ANDROID="$BASE/managed-editors/6000.3.26f1/Editor/Data/PlaybackEngines/AndroidPlayer"
APK="$BASE/Build/AarisGame.apk"
STATUS="$BASE/pinned-1f13-build-stage"
mkdir -p "$BASE/Build" "$BASE/UnityTemp"
exec 9>"$BASE/pinned-1f13-build.lock"
flock -n 9 || { echo PINNED_APK_ALREADY_RUNNING; exit 30; }
say() { echo "$*" | tee -a "$STATUS"; }
failed() { rc=$?; trap - ERR; say "PINNED_APK_FAILED_EXIT_$rc"; exit "$rc"; }
trap failed ERR
say "PINNED_BUILD_SOURCE_SHA=$PIN"
say "PINNED_BUILD_START_UTC=$(date -u +%FT%TZ)"
# Previous exit 137: do not overlap a second interactive Unity Editor.
if pgrep -af '[U]nity -projectPath /workspaces/Unity-Game-Aris' | grep -q '/Editor/Unity'; then
  say PINNED_APK_BLOCKED_OTHER_EDITOR_ACTIVE
  exit 36
fi
if [ "$(awk '/MemAvailable:/ {print $2}' /proc/meminfo)" -lt 6291456 ]; then
  say PINNED_APK_BLOCKED_LOW_AVAILABLE_RAM
  exit 37
fi
ready=0
for n in $(seq 1 240); do
  if grep -qxF TOOLCHAIN_ANDROID_MODULES_VERIFIED "$BASE/pinned-21607-toolchain-stage" 2>/dev/null &&
     grep -qxF FRESH_SOURCE_ALL_LFS_VERIFIED_NO_STALE_LIBRARY "$BASE/pinned-1f13-source-stage" 2>/dev/null; then
    ready=1; break
  fi
  if grep -Eq 'TOOLCHAIN_.*(FAILED|INCOMPLETE)|PINNED_SOURCE_.*FAILED' \
    "$BASE/pinned-1f13-toolchain-stage" "$BASE/pinned-1f13-source-stage" 2>/dev/null; then
    say PINNED_APK_TOOLCHAIN_OR_SOURCE_FAILED; exit 31
  fi
  if (( n % 2 == 1 )); then say PINNED_APK_WAITING_FOR_ANDROID_SUPPORT; fi
  sleep 30
done
test "$ready" -eq 1 || { say PINNED_APK_TOOLCHAIN_TIMEOUT; exit 32; }
test "$(git -C "$PROJECT" rev-parse HEAD)" = "$PIN"
test -x "$EDITOR" && test -x "$ANDROID/OpenJDK/bin/java"
test -d "$ANDROID/NDK/toolchains" && test -d "$ANDROID/SDK/build-tools"
test -s "$PROJECT/Assets/TerrainDemoScene_URP/Scenes/TerrainDemoScene.unity"
# Prior attempt imported a Library from another commit and the serialized
# player MonoScript resolved to null despite correct GUID and source type.
# Fresh source stage guarantees the Unity Library did NOT exist before launch.
# Abort if another batch Unity is still active; never run overlapping builds.
if pgrep -af '[U]nity.*-batchmode.*native-source-' | grep -q '/Editor/Unity'; then
  say PINNED_APK_BLOCKED_PARALLEL_NATIVE_EDITOR
  exit 38
fi
say PINNED_APK_FRESH_LIBRARY_REIMPORT_REQUIRED
say PINNED_APK_ISOLATED_SOURCE_AND_TOOLS_READY

export MALLOC_ARENA_MAX=2
export JAVA_HOME="$ANDROID/OpenJDK"
export PATH="$JAVA_HOME/bin:$PATH"
export AARIS_ANDROID_JDK_ROOT="$JAVA_HOME"
export AARIS_ANDROID_SDK_ROOT="$ANDROID/SDK"
export AARIS_ANDROID_NDK_ROOT="$ANDROID/NDK"
export AARIS_ANDROID_COMPILE_SDK=34
export TMPDIR="$BASE/UnityTemp"
export DISPLAY=:99 LIBGL_ALWAYS_SOFTWARE=1

# Unity's existing Android builder checks both SDK platform 33 and 35.
# The Android module for 6000.3 may ship 34/35 but not older SDK 33.
if test ! -f "$ANDROID/SDK/platforms/android-33/android.jar"; then
  say PINNED_APK_ANDROID_SDK33_INSTALLING
  SDKMANAGER="$(find "$ANDROID/SDK/cmdline-tools" -type f -name sdkmanager -print -quit)"
  test -x "$SDKMANAGER"
  set +o pipefail
  yes | timeout 900 "$SDKMANAGER" --sdk_root="$ANDROID/SDK" \
     "platforms;android-33" >"$BASE/private-sdk33-install.log" 2>&1
  sdk_rc=$?
  set -o pipefail
  test "$sdk_rc" -eq 0
fi
test -f "$ANDROID/SDK/platforms/android-35/android.jar"
test -d "$ANDROID/SDK/build-tools/36.0.0"
say PINNED_APK_REQUIRED_ANDROID_SDKS_READY

if test -s "$APK" && test -s "$BASE/Build/AarisGame.apk.sha256" &&
   (cd "$BASE/Build" && sha256sum -c AarisGame.apk.sha256 >/dev/null) &&
   test -s "$BASE/pinned-1f13-apk-verified.json"; then
  say PINNED_APK_PREVIOUS_VERIFIED_OUTPUT_REUSED
else
  test ! -e "$APK" && test ! -e "$BASE/build-report.json" ||
     { say PINNED_APK_EXISTING_UNVERIFIED_OUTPUT_NOT_OVERWRITTEN; exit 34; }
  say PINNED_APK_UNITY_BATCH_BUILD_STARTED
  set +e
  timeout --signal=TERM --kill-after=35 12000 "$EDITOR" \
    -batchmode -quit -force-glcore -job-worker-count 1 \
    -projectPath "$PROJECT" -buildTarget Android \
    -executeMethod Aaris.MapReview.Editor.AarisCodespaceAndroidBuild.BuildTestApk \
    -logFile "$BASE/private-1f13-unity-build.log" \
    >"$BASE/private-1f13-unity-stdout.log" \
    2>"$BASE/private-1f13-unity-stderr.log"
  unity_rc=$?
  set -e
  test "$unity_rc" -eq 0 ||
     { say "PINNED_APK_UNITY_FAILED_EXIT_$unity_rc"; exit 35; }
fi

test -s "$APK" && test -s "$BASE/build-report.json"
say PINNED_APK_BUILD_OUTPUT_CREATED
SIGNER="$ANDROID/SDK/build-tools/36.0.0/apksigner"
ALIGN="$ANDROID/SDK/build-tools/36.0.0/zipalign"
AAPT="$ANDROID/SDK/build-tools/36.0.0/aapt"
test -x "$SIGNER" && test -x "$ALIGN" && test -x "$AAPT"
"$SIGNER" verify --verbose --print-certs "$APK" \
    >"$BASE/private-apksigner.log" 2>&1
grep -Eq 'v2 scheme.*true' "$BASE/private-apksigner.log"
"$ALIGN" -c -p 4 "$APK" >"$BASE/private-zipalign.log" 2>&1
"$AAPT" dump badging "$APK" >"$BASE/private-aapt-badging.log"
python3 - "$APK" "$BASE/private-aapt-badging.log" \
    "$BASE/pinned-1f13-apk-verified.json" "$PIN" <<'PY'
import hashlib,json,re,sys
from pathlib import Path
from zipfile import ZipFile
apk=Path(sys.argv[1]); badge=Path(sys.argv[2]).read_text(errors="replace")
assert re.search(r"^package: name='com[.]aaris[.]game' versionCode='20261116' versionName='0[.]3[.]3'",badge,re.M)
min_sdk=re.search(r"^sdkVersion:'([0-9]+)'",badge,re.M)
assert min_sdk and int(min_sdk.group(1)) <= 33
with ZipFile(apk) as z:
    names=z.namelist()
    assert len(names)==len(set(names)) and z.testzip() is None
    assert "AndroidManifest.xml" in names and "classes.dex" in names
    assert any(n.startswith("lib/arm64-v8a/") and n.endswith(".so") for n in names)
h=hashlib.sha256()
with apk.open("rb") as f:
    for b in iter(lambda:f.read(2*1024*1024),b""):h.update(b)
digest=h.hexdigest()
(apk.parent/"AarisGame.apk.sha256").write_text(digest+"  AarisGame.apk\n")
Path(sys.argv[3]).write_text(json.dumps({"source_sha":sys.argv[4],
   "sha256":digest,"bytes":apk.stat().st_size,"package":"com.aaris.game",
   "version_code":20261116,"device_installed":False},indent=2))
print("PINNED_APK_SIGNATURE_PACKAGE_ABI_ZIP_SHA256_VERIFIED")
PY
say PINNED_APK_SIGNATURE_PACKAGE_ABI_SHA256_VERIFIED
say PINNED_APK_READY_FOR_DEVICE_INSTALL_TEST
