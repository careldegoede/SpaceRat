using System.Collections.Generic;
using System.IO;
using SpaceRat.Dialogue;
using SpaceRat.Environment;
using SpaceRat.Flight;
using SpaceRat.Scene01;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Rebuilds Scene01_Awakening as a greybox from scratch (menu: SpaceRat > Build Scene01 Greybox).
    /// Materials and volume profiles are regenerated; dialogue assets are only created if missing,
    /// so edits to the dialogue text are preserved.
    /// </summary>
    public static class Scene01GreyboxBuilder
    {
        const string Root = "Assets/SpaceRat";
        const string ScenePath = Root + "/Scenes/Scene01_Awakening.unity";
        const string MatDir = Root + "/Art/Greybox";
        const string SettingsDir = Root + "/Settings";
        const string DialogueDir = Root + "/ScriptableObjects/Dialogue";
        const string HeroShipPath = Root + "/Prefabs/Ship/Sparrow_blue_URP.prefab";
        const string NoisePath ="Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";

        static readonly Vector3 ShipStart = new(260f, 70f, -2600f);
        static readonly Vector3 ShipAim = new(80f, 20f, 0f);
        const float SunDiameter = 600f;

        [MenuItem("SpaceRat/Build Scene01 Greybox")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            foreach (var d in new[] { Root + "/Scenes", MatDir, SettingsDir, DialogueDir })
                EnsureFolder(d);

            // Create the scene first: NewScene unloads unused assets, which would
            // invalidate any asset references loaded before it.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var mats = BuildMaterials();
            var dialogue = EnsureDialogue();
            var (globalProfile, wakeProfile) = BuildProfiles();

            // ---- Environment -------------------------------------------------
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.05f);
            RenderSettings.fog = false;

            var sun = Primitive("Sun", PrimitiveType.Sphere, null, Vector3.zero, Vector3.one * SunDiameter, mats["Sun"]);

            var sunLightGo = new GameObject("SunLight");
            var sunLight = sunLightGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.82f, 0.6f);
            sunLight.intensity = 2.2f;
            sunLight.shadows = LightShadows.Soft;
            sunLightGo.transform.rotation = Quaternion.LookRotation((ShipStart - Vector3.zero).normalized);

            var stars = new GameObject("Starfield", typeof(MeshFilter), typeof(MeshRenderer), typeof(Starfield));
            var starRenderer = stars.GetComponent<MeshRenderer>();
            starRenderer.sharedMaterial = mats["Stars"];
            starRenderer.shadowCastingMode = ShadowCastingMode.Off;
            starRenderer.receiveShadows = false;

            // ---- Ship --------------------------------------------------------
            var ship = new GameObject("Ship");
            ship.transform.SetPositionAndRotation(ShipStart, Quaternion.LookRotation((ShipAim - ShipStart).normalized, Vector3.up));
            var body = new GameObject("Body").transform;
            body.SetParent(ship.transform, false);

            // Exterior: blue Sparrow (URP copy) if the Asset Store pack is imported, else greybox primitives.
            GameObject exterior;
            var heroShip = AssetDatabase.LoadAssetAtPath<GameObject>(HeroShipPath);
            if (heroShip)
            {
                exterior = (GameObject)PrefabUtility.InstantiatePrefab(heroShip, scene);
                exterior.name = "Exterior_SparrowBlue";
                exterior.transform.SetParent(body, false);
            }
            else
            {
                Debug.LogWarning($"[SpaceRat] {HeroShipPath} not found — using greybox ship. Import Sparrow Fighter and run SpaceRat > Convert Sparrow Fighter to URP.");
                exterior = new GameObject("Exterior_Greybox");
                var ex = exterior.transform;
                ex.SetParent(body, false);
                Primitive("Hull", PrimitiveType.Capsule, ex, Vector3.zero, new Vector3(3.6f, 6f, 3.2f), mats["Hull"], new Vector3(90, 0, 0));
                Primitive("Canopy", PrimitiveType.Sphere, ex, new Vector3(0, 1.0f, 3.0f), new Vector3(2.2f, 1.6f, 3.6f), mats["Glass"]);
                Primitive("Wings", PrimitiveType.Cube, ex, new Vector3(0, -0.4f, -1.5f), new Vector3(14f, 0.25f, 3.5f), mats["Hull"]);
                Primitive("Fin", PrimitiveType.Cube, ex, new Vector3(0, 1.9f, -4.4f), new Vector3(0.25f, 3f, 2.5f), mats["Hull"]);
                foreach (float x in new[] { -2.4f, 2.4f })
                {
                    Primitive("Engine", PrimitiveType.Cylinder, ex, new Vector3(x, -0.4f, -5.2f), new Vector3(1.3f, 1.4f, 1.3f), mats["Trim"], new Vector3(90, 0, 0));
                    Primitive("EngineGlow", PrimitiveType.Sphere, ex, new Vector3(x, -0.4f, -6.6f), Vector3.one * 1.1f, mats["EngineGlow"]);
                }
            }
            Primitive("Seat", PrimitiveType.Cube, body, new Vector3(0, -0.6f, 2.6f), new Vector3(1.1f, 0.25f, 1.1f), mats["Trim"]);
            Primitive("SeatBack", PrimitiveType.Cube, body, new Vector3(0, 0.1f, 2.05f), new Vector3(1.1f, 1.4f, 0.2f), mats["Trim"]);
            var pilot = Primitive("Pilot", PrimitiveType.Capsule, body, new Vector3(0, -0.05f, 2.55f), new Vector3(0.7f, 0.6f, 0.6f), mats["Pilot"]);
            Primitive("Head", PrimitiveType.Sphere, pilot.transform, new Vector3(0, 1.2f, 0.05f), new Vector3(0.75f, 0.85f, 0.85f), mats["Pilot"]);

            var alarmGo = new GameObject("AlarmLight");
            alarmGo.transform.SetParent(body, false);
            alarmGo.transform.localPosition = new Vector3(0, 1.1f, 3.4f);
            var alarmLight = alarmGo.AddComponent<Light>();
            alarmLight.type = LightType.Point;
            alarmLight.color = new Color(1f, 0.08f, 0.05f);
            alarmLight.range = 18f;
            alarmLight.intensity = 0f;

            var alarmAudio = ship.AddComponent<AudioSource>();

            var motion = ship.AddComponent<ShipCinematicMotion>();
            SetRefs(motion, ("sun", sun.transform), ("tumbleBody", body));

            // ---- Camera + brain ----------------------------------------------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 20000f;
            camGo.AddComponent<AudioListener>();
            var urpCam = cam.GetUniversalAdditionalCameraData();
            urpCam.renderPostProcessing = true;
            urpCam.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var brain = camGo.AddComponent<CinemachineBrain>();
            camGo.transform.SetPositionAndRotation(ShipStart + new Vector3(14, 6, -38), ship.transform.rotation);

            var noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(NoisePath);
            var shakers = new List<CinemachineBasicMultiChannelPerlin>();

            var shotWide = FollowShot("CM_Wide", ship.transform, new Vector3(14f, 6f, -38f), 55f, noise, shakers);
            var shotSide = FollowShot("CM_Side", ship.transform, new Vector3(-30f, -4f, 6f), 45f, noise, shakers);
            var shotClose = FollowShot("CM_Close", ship.transform, new Vector3(5f, 2.6f, -8f), 40f, noise, shakers);
            var shotPilot = LocalShot("CM_PilotInterior", body, new Vector3(0.55f, 0.6f, 4.4f), new Vector3(0, 0.25f, 2.55f), 60f, noise, shakers);
            var shotPov = LocalShot("CM_PilotPOV", body, new Vector3(0, 0.55f, 2.7f), new Vector3(0, 0.45f, 50f), 70f, noise, shakers);
            shotWide.Priority = 20;

            // ---- Post FX -----------------------------------------------------
            var globalVol = new GameObject("GlobalVolume").AddComponent<Volume>();
            globalVol.isGlobal = true;
            globalVol.priority = 0;
            globalVol.sharedProfile = globalProfile;

            var wakeVol = new GameObject("WakeVolume").AddComponent<Volume>();
            wakeVol.isGlobal = true;
            wakeVol.priority = 10;
            wakeVol.weight = 0f;
            wakeVol.sharedProfile = wakeProfile;

            // ---- Effects + director ------------------------------------------
            var effects = ship.AddComponent<ShipEmergencyEffects>();
            SetRefs(effects, ("alarmLight", alarmLight), ("alarmSource", alarmAudio));
            var so = new SerializedObject(effects);
            var list = so.FindProperty("shakers");
            list.arraySize = shakers.Count;
            for (int i = 0; i < shakers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = shakers[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            var directorGo = new GameObject("Scene01Director");
            var subtitles = directorGo.AddComponent<GreyboxSubtitles>();
            var director = directorGo.AddComponent<Scene01Director>();
            SetRefs(director,
                ("beat1Exterior", dialogue[0]), ("beat2Wake", dialogue[1]), ("beat3Amnesia", dialogue[2]),
                ("subtitles", subtitles), ("brain", brain),
                ("shotWide", shotWide), ("shotSide", shotSide), ("shotClose", shotClose),
                ("shotPilot", shotPilot), ("shotPilotPov", shotPov),
                ("shipMotion", motion), ("effects", effects), ("pilotModel", pilot), ("exteriorModel", exterior),
                ("wakeVolume", wakeVol));

            foreach (var seq in dialogue)
                if (!seq) Debug.LogError("[SpaceRat] Dialogue sequence reference is missing.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettingsFirst(ScenePath);
            Debug.Log($"[SpaceRat] Built greybox scene at {ScenePath}");
        }

        // ---------------------------------------------------------------------

        static CinemachineCamera FollowShot(string name, Transform target, Vector3 offset, float fov,
            NoiseSettings noise, List<CinemachineBasicMultiChannelPerlin> shakers)
        {
            var go = new GameObject(name);
            go.transform.position = target.position + target.rotation * offset;
            var cm = go.AddComponent<CinemachineCamera>();
            cm.Target.TrackingTarget = target;
            cm.Lens = Lens(fov);
            cm.Priority = 0;

            var follow = go.AddComponent<CinemachineFollow>();
            follow.FollowOffset = offset;
            var ts = follow.TrackerSettings;
            ts.BindingMode = BindingMode.LockToTargetWithWorldUp;
            ts.PositionDamping = new Vector3(0.6f, 0.6f, 0.6f);
            follow.TrackerSettings = ts;

            go.AddComponent<CinemachineRotationComposer>();
            AddNoise(go, noise, shakers);
            return cm;
        }

        static CinemachineCamera LocalShot(string name, Transform parent, Vector3 localPos, Vector3 localLookAt,
            float fov, NoiseSettings noise, List<CinemachineBasicMultiChannelPerlin> shakers)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(localLookAt - localPos, Vector3.up);
            var cm = go.AddComponent<CinemachineCamera>();
            var lens = Lens(fov);
            lens.NearClipPlane = 0.05f;
            cm.Lens = lens;
            cm.Priority = 0;
            AddNoise(go, noise, shakers);
            return cm;
        }

        static LensSettings Lens(float fov)
        {
            var lens = LensSettings.Default;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 20000f;
            return lens;
        }

        static void AddNoise(GameObject go, NoiseSettings noise, List<CinemachineBasicMultiChannelPerlin> shakers)
        {
            if (!noise) return;
            var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
            perlin.NoiseProfile = noise;
            perlin.AmplitudeGain = 0f;
            shakers.Add(perlin);
        }

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPos,
            Vector3 localScale, Material mat, Vector3? localEuler = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            if (parent) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler ?? Vector3.zero);
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void SetRefs(Object target, params (string field, Object value)[] refs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in refs)
            {
                var p = so.FindProperty(field);
                if (p == null) { Debug.LogError($"[SpaceRat] {target.GetType().Name} has no field '{field}'"); continue; }
                p.objectReferenceValue = value;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Assets -----------------------------------------------------------

        static Dictionary<string, Material> BuildMaterials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var unlit = Shader.Find("Universal Render Pipeline/Unlit");
            var particlesUnlit = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? unlit;

            var m = new Dictionary<string, Material>
            {
                ["Sun"] = Unlit(unlit, new Color(4.5f, 1.9f, 0.45f)),
                ["Stars"] = Unlit(particlesUnlit, Color.white),
                ["Hull"] = Lit(lit, new Color(0.42f, 0.44f, 0.47f), 0.6f, 0.45f),
                ["Trim"] = Lit(lit, new Color(0.16f, 0.17f, 0.19f), 0.7f, 0.35f),
                ["Glass"] = Lit(lit, new Color(0.05f, 0.12f, 0.16f), 0.1f, 0.95f),
                ["Pilot"] = Lit(lit, new Color(0.55f, 0.45f, 0.32f), 0f, 0.3f),
                ["EngineGlow"] = Unlit(unlit, new Color(0.6f, 2.4f, 4f)),
            };
            foreach (var (name, mat) in m)
            {
                string path = $"{MatDir}/M_{name}.mat";
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.CreateAsset(mat, path);
            }
            return m;
        }

        static Material Unlit(Shader s, Color c)
        {
            var mat = new Material(s);
            mat.SetColor("_BaseColor", c);
            return mat;
        }

        static Material Lit(Shader s, Color c, float metallic, float smoothness)
        {
            var mat = new Material(s);
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        static (VolumeProfile global, VolumeProfile wake) BuildProfiles()
        {
            var global = NewProfile($"{SettingsDir}/Scene01_GlobalVolume.asset");
            var bloom = AddOverride<Bloom>(global);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(2.4f);
            bloom.scatter.Override(0.85f);
            bloom.tint.Override(new Color(1f, 0.6f, 0.3f));
            var vig = AddOverride<Vignette>(global);
            vig.intensity.Override(0.28f);
            vig.smoothness.Override(0.5f);
            var ca = AddOverride<ColorAdjustments>(global);
            ca.contrast.Override(12f);
            var tm = AddOverride<Tonemapping>(global);
            tm.mode.Override(TonemappingMode.Neutral);

            var wake = NewProfile($"{SettingsDir}/Scene01_WakeVolume.asset");
            var dof = AddOverride<DepthOfField>(wake);
            dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(0f);
            dof.gaussianEnd.Override(0.5f);
            dof.gaussianMaxRadius.Override(1.5f);
            dof.highQualitySampling.Override(true);
            var wvig = AddOverride<Vignette>(wake);
            wvig.intensity.Override(0.75f);
            wvig.smoothness.Override(1f);
            var wca = AddOverride<ColorAdjustments>(wake);
            wca.postExposure.Override(-3f);
            wca.saturation.Override(-60f);

            AssetDatabase.SaveAssets();
            return (global, wake);
        }

        static VolumeProfile NewProfile(string path)
        {
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(false);
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static DialogueSequence[] EnsureDialogue()
        {
            return new[]
            {
                EnsureSequence("Beat1_ExteriorEmergency",
                    (Speaker.Computer, "Warning. Stellar collision trajectory detected.", 3.2f),
                    (Speaker.Computer, "Attempting automatic course correction.", 3f),
                    (Speaker.Computer, "Flight control interface unavailable.", 3f),
                    (Speaker.Computer, "Pilot response required.", 2.6f)),
                EnsureSequence("Beat2_WakePilot",
                    (Speaker.Computer, "Pilot. Wake up.", 2.4f),
                    (Speaker.Computer, "Pilot, respond.", 2.4f),
                    (Speaker.Computer, "Impact probability increasing.", 2.8f)),
                EnsureSequence("Beat3_Amnesia",
                    (Speaker.Pilot, "What...? Where am I?", 2.6f),
                    (Speaker.Computer, "You are aboard this vessel. We are on a collision course with a star.", 3.8f),
                    (Speaker.Pilot, "Who am I? What happened?", 2.6f),
                    (Speaker.Computer, "Your identity records are currently inaccessible.", 3.2f),
                    (Speaker.Pilot, "Then change course!", 2.2f),
                    (Speaker.Computer, "Unable to comply. Flight control systems are not responding to my commands.", 4f),
                    (Speaker.Pilot, "Can I steer it?", 2.2f),
                    (Speaker.Computer, "Manual controls may still function. You must try.", 3.2f)),
            };
        }

        static DialogueSequence EnsureSequence(string name, params (Speaker s, string text, float dur)[] lines)
        {
            string path = $"{DialogueDir}/{name}.asset";
            var seq = AssetDatabase.LoadAssetAtPath<DialogueSequence>(path);
            if (seq) return seq;
            seq = ScriptableObject.CreateInstance<DialogueSequence>();
            foreach (var (s, text, dur) in lines)
                seq.lines.Add(new DialogueLine { speaker = s, text = text, duration = dur });
            AssetDatabase.CreateAsset(seq, path);
            return seq;
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)!.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        static void AddSceneToBuildSettingsFirst(string path)
        {
            var scenes = new List<EditorBuildSettingsScene> { new(path, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != path) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
