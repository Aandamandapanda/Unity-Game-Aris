# ALS-Unity crouch animation adapter

**Code source:** https://github.com/schyro/ALS-Unity (MIT, copyright 2026 Serkan Yalnız)

The `AarisAlsCrouchBlend.cs` interpolation and separated crouch idle/move
profile are adapted from `ALSCharacterAnimation.cs` and `ALSMath.cs`. The
MIT copyright and license text are copied to `ALS_Unity_MIT_LICENSE.txt`.
Only the crouch animation-driving design is integrated. The project's physics
motor, collision state, terrain support and other gaits are unchanged.

**Clip source:** Quaternius Universal Animation Library (CC0 1.0),
distributed in ALS-Unity at
`Assets/ALS/Art/Quaternius/Mannequin_UAL1.fbx`.
Source SHA-256:
`3654c2f3361005eb5bd5197dd4809f334bc72d9301e436aaa986943db3fd3450`.

The two crouch clips are `Crouch_Idle_Loop` and `Crouch_Fwd_Loop`.
Source license: https://creativecommons.org/publicdomain/zero/1.0/
Attribution: Quaternius (https://quaternius.com). No additional
unrelated meshes, characters, levels, or animation sets are included.

**Runtime**: two compact standalone humanoid `.anim` clips in
`Assets/Resources/AarisCharacters/Primary/ALS/` are loaded only for a
valid calibrated humanoid. If unavailable, the prior crouch animations remain
the fallback. The crouch pose root doesn't use the legacy 0.73m visual lowering
when these authored crouch clips are selected. Native Unity pose screenshots
and low-end Android measurements are required before merging this draft.
