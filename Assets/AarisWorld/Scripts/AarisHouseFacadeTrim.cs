using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aaris.MapReview
{
    /// <summary>
    /// Single-draw-call exterior detail for the 16 authored expansion homes.
    /// No window geometry or colliders are touched; doorways remain real holes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AarisHouseFacadeTrim : MonoBehaviour
    {
        private static Material sharedStucco;
        private static int materialUsers;
        private Mesh ownedMesh;

        public static void Install(GameObject house, float width, float depth, float height)
        {
            if (house == null || house.GetComponentInChildren<AarisHouseFacadeTrim>() != null ||
                float.IsNaN(width) || float.IsInfinity(width) ||
                float.IsNaN(depth) || float.IsInfinity(depth) ||
                float.IsNaN(height) || float.IsInfinity(height) ||
                width < 7.5f || depth < 8f || height < 4.5f)
                return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) return;
            if (sharedStucco == null)
            {
                sharedStucco = new Material(shader)
                {
                    name = "Aaris Architectural Limestone Fascia",
                    enableInstancing = true
                };
                sharedStucco.SetColor("_BaseColor", new Color(.73f,.70f,.63f));
                sharedStucco.SetFloat("_Metallic", .015f);
                sharedStucco.SetFloat("_Smoothness", .23f);
                // A single offline-baked, tileable 128px limestone texture:
                // no on-device image generation and no per-house duplication.
                Texture2D stone = Resources.Load<Texture2D>(
                    "AarisBuildings/ArchitecturalLimestone");
                if (stone != null)
                    sharedStucco.SetTexture("_BaseMap", stone);
            }

            Mesh mesh = BuildFacade(width, depth, height);
            if (mesh == null) return;
            var detail = new GameObject("Aaris Architectural Roof and Corner Trim");
            detail.layer = house.layer;
            detail.transform.SetParent(house.transform, false);
            var filter = detail.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = detail.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = sharedStucco;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            // Building facades are only noticeable at close range. Unity's
            // native one-step LOD automatically culls the extra draw at a
            // small screen size; no Update, allocations, or per-house scans.
            var lod = detail.AddComponent<LODGroup>();
            lod.fadeMode = LODFadeMode.None;
            lod.animateCrossFading = false;
            lod.SetLODs(new[] { new LOD(.08f, new Renderer[] { renderer }) });
            lod.RecalculateBounds();
            var lifetime = detail.AddComponent<AarisHouseFacadeTrim>();
            lifetime.ownedMesh = mesh;
            materialUsers++;
        }

        private void OnDestroy()
        {
            if (ownedMesh == null) return;
            Destroy(ownedMesh);
            ownedMesh = null;
            materialUsers = Mathf.Max(0, materialUsers - 1);
            if (materialUsers == 0 && sharedStucco != null)
            {
                Destroy(sharedStucco);
                sharedStucco = null;
            }
        }

        private static Mesh BuildFacade(float width, float depth, float height)
        {
            // Thirteen small solid details, combined into a SINGLE MeshRenderer:
            // 4 roof fascia, 5 low plinth, 4 slim corner pilasters.
            // A gap in the front plinth leaves the real entrance unobstructed.
            var vertices = new List<Vector3>(13 * 24);
            var normals = new List<Vector3>(13 * 24);
            var uvs = new List<Vector2>(13 * 24);
            var triangles = new List<int>(13 * 36);
            float x = width * .5f, z = depth * .5f;
            float roofY = height - .31f;
            AddBox(vertices, normals, uvs, triangles,
                new Vector3(0f, roofY, -z-.11f), new Vector3(width+.34f,.20f,.23f));
            AddBox(vertices, normals, uvs, triangles,
                new Vector3(0f, roofY, z+.11f), new Vector3(width+.34f,.20f,.23f));
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(vertices,normals,uvs,triangles,
                    new Vector3(side*(x+.11f),roofY,0f),
                    new Vector3(.23f,.20f,depth+.34f));
            }
            float entryGap = 1.92f;
            float wing = (width-entryGap) * .5f;
            for (int side = -1; side <= 1; side += 2)
            {
                AddBox(vertices,normals,uvs,triangles,
                    new Vector3(side*(entryGap+wing)*.5f,.15f,-z-.065f),
                    new Vector3(wing,.25f,.14f));
            }
            AddBox(vertices,normals,uvs,triangles,
                new Vector3(0f,.15f,z+.065f),new Vector3(width+.12f,.25f,.14f));
            for (int side = -1; side <= 1; side += 2)
                AddBox(vertices,normals,uvs,triangles,
                    new Vector3(side*(x+.065f),.15f,0f),
                    new Vector3(.14f,.25f,depth+.12f));
            for (int sideX = -1; sideX <= 1; sideX += 2)
                for (int sideZ = -1; sideZ <= 1; sideZ += 2)
                    AddBox(vertices,normals,uvs,triangles,
                        new Vector3(sideX*x, height*.50f,sideZ*z),
                        new Vector3(.19f,height-.60f,.19f));

            var mesh = new Mesh { name = "Aaris House Architectural Trim LOD0" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0,uvs);
            mesh.SetTriangles(triangles,0);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static void AddBox(List<Vector3> v, List<Vector3> n,
            List<Vector2> uv, List<int> tris, Vector3 center, Vector3 size)
        {
            Vector3 e = size * .5f;
            float x = e.x, y = e.y, z = e.z;
            // Outward-facing, hard-edged normals; smooth cube corners make
            // architectural stone look rounded and toy-like at oblique angles.
            AddFace(v,n,uv,tris,center,
                new Vector3(-x,-y,-z),new Vector3(-x,y,-z),
                new Vector3(x,y,-z),new Vector3(x,-y,-z));
            AddFace(v,n,uv,tris,center,
                new Vector3(x,-y,z),new Vector3(x,y,z),
                new Vector3(-x,y,z),new Vector3(-x,-y,z));
            AddFace(v,n,uv,tris,center,
                new Vector3(x,-y,-z),new Vector3(x,y,-z),
                new Vector3(x,y,z),new Vector3(x,-y,z));
            AddFace(v,n,uv,tris,center,
                new Vector3(-x,-y,z),new Vector3(-x,y,z),
                new Vector3(-x,y,-z),new Vector3(-x,-y,-z));
            AddFace(v,n,uv,tris,center,
                new Vector3(-x,y,-z),new Vector3(-x,y,z),
                new Vector3(x,y,z),new Vector3(x,y,-z));
            AddFace(v,n,uv,tris,center,
                new Vector3(-x,-y,z),new Vector3(-x,-y,-z),
                new Vector3(x,-y,-z),new Vector3(x,-y,z));
        }

        private static void AddFace(List<Vector3> v, List<Vector3> n,
            List<Vector2> uv, List<int> tris, Vector3 c,
            Vector3 a, Vector3 b, Vector3 d, Vector3 e)
        {
            int first = v.Count;
            Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
            v.Add(a+c); v.Add(b+c); v.Add(d+c); v.Add(e+c);
            for (int i = 0; i < 4; i++) n.Add(normal);
            // Real-world UV scale keeps a ten-metre fascia from stretching
            // the same 128px texture across its entire horizontal span.
            const float MetresPerTile = .85f;
            float along = Vector3.Distance(b, d) / MetresPerTile;
            float across = Vector3.Distance(a, b) / MetresPerTile;
            uv.Add(Vector2.zero); uv.Add(new Vector2(0f, across));
            uv.Add(new Vector2(along, across));
            uv.Add(new Vector2(along, 0f));
            tris.Add(first); tris.Add(first+1); tris.Add(first+2);
            tris.Add(first); tris.Add(first+2); tris.Add(first+3);
        }
    }
}
