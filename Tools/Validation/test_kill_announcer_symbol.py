#!/usr/bin/env python3
"""Authoritative player-only kill announcement, audio and symbol contracts."""
import hashlib
import math
from pathlib import Path
import struct
import unittest
import wave

ROOT = Path(__file__).resolve().parents[2]
AUDIO = (ROOT / "Assets/AarisWorld/Scripts/AarisAudioDirector.cs").read_text()
DIRECTOR = (ROOT / "Assets/AarisWorld/Scripts/AarisOfflineBRDirector.cs").read_text()
GLYPHS = (ROOT / "Assets/AarisWorld/Scripts/AarisHudGlyphs.cs").read_text()


class KillAnnouncerTests(unittest.TestCase):
    def test_player_only_authoritative_fatality_drives_announcement(self):
        report = DIRECTOR.split(
            "internal void ReportRivalElimination(GameObject killer, GameObject victim)", 1
        )[1].split("internal void ShowRivalGunfire", 1)[0]
        self.assertIn("if (victim == null) return;", report)
        self.assertIn("killer.transform == player || killer.transform.IsChildOf(player)", report)
        self.assertIn("ShowPlayerKillConfirmation();", report)
        self.assertNotIn("killer == null", report)

        damaged = DIRECTOR.split(
            "private void OnDamaged(AarisOfflineHealth victim, float loss, GameObject attacker)", 1
        )[1].split("private void SeekCoverFrom", 1)[0]
        self.assertIn("if (victim.IsDead && owner != null)", damaged)
        self.assertIn("owner.ReportRivalElimination(attacker, gameObject);", damaged)

    def test_first_then_repeat_copy_and_counter_are_deterministic(self):
        show = DIRECTOR.split("private void ShowPlayerKillConfirmation()", 1)[1].split(
            "private void EnsurePlayerKillBanner()", 1
        )[0]
        for token in (
            "playerEliminations++;",
            "bool firstKill = playerEliminations == 1;",
            'firstKill ? "FIRST KILL" : "ONE MORE KILL"',
            '"KILLS  " + playerEliminations',
            "AarisAudioDirector.PlayerElimination(firstKill,",
            "playerKillBannerClearAt = Time.time + 2.35f;",
        ):
            self.assertIn(token, show)
        labels = ["FIRST KILL" if count == 1 else "ONE MORE KILL"
                  for count in range(1, 8)]
        self.assertEqual(labels[0], "FIRST KILL")
        self.assertEqual(labels[1:], ["ONE MORE KILL"] * 6)

    def test_announcer_uses_existing_local_voice_pool(self):
        for key in ('"first_kill"', '"one_more_kill"'):
            self.assertIn(key, AUDIO)
        self.assertLess(AUDIO.index('"first_kill"'), AUDIO.index('"step_dirt"'))
        method = AUDIO.split(
            "public static void PlayerElimination(bool firstKill, Vector3 position)", 1
        )[1].split("public static void ReloadStarted", 1)[0]
        self.assertIn('firstKill ? "first_kill" : "one_more_kill"', method)
        self.assertIn("instance.OneShot(", method)
        local = AUDIO.split("private static bool IsLocalCue", 1)[1].split(
            "private void OneShot", 1
        )[0]
        self.assertIn('key == "first_kill"', local)
        self.assertIn('key == "one_more_kill"', local)

    def test_font_independent_kill_symbol_uses_existing_atlas(self):
        for token in (
            "enum Glyph { Fire, Jump, Crouch, Prone, Sprint, Kill }",
            "private static readonly Sprite[] Sprites = new Sprite[8];",
            "private static float Kill(Vector2 p)",
            "AarisHudGlyphs.Get(AarisHudGlyphs.Glyph.Kill)",
            'new GameObject("Kill symbol",',
            "icon.raycastTarget = false;",
        ):
            self.assertIn(token, GLYPHS + DIRECTOR)
        banner = DIRECTOR.split("private void EnsurePlayerKillBanner()", 1)[1].split(
            "internal void ShowRivalGunfire", 1
        )[0]
        self.assertNotIn("☠", banner)
        self.assertIn("playerKillBanner.SetActive(false);", banner)

    def test_packaged_speech_is_valid_audible_mono_pcm(self):
        expected = {
            "first_kill.wav": "62b048523b13ba9a1a013f4150e75ce5203afdb3ab8975db5def38d4e3c996e8",
            "one_more_kill.wav": "2703d73ac4c8cdf63a9c4132ad6441ec33676eecc7ae129aaf6a16f1d2497a7a",
        }
        for name, digest in expected.items():
            path = ROOT / "Assets/Resources/SFX" / name
            data = path.read_bytes()
            self.assertEqual(hashlib.sha256(data).hexdigest(), digest)
            with wave.open(str(path), "rb") as clip:
                self.assertEqual(clip.getnchannels(), 1)
                self.assertEqual(clip.getsampwidth(), 2)
                self.assertEqual(clip.getframerate(), 22050)
                self.assertGreater(clip.getnframes(), 22050)
                samples = struct.unpack(
                    "<" + "h" * clip.getnframes(), clip.readframes(clip.getnframes())
                )
            rms = math.sqrt(sum(value * value for value in samples) / len(samples))
            self.assertGreater(rms, 1200)
            meta = path.with_suffix(path.suffix + ".meta").read_text()
            self.assertIn("forceToMono: 1", meta)
            self.assertIn("preloadAudioData: 1", meta)


if __name__ == "__main__":
    unittest.main()
