#!/usr/bin/env python3
"""Source and deterministic vehicle-stream budget tests, not Android FPS proof."""
import unittest
from pathlib import Path
WORLD=(Path(__file__).resolve().parents[2]/"Assets/AarisWorld/Scripts/AarisVehicleWorld.cs").read_text()
def simulate(frame_delta, outcomes):
    constrained=frame_delta>1/45
    built=probes=0
    for success in outcomes:
        if not (built<2 and probes<8 and (not constrained or (built==0 and probes<3))): break
        probes+=1
        built+=int(success)
    return built,probes
class AndroidVehiclePacing(unittest.TestCase):
    def test_guard_source(self):
        block=WORLD.split("private void Update()",1)[1].split("private AarisVehicleRecord FarthestReplaceableParkedCar(",1)[0]
        for required in ("#if UNITY_ANDROID && !UNITY_EDITOR",
                         "Time.smoothDeltaTime > (1f / 45f)",
                         "const bool constrainedFrame = false;",
                         "(!constrainedFrame || (built == 0 && spawnProbes < 3))"):
            self.assertIn(required,block)
        self.assertLess(block.index("nearestDistanceSq = distanceSq;"),block.index("if (!TryResolvePlanned(record, out Vector3 pos))"))
        self.assertLess(block.index("if (!Build(record, pos))"),block.index("Destroy(retired.gameObject);"))
        self.assertNotIn("renderScale",block)
    def test_budgets(self):
        self.assertEqual(simulate(1/60,[True]*9),(2,2))
        self.assertEqual(simulate(1/45,[False]*9),(0,8))
        self.assertEqual(simulate(1/30,[True]*9),(1,1))
        self.assertEqual(simulate(1/30,[False]*9),(0,3))
        self.assertEqual(simulate(1/15,[False,True,True]),(1,2))
if __name__=="__main__":unittest.main()
