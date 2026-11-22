# 100-bot distance virtualization — 2026-11-22

Target: Unity 6000.3.26f1, Android URP, 100 offline rivals, 2x2 km terrain.

## Runtime policy

- Preserve the existing 100 logical rival slots, health/death events, zone damage, loot and exactly-two player attacker selection.
- Keep the player-adjacent 90m full-frequency physical AI. Distant AI remains time sliced (0.35s cadence, phased by slot on Android).
- Cache the main camera. Offscreen distant 155-485m substitute meshes are deactivated, not merely hidden by camera frustum. 485m+ rivals get no authored visual. Existing close-up realistic rigs remain intact.
- Cull offscreen Humanoid Animator completely. Retire inactive expensive rigs as before. Camera relevance is probed every 0.12s per staggered rival, and never forces full-frequency distant AI.
- At >185m and outside the camera, disable the CharacterController and use terrain-aware, obstacle-index-aware mathematical movement, zone retreat, ammo/reload, medkits and lightweight rival-versus-rival damage. Restore collider by <=165m or on-camera for real hitscan/Red-Lock physics. Virtual combat never inflicts unphysical player damage or on-camera damage.
- Remote mathematical kills use the existing authoritative health/death callbacks, Alive count and an on-screen recent-kill text. Near fights retain normal physical LOS, cover, gun and 1-2 player attacker cap.

## Verified in connected Codespace

- Unity Editor batch QA: RESULT=PASS; both real-hit paths, Red-Lock, 100-slot selection, cover, 2 attacker limit, plus new remote rival elimination/damage attribution/no physical capsules/no imaginary player damage.
- Camera-instrumented 100-bot Linux Editor benchmark: RESULT=PASS; 100/100 moved; 71 of 97 remote-body candidates virtualized; 1.300s simulated; baseline CPU main mean 0.239ms, 100 bots mean 0.344ms (extra 0.105ms/frame). Baseline and loaded samples include Editor overhead.
- C# compiler errors: none during these Editor runs. Warnings in unrelated files remain.

These are isolated Linux Editor tests, **not** a full-terrain GPU or Snapdragon 680 Android FPS measurement. They do not prove zero total CPU load, or guaranteed smoothness on any phone. In particular, on-screen rivals intentionally keep collision/rendering cost for real player hit detection. On-device Development Build profiling is required before claiming final FPS/thermal wins.
