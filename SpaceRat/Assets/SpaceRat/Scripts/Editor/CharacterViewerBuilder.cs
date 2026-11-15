using System.Collections.Generic;
using System.IO;
using SpaceRat.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Builds Assets/SpaceRat/Scenes/CharacterViewer.unity: the converted humanoid characters on a
    /// glowing platform with a sci-fi backdrop (green nebula skybox, Starter Kit station + planet).
    /// Menu: SpaceRat > Build Character Viewer
    /// </summary>
    public static class CharacterViewerBuilder
    {
        const string ScenePath = "Assets/SpaceRat/Scenes/CharacterViewer.unity";
        const string CharacterPrefabs = "Assets/SpaceRat/Prefabs/Characters";
        const string SkyboxPath = "Assets/SpaceSkies Free/Skybox_2/Green_2K_Resoution.mat"; // (sic)
        const string KitPrefabs = "Assets/Mwester Studios/Simple Space Starter Kit/Prefabs";
        const string GlobalProfilePath = "Assets/SpaceRat/Settings/Scene01_GlobalVolume.asset";
        public const string PilotOutfit = "Man_03_URP"; // pants, boots, pullover, side bag
        const string MatDir = "Assets/SpaceRat/Art/Greybox";

        [MenuItem("SpaceRat/Build Character Viewer")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ---- Environment ---------------------------------------------------
            var sky = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.10f, 0.16f, 0.14f);
            RenderSettings.ambientEquatorColor = new Color(0.06f, 0.08f, 0.08f);
            RenderSettings.ambientGroundColor = new Color(0.02f, 0.02f, 0.03f);

            Light("KeyLight", LightType.Directional, new Color(1f, 0.86f, 0.72f), 1.6f, new Vector3(35, 150, 0), Vector3.zero, true);
            Light("RimLight_Cyan", LightType.Point, new Color(0.3f, 0.9f, 1f), 12f, Vector3.zero, new Vector3(-1.6f, 2.2f, -1.6f), false, 7f);
            Light("RimLight_Teal", LightType.Point, new Color(0.25f, 1f, 0.6f), 8f, Vector3.zero, new Vector3(1.8f, 1.4f, -1.4f), false, 6f);

            // Platform: dark metal disc with a glowing ring underneath.
            var metal = Mat("M_ViewerPlatform", "Universal Render Pipeline/Lit", new Color(0.12f, 0.13f, 0.15f), 0.8f, 0.7f);
            var glow = Mat("M_ViewerRing", "Universal Render Pipeline/Unlit", new Color(0.4f, 2.6f, 3.2f), 0, 0);
            Prim("Platform", PrimitiveType.Cylinder, new Vector3(0, -0.05f, 0), new Vector3(2.6f, 0.05f, 2.6f), metal);
            Prim("PlatformRing", PrimitiveType.Cylinder, new Vector3(0, -0.11f, 0), new Vector3(2.75f, 0.02f, 2.75f), glow);

            // Backdrop: station and a planet, placed behind the character (camera starts in front).
            PlaceBackdrop($"{KitPrefabs}/Station/Station.prefab", new Vector3(40f, 18f, -170f), 140f, new Vector3(0, 35, 10));
            PlaceBackdrop($"{KitPrefabs}/Planet and Star/Planet 1.prefab", new Vector3(-260f, 90f, -520f), 260f, Vector3.zero);

            // ---- Characters ----------------------------------------------------
            var controller = AssetDatabase.IsValidFolder("Assets/Kevin Iglesias") ? CharacterAnimatorBuilder.Build() : null;
            var variants = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { CharacterPrefabs }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!Path.GetFileName(path).StartsWith("Man_")) continue;
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path), scene);
                inst.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var anim = inst.GetComponentInChildren<Animator>();
                if (anim && anim.avatar && anim.avatar.isHuman)
                {
                    if (controller)
                    {
                        anim.runtimeAnimatorController = controller;
                        anim.applyRootMotion = false;
                    }
                    else anim.gameObject.AddComponent<RelaxedPose>();
                }
                else
                    Debug.LogWarning($"[SpaceRat] {inst.name} has no humanoid Animator — it will stay in its bind pose.");
                variants.Add(inst);
            }
            if (variants.Count == 0)
                Debug.LogError("[SpaceRat] No character prefabs found — run SpaceRat > Convert Adventure Character to URP first.");

            // ---- Camera, post, viewer --------------------------------------------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.fieldOfView = 45f;
            cam.farClipPlane = 5000f;
            camGo.AddComponent<AudioListener>();
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(GlobalProfilePath);
            if (profile)
            {
                var vol = new GameObject("GlobalVolume").AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            var viewer = new GameObject("CharacterViewer").AddComponent<CharacterViewer>();
            var so = new SerializedObject(viewer);
            so.FindProperty("cam").objectReferenceValue = cam;
            var list = so.FindProperty("variants");
            list.arraySize = variants.Count;
            for (int i = 0; i < variants.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = variants[i];
            var statesProp = so.FindProperty("animStates");
            var names = controller ? CharacterAnimatorBuilder.StateNames : new string[0];
            statesProp.arraySize = names.Length;
            for (int i = 0; i < names.Length; i++) statesProp.GetArrayElementAtIndex(i).stringValue = names[i];
            so.FindProperty("startIndex").intValue = Mathf.Max(0, variants.FindIndex(v => v.name.StartsWith(PilotOutfit)));
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) scenes.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SpaceRat] Built {ScenePath} with {variants.Count} character variants.");
        }

        static void PlaceBackdrop(string prefabPath, Vector3 pos, float targetSize, Vector3 euler)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab) { Debug.LogWarning($"[SpaceRat] Backdrop {prefabPath} not found — skipped."); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(euler));
            var b = new Bounds(); bool first = true;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (size > 0.001f) go.transform.localScale *= targetSize / size;
            go.transform.position = pos;
            // Disable any VFX Graph children (package not installed).
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains("VFX")) t.gameObject.SetActive(false);
        }

        static void Light(string name, LightType type, Color color, float intensity, Vector3 euler, Vector3 pos, bool shadows, float range = 10f)
        {
            var go = new GameObject(name);
            var l = go.AddComponent<Light>();
            l.type = type;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(euler));
        }

        static void Prim(string name, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.position = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        static Material Mat(string name, string shader, Color color, float metallic, float smoothness)
        {
            string path = $"{MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = !mat;
            if (isNew) mat = new Material(Shader.Find(shader));
            mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
