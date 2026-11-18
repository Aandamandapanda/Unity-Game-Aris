import unittest
from pathlib import Path
SRC=(Path(__file__).resolve().parents[2]/"Assets/AarisWorld/Scripts/AarisThirdPersonPlayer.cs").read_text()
def safe(length,hit):return length if hit is None else max(0,min(length,hit-.08))
class CameraCollision(unittest.TestCase):
 def test_close_wall_no_overshoot(self):
  for hit in [0,.02,.06,.08,.1,.2,.5,1.5,2.5]:
   d=safe(2.5,hit);self.assertTrue(0<=d<=hit)
  self.assertEqual(safe(2.5,.02),0)
  self.assertAlmostEqual(safe(2.5,.2),.12)
 def test_source_and_no_extra_physics(self):
  method=SRC.split("private Vector3 ObstructionSafe(",1)[1].split("private void UpdatePlayerNearClipVisibility(",1)[0]
  self.assertIn("Mathf.Clamp(hit.distance - 0.08f, 0f, length)",method)
  self.assertNotIn("Mathf.Max(0.12f",method)
  camera=SRC.split("private void PlaceCamera(bool instant)",1)[1].split("private void LateUpdate()",1)[0]
  self.assertIn("next = ObstructionSafe(pivot, next);",camera)
  self.assertIn("if ((next - preCollision).sqrMagnitude > 0.00000001f)",camera)
  self.assertIn("cameraVelocity = Vector3.zero;",camera)
  self.assertEqual(camera.count("ObstructionSafe(pivot, next)"),1)
if __name__=="__main__":unittest.main()
