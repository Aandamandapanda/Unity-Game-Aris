#!/usr/bin/env python3
"""Source-level guardrails for reversible Android-only MSAA tuning.

Does not replace Unity Editor compilation or Android GPU profiling.
"""
from pathlib import Path
import re
import unittest

ROOT = Path(__file__).resolve().parents[2]
CODE = ROOT / 'Assets/AarisWorld/Scripts/AarisMobileRuntime.cs'
URP = ROOT / 'Assets/TerrainDemoScene_URP/Settings/URP/URP-HighQuality Pipeline Asset.asset'


class AndroidMsaaBudget(unittest.TestCase):
    def test_android_gpu_limit_and_native_scale(self):
        s = CODE.read_text()
        setup = s.split('private void ConfigureAndroidQuality()', 1)[1].split('private void SampleMobileFrameTiming()', 1)[0]
        self.assertIn('if (constrainedGraphics)', setup)
        self.assertIn('appliedMsaaSamples = Mathf.Min(originalMsaaSamples, 2);', setup)
        self.assertIn('androidPipeline.msaaSampleCount = appliedMsaaSamples;', setup)
        self.assertIn('originalRenderScale = androidPipeline.renderScale;', setup)

    def test_safe_restore(self):
        cleanup = CODE.read_text().split('private void OnDestroy()', 1)[1]
        self.assertRegex(cleanup, r'if \(modifiedMsaaSamples && androidPipeline != null &&\s*androidPipeline.msaaSampleCount == appliedMsaaSamples\)\s*androidPipeline.msaaSampleCount = originalMsaaSamples;')

    def test_unmodified_high_quality_urp(self):
        p = URP.read_text()
        self.assertIn('m_MSAA: 4', p)
        self.assertIn('m_RenderScale: 1', p)


if __name__ == '__main__':
    unittest.main(verbosity=2)
