# Offline gun model source, redistribution and quality limits

## PBR M4A1 replacement

The **AssaultRifle** slot now uses a higher-fidelity M4A1 by **nisu**, distributed as **CC0 1.0** on OpenGameArt:
https://opengameart.org/content/m4a1

Source attachment used for the vendored asset:
https://opengameart.org/sites/default/files/m4a1_0.zip

The downloaded ZIP was verified before conversion:

- Source ZIP SHA-256: `ed5779ec82718861964227e2aad2a900978ea087081154365d6d86246be62f0d`
- Original `M4A1.fbx`: 278,384 bytes; Git blob `e3afbe0d4e42530fb0b5fb33445e8043eccd9941`
- Mobile base color: 1024x1024; Git blob `2f9a44b109d17244de072c238eccc007cd2d228d`
- Mobile normal map: 1024x1024; Git blob `5a6ee12b1e8c3b194de256403652f1fd63a4e49c`
- Packed metallic/smoothness: 512x512; Git blob `9109237e8c2928dd9a34ba969c2b24ea1655b169`

The source 2048x2048 base color, normal, metallic and roughness maps were converted locally for the Android build. Base color and normal are capped at 1024; metallic and inverted roughness are packed into one 512 RGBA map (metallic in RGB, smoothness in alpha). The source height map is intentionally omitted to avoid extra mobile texture memory and parallax cost. The game performs no runtime asset download.

## PBR Grease Gun and Shotgun replacements

The **SMG** and **Shotgun** slots use the Grease Gun M3 and Shotgun from
**3dmodelscc0 / nisu, Free CC0 Guns & Explosives Pack**. The author publishes
the pack under **CC0 1.0 / public domain**:

- Author/source page: https://3dmodelscc0.itch.io/free-cc0-guns-explosives-pack
- Pinned public archive mirror used for provenance:
  https://github.com/BoQsc/cc0-guns-explosives-pack-glb/releases/download/0/Guns.Explosives.rar

The vendored FBXs are byte-identical to an independently documented CC0
provenance copy (Dayan-02/GV_game@8b1f8e0456424aa9e59ec4ef43196197889f171a):

| Aaris asset | Bytes | Git blob SHA-1 | Original source SHA-256 |
| --- | ---: | --- | --- |
| PBR/GreaseGun/Grease_Gun.fbx | 247,072 | ebebfcac66b29eaa9ca64bdbe8bc1106ebf4a97b | 68C31EBBF1967B59D12F0EF10D5C1750522A4A7C152B8529F0C326971A0822BD |
| PBR/Shotgun/Shotgun.fbx | 78,336 | b7acd94e87c909753495ce42465532ac79c0510c | 2184F1477025779F05BF4A20C72107518AA5AE8CA69B96DCD6E83D060C570932 |

The original texture sets contain base color, normal, metallic, roughness and
height maps. For the Android build, base color and normal are capped at
1024x1024, metallic + inverted roughness are packed into one 512x512
metallic/smoothness texture, and the height map is omitted. Unity importer
settings keep base color sRGB, normal/metallic data linear, and mobile texture
compression enabled. This preserves normal-map and metal/rough surface detail
without shipping the source multi-megabyte texture set or adding parallax cost.

All three active PBR firearm families (M4A1, Grease Gun and Shotgun) are loaded
from committed Resources assets. No firearm art is downloaded at runtime.

Five rigged FBX firearm models are vendored from **Pichuliru, CC0 Flat Guns East** (2022). Original source: https://opengameart.org/content/cc0-flat-guns-east ; upstream curated copy: https://github.com/petroulacl/fps-asset-kit . OpenGameArt explicitly marks the artist's 10-model East pack **CC0 1.0** and includes FBX/GLB/OBJ/Blend. Keep this record when redistributing the game.

Source tree pinned to `petroulacl/fps-asset-kit@a19b7458a593598211c95ec46ef4eb4b6d1f94d7`. Each file is independently verified using Git's canonical blob SHA-1 (not a truncated filename checksum):

| Unity local FBX | Original source filename | Git blob SHA-1 |
| --- | --- | --- |
| SMG.fbx | SMG_Compact_East.Rig.fbx | 2fadbba99ac6d3373b8f6b04786374c42c54394f |
| AssaultRifle.fbx | Rifle_Assault_East.Rig.fbx | 8f2109f415755ad0af8aaa1d911ddfea7c2064cd |
| BattleRifle.fbx | Rifle_Battle_East.RIg.fbx | 24f2bfee4166e2208cb070b3de273f0989fafc9d |
| Sniper.fbx | Sniper_Rifle_East.Rig.fbx | a743e79ea184966021aba937d30cb6876e2cbaba |
| Shotgun.fbx | Shotgun_Pump_East.RIg.fbx | 3590c4b06c378037190edd870ca0c24fdaf5ae8a |

The binary FBX files must be **committed to this game repository**; a deleted upstream link cannot break a shipped APK. The GitHub Actions file `.github/workflows/aaris-weapon-vendor.yml` retrieves only these five pinned assets once. No actual game ever downloads weapons from that link.

**Honest visual limitation:** The BattleRifle and Sniper slots still use the original CC0 **flat-shaded/rigged** firearm shapes. AssaultRifle, SMG and Shotgun now use locally vendored PBR model/texture sets, but they should only be called visually verified after Unity/device inspection confirms import orientation, shading, scale and hand fit. No PUBG/BGMI meshes, proprietary skins, trademarked art or other copyrighted commercial-game assets have been copied.

The Unity model poses/hand orientations must be inspected in Editor before declaring the feature ready. The initial offline catalog uses balanced *Aaris-specific* parameters, not copied proprietary game values.

All firearm art remains 3D visual decoration only, never alters collision, projectile physics, buildings, terrain or the 2km player controller.
