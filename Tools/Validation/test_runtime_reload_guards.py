from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets/AarisWorld/Scripts"
hud = (SRC / "AarisWeaponHUD.cs").read_text("utf-8")
visual = (SRC / "AarisCharacterVisual.cs").read_text("utf-8")
audio = (SRC / "AarisAudioDirector.cs").read_text("utf-8")

assert "if (captions.Count < 6) return;" in hud
assert "captions.Count > 5 && captions[5] != null" in hud
assert "captions.Count > 0 && captions[0] != null" in hud

assert "!output.IsValid()" in visual
assert "index >= previous.Length || index >= output.GetInputCount()" in visual
assert "!layered.IsValid() || layered.GetInputCount() <= 1" in visual
assert "!playable.IsValid() || clip == null" in visual

assert "voices[selected] == null) return;" in audio
assert "AudioSource candidateVoice = voices[candidate];" in audio
assert "candidateVoice == null || candidateVoice.isPlaying" in audio
assert "if (voice == null) return;" in audio

print("runtime reload guard regression checks passed")
