#!/data/data/com.termux/files/usr/bin/bash
# Phone-only Unity approval. Does not use Cloudflare, public logs, GitHub Issues or ULF files.
set -euo pipefail
umask 077
REPO="$(printenv MAIN_REPO || echo sundamunda289-art/Unity-Game-Aris)"
PROFILE="$HOME/.config/aaris-ai/codespace-gh"
command -v pkg >/dev/null || { echo 'Please run inside Termux'; exit 2; }
pkg install -y gh python openssh jq curl
export GH_CONFIG_DIR="$PROFILE"
# Prevent tokens from a different shell environment overriding the selected profile.
unset GH_TOKEN GITHUB_TOKEN
# The selected Codespaces account can host a fork; never query the master
# repository for a Codespace belonging to that other account.
CS_USER="$(gh api user --jq .login 2>/dev/null)" || {
  echo 'Codespace GitHub login missing. Run aaris-switch.sh once first.'
  exit 3
}
[[ "$CS_USER" =~ ^[A-Za-z0-9][A-Za-z0-9-]{0,38}$ ]] || {
  echo 'Unexpected Codespace account identity'; exit 3;
}
LIST="$(gh codespace list --json name,displayName,state,repository --limit 100)" || {
  echo 'Cannot list Codespaces in the selected hosting account'; exit 4;
}
# Explicit selection wins: never sign in to a stale Codespace by mistake.
# Example: AARIS_CODESPACE_NAME=aaris-ai-ubuntu-minimal-... bash termux_unity_allow.sh
if [[ -n "${AARIS_CODESPACE_NAME:-}" ]]; then
  [[ "$AARIS_CODESPACE_NAME" =~ ^[A-Za-z0-9-]+$ ]] || {
    echo 'Invalid exact Codespace name'; exit 5;
  }
  CS="$(printf '%s' "$LIST" | jq -r --arg name "$AARIS_CODESPACE_NAME" --arg repo "$CS_USER/Unity-Game-Aris" '[.[] | select(.name == $name and .repository == $repo and .state == "Available")] | if length == 1 then .[0].name else empty end')"
  [[ "$CS" == "$AARIS_CODESPACE_NAME" ]] || {
    echo 'Exact available Codespace missing from selected hosting account'; exit 5;
  }
else
  # Backward-compatible default only if there is one eligible Ubuntu Codespace.
  CS="$(printf '%s' "$LIST" | jq -r --arg repo "$CS_USER/Unity-Game-Aris" '[.[] | select(.repository == $repo and .state == "Available" and (.displayName == "aaris-ai-ubuntu-minimal" or .displayName == "aaris-ai-ubuntu-fallback-2"))] | if length == 1 then .[0].name else empty end')"
  [[ -n "$CS" ]] || { echo 'Several Codespaces exist. Set AARIS_CODESPACE_NAME to choose one explicitly.'; exit 5; }
fi
[[ "$CS" =~ ^[A-Za-z0-9-]+$ ]] || { echo 'Codespace not found; use CS START first'; exit 5; }
printf 'Using Codespace: %s\n' "$CS"
WORK="$(mktemp -d "$HOME/.aaris-unity-allow.XXXXXXXX")"
trap 'rm -rf "$WORK"' EXIT
curl -fsSL --retry 2 "https://raw.githubusercontent.com/$REPO/main/Tools/Codespaces/mobile_license.py" -o "$WORK/remote.py"
grep -q 'AARIS_AUTH_LINK=' "$WORK/remote.py" || { echo 'Remote helper not recognized'; exit 6; }
# Explicit per-invocation opt-in only: Unity CLI 1.x may need a file-backed
# credential store on headless Codespaces without a usable Secret Service.
# Never enable for everyone and never print Unity tokens.
if [[ "${AARIS_UNITY_INSECURE_STORAGE:-0}" == "1" ]]; then
  python3 - "$WORK/remote.py" <<'PY'
from pathlib import Path
import sys
helper = Path(sys.argv[1])
src = helper.read_text()
old = '[cli,"auth","login","--format","json"]'
new = '[cli,"auth","login","--insecure-storage","--format","json"]'
if src.count(old) != 1:
    raise SystemExit("Unity CLI helper differs from reviewed source; refusing unsafe patch")
helper.write_text(src.replace(old, new))
PY
  echo 'Unity login: using approved private file credential storage on this Codespace.'
fi
echo 'Starting official Unity sign-in on your Codespace...'
# The URL travels only between your Codespace and your own phone.
set +e
gh codespace ssh -c "$CS" -- 'python3 -u -' < "$WORK/remote.py" 2>"$WORK/ssh-errors.log" |
while IFS= read -r line; do
  case "$line" in
    AARIS_AUTH_LINK=*)
      URL="$(printf '%s' "$line" | cut -c17-)"
      if python3 - "$URL" <<'PY'
import sys
from urllib.parse import urlsplit
try:
    p=urlsplit(sys.argv[1]); h=(p.hostname or '').lower()
    ok=p.scheme=='https' and not p.username and not p.password and len(sys.argv[1])<4000 and any(h==s or h.endswith('.'+s) for s in ('unity.com','unity3d.com'))
except ValueError: ok=False
sys.exit(0 if ok else 1)
PY
      then
        printf '\nUnity official login link (only visible on your phone):\n%s\n\n' "$URL"
        if command -v termux-open-url >/dev/null 2>&1; then
          termux-open-url "$URL" || true
        fi
        echo 'Sign in with your Unity account/Google and tap Allow. Return to Termux afterward.'
      else echo 'Rejected unexpected login URL.'; fi
      ;;
    UNITY_AUTH_STARTED) echo 'Unity sign-in started.' ;;
    UNITY_AUTH_ALREADY_VALID) echo 'Existing Unity account login valid.' ;;
    UNITY_AUTH_VERIFIED) echo 'Unity browser login verified on Codespace.' ;;
    UNITY_LICENSE_ALREADY_ACTIVE|UNITY_PERSONAL_LICENSE_VERIFIED)
      echo 'SUCCESS: Unity license verified on the Codespace.' ;;
    UNITY_PERSONAL_ACTIVATION_START) echo 'Unity Personal activation started.' ;;
    UNITY_PERSONAL_NOT_ACTIVE_HUB_MAY_BE_REQUIRED) echo 'Unity Personal activation was not accepted; Unity Hub may be needed.' ;;
    UNITY_AUTH_NOT_COMPLETED) echo 'Unity Allow/login not completed in time.' ;;
    UNITY_CLI_MISSING_RUN_CS_INSTALL_FIRST) echo 'Unity CLI is not installed on Codespace.' ;;
    UNITY_PERSONAL_ACTIVATION_ERROR) echo 'Unity Personal activation failed.' ;;
    *) : ;; # Never echo arbitrary remote output containing credentials
  esac
done
PIPE_EXIT=$?
set -e
if (( PIPE_EXIT != 0 )); then
  echo 'Unity auth was not verified. Check your Codespace, SSH connection and login.'
  exit "$PIPE_EXIT"
fi
