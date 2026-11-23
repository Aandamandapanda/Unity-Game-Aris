#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Aaris.MapReview.Editor
{
    // Original geometry, authored for this project; no third-party artwork.
    // Bakes ordinary persistent mesh/material/prefab assets, never runtime art.
    public static class AarisCompactPistolBake
    {
        private const string Folder = "Assets/Resources/AarisWeapons/Authored/Pistol";

        private static Material Finish(string name, Color color, float metal)
        {
            string path = Folder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(result, path);
            }
            result.name = name;
            result.SetColor("_BaseColor", color);
            result.SetFloat("_Metallic", metal);
            result.SetFloat("_Smoothness", .32f);
            result.enableInstancing = true;
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void Part(List<CombineInstance> parts, PrimitiveType shape,
            Vector3 position, Vector3 scale, Quaternion rotation)
        {
            var source = GameObject.CreatePrimitive(shape);
            parts.Add(new CombineInstance {
                mesh = source.GetComponent<MeshFilter>().sharedMesh,
                transform = Matrix4x4.TRS(position, rotation, scale)
            });
            Object.DestroyImmediate(source);
        }

        private static void Box(List<CombineInstance> parts, Vector3 position, Vector3 scale)
            => Part(parts, PrimitiveType.Cube, position, scale, Quaternion.identity);

        // Inspect the actual saved asset using Unity's URP camera, not a mockup.
        public static void Preview()
        {
            var asset = Resources.Load<GameObject>("AarisWeapons/Authored/Pistol/CompactPistol");
            if (asset == null) throw new InvalidOperationException("BAKE_PISTOL_FIRST");
            var root = Object.Instantiate(asset);
            root.transform.position = Vector3.zero;
            var light = new GameObject("Temporary pistol preview light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.48f, .50f, .55f);
            var camera = new GameObject("Temporary pistol preview camera").AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.12f, .14f, .17f);
            camera.nearClipPlane = .01f; camera.farClipPlane = 5f;
            camera.orthographic = true; camera.orthographicSize = .12f;
            var target = new RenderTexture(1024, 768, 24);
            camera.targetTexture = target;
            string output = Path.Combine(Path.GetTempPath(), "aaris-progunplay-qa");
            Directory.CreateDirectory(output);
            Vector3[] positions = {new Vector3(.4f, .20f, .35f),
                new Vector3(-.45f, .10f, .02f), new Vector3(.36f, .10f, -.28f)};
            for (int i = 0; i < positions.Length; i++)
            {
                camera.transform.position = positions[i];
                camera.transform.LookAt(new Vector3(0f, -.035f, .003f));
                camera.Render();
                RenderTexture.active = target;
                var image = new Texture2D(1024, 768, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1024, 768), 0, 0); image.Apply();
                File.WriteAllBytes(Path.Combine(output, "pistol-" + i + ".png"), image.EncodeToPNG());
                Object.DestroyImmediate(image);
            }
            RenderTexture.active = null; camera.targetTexture = null;
            target.Release(); Object.DestroyImmediate(target);
            Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(light.gameObject);
            Object.DestroyImmediate(root);
            Debug.Log("AARIS_PISTOL_PREVIEW_PASS views=3");
        }

        [MenuItem("Aaris/Weapons/Bake original compact pistol")]
        public static void Bake()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("BAKE_REQUIRES_EDIT_MODE");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var steel = new List<CombineInstance>();
            var polymer = new List<CombineInstance>();
            // 23.5cm long, +Z barrel. Frame, slide and textured silhouette.
            Box(steel, new Vector3(0f, .014f, 0f), new Vector3(.031f, .028f, .215f));
            Box(steel, new Vector3(0f, .028f, -.002f), new Vector3(.025f, .009f, .202f));
            Box(polymer, new Vector3(0f, -.009f, -.014f), new Vector3(.032f, .021f, .187f));
            Part(polymer, PrimitiveType.Cube, new Vector3(0f, -.061f, -.065f),
                new Vector3(.032f, .104f, .047f), Quaternion.Euler(-16f, 0f, 0f));
            Box(steel, new Vector3(0f, -.115f, -.050f), new Vector3(.035f, .010f, .051f));
            // Real open trigger guard, with separate trigger and magazine pad.
            Box(steel, new Vector3(0f, -.045f, -.001f), new Vector3(.011f, .006f, .055f));
            Box(steel, new Vector3(0f, -.031f, .025f), new Vector3(.011f, .032f, .006f));
            Part(steel, PrimitiveType.Cube, new Vector3(0f, -.029f, -.006f),
                new Vector3(.006f, .024f, .006f), Quaternion.Euler(18f, 0f, 0f));
            // Cylindrical barrel, recessed dark bore and iron sights.
            Part(steel, PrimitiveType.Cylinder, new Vector3(0f, .012f, .108f),
                new Vector3(.016f, .009f, .016f), Quaternion.Euler(90f, 0f, 0f));
            Part(polymer, PrimitiveType.Cylinder, new Vector3(0f, .012f, .1176f),
                new Vector3(.010f, .0003f, .010f), Quaternion.Euler(90f, 0f, 0f));
            Box(polymer, new Vector3(0f, .037f, .082f), new Vector3(.005f, .010f, .010f));
            Box(polymer, new Vector3(-.009f, .037f, -.082f), new Vector3(.006f, .010f, .014f));
            Box(polymer, new Vector3(.009f, .037f, -.082f), new Vector3(.006f, .010f, .014f));
            for (int i = 0; i < 6; i++)
            {
                float z = -.090f + i * .006f;
                Box(polymer, new Vector3(-.016f, .014f, z), new Vector3(.0015f, .020f, .002f));
                Box(polymer, new Vector3(.016f, .014f, z), new Vector3(.0015f, .020f, .002f));
            }
            var metalMesh = new Mesh(); metalMesh.CombineMeshes(steel.ToArray(), true);
            var gripMesh = new Mesh(); gripMesh.CombineMeshes(polymer.ToArray(), true);
            var combined = new Mesh { name = "CompactPistolMesh" };
            combined.CombineMeshes(new[] {
                new CombineInstance {mesh = metalMesh, transform = Matrix4x4.identity},
                new CombineInstance {mesh = gripMesh, transform = Matrix4x4.identity}
            }, false);
            combined.RecalculateBounds();
            string meshPath = Folder + "/CompactPistolMesh.asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (saved == null) { AssetDatabase.CreateAsset(combined, meshPath); saved = combined; }
            else { EditorUtility.CopySerialized(combined, saved); Object.DestroyImmediate(combined); }
            Object.DestroyImmediate(metalMesh); Object.DestroyImmediate(gripMesh);
            var root = new GameObject("CompactPistol");
            root.AddComponent<MeshFilter>().sharedMesh = saved;
            root.AddComponent<MeshRenderer>().sharedMaterials = new[] {
                Finish("Pistol Slide Metal", new Color(.15f, .17f, .18f), .72f),
                Finish("Pistol Grip Polymer", new Color(.025f, .033f, .036f), .03f)
            };
            PrefabUtility.SaveAsPrefabAsset(root, Folder + "/CompactPistol.prefab");
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("AARIS_PISTOL_BAKE_PASS triangles=" + saved.triangles.Length / 3);
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
#endif
