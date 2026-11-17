#!/usr/bin/env python3
"""Vehicle/on-foot HUD handoff must preserve authored component states.

Source invariants plus a deterministic state-machine test; Unity Play Mode
is required to confirm actual Canvas and GraphicRaycaster transitions.
"""
import itertools
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "Assets/AarisWorld/Scripts/AarisVehicleSession.cs").read_text("utf-8")
HANDOFF = SOURCE[SOURCE.index("private void SetFootHudVisible(bool show)"):
                 SOURCE.index("private bool TryFindExit(")]


class VehicleHudStateRestoration(unittest.TestCase):
    def test_preserve_canvas_and_raycaster_states_separately(self):
        self.assertIn("private bool[] previousFootCanvasEnabled;", SOURCE)
        self.assertIn("private bool[] previousFootRaycasterEnabled;", SOURCE)
        self.assertIn("previousFootCanvasEnabled[i] = canvas != null && canvas.enabled;", HANDOFF)
        self.assertIn("previousFootRaycasterEnabled[i] = raycaster != null && raycaster.enabled;", HANDOFF)
        self.assertIn("canvas.enabled = previousFootCanvasEnabled[i];", HANDOFF)
        self.assertIn("raycaster.enabled = previousFootRaycasterEnabled[i];", HANDOFF)
        self.assertNotIn("canvas.enabled = show;", HANDOFF)
        self.assertNotIn("raycaster.enabled = show;", HANDOFF)

    def test_idempotent_hide_and_restore_cannot_destroy_saved_states(self):
        self.assertIn("private bool footHudHidden;", SOURCE)
        self.assertIn("if (footHudHidden == !show) return;", HANDOFF)
        self.assertIn("footHudHidden = !show;", HANDOFF)
        # Hiding twice must not overwrite the original flags with false.
        for initial in itertools.product((False, True), repeat=4):
            original = list(initial)
            current = list(initial)
            saved = None
            hidden = False
            for show in (False, False, True, True, False, True):
                if hidden == (not show):
                    continue
                if not show:
                    saved = current[:]
                    current = [False] * 4
                else:
                    current = saved[:]
                hidden = not show
            self.assertFalse(hidden)
            self.assertEqual(current, original)

    def test_foot_hud_source_and_vehicle_handoff_still_use_single_owner(self):
        self.assertIn("var battle = FindFirstObjectByType<AarisBattleHUD>();", HANDOFF)
        self.assertIn("var weapons = FindFirstObjectByType<AarisWeaponHUD>();", HANDOFF)
        self.assertIn("footCanvases = new[] { first, second };", HANDOFF)
        self.assertIn("SetFootHudVisible(false);", SOURCE)
        self.assertIn("SetFootHudVisible(true);", SOURCE)
        self.assertLess(SOURCE.index("SetFootHudVisible(false);"),
                        SOURCE.index("footMotor.enabled = false;"))
        self.assertLess(SOURCE.index("footMotor.enabled = true;"),
                        SOURCE.index("SetFootHudVisible(true);"))


if __name__ == "__main__":
    unittest.main(verbosity=2)
