using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Makes URP Lit copies of Built-in "Standard" materials from a vendor folder, plus prefab
    /// variants that use them — the vendor folder itself is never modified.
    /// Menu: SpaceRat > Convert Adventure Character to URP
    /// </summary>
    public static class StandardToUrpConverter
    {
        [MenuItem("SpaceRat/Convert Adventure Character to URP")]
        public static void ConvertAdventureCharacter() =>
            Convert("Assets/Adventure_Character",
                    "Assets/SpaceRat/Art/AdventureCharacter",
                    "Assets/SpaceRat/Prefabs/Characters");

        public static void Convert(string srcFolder, string matDst, string prefabDst)
        {
            if (!AssetDatabase.IsValidFolder(srcFolder))
            {
                Debug.LogError($"[SpaceRat] {srcFolder} not found — import the pack first.");
                return;
            }
            EnsureFolder(matDst);
            EnsureFolder(prefabDst);

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var map = new Dictionary<Material, Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { srcFolder }))
            {
                var src = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (!src || !src.shader || !src.shader.name.StartsWith("Standard")) continue;
                map[src] = ConvertMaterial(src, lit, $"{matDst}/{src.name}_URP.mat");
            }

            int prefabs = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { srcFolder }))
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
                PrefabUtility.SaveAsPrefabAsset(inst, $"{prefabDst}/{Path.GetFileNameWithoutExtension(srcPath)}_URP.prefab");
                Object.DestroyImmediate(inst);
                prefabs++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceRat] Converted {map.Count} materials and {prefabs} prefabs from {srcFolder} to URP.");
        }

        static Material ConvertMaterial(Material src, Shader lit, string path)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = !mat;
            if (isNew) mat = new Material(lit);
            else mat.shader = lit;
            mat.shaderKeywords = new string[0];

            mat.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
            mat.SetTextureScale("_BaseMap", src.GetTextureScale("_MainTex"));
            mat.SetColor("_BaseColor", src.GetColor("_Color"));

            var metal = src.GetTexture("_MetallicGlossMap");
            mat.SetTexture("_MetallicGlossMap", metal);
            mat.SetFloat("_Metallic", src.GetFloat("_Metallic"));
            // Standard and URP both pack smoothness in the metallic map's alpha (or albedo alpha).
            mat.SetFloat("_Smoothness", metal ? src.GetFloat("_GlossMapScale") : src.GetFloat("_Glossiness"));
            mat.SetFloat("_SmoothnessTextureChannel", src.GetFloat("_SmoothnessTextureChannel"));
            if (metal) mat.EnableKeyword("_METALLICSPECGLOSSMAP");

            var normal = src.GetTexture("_BumpMap");
            mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_BumpScale", src.GetFloat("_BumpScale"));
            if (normal) mat.EnableKeyword("_NORMALMAP");

            var ao = src.GetTexture("_OcclusionMap");
            mat.SetTexture("_OcclusionMap", ao);
            mat.SetFloat("_OcclusionStrength", src.GetFloat("_OcclusionStrength"));
            if (ao) mat.EnableKeyword("_OCCLUSIONMAP");

            if (src.IsKeywordEnabled("_EMISSION"))
            {
                mat.SetTexture("_EmissionMap", src.GetTexture("_EmissionMap"));
                mat.SetColor("_EmissionColor", src.GetColor("_EmissionColor"));
                mat.EnableKeyword("_EMISSION");
            }

            // Standard _Mode: 0 Opaque, 1 Cutout, 2 Fade, 3 Transparent
            int mode = Mathf.RoundToInt(src.GetFloat("_Mode"));
            if (mode == 1)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", src.GetFloat("_Cutoff"));
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else if (mode >= 2)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)RenderQueue.Transparent;
            }
            else
            {
                mat.renderQueue = -1;
            }

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
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
