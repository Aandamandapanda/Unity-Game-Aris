#!/usr/bin/env python3
"""Static regression gate for distant authored-collider settlement overlap.

Requires separate Unity Play Mode + on-device testing for visual confirmation.
"""
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2] / "Assets/AarisWorld/Scripts"
EXPANSION = (ROOT / "AarisSettlementExpansion.cs").read_text(encoding="utf-8")
STREAMER = (ROOT / "AarisNearbyColliders.cs").read_text(encoding="utf-8")


class StreamedSettlementSafety(unittest.TestCase):
    def test_cache_ready_before_site_probes(self):
        self.assertIn("DefaultExecutionOrder(-250)", STREAMER)
        start = EXPANSION[EXPANSION.index("private void Start()"):
                          EXPANSION.index("private bool TryRelocateInteractiveSite(")]
        self.assertIn("nearbyColliderBounds = FindFirstObjectByType<AarisNearbyColliders>();", start)
        self.assertLess(start.index("nearbyColliderBounds = FindFirstObjectByType"),
                        start.index("foreach (var site in plan.sites)"))

    def test_streamed_disabled_colliders_guard_physics_gap(self):
        block = EXPANSION[EXPANSION.index("private bool CollidersBlock("):]
        self.assertIn("nearbyColliderBounds.HasPotentialInactiveObstacle(center, half, rotation)", block)
        self.assertLess(block.index("HasPotentialInactiveObstacle("),
                        block.index("Physics.OverlapBoxNonAlloc("))
        self.assertIn("if (n >= obstacles.Length)", block)
        self.assertIn("candidates = Physics.OverlapBox(center, half, rotation,", block)
        self.assertIn("if (collider.bounds.max.y < y + 0.1f) continue;", block)
        self.assertNotIn(".enabled = true", block)
        self.assertNotIn("Physics.SyncTransforms()", block)

    def test_streamer_preserves_authored_streaming_contract(self):
        probe = STREAMER[STREAMER.index("internal bool HasPotentialInactiveObstacle("):
                         STREAMER.index("private void OnEnable()")]
        self.assertIn("streamed[i] && c != null && !c.enabled", probe)
        self.assertIn("worldBounds[i].Intersects(probe)", probe)
        self.assertIn("if (probeVisited[i] == probeStamp) continue;", probe)
        self.assertNotIn(".enabled = true", probe)
        self.assertIn("for (int xCell = Cell(bounds.min.x);", STREAMER)

    def test_relocation_remains_bounded_and_validated(self):
        code = EXPANSION[EXPANSION.index("private bool TryRelocateInteractiveSite("):
                         EXPANSION.index("private static bool CoordinatesValid(")]
        for phrase in ("TryGround(site, terrains,", "TreesBlock(site, terrains)",
                       "CollidersBlock(site, candidateY)", "site.x = originalX;",
                       "site.z = originalZ;"):
            self.assertIn(phrase, code)


if __name__ == "__main__":
    unittest.main(verbosity=2)
