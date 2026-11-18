# Incomplete Humanoid arm-rig fallback — 18 November 2026

**Cause:** The weapon could be camera/chest-anchored when the
right arm was present, even though the analytic two-hand IK also
required left-arm bones. In a partial Humanoid import this could
leave a floating weapon and unsolved wrists.

**Fix:** One seven-Transform readiness check now controls both
chest anchoring and the two-arm IK. When a required bone is
absent, use the existing right-hand world-pose fallback instead.
The normal full-Humanoid pose is unchanged.

**Cost:** Cached Transform checks only; no extra physics,
materials, animator searches, renderer passes or allocations.

**QA:** Numerical/source matrix plus weapon and locomotion
regressions. The visible Unity Editor has an empty unsaved
scene owned by another session; no gameplay screenshot or
Android FPS validation is claimed.
