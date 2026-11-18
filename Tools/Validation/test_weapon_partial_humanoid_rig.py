from pathlib import Path
from itertools import product
import unittest
s=(Path(__file__).resolve().parents[2]/"Assets/AarisWorld/Scripts/AarisWeaponInventory.cs").read_text()
class GripRig(unittest.TestCase):
 def test_all_seven_bones_required(self):
  h=s.split("private bool HasCompleteHumanoidGripRig()",1)[1].split("private void AttachModelsToSockets()",1)[0]
  names=("chest","hand","rightUpperArm","rightForearm","leftUpperArm","leftForearm","leftHand")
  for n in names:self.assertIn(n+" != null",h)
  for flags in product((False,True),repeat=7):self.assertEqual(all(flags),sum(flags)==7)
 def test_fallback_and_ik_agree(self):
  attach=s.split("private void AttachModelsToSockets()",1)[1].split("private static void ReachArm(",1)[0]
  align=s.split("private void AlignSupportHand()",1)[1].split("private void LateUpdate()",1)[0]
  self.assertIn("if (HasCompleteHumanoidGripRig())",attach)
  self.assertIn("!HasCompleteHumanoidGripRig()",align)
  self.assertIn("hand.position + hand.rotation * equipped.Spec.handOffset",attach)
  self.assertIn("ReachArm(leftUpperArm, leftForearm, leftHand,",align)
  self.assertIn("ReachArm(rightUpperArm, rightForearm, hand,",align)
  self.assertNotIn("GetBoneTransform",attach.split("if (HasCompleteHumanoidGripRig())",1)[0])
if __name__=="__main__":unittest.main()
