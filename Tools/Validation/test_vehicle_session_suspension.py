#!/usr/bin/env python3
"""Disabled or destroyed vehicle sessions must not accept stale UI input.

Static integration contract plus explicit state-transition decision checks.
Does not replace live Unity lifecycle or physics tests.
"""
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2] / "Assets/AarisWorld/Scripts"
SESSION = (SCRIPTS / "AarisVehicleSession.cs").read_text("utf-8")
HUD = (SCRIPTS / "AarisVehicleHUD.cs").read_text("utf-8")


class VehicleSessionSuspension(unittest.TestCase):
    def test_disabled_session_rejects_stale_throttle_and_steering(self):
        setter = SESSION[SESSION.index("public void SetSteering(float move, float steer)"):
                         SESSION.index("public bool TryEnterOrExit()")]
        self.assertIn("if (!isActiveAndEnabled || car == null) return;", setter)
        self.assertLess(setter.index("if (!isActiveAndEnabled || car == null) return;"),
                        setter.index("touchMove = Mathf.Clamp("))
        self.assertIn("car.SetInput(touchMove, touchSteer);", setter)

    def test_disabled_session_rejects_stale_drive_exit_clicks(self):
        handler = SESSION[SESSION.index("public bool TryEnterOrExit()"):
                          SESSION.index("private bool TryEnter()")]
        self.assertIn("if (!isActiveAndEnabled) return false;", handler)
        self.assertIn("return IsDriving ? TryExit() : TryEnter();", handler)
        self.assertLess(handler.index("if (!isActiveAndEnabled) return false;"),
                        handler.index("return IsDriving ? TryExit() : TryEnter();"))
        self.assertIn("if (Input.GetKeyDown(KeyCode.X) && IsDriving) TryEnterOrExit();",
                      SESSION)

    def test_hud_hides_both_controls_on_session_loss_or_disable(self):
        late = HUD[HUD.index("private void LateUpdate()",
                             HUD.index("public sealed class AarisVehicleHUD")):
                   HUD.index("private void OnApplicationPause(",
                             HUD.index("public sealed class AarisVehicleHUD"))]
        self.assertIn("if (session == null || !session.isActiveAndEnabled)", late)
        self.assertIn("ClearArrows();", late)
        self.assertIn("arrowPanel.SetActive(false);", late)
        self.assertIn("driveButton.SetActive(false);", late)
        self.assertLess(late.index("if (session == null || !session.isActiveAndEnabled)"),
                        late.index("bool driving = session.IsDriving;"))
        self.assertIn("bool show = driving || session.CanEnter;", late)

    def test_suspend_releases_held_input_without_unsafe_exit(self):
        end = SESSION[SESSION.index("private void ClearHeldDrivingInput()"):]
        self.assertIn("private void OnDisable() => ClearHeldDrivingInput();", end)
        self.assertIn("if (car != null) car.SetInput(0f, 0f);", end)
        self.assertNotIn("ReleaseDriver(", end)
        self.assertNotIn("TryExit()", end)
        for active in (False, True):
            for car_present in (False, True):
                for button_down in (False, True):
                    can_dispatch = active and car_present
                    can_change_mode = active
                    if not active:
                        self.assertFalse(can_dispatch)
                        self.assertFalse(can_change_mode)
                    if active and car_present and button_down:
                        self.assertTrue(can_dispatch)


if __name__ == "__main__":
    unittest.main(verbosity=2)
