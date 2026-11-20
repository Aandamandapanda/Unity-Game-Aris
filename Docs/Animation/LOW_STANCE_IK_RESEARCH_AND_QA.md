# Low-stance IK replacement: research and integration notes

Target: Unity 6 URP Humanoid standing/crouch/prone transitions on a low-memory Android runtime. This branch replaces the fixed, asymmetric crouch-foot coordinates and the forced *upward* knee hint during settled prone with a rig-relative target/hint solver.

## Source research and licensing

- **Unity Animation Rigging – Two Bone IK**: https://docs.unity.cn/Packages/com.unity.animation.rigging@1.2/manual/constraints/TwoBoneIKConstraint.html. Official target, hint and per-constraint weight model; referenced for the architecture, *not copied*. Unity's package and examples have their own licensing.
- **Unity animation-jobs-samples, TwoBoneIKJob**: https://github.com/Unity-Technologies/animation-jobs-samples. Referenced for animation-stream constraint sequencing and two-bone geometry. This project uses its own existing Unity analytic IK code instead of importing the job to avoid another write to the same bones. No sample source copied.
- **schyro/ALS-Unity**: https://github.com/schyro/ALS-Unity. MIT code, character/animation assets described as CC0. Shows speed-matched gait and authored animation plus corrective foot IK, but its own README notes crouch sliding and limited foot locking. No repo code or animation assets copied.
- **Quaternius Universal Animation Library**: publicly distributed as CC0. Candidate to *evaluate*, not imported: a complete new animation retarget needs visual QA against this game's humanoid avatar and import settings. No third-party clip is included in this branch.

The new formulas below are original code implementing the documented target/hint approach, not verbatim third-party source. No third-party dependencies, assets or license obligations have been introduced.

## Replacement design

1. Derive actual hip width from the current left/right thighs, not a fixed 38 cm crouch footprint. Give the two foot targets symmetric lateral offsets and preserve a **bounded** authored forward stagger.
2. Derive each prone leg extension separately from the current upper/lower leg spans. Never demand a fixed 1.055 m leg extension when the retargeted avatar is shorter.
3. Replace the old late-prone **upward** pole with a smoothly interpolated **downward support-plane** hint. Crouch keeps the anatomical forward pole; the existing analytic two-bone solver enforces knee-floor clearance rather than forcing unnatural raised triangles.
4. Use the existing stillness and turn gates: do not drag moving feet, do not change physics/controller/camera state, and do not add raycasts, heap allocations or animation graph layers. Preserve leg reach, foot X/Z locking and the existing terrain support solver.
5. If the native Unity rig shows bad anatomy, treat this as a failed visual gate and keep the PR in draft instead of merging unproven changes.

## QA gates

- Python source/geometry and mobile regression tests, including stance width, reach bounds and continuous knee-hint weights.
- Native Unity 6000.3.26f1 compilation (zero CS errors), front/side/back settled pose screenshots and time-sampled 16-frame stance transitions.
- The existing `SETTLED_PRONE_KNEE_TOO_HIGH` guard, grounded foot clearance and knee lateral flare. A success marker alone is insufficient when another QA error was recorded.
- Android 4 GB device FPS, GC allocation, slopes and stairs remain separate tests. No performance assertion is based on desktop Editor Play Mode alone.

**Baseline before this branch:** native Unity commit d58f1c57 produced all 12 settled-pose photos but failed `SETTLED_PRONE_KNEE_TOO_HIGH`: recorded left/right lower-leg heights above terrain ~0.406 / 0.343 m. This is the primary target, not a claim that the new branch has passed.
