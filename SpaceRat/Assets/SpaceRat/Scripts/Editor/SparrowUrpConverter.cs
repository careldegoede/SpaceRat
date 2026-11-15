using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Makes URP copies of the Sparrow Fighter (Asset Store) materials and prefabs without touching
    /// the vendor folder. The vendor uses the Built-in "Autodesk Interactive" shader with separate
    /// metallic and roughness maps; URP Lit wants metallic (R) + smoothness (A) packed into one map.
    /// Menu: SpaceRat > Convert Sparrow Fighter to URP
    /// </summary>
    public static class SparrowUrpConverter
    {
        const string Src = "Assets/Sparrow_Fighter";
        const string Dst = "Assets/SpaceRat/Art/Sparrow";
        const string PrefabDst = "Assets/SpaceRat/Prefabs/Ship";

        [MenuItem("SpaceRat/Convert Sparrow Fighter to URP")]
        public static void Convert()
        {
            if (!AssetDatabase.IsValidFolder(Src))
            {
                Debug.LogError($"[SpaceRat] {Src} not found — import Sparrow Fighter first.");
                return;
            }
            EnsureFolder(Dst);
            EnsureFolder(PrefabDst);

            var packedTex = BuildPackedMetallicSmoothness();
            var map = BuildMaterials(packedTex);
            BuildPrefabs(map);
            AssetDatabase.SaveAssets();
            Debug.Log("[SpaceRat] Sparrow Fighter URP conversion complete.");
        }

        static Texture2D BuildPackedMetallicSmoothness()
        {
            var metal = AssetDatabase.LoadAssetAtPath<Texture2D>(Src + "/Textures/Sparrow_metallic.tif");
            var rough = AssetDatabase.LoadAssetAtPath<Texture2D>(Src + "/Textures/Sparrow_roughness.tif");
            int w = metal.width, h = metal.height;
            var m = ReadViaGpu(metal, w, h);
            var r = ReadViaGpu(rough, w, h);
            var packed = new Color32[m.Length];
            for (int i = 0; i < m.Length; i++)
            {
                byte mv = m[i].r;
                packed[i] = new Color32(mv, mv, mv, (byte)(255 - r[i].r));
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            tex.SetPixels32(packed);
            tex.Apply();
            string path = Dst + "/Sparrow_MetallicSmoothness.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.sRGBTexture = false;
            ti.alphaSource = TextureImporterAlphaSource.FromInput;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Dictionary<Material, Material> BuildMaterials(Texture2D packedTex)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var map = new Dictionary<Material, Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { Src }))
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                string path = $"{Dst}/{src.name}_URP.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool isNew = !mat;
                if (isNew) mat = new Material(lit);
                else mat.shader = lit;

                mat.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_MetallicGlossMap", packedTex);
                mat.SetFloat("_Metallic", 1f);
                mat.SetFloat("_Smoothness", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_BumpMap", src.GetTexture("_BumpMap"));
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_OcclusionMap", src.GetTexture("_OcclusionMap"));
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
                mat.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                mat.SetColor("_EmissionColor", Color.white * 2f);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                if (isNew) AssetDatabase.CreateAsset(mat, path);
                else EditorUtility.SetDirty(mat);
                map[src] = mat;
            }
            return map;
        }

        static void BuildPrefabs(Dictionary<Material, Material> map)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { Src }))
            {
                var srcPath = AssetDatabase.GUIDToAssetPath(guid);
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(srcPath));
                foreach (var rend in inst.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = rend.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                        if (mats[i] && map.TryGetValue(mats[i], out var urp)) mats[i] = urp;
                    rend.sharedMaterials = mats;
                }
                string name = Path.GetFileNameWithoutExtension(srcPath).Replace(" Variant", "") + "_URP.prefab";
                PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabDst}/{name}");
                Object.DestroyImmediate(inst);
            }
        }

        static Color32[] ReadViaGpu(Texture2D src, int w, int h)
        {
            // Blit + ReadPixels avoids having to flip the vendor texture's Read/Write import flag.
            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            t.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = t.GetPixels32();
            Object.DestroyImmediate(t);
            return px;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
