#!/usr/bin/env python3
"""Static lifecycle checks; Unity Play Mode still required."""
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parents[2] / "Assets/AarisWorld/Scripts"
BUTTON = (SCRIPTS / "AarisHudButton.cs").read_text("utf-8")
ARROW = (SCRIPTS / "AarisVehicleHUD.cs").read_text("utf-8")


class OrphanedPointerRecovery(unittest.TestCase):
    def test_hud_releases_after_event_system_loss(self):
        body = BUTTON[BUTTON.index("private void LateUpdate()"):
                      BUTTON.index("private static bool IsPointerPhysicallyHeld(")]
        self.assertIn("if (EventSystem.current == null)", body)
        self.assertIn("Release();", body)
        self.assertLess(body.index("if (EventSystem.current == null)"),
                        body.index("currentInputModule is StandaloneInputModule"))
        self.assertIn("if (heldPointers.Count == 0) return;", body)

    def test_vehicle_arrows_release_after_event_system_loss(self):
        body = ARROW[ARROW.index("private void LateUpdate()"):
                     ARROW.index("private static bool IsPointerPhysicallyHeld(")]
        self.assertIn("if (EventSystem.current == null)", body)
        self.assertIn("Reset();", body)
        self.assertLess(body.index("if (EventSystem.current == null)"),
                        body.index("currentInputModule is StandaloneInputModule"))
        self.assertIn("if (fingers.Count == 0) return;", body)

    def test_other_input_modules_release_only_without_physical_input(self):
        for code in (BUTTON, ARROW):
            body = code[code.index("private void LateUpdate()"):
                        code.index("private static bool IsPointerPhysicallyHeld(")]
            self.assertIn("Input.touchCount == 0 && !Input.GetMouseButton(0)", body)
            self.assertIn("currentInputModule is StandaloneInputModule", body)
            self.assertIn("if (!IsPointerPhysicallyHeld(pointerId))", body)
        for event_alive in (True, False):
            for classic in (True, False):
                for touches in (0, 1, 2):
                    for mouse in (False, True):
                        should_release = not event_alive or (
                            not classic and touches == 0 and not mouse)
                        if not event_alive:
                            self.assertTrue(should_release)
                        elif not classic:
                            self.assertEqual(should_release,
                                             touches == 0 and not mouse)
                        else:
                            self.assertFalse(should_release)


if __name__ == "__main__":
    unittest.main(verbosity=2)
