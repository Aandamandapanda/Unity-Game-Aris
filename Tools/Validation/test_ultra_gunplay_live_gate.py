"""Contract checks for the opt-in real Unity gunplay and 100-rival acceptance gate.

No Android FPS is asserted: this runs in a disposable Linux Unity Editor.
"""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SH = (ROOT / "Tools/CI/ultra_gunplay_playmode_sandbox.sh").read_text()
YML = (ROOT / ".github/workflows/aaris-ultra-gunplay-live-playmode.yml").read_text()


class GunplayLiveGateContracts(unittest.TestCase):
    def test_gated_exact_owner_and_existing_codespace(self):
        self.assertIn("github.event.issue.number == 77", YML)
        self.assertIn("github.event.comment.user.login == 'sundamunda289-art'", YML)
        self.assertIn("github.event.comment.body == 'AARIS ULTRA GUNPLAY PLAYMODE'", YML)
        self.assertIn("MACHINE: aaris-ai-ubuntu-minimal-gxpgxwqvv9rq29x46", YML)
        self.assertIn("secrets.CODESPACE_PAT", YML)
        self.assertIn('test "$result" -eq 0', YML)

    def test_disposable_asset_copy_never_changes_live_worktree(self):
        self.assertIn("ROOT=/workspaces/Unity-Game-Aris", SH)
        self.assertIn('EXPECTED="${1:-}"', SH)
        self.assertIn('ACTUAL="$(git -C "$ROOT" rev-parse HEAD)"', SH)
        self.assertIn('"$ACTUAL" == "$EXPECTED"', SH)
        self.assertIn("AARIS_ULTRA_QA_EDITOR_BUSY_NO_SECOND_IMPORT", SH)
        self.assertIn("mktemp -d /tmp/aaris-ultra-gunplay.", SH)
        self.assertIn('cp -a --reflink=auto "$ROOT/Assets"', SH)
        self.assertIn('"$ROOT/Packages" "$ROOT/ProjectSettings" "$STAGE/"', SH)
        self.assertIn('rm -rf -- "$STAGE"', SH)
        self.assertIn("AARIS_ULTRA_QA_ORIGINAL_WORKTREE_UNCHANGED=yes", SH)
        for forbidden in ("git worktree add", "git reset --hard", "git clean -fd",
                          "git stash", "git checkout -f", "gh codespace create"):
            self.assertNotIn(forbidden, SH)

    def test_three_native_qas_use_real_editor_and_report(self):
        self.assertIn('xvfb-run -a', SH)
        self.assertIn('Aaris.MapReview.Editor.AarisProGunplayQA.Run', SH)
        self.assertIn('Aaris.MapReview.Editor.AarisBotCombatIntegrationQA.Run', SH)
        self.assertIn('Aaris.MapReview.Editor.AarisFullSceneCombatSoak.Run', SH)
        self.assertIn('AARIS_FULLSCENE_SOAK_SECONDS=15', SH)
        self.assertIn('grep -qx \'RESULT=PASS\'', SH)
        self.assertIn("'^SPAWNED_REAL_SCENE=100/100 '", SH)
        self.assertIn("'^PASS sustained 100-bot combat'", SH)
        self.assertIn("AARIS_ULTRA_QA_DEVICE_FPS=not_measured", SH)
        for key in ("gunplay", "bot_physics", "full_scene"):
            self.assertIn(f"AARIS_ULTRA_QA_STAGE_{key}=PASS", YML)
        self.assertIn("github.event_name == 'pull_request'", YML)
        self.assertIn("bash -n Tools/CI/ultra_gunplay_playmode_sandbox.sh", YML)


if __name__ == "__main__":
    unittest.main()
