# Responsive car acceleration and mobile light allocation

This isolated vehicle upgrade retains the authored seven car families, all spawn points, collision shapes, WheelCollider solver, input handling and camera state. Continuous cubic SmoothStep torque interpolates the original 1500 / 1180 / 850 / 580 N.m targets over 0-7, 7-14 and 14-21 m/s instead of dropping torque abruptly at each threshold. It introduces no heap allocations or queries in the physics hot path.

The optional no-shadow headlight spotlight is constructed only at first driver entry. Parked cars retain their shared URP lens geometry and materials, but instantiate zero native Light components. The light is parented to the original lamp frame under the visual-body pivot and is reused for every subsequent drive; exiting disables it. No new per-frame Light creation occurs.

Python source and numerical regressions check boundary continuity, monotonicity, and light lifecycle. No Unity Editor or Android player was available in the Codespace; compilation, mobile FPS and on-screen alignment remain unverified and should be measured on a real device before release.
