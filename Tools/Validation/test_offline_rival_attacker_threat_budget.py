#!/usr/bin/env python3
"""Source contract and deterministic selection model: two-player-attacker threat budget.

Not an in-Editor PlayMode or device FPS test.
"""
from pathlib import Path
import unittest

SOURCE = (Path(__file__).resolve().parents[2] /
          "Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs").read_text("utf-8")


def player_threat(distance, armed, magazine, reserve, visible=True, los=True):
    if not visible or not los or distance >= 42:
        return False
    return (armed and (magazine > 0 or reserve > 0)) or distance <= 12


def select_two(candidates):
    allowed = [c for c in candidates
               if player_threat(c["distance"], c["armed"], c["magazine"],
                                c["reserve"], c["visible"], c["los"])]
    return [c["name"] for c in sorted(allowed, key=lambda c: c["distance"])[:2]]


def rival(name, distance, armed, magazine=0, reserve=0, visible=True, los=True):
    return dict(name=name, distance=distance, armed=armed, magazine=magazine,
                reserve=reserve, visible=visible, los=los)


class PlayerAttackerThreatBudget(unittest.TestCase):
    def test_source_binds_threat_to_actual_two_slot_selector(self):
        director = SOURCE.split("private void RefreshPlayerAttackers()", 1)[1].split(
            "internal bool MayAttackPlayer", 1)[0]
        rival_source = SOURCE.split("internal sealed class AarisOfflineRival", 1)[1]
        sight = rival_source.split("internal bool HasPlayerSightForSelection()", 1)[1].split(
            "private bool HasSight()", 1)[0]
        self.assertIn("private const int AttackerSelectionPerFrame = 12;", SOURCE)
        self.assertIn("bot.HasPlayerSightForSelection()", director)
        self.assertIn("return bot == attackerOne || bot == attackerTwo;", SOURCE)
        self.assertIn("private const float UnarmedPlayerInterestRadius = 12f;", rival_source)
        self.assertIn("carriedGun == null || (carriedMagazine <= 0 && carriedReserve <= 0)", sight)
        self.assertIn("UnarmedPlayerInterestRadius * UnarmedPlayerInterestRadius", sight)
        self.assertIn("return HasSightTo(playerTarget);", sight)

    def test_distant_unarmed_cannot_block_two_armed_attackers(self):
        bots = [rival("empty-18m", 18, False),
                rival("armed-24m", 24, True, 12),
                rival("empty-30m", 30, False),
                rival("armed-34m", 34, True, 0, 20)]
        self.assertEqual(select_two(bots), ["armed-24m", "armed-34m"])

    def test_nearby_unarmed_melee_still_engages(self):
        bots = [rival("fist-8m", 8, False),
                rival("gun-24m", 24, True, 5)]
        self.assertEqual(select_two(bots), ["fist-8m", "gun-24m"])

    def test_empty_magazine_with_reserve_still_eligible(self):
        bots = [rival("reload-25m", 25, True, 0, 30),
                rival("empty-11m", 11, True, 0, 0),
                rival("loaded-29m", 29, True, 4)]
        self.assertEqual(select_two(bots), ["empty-11m", "reload-25m"])

    def test_invisible_occluded_and_out_of_range_cannot_reserve_slots(self):
        bots = [rival("invisible", 8, True, 10, visible=False),
                rival("cover", 10, True, 10, los=False),
                rival("far", 44, True, 10),
                rival("real", 20, True, 10)]
        self.assertEqual(select_two(bots), ["real"])


if __name__ == "__main__":
    unittest.main(verbosity=2)
