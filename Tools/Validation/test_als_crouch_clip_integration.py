"""Static check of the MIT ALS-Unity / CC0 Quaternius crouch integration.

Run without Unity: python3 Tools/Validation/test_als_crouch_clip_integration.py
This does not replace the required real Unity pose test.
"""
from pathlib import Path
import unittest

root = Path(__file__).resolve().parents[2]
visual = (root / "Assets/AarisWorld/Scripts/AarisCharacterVisual.cs").read_text()
blend = (root / "Assets/AarisWorld/Scripts/AarisAlsCrouchBlend.cs").read_text()
asset_dir = root / "Assets/Resources/AarisCharacters/Primary/ALS"
license_file = root / "Docs/ThirdParty/ALS_Unity_MIT_LICENSE.txt"
attribution = root / "Docs/ThirdParty/ALS_CROUCH_CC0_ATTRIBUTION.md"

class ALSCrouchIntegration(unittest.TestCase):
    def test_cc0_als_crouch_blend_assets_and_fallback(self):
        assert "MovingWeight(" in blend and "InterpTo(" in blend
        assert "StanceBlendSeconds = 0.25f" in blend
        assert "NominalForwardMetresPerSecond = 0.72f" in blend
        assert "Mathf.Clamp01(deltaTime * speed)" in blend
        assert "Resources.Load<AnimationClip>(AlsCrouchIdlePath)" in visual
        assert "Resources.Load<AnimationClip>(AlsCrouchForwardPath)" in visual
        assert "alsCrouchLoaded = alsCrouchIdle != null && alsCrouchForward != null" in visual
        assert "alsCrouchIdle.humanMotion && alsCrouchForward.humanMotion" in visual
        assert "crouchClip = alsCrouchLoaded ? alsCrouchIdle" in visual
        assert "crouchWalkClip = alsCrouchLoaded ? alsCrouchForward" in visual
        assert "AarisAlsCrouchBlend.MovingWeight(" in visual
        # CC0, natural mocap, ALS and community Humanoid clips already include true
        # crouch hip lowering; a second legacy root drop would bury the knees/feet.
        assert "crouchLowering = cc0CrouchLoaded || naturalMocapLoaded ||" in visual
        assert "alsCrouchLoaded || communityStanceClipsLoaded ? 0f" in visual
        assert 'FindClip("crouch", "crouch")' in visual  # compatible fallback
        assert 'FindClip("crouch_walk", "crouch")' in visual

        for filename, expected_name in (
            ("als_crouch_idle.anim", "als_crouch_idle"),
            ("als_crouch_forward.anim", "als_crouch_forward"),
        ):
            p = asset_dir / filename
            assert p.exists(), p
            assert 8_000 < p.stat().st_size < 600_000, (p, p.stat().st_size)
            data = p.read_text()
            assert "m_AnimationClipSettings:" in data
            assert "m_Legacy: 0" in data
            assert f"m_Name: {expected_name}" in data
            assert (asset_dir / (filename + ".meta")).exists()

        assert (asset_dir.parent / "ALS.meta").exists()
        assert license_file.exists()
        assert "MIT License" in license_file.read_text()
        assert "Copyright (c) 2026 Serkan" in license_file.read_text()
        assert "Quaternius" in attribution.read_text()
        assert "CC0 1.0" in attribution.read_text()

        # Reference behaviour: ALSMath.InterpTo follows the target by clamp01(dt*speed).
        moving = 0.0
        for _ in range(15):
            target = 1.0
            moving += (target - moving) * min(1.0, (1.0 / 60.0) * 10.0)
        assert 0.93 <= moving <= 0.94, moving
        for _ in range(15):
            moving += (0.0 - moving) * min(1.0, (1.0 / 60.0) * 10.0)
        assert moving < 0.062, moving


if __name__ == "__main__":
    unittest.main()
