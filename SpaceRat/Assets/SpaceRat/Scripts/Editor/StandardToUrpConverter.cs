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
            ApplyStandardToLit(src, mat, lit);
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Converts Standard materials of a vendor pack to URP Lit IN PLACE (like Unity's render pipeline
        /// converter). Reversible by re-importing the pack. Use when the pack's own prefabs/scenes must work.
        /// </summary>
        public static int ConvertInPlace(string folder)
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var particles = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { folder }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m || !m.shader) continue;
                string sh = m.shader.name;
                var snapshot = new Material(m);   // read the original values from a copy
                if (sh.StartsWith("Standard"))
                {
                    ApplyStandardToLit(snapshot, m, lit);
                }
                else if (sh.StartsWith("Nature/"))
                {
                    m.shader = lit;
                    m.shaderKeywords = new string[0];
                    if (snapshot.HasProperty("_MainTex")) m.SetTexture("_BaseMap", snapshot.GetTexture("_MainTex"));
                    if (snapshot.HasProperty("_Color")) m.SetColor("_BaseColor", snapshot.GetColor("_Color"));
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.4f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.renderQueue = (int)RenderQueue.AlphaTest;
                }
                else if (sh.StartsWith("Legacy Shaders/Particles") && particles)
                {
                    m.shader = particles;
                    m.SetTexture("_BaseMap", snapshot.HasProperty("_MainTex") ? snapshot.GetTexture("_MainTex") : null);
                    m.SetColor("_BaseColor", snapshot.HasProperty("_TintColor") ? snapshot.GetColor("_TintColor") : Color.white);
                    m.SetFloat("_Surface", 1f);
                    m.SetFloat("_Blend", 2f); // additive
                    m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    m.renderQueue = (int)RenderQueue.Transparent;
                }
                else { Object.DestroyImmediate(snapshot); continue; }
                Object.DestroyImmediate(snapshot);
                EditorUtility.SetDirty(m);
                n++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[SpaceRat] Converted {n} materials in place under {folder}.");
            return n;
        }

        [MenuItem("SpaceRat/Fix Sci-Fi Modular Pack Materials (URP, in place)")]
        public static void FixSciFiModularPack() => ConvertInPlace("Assets/Sci-Fi Styled Modular Pack");

        static void ApplyStandardToLit(Material src, Material mat, Shader lit)
        {
            mat.shader = lit;
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
