import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "AarisWorld" / "Scripts"


class CombatMotorCameraAuthorityTests(unittest.TestCase):
    def test_reticle_damage_and_hit_direction_use_firing_camera(self):
        hud = (SCRIPTS / "AarisCombatFeedbackHUD.cs").read_text()
        motor = (SCRIPTS / "AarisThirdPersonPlayer.cs").read_text()
        battle = (SCRIPTS / "AarisBattleActions.cs").read_text()
        self.assertIn("public Camera AimCamera => followCamera;", motor)
        self.assertIn("equippedGun.TryFire(motor.AimCamera, gameObject)", battle)
        self.assertIn("motor = GetComponent<AarisThirdPersonPlayer>();", hud)
        self.assertIn("aimCamera = motor != null ? motor.AimCamera : null;", hud)
        self.assertEqual(hud.count("RefreshAimCamera();"), 3)
        self.assertNotIn("Camera.main", hud,
                         "MainCamera can differ from the weapon's aim camera")


if __name__ == "__main__":
    unittest.main()
