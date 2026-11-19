#!/usr/bin/env python3
"""Source-level admission regression, not Unity/Android profiling."""
from pathlib import Path
import unittest

S = (Path(__file__).resolve().parents[2] /
     "Assets/AarisWorld/Scripts/AarisOfflineGlooWall.cs").read_text(encoding="utf-8")
def part(a,b): return S.split(a,1)[1].split(b,1)[0]

class GlooPreviewPoseBudget(unittest.TestCase):
    def test_pose_cache_is_short_and_uses_camera_and_player(self):
        body = part("private void UpdatePlacement()", "private float FindOpenDistance(")
        for token in ("PreviewPhysicsMaxAgeSeconds = 0.08f",
                      "Time.unscaledTime", "now < nextPreviewPhysicsRefresh",
                      "player == previewPlayerPosition", "cameraPosition == previewCameraPosition",
                      "look == previewLook", "ghost.activeSelf", "previewPoseCached = true;",
                      "nextPreviewPhysicsRefresh = now + PreviewPhysicsMaxAgeSeconds"):
            self.assertIn(token, S)
        self.assertLess(body.index("if (previewPoseCached &&"),
                        body.index("CompleteRayHits(cameraPosition,"))
    def test_existing_original_collision_and_visuals_remain(self):
        body = part("private void UpdatePlacement()", "private float FindOpenDistance(")
        for token in ("FindOpenDistance(player, ahead, desired)",
                      "TryFindSupport(proposedPoint, player.y, out float centerY)",
                      "TryFindSupport(proposedPoint - edge, player.y, out float leftY)",
                      "TryFindSupport(proposedPoint + edge, player.y, out float rightY)",
                      "placementValid = HasCoverClearance(top);",
                      "ghost.transform.SetPositionAndRotation(proposedPoint, proposedRotation);",
                      "ghostTintValid != placementValid", "forwardHits, out cameraHitResults"):
            self.assertIn(token,body)
    def test_release_always_fresh_before_spending_wall(self):
        release=part("public void ReleaseAndDeploy()", "private int CompleteRayHits(")
        self.assertLess(release.index("previewPoseCached = false;"),release.index("UpdatePlacement();"))
        self.assertLess(release.index("UpdatePlacement();"),release.index("bool allowed = CanUse && placementValid"))
        self.assertLess(release.index("bool allowed ="),release.index("carriedWalls--;"))
        self.assertIn("root.AddComponent<AarisGlooWallHealth>()",release)
    def test_new_aim_and_cancel_invalidate_cache(self):
        begin=part("public void BeginAim()", "public void CancelAim()")
        cancel=part("public void CancelAim()", "public void ReleaseAndDeploy()")
        body=part("private void UpdatePlacement()", "private float FindOpenDistance(")
        self.assertLess(begin.index("previewPoseCached = false;"),begin.index("UpdatePlacement();"))
        self.assertIn("previewPoseCached = false;",cancel)
        self.assertIn("previewPoseCached = false;",body)
    def test_continuous_aim_input_still_updates_and_revalidates(self):
        update=part("private void Update()", "public void AddWalls(")
        self.assertIn("if (!aiming) return;",update)
        self.assertIn("if (!CanUse) { CancelAim(); return; }",update)
        self.assertIn("UpdatePlacement();",update)
        placement=part("private void UpdatePlacement()", "private float FindOpenDistance(")
        for token in ("previewPlayerPosition = player;", "previewCameraPosition = cameraPosition;",
                      "previewLook = look;","ghost.activeSelf"):
            self.assertIn(token,placement)
if __name__=="__main__": unittest.main(verbosity=2)
