#!/usr/bin/env python3
"""Source contracts for essential mobile loot import and next-frame JSON decode."""
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets/AarisWorld/Scripts"
GATE = (SRC / "AarisMobileStreamingFrameGate.cs").read_text()
LOOT = (SRC / "AarisWeaponLootWorld.cs").read_text()
WEAPON = (SRC / "AarisWeaponCatalog.cs").read_text()
FIXTURE = (ROOT / "Tools/Validation/StreamingAdmissionTests.cs").read_text()


class EssentialLootManifestAndParseFrame(unittest.TestCase):
    def test_essential_loot_import_is_direct_and_priority_preserved(self):
        body = LOOT.split("internal static IEnumerator LoadForAndroidAsync()", 1)[1].split(
            "#endif", 1)[0]
        self.assertIn('Resources.LoadAsync<TextAsset>(\n                "AarisWeapons/loot_candidates")', body)
        self.assertIn("request.priority = 0;", body)
        self.assertIn("TrackEssentialLootManifestLoad(request);", body)
        self.assertIn("ReserveNextFrameForEssentialJsonParse();", body)
        self.assertLess(body.index("TrackEssentialLootManifestLoad(request);"),
                        body.index("yield return request;"))
        self.assertLess(body.index("ReserveNextFrameForEssentialJsonParse();"),
                        body.index("yield return null;"))
        self.assertNotIn("TryStartOptionalLoadAsync", body)
        self.assertIn("AndroidWarmupFinished = true;", body)

    def test_weapon_parse_also_reserves_next_render_frame(self):
        body = WEAPON.split("internal static IEnumerator LoadForMatchAsync()", 1)[1].split(
            "public static GameObject ModelFor(", 1)[0]
        self.assertIn("ReserveNextFrameForEssentialJsonParse();", body)
        self.assertIn("Resources.LoadAsync<GameObject>(spec.modelResource)", body)
        self.assertLess(body.index("ReserveNextFrameForEssentialJsonParse();"),
                        body.index("var definitions = ReadDefinitions("))
        self.assertNotIn("TryStartOptionalLoadAsync", body)

    def test_parallel_completion_and_parse_fences_apply_to_optional_only(self):
        self.assertIn("internal static void TrackEssentialLootManifestLoad(ResourceRequest request)", GATE)
        self.assertIn("private static bool IsEssentialLootManifestBusy()", GATE)
        self.assertIn("essentialLootCompletionFrame = Time.frameCount;", GATE)
        self.assertIn("System.Math.Max(essentialJsonParseReservedThroughFrame,", GATE)
        self.assertEqual(GATE.count("if (IsEssentialJsonParseFrame()) return false;"), 2)
        self.assertEqual(GATE.count("bool essentialLootBusy = IsEssentialLootManifestBusy();"), 2)
        self.assertEqual(GATE.count("if (essentialAudioBusy || essentialWeaponBusy || essentialLootBusy) return false;"), 2)
        self.assertIn("essentialJsonParseReservedThroughFrame = -1;", GATE)
        self.assertIn("EssentialLootDeferredAttempts = 0;", GATE)
        self.assertNotIn("QualitySettings.", GATE)
        self.assertNotIn("TerrainData.", GATE)
        self.assertIn("private static void EssentialLootAndJsonParseFence()", FIXTURE)
        self.assertIn("EssentialWeaponFence(); EssentialLootAndJsonParseFence(); Pressure();", FIXTURE)
        self.assertIn('Configure(8192, "Adreno 730");', FIXTURE)


if __name__ == "__main__":
    unittest.main(verbosity=2)
