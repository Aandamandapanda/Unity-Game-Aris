#!/usr/bin/env python3
"""Source-level real-player DRIVE regression for blocked parked cars.

No claim of Unity physics or Android playtesting: the deterministic selection
model validates entry fallback priorities, and source guards prevent hotpath
regressions.
"""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SOURCE = (ROOT / "Assets/AarisWorld/Scripts/AarisVehicleSession.cs").read_text()


def choose_on_tap(cars, radius=4.0):
    nearest = None
    best = radius * radius
    for index, (distance, active, occupied, grounded, reachable) in enumerate(cars):
        d2 = distance * distance
        if not active or occupied or d2 >= best or not grounded or not reachable:
            continue
        nearest, best = index, d2
    return nearest


class VehicleEntryReachability(unittest.TestCase):
    def test_blocked_nearest_yields_reachable_second(self):
        cars = [(1.2, True, False, True, False),
                (2.8, True, False, True, True),
                (3.5, True, False, True, True)]
        self.assertEqual(choose_on_tap(cars), 1)
        self.assertEqual(choose_on_tap(cars, 2.0), None)

    def test_occupied_and_rolling_cars_cannot_steal_entry(self):
        cars = [(0.6, True, True, True, True),
                (1.2, True, False, False, True),
                (2.2, False, False, True, True),
                (3.2, True, False, True, True)]
        self.assertEqual(choose_on_tap(cars), 3)

    def test_refresh_then_fallback_before_driver_handoff(self):
        entry = SOURCE.split("private bool TryEnter()", 1)[1].split(
            "private AarisVehicleMotor FindReachableCarOnTap()", 1)[0]
        self.assertLess(entry.index("nearbyColliders.RefreshNow();"),
                        entry.index("HasClearVehicleAccess(eye, target, nearbyCar.transform)"))
        self.assertLess(entry.index("FindReachableCarOnTap();"),
                        entry.index("if (!nearbyCar.TryTakeDriver(this))"))
        self.assertIn("if (nearbyCar == null) return false;", entry)

    def test_fallback_is_button_only_not_hotpath(self):
        update = SOURCE.split("private void Update()", 1)[1].split(
            "private void LateUpdate()", 1)[0]
        self.assertNotIn("FindReachableCarOnTap()", update)
        self.assertNotIn("nearbyColliders.RefreshNow()", update)
        fallback = SOURCE.split("private AarisVehicleMotor FindReachableCarOnTap()", 1)[1].split(
            "private bool HasClearVehicleAccess(", 1)[0]
        for guard in ("FindObjectsByType<AarisVehicleMotor>",
                      "candidate.isActiveAndEnabled",
                      "candidate.IsOccupied", "candidate.IsSafeToExit",
                      "d2 >= nearestSquared",
                      "HasClearVehicleAccess(eye, target, candidate.transform)"):
            self.assertIn(guard, fallback)


if __name__ == "__main__":
    unittest.main()
