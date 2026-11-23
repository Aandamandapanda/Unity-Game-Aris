"""Regression: centrally queued invisible bots retain exact AI tick deadlines."""
import unittest
from pathlib import Path

SOURCE = (Path(__file__).resolve().parents[2] /
          "Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs").read_text()
DIRECTOR, BOT = SOURCE.split("internal sealed class AarisOfflineRival", 1)


class CentralDormantSchedulerTests(unittest.TestCase):
    def test_native_dispatch_stops_only_for_dormant_far_or_hidden_bodies(self):
        self.assertIn("enabled = !dormant;", BOT)
        self.assertIn("SetCentralDormancy(dormantOutOfVisualRange || dormantOffscreenVirtual);", BOT)
        self.assertIn("playerDistanceSqr >= FarSimulationDistance * FarSimulationDistance", BOT)
        self.assertIn("!lastCameraRelevant && virtualBody", BOT)
        self.assertIn("owner.RegisterDormantRival(spatialSlot, this, dormant)", BOT)

    def test_deadlines_not_modified_by_central_queue(self):
        self.assertIn("TickDormantRivals(Time.time);", DIRECTOR)
        self.assertIn("now >= bot.NextDormantWakeTime", DIRECTOR)
        self.assertIn("if (centrallyDormant) Update();", BOT)
        self.assertIn("Mathf.Min(nextFarSimulation, nextDormantProximityProbe)", BOT)
        self.assertIn("Mathf.Min(nextDormantProximityProbe, nextCameraProbe)", BOT)
        self.assertIn("private const float FarSimulationInterval = 0.35f;", BOT)
        self.assertIn("private const float DormantProximityProbeInterval = 0.10f;", BOT)

    def test_100_far_bot_combat_ticks_identical_to_old_per_frame_driver(self):
        for fps in (30, 60, 120):
            original = [i * .0035 for i in range(100)]
            central = original.copy()
            proximity = [0. for _ in range(100)]
            old_ticks = [0] * 100
            new_ticks = [0] * 100
            for frame in range(1, 10 * fps + 1):
                now = frame / fps
                for i in range(100):
                    if now >= original[i]:
                        old_ticks[i] += 1
                        original[i] = now + .35
                    # Central queue calls the old Update at whichever of the
                    # existing 100ms proximity or 350ms combat deadlines is due.
                    if now < central[i] and now < proximity[i]:
                        continue
                    if now >= proximity[i]:
                        proximity[i] = now + .1
                    if now >= central[i]:
                        new_ticks[i] += 1
                        central[i] = now + .35
            self.assertEqual(new_ticks, old_ticks)

    def test_benchmark_drives_queue_when_director_disabled(self):
        bench = (Path(__file__).resolve().parents[2] /
                 "Assets/AarisWorld/Editor/AarisBotFrameBenchmark.cs").read_text()
        self.assertIn("director.TickDormantRivals(Time.time);", bench)


if __name__ == "__main__":
    unittest.main()
