"""Regression: centrally queued invisible bots retain exact AI tick deadlines."""
import heapq
import unittest
from pathlib import Path

SOURCE = (Path(__file__).resolve().parents[2] /
          "Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs").read_text()
DIRECTOR, BOT = SOURCE.split("internal sealed class AarisOfflineRival", 1)


class CentralDormantSchedulerTests(unittest.TestCase):
    def test_native_dispatch_stops_only_for_dormant_far_or_hidden_bodies(self):
        self.assertIn("enabled = !dormant;", BOT)
        self.assertRegex(BOT, r"SetCentralDormancy\(dormantOutOfVisualRange \|\| dormantOffscreenVirtual \|\|\s*dormantVisibleFarVirtual\);")
        self.assertIn("playerDistanceSqr >= FarSimulationDistance * FarSimulationDistance", BOT)
        self.assertIn("!lastCameraRelevant && virtualBody", BOT)
        self.assertIn("owner.RegisterDormantRival(spatialSlot, this, dormant)", BOT)

    def test_deadlines_not_modified_by_central_queue(self):
        self.assertIn("TickDormantRivals(now, playerPosition);", DIRECTOR)
        self.assertIn("now >= bot.NextDormantWakeTime", DIRECTOR)
        wake = BOT.split("internal void TickDormantFromDirector(", 1)[1].split(
            "internal void ForceDormantProximityProbe(", 1)[0]
        self.assertIn("TickRival(now, playerPosition);", wake)
        self.assertNotIn("Time.time", wake)
        self.assertNotIn("playerTarget.position", wake)
        self.assertIn("Mathf.Min(nextFarSimulation, nextDormantProximityProbe)", BOT)
        self.assertIn("Mathf.Min(nextDormantProximityProbe, nextCameraProbe)", BOT)
        self.assertIn("private const float FarSimulationInterval = 0.35f;", BOT)
        self.assertIn("nextDormantProximityProbe = nextCameraProbe;", BOT)

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

    def test_min_heap_preserves_existing_combat_ticks_without_roster_scans(self):
        # The fixed heap must not modify per-bot clocks or suppress remote
        # kills, and the due path must no longer walk the full 100-slot roster.
        self.assertIn("private float nextDormantSweepAt = float.PositiveInfinity;", DIRECTOR)
        self.assertIn("now < nextDormantSweepAt", DIRECTOR)
        for token in (
            "private readonly int[] dormantRivalHeap = new int[RivalCount];",
            "private readonly int[] dormantRivalHeapPositions = new int[RivalCount];",
            "private readonly float[] dormantRivalWakeTimes = new float[RivalCount];",
            "ScheduleDormantRival(slot, bot.NextDormantWakeTime);",
            "RemoveDormantHeapAt(0);",
        ):
            self.assertIn(token, DIRECTOR)
        self.assertIn("if (now >= bot.NextDormantWakeTime)", DIRECTOR)
        sweep = DIRECTOR.split("private void TickDormantRivals(float now,", 1)[1].split(
            "private const int MaximumSpawnPasses", 1)[0]
        self.assertIn("while (dormantRivalHeapCount != 0", sweep)
        self.assertNotIn("for (int i = 0; i < RivalCount; i++)", sweep)

        for fps in (30, 60, 120):
            for count in (1, 10, 40, 100):
                # Android's 350ms far simulation uses 100 authored phases.
                phase_step = .35 / max(count, 1)
                baseline = [i * phase_step for i in range(count)]
                optimized = baseline.copy()
                baseline_ticks = [0] * count
                optimized_ticks = [0] * count
                generations = [0] * count
                heap = [(deadline, i, 0)
                        for i, deadline in enumerate(optimized)]
                heapq.heapify(heap)
                heap_pops = 0
                old_scan_deadlines = baseline.copy()
                old_next_global = min(old_scan_deadlines)
                old_candidate_checks = 0
                for frame in range(1, fps * 10 + 1):
                    now = frame / fps
                    for i in range(count):
                        if now >= baseline[i]:
                            baseline_ticks[i] += 1
                            baseline[i] = now + .35

                    # Exact mirror of the previous global-min implementation:
                    # one due actor caused every dormant slot to be inspected.
                    if now >= old_next_global:
                        old_candidate_checks += count
                        for i in range(count):
                            if now >= old_scan_deadlines[i]:
                                old_scan_deadlines[i] = now + .35
                        old_next_global = min(old_scan_deadlines)

                    # New queue: pop and reschedule only actors actually due.
                    while heap and heap[0][0] <= now:
                        _, i, generation = heapq.heappop(heap)
                        if generation != generations[i]:
                            continue
                        heap_pops += 1
                        if now >= optimized[i]:
                            optimized_ticks[i] += 1
                            optimized[i] = now + .35
                        generations[i] += 1
                        heapq.heappush(heap, (optimized[i], i, generations[i]))
                self.assertEqual(baseline_ticks, optimized_ticks)
                if count == 100:
                    self.assertLess(heap_pops, old_candidate_checks * .40)

    def test_camera_and_teleport_wakes_reorder_existing_heap_entries(self):
        near = DIRECTOR.split("private void WakeDormantRivalsNearPlayer", 1)[1].split(
            "internal void RegisterDormantRival", 1)[0]
        camera = DIRECTOR.split("private void WakeRivalsOnCameraChange", 1)[1].split(
            "internal bool TryVirtualGround", 1)[0]
        self.assertIn("bot.ForceDormantProximityProbe();", near)
        self.assertIn("ScheduleDormantRival(i, now);", near)
        self.assertIn("ScheduleDormantRival(i, now);", camera)
        self.assertIn("index = SiftDormantHeapUp(index);", DIRECTOR)
        self.assertIn("SiftDormantHeapDown(index);", DIRECTOR)

    def test_sleeping_slots_defer_native_liveness_to_existing_deadline(self):
        sweep = DIRECTOR.split("public void TickDormantRivals(float now)", 1)[1].split(
            "private const int MaximumSpawnPasses", 1)[0]
        self.assertIn("ReferenceEquals(bot, null)", sweep)
        due = sweep.index("if (now >= bot.NextDormantWakeTime)")
        alive = sweep.index("if (bot == null || !bot.IsAlive)")
        tick = sweep.index("bot.TickDormantFromDirector(now, playerPosition);")
        self.assertLess(due, alive)
        self.assertLess(alive, tick)
        self.assertIn("SetCentralDormancy(false)", BOT.split("private void OnDied", 1)[1])
        self.assertIn("RegisterDormantRival(spatialSlot, this, false)",
                      BOT.split("private void OnDestroy", 1)[1])

    def test_benchmark_drives_queue_when_director_disabled(self):
        bench = (Path(__file__).resolve().parents[2] /
                 "Assets/AarisWorld/Editor/AarisBotFrameBenchmark.cs").read_text()
        self.assertIn("director.TickDormantRivals(Time.time);", bench)

    def test_real_scene_soak_drives_queue_when_director_disabled(self):
        qa = (Path(__file__).resolve().parents[2] /
              "Assets/AarisWorld/Editor/AarisFullSceneCombatSoak.cs").read_text()
        self.assertIn("director.enabled = false;", qa)
        self.assertIn("if (stage >= 3 && director != null && !director.enabled)", qa)
        self.assertIn("director.TickDormantRivals(Time.time);", qa)


if __name__ == "__main__":
    unittest.main()
