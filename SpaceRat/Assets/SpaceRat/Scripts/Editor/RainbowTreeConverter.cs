using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Realistic Tree 9 "Rainbow Tree" (Pixel Games, free) is built with the legacy Tree Creator, whose shaders
    /// URP can't render. This makes URP prefabs (SpaceRat > Convert Rainbow Trees to URP):
    /// - the baked mesh stored inside each vendor prefab (referenced, not copied: the vendor folder is git-ignored)
    /// - URP Lit materials of our own: bark (opaque), leaves (alpha clip, two-sided)
    /// - no Tree component, a trunk collider; prefabs in Assets/SpaceRat/Prefabs/Environment/RainbowTree_N_URP
    /// Then adds them as tree prototypes to the Scene 3 terrain so they can be painted (Terrain > Paint Trees).
    /// </summary>
    public static class RainbowTreeConverter
    {
        const string Vendor = "Assets/Tree9/";
        const string ArtDir = "Assets/SpaceRat/Art/Trees";
        const string PrefabDir = "Assets/SpaceRat/Prefabs/Environment";

        [MenuItem("SpaceRat/Convert Rainbow Trees to URP")]
        public static void Convert()
        {
            if (!AssetDatabase.IsValidFolder(Vendor.TrimEnd('/'))) { Debug.LogWarning("[SpaceRat] Realistic Tree 9 not imported."); return; }
            Scene01GreyboxBuilder.EnsureFolder(ArtDir);
            Scene01GreyboxBuilder.EnsureFolder(PrefabDir);

            var made = new System.Collections.Generic.List<GameObject>();
            foreach (var n in new[] { "Tree9_2", "Tree9_3", "Tree9_4", "Tree9_5" })
            {
                string src = Vendor + n + ".prefab";
                var subs = AssetDatabase.LoadAllAssetsAtPath(src);
                var mesh = subs.OfType<Mesh>().FirstOrDefault();
                var bark = subs.OfType<Material>().FirstOrDefault(m => m.name.Contains("Bark"));
                var leaf = subs.OfType<Material>().FirstOrDefault(m => m.name.Contains("Leaf"));
                if (!mesh) { Debug.LogWarning($"[SpaceRat] {n}: no baked mesh found."); continue; }

                var go = new GameObject($"RainbowTree_{n.Substring(6)}_URP");
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                // Tree Creator meshes: submesh 0 = bark, submesh 1 = leaves.
                var mats = new Material[Mathf.Max(1, mesh.subMeshCount)];
                mats[0] = Lit($"{ArtDir}/{n}_Bark_URP.mat", bark, false);
                if (mats.Length > 1) mats[1] = Lit($"{ArtDir}/{n}_Leaves_URP.mat", leaf, true);
                for (int i = 2; i < mats.Length; i++) mats[i] = mats[1];
                mr.sharedMaterials = mats;

                // Trunk collider (a thin capsule at the base) so you can't walk through it.
                var col = go.AddComponent<CapsuleCollider>();
                col.radius = Mathf.Max(0.25f, mesh.bounds.size.x * 0.035f);
                col.height = mesh.bounds.size.y * 0.45f;
                col.center = new Vector3(0f, col.height * 0.5f, 0f);

                // LOD Group: the terrain treats it as a modern mesh tree (no legacy billboard shader needed),
                // drawing the full mesh up close and culling it when it gets tiny on screen.
                var lod = go.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(0.015f, new Renderer[] { mr }) });
                lod.RecalculateBounds();

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{go.name}.prefab");
                Object.DestroyImmediate(go);
                made.Add(prefab);
            }
            AssetDatabase.SaveAssets();
            AddToTerrain(made);
            Debug.Log($"[SpaceRat] Rainbow trees converted: {made.Count} prefabs in {PrefabDir}");
        }

        static Material Lit(string path, Material src, bool leaves)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            m.shader = shader;
            var tex = src ? src.mainTexture : null;
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", src && src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
            m.SetFloat("_Smoothness", leaves ? 0.25f : 0.1f);
            m.SetFloat("_Metallic", 0f);
            if (leaves)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", src && src.HasProperty("_Cutoff") ? src.GetFloat("_Cutoff") : 0.3f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);   // leaves visible from both sides
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Adds the prefabs as tree prototypes on every terrain in the open scene (once).</summary>
        static void AddToTerrain(System.Collections.Generic.List<GameObject> prefabs)
        {
            foreach (var terrain in Object.FindObjectsByType<Terrain>())
            {
                var data = terrain.terrainData;
                var list = data.treePrototypes.ToList();
                foreach (var p in prefabs)
                    if (p && !list.Any(t => t.prefab == p)) list.Add(new TreePrototype { prefab = p, bendFactor = 0f });
                data.treePrototypes = list.ToArray();
                data.RefreshPrototypes();
                EditorUtility.SetDirty(data);
                Debug.Log($"[SpaceRat] {terrain.name}: {list.Count} tree prototypes ready to paint.");
            }
        }
    }
}
