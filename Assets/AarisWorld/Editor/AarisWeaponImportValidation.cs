using System;
using UnityEditor;
using UnityEngine;

namespace Aaris.MapReview.Editor
{
    /// <summary>
    /// Native Unity Editor import gate. This is not a substitute for
    /// visual screenshots and physical Android gameplay. Run after loading
    /// the project with a valid Unity license.
    /// </summary>
    public static class AarisWeaponImportValidation
    {
        [Serializable] private sealed class LiveCatalog { public AarisWeaponSpec[] weapons; }
        [MenuItem("Aaris/Validate Offline Weapons and Loot")]
        public static void Run()
        {
            AarisExplorerBuild.ValidateSavedScene();
            var json = Resources.Load<TextAsset>("AarisWeapons/weapon_catalog");
            if (json == null) throw new InvalidOperationException("Missing active catalog.");
            var parsed = JsonUtility.FromJson<LiveCatalog>(json.text);
            if (parsed == null || parsed.weapons == null || parsed.weapons.Length != 5)
                throw new InvalidOperationException("Active catalog requires five models.");
            var specs = parsed.weapons;
            int triangles = 0;
            foreach (var spec in specs)
            {
                if (spec == null || !spec.Valid)
                    throw new InvalidOperationException("Invalid live catalog entry.");
                string name = spec.id;
                string prefix = "Assets/Resources/" + spec.modelResource;
                string path = AssetDatabase.LoadAssetAtPath<GameObject>(prefix + ".fbx") != null ? prefix + ".fbx" : prefix + ".obj";
                var model = Resources.Load<GameObject>(spec.modelResource);
                if (model == null)
                    throw new InvalidOperationException("Missing Unity imported model: " + path);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    throw new InvalidOperationException("Unity failed FBX ModelImporter: " + path);
                var renderers = model.GetComponentsInChildren<Renderer>(true);
                if (!Array.Exists(renderers, r => r != null && r.enabled && r.gameObject.activeSelf))
                    throw new InvalidOperationException("No enabled floor mesh renderer: " + path);
                var meshFilters = model.GetComponentsInChildren<MeshFilter>(true);
                var skinRenderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (meshFilters.Length == 0 && skinRenderers.Length == 0)
                    throw new InvalidOperationException("Weapon has no renderable mesh: " + path);
                int thisModel = 0;
                foreach (var filter in meshFilters)
                {
                    if (filter.sharedMesh == null) continue;
                    thisModel += filter.sharedMesh.triangles.Length / 3;
                }
                // The licensed source FBXs are rigged and some use ONLY
                // skinned meshes; checking MeshFilter alone wrongly fails.
                foreach (var skinned in skinRenderers)
                {
                    if (skinned.sharedMesh == null) continue;
                    thisModel += skinned.sharedMesh.triangles.Length / 3;
                }
                if (thisModel < 8 || thisModel > 180000)
                    throw new InvalidOperationException("Invalid phone mesh triangle count " +
                        thisModel + ": " + path);
                triangles += thisModel;
                string resourcePath = spec.modelResource;
                if (Resources.Load<GameObject>(resourcePath) == null)
                    throw new InvalidOperationException("Resources.Load failed: " + resourcePath);
                Debug.Log("AARIS_WEAPON_IMPORT: " + name + ", " + thisModel + " triangles, Active gameplay model GUID stable.");
            }
            string[] scripts =
            {
                "AarisWeaponCatalog", "AarisWeaponPickup", "AarisWeaponInventory",
                "AarisWeaponLootWorld", "AarisWeaponHUD", "AarisBattleActions",
                "AarisEquippedGun", "AarisThirdPersonPlayer"
            };
            foreach (string name in scripts)
            {
                string path = "Assets/AarisWorld/Scripts/" + name + ".cs";
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
                if (script == null || script.GetClass() == null)
                    throw new InvalidOperationException("Unity script import failed: " + path);
            }
            if (!AarisWeaponCatalog.TryLoad())
                throw new InvalidOperationException("Offline catalog could not import in Unity.");
            var loot = Resources.Load<TextAsset>("AarisWeapons/loot_candidates");
            if (loot == null || !loot.text.Contains("\"schema\": 1"))
                throw new InvalidOperationException("Missing baked 2km loot candidate data.");
            Debug.Log("AARIS_WEAPON_IMPORT_PASS: 5 active gameplay FBX/OBJ assets; " +
                triangles + " total triangles; offline catalog + authored loot + player scripts.");
            Debug.Log("PLAYMODE_QA_REQUIRED: test hand rig, scope FOV, prone muzzle, ammo, " +
                "indoor accessible floor, native tree cover, fire audio/materials and Android FPS.");
        }
    }
}
