#!/usr/bin/env bash
# Package the verified Unity build under an independent Android install ID.
set -Eeuo pipefail
umask 077
source_commit="${1:?source commit required}"
source_hash="${2:?original APK SHA-256 required}"
[[ "$source_commit" =~ ^[a-f0-9]{40}$ && "$source_hash" =~ ^[a-f0-9]{64}$ ]]
project="/tmp/aaris-native-android-build-worktrees/$source_commit"
base=/tmp/aaris-codespaces/android-apk
source_apk="$base/Build/AarisGame.apk"
run="/tmp/aaris-offline-install-${source_commit:0:7}"
android="$HOME/.local/share/aaris-unity-editors/6000.3.26f1/Editor/Data/PlaybackEngines/AndroidPlayer"
build_tools="$android/SDK/build-tools/36.0.0"
export JAVA_HOME="$base/temurin-openjdk-17"
export PATH="$JAVA_HOME/bin:$PATH"
export GRADLE_USER_HOME="$base/GradleUserHome"

test "$(git -C "$project" rev-parse HEAD)" = "$source_commit"
printf '%s  %s\n' "$source_hash" "$source_apk" | sha256sum --check --status
mkdir -p "$run"
exec 9>"$run/packaging.lock"
flock -w 30 9
output="$run/AarisGame-Offline.apk"
if ! test -s "$output"; then
  stage="$(mktemp -d "$run/package.XXXXXX")"
  cp -a --reflink=auto "$project/Library/Bee/Android/Prj/IL2CPP/Gradle" "$stage/Gradle"
  cd "$stage/Gradle"
  python3 - <<'PY'
from pathlib import Path
import re

project = Path("launcher/build.gradle")
text, edits = re.subn(r'(?m)^(\s*applicationId\s+)"com\.aaris\.game"[ \t]*$',
                      r'\1"com.aaris.game.offline"', project.read_text())
assert edits == 1
project.write_text(text)
strings = Path("launcher/src/main/res/values/strings.xml")
text, edits = re.subn(r'(<string name="app_name">)[^<]+',
                      r'\1Aaris Game Offline', strings.read_text())
assert edits == 1
strings.write_text(text)
PY
  launcher="$(find "$android/Tools/gradle/lib" -maxdepth 1 -type f -name 'gradle-launcher-*.jar' -print -quit)"
  test -n "$launcher"
  if ! timeout 600 "$JAVA_HOME/bin/java" -classpath "$launcher" org.gradle.launcher.GradleMain \
       -Dorg.gradle.jvmargs=-Xmx4096m --offline --console=plain :launcher:assembleRelease \
       >"$stage/gradle-private.log" 2>&1 9>&-; then
    echo AARIS_OFFLINE_GRADLE_FAILED
    exit 20
  fi
  "$build_tools/zipalign" -P 16 -f 4 launcher/build/outputs/apk/release/launcher-release.apk "$stage/aligned.apk"
  "$build_tools/apksigner" sign --ks "$HOME/.android/debug.keystore" \
    --ks-key-alias androiddebugkey --ks-pass pass:android --key-pass pass:android \
    --v1-signing-enabled true --v2-signing-enabled true --v3-signing-enabled true \
    --out "$stage/AarisGame-Offline.apk" "$stage/aligned.apk"
  mv "$stage/AarisGame-Offline.apk" "$output"
fi

"$build_tools/apksigner" verify --verbose "$output" > "$run/signer.txt"
"$build_tools/apksigner" verify --verbose --min-sdk-version 23 "$output" > "$run/signer-v1.txt"
grep -q 'Verified using v1 scheme.*: true' "$run/signer-v1.txt"
for scheme in v2 v3; do grep -q "Verified using $scheme scheme.*: true" "$run/signer.txt"; done
"$build_tools/zipalign" -c -P 16 4 "$output"
"$build_tools/aapt" dump badging "$output" > "$run/badging.txt"
grep -q "^package: name='com.aaris.game.offline'" "$run/badging.txt"
grep -qxF "application-label:'Aaris Game Offline'" "$run/badging.txt"
grep -qxF "sdkVersion:'26'" "$run/badging.txt"
grep -qxF "targetSdkVersion:'33'" "$run/badging.txt"

python3 - "$source_apk" "$output" <<'PY'
import struct
import sys
from hashlib import sha256
from zipfile import ZipFile

def without_swappy_build_id(binary):
    # Gradle relinks Unity's frame-pacing wrapper. Its GNU build ID can differ
    # even when all executable bytes are identical.
    assert binary[:4] == b"\x7fELF" and binary[5] == 1
    if binary[4] == 2:
        table_offset = struct.unpack_from("<Q", binary, 40)[0]
        entry_size, count, names_index = struct.unpack_from("<HHH", binary, 58)
        range_format, range_offset = "<QQ", 24
    else:
        assert binary[4] == 1
        table_offset = struct.unpack_from("<I", binary, 32)[0]
        entry_size, count, names_index = struct.unpack_from("<HHH", binary, 46)
        range_format, range_offset = "<II", 16
    names_offset, names_size = struct.unpack_from(
        range_format, binary, table_offset + names_index * entry_size + range_offset)
    names = binary[names_offset:names_offset + names_size]
    for index in range(count):
        entry = table_offset + index * entry_size
        name_offset = struct.unpack_from("<I", binary, entry)[0]
        if names[name_offset:].split(b"\0", 1)[0] != b".note.gnu.build-id":
            continue
        note_offset, note_size = struct.unpack_from(
            range_format, binary, entry + range_offset)
        assert note_size == 36 and binary[note_offset:note_offset + 16] == (
            struct.pack("<III", 4, 20, 3) + b"GNU\0")
        start = note_offset + 16
        return binary[:start] + bytes(20) + binary[start + 20:]
    raise AssertionError("Swappy GNU build ID note missing")


with ZipFile(sys.argv[1]) as original, ZipFile(sys.argv[2]) as candidate:
    assert candidate.testzip() is None
    assert len(candidate.namelist()) == len(set(candidate.namelist()))
    for abi in ("arm64-v8a", "armeabi-v7a"):
        for library in ("libmain.so", "libunity.so", "libil2cpp.so"):
            assert f"lib/{abi}/{library}" in candidate.namelist()
    def payload_names(archive):
        return {name for name in archive.namelist()
                if name.startswith(("assets/bin/", "lib/")) and not name.endswith("/")}
    def digest(stream):
        result = sha256()
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            result.update(block)
        return result.digest()
    payload = payload_names(original)
    assert payload and payload == payload_names(candidate)
    for name in sorted(payload):
        with original.open(name) as left, candidate.open(name) as right:
            if name.endswith("/libswappywrapper.so"):
                assert without_swappy_build_id(left.read()) == without_swappy_build_id(right.read()), name
            else:
                assert digest(left) == digest(right), name
print("AARIS_OFFLINE_UNITY_PAYLOAD_VERIFIED")
PY
cd "$run"
sha256sum AarisGame-Offline.apk > AarisGame-Offline.sha256
cat AarisGame-Offline.sha256
echo AARIS_OFFLINE_APK_REBUILT_VERIFIED
