# Pinned CC0 PBR BattleRifle and Sniper source

These offline firearm visuals are deterministic derivatives of:
kter1/fps-mode-public@41fc12d8bb095dcd03451060d2aa62a96d9667d5

Every source file is verified with Git's canonical blob SHA-1.
The converter excludes the source LOD node, exports only the full-view
mesh, caps base/normal maps at 1024, and packs metallic/smoothness
into a 512 RGBA map. The game performs no runtime asset download.

## BattleRifle

- Creator: 3DModelsCC0
- License: CC0-1.0
- Source: https://3dmodelscc0.itch.io/free-cc0-guns-explosives-pack
- Physical source length: 1.0165 m
- Exported view triangles: 5801
- Exported vertices: 6310
- Runtime materials: BattleRifle_Finish, BattleRifle_Drum

## Sniper

- Creator: Poly Haven
- License: CC0-1.0
- Source: https://polyhaven.com/a/bolt_action_rifle_7_62
- Physical source length: 1.231 m
- Exported view triangles: 19365
- Exported vertices: 18826
- Runtime materials: Sniper_Optic, Sniper_Finish

## Pinned source Git blobs

- 21469c379635774adcd2506cee4d866afdddd101  manifest/weapons.json
- e385ed98f186c0015c3f1f5eccabc2fc109336a7  licenses/3dmodelscc0-guns.txt
- a82c35a1623f210f35353da579e18a63adc3944a  models/weapons/lmg_l762.gltf
- 2931e26970820d2d99f9de7e350aa1f51caf21ec  models/weapons/lmg_l762.bin
- fa4f52c6f766a17656c701f8cc708e02c94a00e8  models/weapons/textures/ak47_albedo.jpg
- b3f74726c897e50c881d3f3065b468ce062386ca  models/weapons/textures/ak47_normal.jpg
- 8f7e2a5f3b11e322070f6bdc36c1a0ea2908ed34  models/weapons/textures/ak47_orm.jpg
- 2aaa6a77f1216b706a3a75169c8de0a8b4350e29  models/weapons/textures/suomi_albedo.jpg
- 97b38685a6e7de1424a3c598d0e646d0e3c05b1a  models/weapons/textures/suomi_normal.jpg
- a8447a22d7ec3c3b0a1687bf53d44b55c8270a92  models/weapons/textures/suomi_orm.jpg
- c5d2656da79da0fd094d8a2016795042db5b4917  models/weapons/sniper_s90.gltf
- fdaa08fb4b303464f5d303838d013b9d067529b3  models/weapons/sniper_s90.bin
- 39c904d0b88fb7323a9b5df74c3b4602a3f6860c  models/weapons/textures/bolt_rifle_accessories_albedo.jpg
- 377af0b0f744537b049f51c4c6be89a5f9839a9d  models/weapons/textures/bolt_rifle_accessories_normal.jpg
- 9388c71629bf5dacc354c028010529fba740aaf9  models/weapons/textures/bolt_rifle_accessories_orm.jpg
- caca94e2c79cbedb289e5e27dbc62e0af0d69a4d  models/weapons/textures/bolt_rifle_albedo.jpg
- a6b3185a948e9bcc61e94316b8831e43efb91ce5  models/weapons/textures/bolt_rifle_normal.jpg
- b15ae58dc36531d70b528e2426d5adcd46d9b464  models/weapons/textures/bolt_rifle_orm.jpg
