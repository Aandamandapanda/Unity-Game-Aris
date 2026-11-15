#!/usr/bin/env python3
"""Vendor two pinned CC0 firearms into Unity-native OBJ + mobile PBR textures.

Every downloaded source byte is checked using Git's canonical blob SHA-1.
Generated OBJ excludes the source LOD meshes and needs no runtime glTF importer.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
import shutil
import struct
import tempfile
import urllib.request
from pathlib import Path

from PIL import Image

SOURCE_REPO = "kter1/fps-mode-public"
SOURCE_COMMIT = "41fc12d8bb095dcd03451060d2aa62a96d9667d5"
RAW_ROOT = (
    "https://raw.githubusercontent.com/" + SOURCE_REPO + "/" +
    SOURCE_COMMIT + "/content/"
)

SOURCE_BLOBS = {
    "manifest/weapons.json": "21469c379635774adcd2506cee4d866afdddd101",
    "licenses/3dmodelscc0-guns.txt": "e385ed98f186c0015c3f1f5eccabc2fc109336a7",
    "models/weapons/lmg_l762.gltf": "a82c35a1623f210f35353da579e18a63adc3944a",
    "models/weapons/lmg_l762.bin": "2931e26970820d2d99f9de7e350aa1f51caf21ec",
    "models/weapons/textures/ak47_albedo.jpg": "fa4f52c6f766a17656c701f8cc708e02c94a00e8",
    "models/weapons/textures/ak47_normal.jpg": "b3f74726c897e50c881d3f3065b468ce062386ca",
    "models/weapons/textures/ak47_orm.jpg": "8f7e2a5f3b11e322070f6bdc36c1a0ea2908ed34",
    "models/weapons/textures/suomi_albedo.jpg": "2aaa6a77f1216b706a3a75169c8de0a8b4350e29",
    "models/weapons/textures/suomi_normal.jpg": "97b38685a6e7de1424a3c598d0e646d0e3c05b1a",
    "models/weapons/textures/suomi_orm.jpg": "a8447a22d7ec3c3b0a1687bf53d44b55c8270a92",
    "models/weapons/sniper_s90.gltf": "c5d2656da79da0fd094d8a2016795042db5b4917",
    "models/weapons/sniper_s90.bin": "fdaa08fb4b303464f5d303838d013b9d067529b3",
    "models/weapons/textures/bolt_rifle_accessories_albedo.jpg": "39c904d0b88fb7323a9b5df74c3b4602a3f6860c",
    "models/weapons/textures/bolt_rifle_accessories_normal.jpg": "377af0b0f744537b049f51c4c6be89a5f9839a9d",
    "models/weapons/textures/bolt_rifle_accessories_orm.jpg": "9388c71629bf5dacc354c028010529fba740aaf9",
    "models/weapons/textures/bolt_rifle_albedo.jpg": "caca94e2c79cbedb289e5e27dbc62e0af0d69a4d",
    "models/weapons/textures/bolt_rifle_normal.jpg": "a6b3185a948e9bcc61e94316b8831e43efb91ce5",
    "models/weapons/textures/bolt_rifle_orm.jpg": "b15ae58dc36531d70b528e2426d5adcd46d9b464",
}

SPECS = {
    "BattleRifle": {
        "source": "lmg_l762",
        "manifest_key": "lmg_l762",
        "triangles": 5801,
        "length_m": 1.0165,
        "materials": {
            "lmg_l762_finish": (
                "BattleRifle_Finish",
                "ak47_albedo.jpg", "ak47_normal.jpg", "ak47_orm.jpg",
            ),
            "lmg_l762_drum": (
                "BattleRifle_Drum",
                "suomi_albedo.jpg", "suomi_normal.jpg", "suomi_orm.jpg",
            ),
        },
    },
    "Sniper": {
        "source": "sniper_s90",
        "manifest_key": "sniper_s90",
        "triangles": 19365,
        "length_m": 1.231,
        "materials": {
            "sniper_s90_optic": (
                "Sniper_Optic",
                "bolt_rifle_accessories_albedo.jpg",
                "bolt_rifle_accessories_normal.jpg",
                "bolt_rifle_accessories_orm.jpg",
            ),
            "sniper_s90_finish": (
                "Sniper_Finish",
                "bolt_rifle_albedo.jpg",
                "bolt_rifle_normal.jpg",
                "bolt_rifle_orm.jpg",
            ),
        },
    },
}

COMPONENTS = {
    5121: ("B", 1),
    5123: ("H", 2),
    5125: ("I", 4),
    5126: ("f", 4),
}
TYPE_WIDTH = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4}


def git_blob_sha(data: bytes) -> str:
    header = f"blob {len(data)}\0".encode()
    return hashlib.sha1(header + data).hexdigest()


def guid(path: Path) -> str:
    return hashlib.md5(("aaris-pbr-derived:" + path.as_posix()).encode()).hexdigest()


def download_sources(target: Path) -> None:
    for relative, expected in SOURCE_BLOBS.items():
        out = target / relative
        out.parent.mkdir(parents=True, exist_ok=True)
        req = urllib.request.Request(
            RAW_ROOT + relative,
            headers={"User-Agent": "AarisPinnedWeaponVendor/1.0"},
        )
        with urllib.request.urlopen(req, timeout=90) as response:
            data = response.read()
        actual = git_blob_sha(data)
        if actual != expected:
            raise RuntimeError(
                f"Git blob mismatch for {relative}: {actual} != {expected}")
        out.write_bytes(data)
        print("SOURCE", relative, len(data), actual)


def verify_sources(source: Path) -> None:
    for relative, expected in SOURCE_BLOBS.items():
        path = source / relative
        if not path.is_file():
            raise FileNotFoundError(path)
        data = path.read_bytes()
        actual = git_blob_sha(data)
        if actual != expected:
            raise RuntimeError(
                f"Git blob mismatch for {relative}: {actual} != {expected}")


def read_accessor(doc: dict, blob: bytes, index: int):
    accessor = doc["accessors"][index]
    if accessor.get("sparse"):
        raise ValueError("Sparse accessors are intentionally unsupported")
    view = doc["bufferViews"][accessor["bufferView"]]
    if view.get("buffer", 0) != 0:
        raise ValueError("Only one pinned external buffer is expected")
    fmt, scalar_bytes = COMPONENTS[accessor["componentType"]]
    width = TYPE_WIDTH[accessor["type"]]
    packed = scalar_bytes * width
    stride = view.get("byteStride", packed)
    if stride < packed:
        raise ValueError("Invalid byteStride")
    start = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    unpack = struct.Struct("<" + fmt * width)
    return [
        unpack.unpack_from(blob, start + i * stride)
        for i in range(accessor["count"])
    ]


def model_meta(path: Path) -> str:
    return f"""fileFormatVersion: 2
guid: {guid(path)}
ModelImporter:
  serializedVersion: 22200
  externalObjects: {{}}
  materials:
    materialImportMode: 1
    materialName: 0
    materialSearch: 1
    materialLocation: 0
  meshes:
    globalScale: 1
    useFileScale: 1
    meshCompression: 1
    isReadable: 0
  tangentSpace:
    normalImportMode: 0
    tangentImportMode: 2
  animations:
    importAnimation: 0
  importAnimation: 0
  userData: ''
  assetBundleName: ''
  assetBundleVariant: ''
"""


def default_meta(path: Path) -> str:
    return f"""fileFormatVersion: 2
guid: {guid(path)}
DefaultImporter:
  externalObjects: {{}}
  userData: ''
  assetBundleName: ''
  assetBundleVariant: ''
"""


def texture_meta(path: Path, max_size: int, normal: bool = False,
                 packed: bool = False) -> str:
    return f"""fileFormatVersion: 2
guid: {guid(path)}
TextureImporter:
  internalIDToNameTable: []
  externalObjects: {{}}
  serializedVersion: 12
  mipmaps:
    mipMapMode: 0
    enableMipMap: 1
    sRGBTexture: {0 if normal or packed else 1}
    linearTexture: 0
    fadeOut: 0
    borderMipMap: 0
    mipMapsPreserveCoverage: 0
    alphaTestReferenceValue: 0.5
    mipMapFadeDistanceStart: 1
    mipMapFadeDistanceEnd: 3
  bumpmap:
    convertToNormalMap: 0
    externalNormalMap: 0
    heightScale: 0.25
    normalMapFilter: 0
    flipGreenChannel: 0
  isReadable: 0
  streamingMipmaps: 1
  streamingMipmapsPriority: 0
  vTOnly: 0
  ignoreMipmapLimit: 0
  grayScaleToAlpha: 0
  generateCubemap: 6
  cubemapConvolution: 0
  seamlessCubemap: 0
  textureFormat: 1
  maxTextureSize: {max_size}
  textureSettings:
    serializedVersion: 2
    filterMode: 2
    aniso: 4
    mipBias: 0
    wrapU: 0
    wrapV: 0
    wrapW: 0
  nPOTScale: 0
  lightmap: 0
  compressionQuality: 75
  spriteMode: 0
  spriteExtrude: 1
  spriteMeshType: 1
  alignment: 0
  spritePivot: {{x: 0.5, y: 0.5}}
  spritePixelsToUnits: 100
  spriteBorder: {{x: 0, y: 0, z: 0, w: 0}}
  spriteGenerateFallbackPhysicsShape: 1
  alphaUsage: {1 if packed else 0}
  alphaIsTransparency: 0
  textureType: {1 if normal else 0}
  textureShape: 1
  singleChannelComponent: 0
  flipbookRows: 1
  flipbookColumns: 1
  platformSettings:
  - serializedVersion: 3
    buildTarget: DefaultTexturePlatform
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 75
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 0
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  - serializedVersion: 3
    buildTarget: Android
    maxTextureSize: {max_size}
    resizeAlgorithm: 0
    textureFormat: -1
    textureCompression: 1
    compressionQuality: 75
    crunchedCompression: 0
    allowsAlphaSplitting: 0
    overridden: 1
    ignorePlatformSupport: 0
    androidETC2FallbackOverride: 0
    forceMaximumCompressionQuality_BC6H_BC7: 0
  userData: ''
  assetBundleName: ''
  assetBundleVariant: ''
"""


def fit_image(path: Path, limit: int, mode: str) -> Image.Image:
    image = Image.open(path).convert(mode)
    image.thumbnail((limit, limit), Image.Resampling.LANCZOS)
    return image


def write_text_with_meta(path: Path, text: str, meta_text: str) -> None:
    path.write_text(text, encoding="utf-8")
    Path(str(path) + ".meta").write_text(meta_text, encoding="utf-8")


def export_weapon(source_root: Path, repo_root: Path, weapon_id: str,
                  spec: dict, manifest: dict) -> dict:
    source_name = spec["source"]
    model_root = source_root / "models/weapons"
    doc = json.loads((model_root / f"{source_name}.gltf").read_text())
    blob = (model_root / f"{source_name}.bin").read_bytes()
    if doc["buffers"][0]["byteLength"] != len(blob):
        raise ValueError(f"{source_name}: buffer length mismatch")

    entry = manifest["weapons"][spec["manifest_key"]]
    if entry["license"] != "CC0-1.0":
        raise ValueError(f"{source_name}: source is not CC0")
    if int(entry["triangles"]["view"]) != spec["triangles"]:
        raise ValueError(f"{source_name}: manifest triangle count drift")
    if not math.isclose(float(entry["length_m"]), spec["length_m"],
                        rel_tol=0.0, abs_tol=0.0001):
        raise ValueError(f"{source_name}: physical length drift")

    out = repo_root / "Assets/Resources/AarisWeapons/PBR" / weapon_id
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)

    obj_path = out / f"{weapon_id}.obj"
    mtl_path = out / f"{weapon_id}.mtl"
    obj = [
        "# Aaris offline firearm; deterministic derivative of pinned CC0 glTF.",
        f"# Source {SOURCE_REPO}@{SOURCE_COMMIT} / {source_name}",
        f"mtllib {weapon_id}.mtl",
    ]
    v_base = vt_base = vn_base = 0
    triangles = 0
    used_materials = []

    for node in doc.get("nodes", []):
        if node.get("name") == "LOD":
            continue
        if any(key in node for key in ("matrix", "translation", "rotation", "scale")):
            raise ValueError(f"{source_name}: unexpected node transform")
        mesh_index = node.get("mesh")
        if mesh_index is None:
            continue
        mesh = doc["meshes"][mesh_index]
        if mesh.get("name") == "LOD":
            continue
        for primitive_index, primitive in enumerate(mesh.get("primitives", [])):
            if primitive.get("mode", 4) != 4:
                raise ValueError(f"{source_name}: non-triangle primitive")
            attrs = primitive["attributes"]
            positions = read_accessor(doc, blob, attrs["POSITION"])
            normals = read_accessor(doc, blob, attrs["NORMAL"])
            uvs = read_accessor(doc, blob, attrs["TEXCOORD_0"])
            indices = read_accessor(doc, blob, primitive["indices"])
            if not (len(positions) == len(normals) == len(uvs)):
                raise ValueError(f"{source_name}: attribute count mismatch")
            flat_indices = [int(value[0]) for value in indices]
            if len(flat_indices) % 3:
                raise ValueError(f"{source_name}: triangle index count drift")
            source_material = doc["materials"][primitive["material"]]["name"]
            if source_material not in spec["materials"]:
                raise ValueError(
                    f"{source_name}: unpinned material {source_material}")
            material = spec["materials"][source_material][0]
            if material not in used_materials:
                used_materials.append(material)

            obj.append(
                f"o {weapon_id}_{node.get('name', mesh.get('name', 'Mesh'))}_{primitive_index}")
            obj.append(f"usemtl {material}")
            for x, y, z in positions:
                obj.append(f"v {x:.7f} {y:.7f} {z:.7f}")
            for u, v in uvs:
                obj.append(f"vt {u:.7f} {1.0 - v:.7f}")
            for x, y, z in normals:
                obj.append(f"vn {x:.7f} {y:.7f} {z:.7f}")
            for i in range(0, len(flat_indices), 3):
                refs = []
                for idx in flat_indices[i:i + 3]:
                    refs.append(
                        f"{v_base + idx + 1}/{vt_base + idx + 1}/{vn_base + idx + 1}")
                obj.append("f " + " ".join(refs))
            triangles += len(flat_indices) // 3
            v_base += len(positions)
            vt_base += len(uvs)
            vn_base += len(normals)

    if triangles != spec["triangles"]:
        raise ValueError(
            f"{source_name}: exported {triangles} triangles, expected {spec['triangles']}")

    mtl = ["# Stable material names are used by runtime slot-aware URP binding."]
    texture_root = model_root / "textures"
    for source_material, material_spec in spec["materials"].items():
        material, albedo_name, normal_name, orm_name = material_spec
        if material not in used_materials:
            raise ValueError(f"{source_name}: material {material} was not rendered")

        base_out = out / f"{material}_Base_Color.png"
        normal_out = out / f"{material}_Normal.png"
        packed_out = out / f"{material}_MetallicSmoothness.png"

        base = fit_image(texture_root / albedo_name, 1024, "RGBA")
        base.save(base_out, optimize=True)
        Path(str(base_out) + ".meta").write_text(
            texture_meta(base_out, 1024), encoding="utf-8")

        normal = fit_image(texture_root / normal_name, 1024, "RGB")
        normal.save(normal_out, optimize=True)
        Path(str(normal_out) + ".meta").write_text(
            texture_meta(normal_out, 1024, normal=True), encoding="utf-8")

        orm = fit_image(texture_root / orm_name, 512, "RGB")
        _, roughness, metallic = orm.split()
        smoothness = roughness.point(lambda value: 255 - value)
        packed = Image.merge("RGBA", (metallic, metallic, metallic, smoothness))
        packed.save(packed_out, optimize=True)
        Path(str(packed_out) + ".meta").write_text(
            texture_meta(packed_out, 512, packed=True), encoding="utf-8")

        mtl.extend([
            "",
            f"newmtl {material}",
            "Ka 0 0 0",
            "Kd 1 1 1",
            "Ks 0.25 0.25 0.25",
            "Ns 64",
            f"map_Kd {base_out.name}",
        ])

    write_text_with_meta(obj_path, "\n".join(obj) + "\n", model_meta(obj_path))
    write_text_with_meta(mtl_path, "\n".join(mtl) + "\n", default_meta(mtl_path))

    return {
        "weapon": weapon_id,
        "source": source_name,
        "creator": entry["creator"],
        "source_page": entry["source_page"],
        "license": entry["license"],
        "triangles": triangles,
        "vertices": v_base,
        "length_m": entry["length_m"],
        "materials": used_materials,
    }


def write_provenance(repo_root: Path, results: list[dict]) -> None:
    target = repo_root / "Docs/WEAPON_PBR_BATTLE_SNIPER_SOURCE.md"
    lines = [
        "# Pinned CC0 PBR BattleRifle and Sniper source",
        "",
        "These offline firearm visuals are deterministic derivatives of:",
        SOURCE_REPO + "@" + SOURCE_COMMIT,
        "",
        "Every source file is verified with Git's canonical blob SHA-1.",
        "The converter excludes the source LOD node, exports only the full-view",
        "mesh, caps base/normal maps at 1024, and packs metallic/smoothness",
        "into a 512 RGBA map. The game performs no runtime asset download.",
        "",
    ]
    for result in results:
        lines.extend([
            "## " + result["weapon"],
            "",
            "- Creator: " + result["creator"],
            "- License: " + result["license"],
            "- Source: " + result["source_page"],
            "- Physical source length: " + str(result["length_m"]) + " m",
            "- Exported view triangles: " + str(result["triangles"]),
            "- Exported vertices: " + str(result["vertices"]),
            "- Runtime materials: " + ", ".join(result["materials"]),
            "",
        ])
    lines.extend(["## Pinned source Git blobs", ""])
    for relative, sha in SOURCE_BLOBS.items():
        lines.append("- " + sha + "  " + relative)
    lines.append("")
    target.write_text("\n".join(lines), encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo", type=Path, default=Path("."))
    parser.add_argument("--source", type=Path)
    parser.add_argument("--download", action="store_true")
    args = parser.parse_args()

    repo_root = args.repo.resolve()
    if args.download:
        with tempfile.TemporaryDirectory(prefix="aaris-pbr-weapons-") as tmp:
            source = Path(tmp)
            download_sources(source)
            verify_sources(source)
            manifest = json.loads((source / "manifest/weapons.json").read_text())
            results = [
                export_weapon(source, repo_root, weapon_id, spec, manifest)
                for weapon_id, spec in SPECS.items()
            ]
            write_provenance(repo_root, results)
    else:
        if args.source is None:
            parser.error("--source is required unless --download is used")
        source = args.source.resolve()
        verify_sources(source)
        manifest = json.loads((source / "manifest/weapons.json").read_text())
        results = [
            export_weapon(source, repo_root, weapon_id, spec, manifest)
            for weapon_id, spec in SPECS.items()
        ]
        write_provenance(repo_root, results)

    for result in results:
        print("AARIS_PBR_VENDOR", json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    main()
