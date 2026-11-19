#!/usr/bin/env python3
"""Android authored aircraft finish load staging; static checks only."""
import unittest
from pathlib import Path

DIR = Path(__file__).resolve().parents[2] / "Assets/AarisWorld/Scripts"
DROP = (DIR / "AarisOfflineDrop.cs").read_text(encoding="utf-8")
VIS = (DIR / "AarisOfflineDropVisuals.cs").read_text(encoding="utf-8")
MAT = Path(__file__).resolve().parents[2] / "Assets/Resources/AarisDrop/Materials"


class AircraftFinishPrewarm(unittest.TestCase):
    def test_all_five_real_saved_finishes_staged(self):
        begin = VIS.split("internal static IEnumerator PrewarmAircraftFinishes()", 1)[1].split(
            "internal static IEnumerator PrewarmCanopyPrefab()", 1)[0]
        for name in ("HulledMetal", "DarkCockpit", "WingPanels", "EngineDark", "SafetyAccent"):
            self.assertIn(f'"CargoPlane-{name}"', VIS)
            self.assertTrue((MAT / f"CargoPlane-{name}.mat").exists())
        self.assertIn('Resources.LoadAsync<Material>(', begin)
        self.assertIn('"AarisDrop/Materials/" + name', begin)
        self.assertIn('request.priority = -1;', begin)
        self.assertIn('yield return request;', begin)
        self.assertIn('yield return null;', begin)
        self.assertNotIn('Instantiate(', begin)
        self.assertNotIn('new Material(', begin)
    def test_preload_precedes_native_plane_creation(self):
        android = DROP.split("private IEnumerator LoadPlaneAndBeginFlight()", 1)[1].split(
            "private void CompleteFlightSetup()", 1)[0]
        self.assertLess(android.index("PrewarmAircraftFinishes()"),
                        android.index("AarisOfflineDropVisuals.CreatePlane("))
        self.assertIn("if (!isActiveAndEnabled) yield break;", android)
        self.assertIn("out propellers, source, highDetail, true", android)
    def test_cached_android_finish_preserves_existing_material_fallback(self):
        saved = VIS.split("private static Material SavedAircraftFinish(", 1)[1].split(
            "private static Material EnsureDaylightFill(", 1)[0]
        self.assertIn('aircraftFinishCache.TryGetValue(materialName, out saved)', saved)
        self.assertIn('!aircraftFinishesPrewarmed', saved)
        self.assertIn('Resources.Load<Material>("AarisDrop/Materials/" + materialName)', saved)
        self.assertIn('saved.shader.name.StartsWith("Universal Render Pipeline/"', saved)
        self.assertIn("Paint(ref fallback, fallbackColor)", saved)
        self.assertIn("SetAircraftFinish(material, metallic, smoothness)", saved)
        self.assertIn("#else", saved)
    def test_reset_caches_without_overwriting_authored_assets(self):
        reset = VIS.split("private static void ResetCanopyPrewarm()", 1)[1].split(
            "internal static IEnumerator PrewarmAircraftFinishes()", 1)[0]
        self.assertIn("aircraftFinishCache.Clear();", reset)
        self.assertIn("aircraftFinishesPrewarmed = false;", reset)
        self.assertIn("RuntimeInitializeLoadType.SubsystemRegistration", VIS)
    def test_visuals_and_editor_path_remain(self):
        self.assertIn('CreatePlane(pathStart,\n                planeHeadingRotation, out propellers);', DROP)
        self.assertIn('Resources.Load<GameObject>("AarisDrop/CargoPlaneRealistic")', VIS)
        self.assertIn('SavedAircraftFinish("CargoPlane-HulledMetal"', VIS)
        self.assertIn('SavedAircraftFinish("CargoPlane-WingPanels"', VIS)
        self.assertIn('hullPaint = EnsureDaylightFill(hullPaint, ref hullDaylightFinish);', VIS)
        self.assertIn("AddAircraftMarkerLights(root.transform)", VIS)


if __name__ == "__main__":
    unittest.main(verbosity=2)
