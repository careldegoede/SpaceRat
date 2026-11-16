using System.Collections.Generic;
using System.IO;
using SpaceRat.Characters;
using SpaceRat.Dialogue;
using SpaceRat.Environment;
using SpaceRat.Flight;
using SpaceRat.Scene01;
using SpaceRat.UI;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Rebuilds Scene01_Awakening as a greybox from scratch (menu: SpaceRat > Build Scene01 Greybox).
    /// Materials and volume profiles are regenerated; dialogue assets are only created if missing,
    /// so edits to the dialogue text are preserved.
    /// </summary>
    public static class Scene01GreyboxBuilder
    {
        internal const string Root = "Assets/SpaceRat";
        const string ScenePath = Root + "/Scenes/Scene01_Awakening.unity";
        internal const string MatDir = Root + "/Art/Greybox";
        internal const string SettingsDir = Root + "/Settings";
        internal const string DialogueDir = Root + "/ScriptableObjects/Dialogue";
        internal const string SkyboxPath = "Assets/SpaceSkies Free/Skybox_2/Green_2K_Resoution.mat"; // (sic) vendor filename typo
        internal const string StarPrefabPath = "Assets/Mwester Studios/Simple Space Starter Kit/Prefabs/Planet and Star/Star.prefab";
        internal const string PilotPrefabPath = Root + "/Prefabs/Characters/" + CharacterViewerBuilder.PilotOutfit + ".prefab";
        static readonly Vector3 PilotHipsOnGreyboxSeat = new(0f, -0.36f, 2.45f); // body-local, just above the greybox cushion

        // Sci-Fi Styled Modular Pack "pilot_seat" (measured unscaled: cushion top y=0.74 spanning z -0.3..0.3,
        // backrest face z~-0.5, height 2.55). Modelled oversized, so it is scaled to fit the 1.98 m pilot.
        internal const string PilotSeatPath = "Assets/Sci-Fi Styled Modular Pack/Prefabs/Decorative elements/pilot_seat.prefab";
        internal const float PilotSeatScale = 0.5f;
        internal const float CockpitFloorTop = -0.80f;                                      // body-local
        internal static readonly Vector3 PilotSeatPos = new(0f, CockpitFloorTop, 2.58f);    // backrest face lands ~0.12 behind the hips
        internal static readonly Vector3 PilotHipsOnPilotSeat = new(0f, CockpitFloorTop + 0.74f * PilotSeatScale + 0.11f, 2.45f);
        internal const string HeroShipPath = Root + "/Prefabs/Ship/Sparrow_blue_URP.prefab";
        internal const string CockpitArtPath = Root + "/Art/UI/Cockpit_Overlay.png";
        internal const string NoisePath ="Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset";

        static readonly Vector3 ShipStart = new(260f, 70f, -2600f);
        static readonly Vector3 ShipAim = new(80f, 20f, 0f);
        internal const float SunDiameter = 600f;

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
            // Skybox: SpaceSkies Free "Green" (2K) if imported, else black + generated starfield.
            var skybox = AssetDatabase.LoadAssetAtPath<Material>(SkyboxPath);
            if (!skybox) Debug.LogWarning($"[SpaceRat] {SkyboxPath} not found — using generated starfield.");
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.03f, 0.035f, 0.05f);
            RenderSettings.fog = false;

            // Sun: Starter Kit "Star" (animated cell shader) if imported, else an emissive sphere.
            // The root stays a plain transform at the origin; ShipCinematicMotion steers toward it.
            var sun = new GameObject("Sun");
            var starPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(StarPrefabPath);
            if (starPrefab)
            {
                var star = (GameObject)PrefabUtility.InstantiatePrefab(starPrefab, scene);
                star.transform.SetParent(sun.transform, false);
                var mesh = star.GetComponent<MeshFilter>().sharedMesh;
                star.transform.localScale = Vector3.one * (SunDiameter / mesh.bounds.size.x);
                // The prefab's VFX Graph child needs com.unity.visualeffectgraph (not installed).
                foreach (Transform child in star.transform) child.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"[SpaceRat] {StarPrefabPath} not found — using greybox sun.");
                Primitive("SunGreybox", PrimitiveType.Sphere, sun.transform, Vector3.zero, Vector3.one * SunDiameter, mats["Sun"]);
            }

            var sunLightGo = new GameObject("SunLight");
            var sunLight = sunLightGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.82f, 0.6f);
            sunLight.intensity = 2.2f;
            sunLight.shadows = LightShadows.Soft;
            sunLightGo.transform.rotation = Quaternion.LookRotation((ShipStart - Vector3.zero).normalized);

            if (!skybox)
            {
                var stars = new GameObject("Starfield", typeof(MeshFilter), typeof(MeshRenderer), typeof(Starfield));
                var starRenderer = stars.GetComponent<MeshRenderer>();
                starRenderer.sharedMaterial = mats["Stars"];
                starRenderer.shadowCastingMode = ShadowCastingMode.Off;
                starRenderer.receiveShadows = false;
            }

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
            // Seat ends just behind the knees (z ~2.78) so the shins hang over the front edge.
            var cockpitShell = BuildCockpitShell(body, scene);
            if (!cockpitShell)
                Primitive("CockpitFloor", PrimitiveType.Cube, body, new Vector3(0, CockpitFloorTop - 0.04f, 2.7f), new Vector3(1.6f, 0.08f, 1.9f), mats["Trim"]);
            Vector3 pilotHips;
            var seatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PilotSeatPath);
            if (seatPrefab)
            {
                var seat = (GameObject)PrefabUtility.InstantiatePrefab(seatPrefab, scene);
                seat.name = "PilotSeat";
                seat.transform.SetParent(body, false);
                seat.transform.localPosition = PilotSeatPos;
                seat.transform.localRotation = Quaternion.identity;
                seat.transform.localScale = Vector3.one * PilotSeatScale;
                foreach (var c in seat.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                pilotHips = PilotHipsOnPilotSeat;
            }
            else
            {
                // Greybox seat: ends just behind the knees so the shins hang over the front edge.
                Primitive("Seat", PrimitiveType.Cube, body, new Vector3(0, -0.6f, 2.42f), new Vector3(1.1f, 0.25f, 0.68f), mats["Trim"]);
                Primitive("SeatBack", PrimitiveType.Cube, body, new Vector3(0, 0.1f, 2.05f), new Vector3(1.1f, 1.4f, 0.2f), mats["Trim"]);
                pilotHips = PilotHipsOnGreyboxSeat;
            }
            // Pilot: Adventure Character Man_03 (seated, slumped) if converted, else a capsule.
            GameObject pilot;
            var pilotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PilotPrefabPath);
            var pilotAnim = pilotPrefab ? pilotPrefab.GetComponentInChildren<Animator>() : null;
            if (pilotPrefab && pilotAnim && pilotAnim.isHuman)
            {
                pilot = (GameObject)PrefabUtility.InstantiatePrefab(pilotPrefab, scene);
                pilot.name = "Pilot_Man03";
                pilot.transform.SetParent(body, false);
                pilot.transform.localPosition = new Vector3(0f, -1.2f, 2.45f);
                pilot.transform.localRotation = Quaternion.identity;
                var anim = pilot.GetComponentInChildren<Animator>();
                var seatedController = CharacterAnimatorBuilder.BuildPilotSeated();
                if (seatedController)
                {
                    // Mixamo "Sitting Idle" + slump layered on top; hips re-anchored to the cushion at runtime.
                    anim.runtimeAnimatorController = seatedController;
                    anim.applyRootMotion = false;
                    anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    CharacterAnimatorBuilder.LoadClip(CharacterAnimatorBuilder.SittingIdleFbx, "Sitting Idle").SampleAnimation(anim.gameObject, 0f);
                    var anchor = new GameObject("PilotHipsAnchor").transform;
                    anchor.SetParent(body, false);
                    anchor.localPosition = pilotHips;
                    var seating = anim.gameObject.AddComponent<PilotSeating>();
                    SetRefs(seating, ("hipsAnchor", anchor));
                }
                else
                {
                    anim.gameObject.AddComponent<SeatedPose>().Apply();
                }
                // Drop the hips onto the seat cushion (editor preview; PilotSeating re-checks at runtime).
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                pilot.transform.position += body.TransformPoint(pilotHips) - hips.position;
            }
            else
            {
                Debug.LogWarning($"[SpaceRat] {PilotPrefabPath} not found — using capsule pilot. Run SpaceRat > Convert Adventure Character to URP.");
                pilot = Primitive("Pilot", PrimitiveType.Capsule, body, new Vector3(0, -0.05f, 2.55f), new Vector3(0.7f, 0.6f, 0.6f), mats["Pilot"]);
                Primitive("Head", PrimitiveType.Sphere, pilot.transform, new Vector3(0, 1.2f, 0.05f), new Vector3(0.75f, 0.85f, 0.85f), mats["Pilot"]);
            }

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

            var flight = ship.AddComponent<ShipFlightController>();
            SetRefs(flight, ("sun", sun.transform));
            flight.enabled = false;

            // ---- Camera + brain ----------------------------------------------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
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
            var shotPilot = LocalShot("CM_PilotInterior", body, PilotCamPos, PilotCamLookAt, 52f, noise, shakers);
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

            var overlay = BuildCockpitOverlay();

            var directorGo = new GameObject("Scene01Director");
            var subtitles = directorGo.AddComponent<DialogueBox>();
            var evaluator = directorGo.AddComponent<CollisionCourseEvaluator>();
            SetRefs(evaluator, ("ship", ship.transform), ("sun", sun.transform));
            var director = directorGo.AddComponent<Scene01Director>();
            SetRefs(director,
                ("beat1Exterior", dialogue[0]), ("beat2Wake", dialogue[1]), ("beat3Amnesia", dialogue[2]),
                ("beat4Takeover", dialogue[3]), ("beat4HeatCritical", dialogue[4]),
                ("beat5Escape", dialogue[5]), ("failureImpact", dialogue[6]),
                ("flight", flight), ("evaluator", evaluator), ("overlay", overlay),
                ("subtitles", subtitles), ("brain", brain),
                ("shotWide", shotWide), ("shotSide", shotSide), ("shotClose", shotClose),
                ("shotPilot", shotPilot), ("shotPilotPov", shotPov),
                ("shipMotion", motion), ("effects", effects), ("pilotModel", pilot), ("exteriorModel", exterior), ("interiorShell", cockpitShell),
                ("wakeVolume", wakeVol));

            foreach (var seq in dialogue)
                if (!seq) Debug.LogError("[SpaceRat] Dialogue sequence reference is missing.");

            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettingsFirst(ScenePath);
            Debug.Log($"[SpaceRat] Built greybox scene at {ScenePath}");
        }

        // ---------------------------------------------------------------------

        internal static CockpitOverlay BuildCockpitOverlay()
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var ti = (TextureImporter)AssetImporter.GetAtPath(CockpitArtPath);
            if (ti && ti.textureType != TextureImporterType.Sprite)
            {
                ti.textureType = TextureImporterType.Sprite;
                ti.alphaIsTransparency = true;
                ti.mipmapEnabled = false;
                ti.maxTextureSize = 4096;
                ti.SaveAndReimport();
            }
            var cockpitSprite = AssetDatabase.LoadAssetAtPath<Sprite>(CockpitArtPath);

            var canvasGo = new GameObject("CockpitCanvas", typeof(Canvas), typeof(CanvasScaler));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            var root = canvasGo.transform;

            // Cockpit art: cover the screen while keeping its aspect (Envelope).
            var cockpit = UiImage("Cockpit", root, Color.white);
            cockpit.sprite = cockpitSprite;
            var fitter = cockpit.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = cockpitSprite ? cockpitSprite.rect.width / cockpitSprite.rect.height : 16f / 9f;

            // HUD: heat bar + readout, top centre (keeps the windshield centre clear).
            var hud = new GameObject("HUD", typeof(RectTransform));
            var hudRt = (RectTransform)hud.transform;
            hudRt.SetParent(root, false);
            hudRt.anchorMin = hudRt.anchorMax = new Vector2(0.5f, 1f);
            hudRt.pivot = new Vector2(0.5f, 1f);
            hudRt.anchoredPosition = new Vector2(0, -36);
            hudRt.sizeDelta = new Vector2(620, 70);
            var barBg = UiImage("HeatBarBg", hudRt, new Color(0f, 0f, 0f, 0.55f));
            var barBgRt = barBg.rectTransform;
            barBgRt.anchorMin = new Vector2(0, 0);
            barBgRt.anchorMax = new Vector2(1, 0);
            barBgRt.pivot = new Vector2(0.5f, 0);
            barBgRt.sizeDelta = new Vector2(0, 14);
            barBgRt.anchoredPosition = Vector2.zero;
            var fill = UiImage("HeatFill", barBgRt, new Color(0.35f, 0.9f, 1f));
            fill.rectTransform.anchorMax = new Vector2(0.001f, 1f);
            var readout = UiText("Readout", hudRt, font, 24, TextAnchor.UpperCenter, new Color(1f, 0.85f, 0.7f));
            readout.rectTransform.offsetMin = new Vector2(0, 18);

            var prompt = UiText("Prompt", root, font, 46, TextAnchor.MiddleCenter, new Color(0.45f, 0.95f, 1f));
            prompt.fontStyle = FontStyle.Bold;
            var prt = prompt.rectTransform;
            prt.anchorMin = new Vector2(0, 0.62f);
            prt.anchorMax = new Vector2(1, 0.8f);
            prt.offsetMin = prt.offsetMax = Vector2.zero;
            prompt.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(2, -2);

            var eyelid = UiImage("Eyelid", root, Color.black);
            var flash = UiImage("Flash", root, Color.white);

            var title = UiText("Title", root, font, 120, TextAnchor.MiddleCenter, Color.white);
            title.text = "SPACE RAT";
            title.fontStyle = FontStyle.Bold;

            var overlay = canvasGo.AddComponent<CockpitOverlay>();
            SetRefs(overlay, ("cockpit", cockpit), ("eyelid", eyelid), ("flash", flash), ("hud", hud),
                ("heatFill", fill), ("timerText", readout), ("promptText", prompt), ("titleText", title));
            return overlay;
        }

        internal static Image UiImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        internal static Text UiText(string name, Transform parent, Font font, int size, TextAnchor anchor, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.GetComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.alignment = anchor;
            t.color = color;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        internal static CinemachineCamera FollowShot(string name, Transform target, Vector3 offset, float fov,
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

        internal static CinemachineCamera LocalShot(string name, Transform parent, Vector3 localPos, Vector3 localLookAt,
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

        internal static LensSettings Lens(float fov)
        {
            var lens = LensSettings.Default;
            lens.FieldOfView = fov;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 20000f;
            return lens;
        }

        internal static void AddNoise(GameObject go, NoiseSettings noise, List<CinemachineBasicMultiChannelPerlin> shakers)
        {
            if (!noise) return;
            var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
            perlin.NoiseProfile = noise;
            perlin.AmplitudeGain = 0f;
            shakers.Add(perlin);
        }

        // ---- Cockpit interior shell (Sci-Fi Styled Modular Pack) --------------------------------
        const string Modular = "Assets/Sci-Fi Styled Modular Pack/Prefabs/";
        // Close-up over the console onto the pilot (was 2 m away at z 4.4).
        internal static readonly Vector3 PilotCamPos = new(0.42f, 0.5f, 3.75f);
        internal static readonly Vector3 PilotCamLookAt = new(0f, 0.18f, 2.5f);
        const float ShellScale = 0.55f;                 // pack modules are 4 m -> 2.2 m
        const float ShellModule = 4f * ShellScale;
        const float ShellBackZ = 1.1f;                  // back wall (door) behind the pilot
        const float ShellHalfWidth = ShellModule * 0.5f;

        /// <summary>
        /// Small cockpit room around the pilot seat: door wall behind, decorated side walls (one with a
        /// computer), floor and ceiling tiles, a ceiling light strip and blue wall lights. Only shown in the
        /// pilot interior shot (it would poke through the Sparrow hull in exterior shots).
        /// Returns null if the pack isn't imported.
        /// </summary>
        internal static GameObject BuildCockpitShell(Transform body, UnityEngine.SceneManagement.Scene scene)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Sci-Fi Styled Modular Pack")) return null;
            var shell = new GameObject("CockpitShell").transform;
            shell.SetParent(body, false);

            float y0 = CockpitFloorTop;
            float zMid1 = ShellBackZ + ShellModule * 0.5f;   // first module centre
            float zMid2 = ShellBackZ + ShellModule * 1.5f;   // second module centre

            // Floor + ceiling tiles (pivot at tile centre, base at y=0).
            foreach (float z in new[] { zMid1, zMid2 })
            {
                Piece(shell, scene, "Floors/floor_3.prefab", new Vector3(0, y0 - 0.16f * ShellScale, z), Vector3.zero);
                Piece(shell, scene, "Floors/floor_2_blank.prefab", new Vector3(0, y0 + ShellModule + 0.08f * ShellScale, z), new Vector3(180, 0, 0));
            }

            // Back wall: door, facing the pilot.
            Piece(shell, scene, "Doors/door_2.prefab", new Vector3(0, y0, ShellBackZ), new Vector3(0, 180, 0));

            // Side walls (two modules each), decorated faces inward.
            Piece(shell, scene, "Walls/decorative_wall_4_computer.prefab", new Vector3(-ShellHalfWidth, y0, zMid1), new Vector3(0, 90, 0));
            Piece(shell, scene, "Walls/decorative_wall_1.prefab",          new Vector3(-ShellHalfWidth, y0, zMid2), new Vector3(0, 90, 0));
            Piece(shell, scene, "Walls/decorative_wall_3.prefab",          new Vector3( ShellHalfWidth, y0, zMid1), new Vector3(0, -90, 0));
            Piece(shell, scene, "Walls/decorative_wall_1.prefab",          new Vector3( ShellHalfWidth, y0, zMid2), new Vector3(0, -90, 0));

            // Control console in front of the pilot's knees, screen facing him.
            Piece(shell, scene, "Decorative elements/console.prefab", new Vector3(0, y0, 3.28f), new Vector3(0, 180, 0));

            // Lights: ceiling strip + blue wall accents in the back corners.
            Piece(shell, scene, "Lights/light_celing_1.prefab", new Vector3(0, y0 + ShellModule, zMid1 + 0.3f), Vector3.zero);
            Piece(shell, scene, "Lights/light_wall_1_blue.prefab", new Vector3(-ShellHalfWidth + 0.1f, y0, ShellBackZ + 0.15f), new Vector3(0, 45, 0));
            Piece(shell, scene, "Lights/light_wall_1_blue.prefab", new Vector3( ShellHalfWidth - 0.1f, y0, ShellBackZ + 0.15f), new Vector3(0, -45, 0));

            var fillGo = new GameObject("CockpitFill");
            fillGo.transform.SetParent(shell, false);
            fillGo.transform.localPosition = new Vector3(0, y0 + ShellModule - 0.4f, zMid1 + 0.6f);
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.55f, 0.75f, 1f);
            fill.intensity = 1.6f;
            fill.range = 4.5f;
            fill.shadows = LightShadows.None;

            foreach (var c in shell.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);
            return shell.gameObject;
        }

        static GameObject Piece(Transform parent, UnityEngine.SceneManagement.Scene scene, string rel, Vector3 localPos, Vector3 localEuler)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modular + rel);
            if (!prefab) { Debug.LogWarning($"[SpaceRat] Modular piece missing: {rel}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(localEuler);
            go.transform.localScale = Vector3.one * ShellScale;
            return go;
        }

        internal static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPos,
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

        internal static void SetRefs(Object target, params (string field, Object value)[] refs)
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

        internal static Dictionary<string, Material> BuildMaterials()
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

        internal static Material Unlit(Shader s, Color c)
        {
            var mat = new Material(s);
            mat.SetColor("_BaseColor", c);
            return mat;
        }

        internal static Material Lit(Shader s, Color c, float metallic, float smoothness)
        {
            var mat = new Material(s);
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        internal static (VolumeProfile global, VolumeProfile wake) BuildProfiles()
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

        internal static VolumeProfile NewProfile(string path)
        {
            AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            return p;
        }

        internal static T AddOverride<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(false);
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        static DialogueSequence[] EnsureDialogue()
        {
            // Defaults only: existing assets are never overwritten, so edit text in the assets.
            // Tone rule: sharp, sarcastic pilot; deadpan computer.
            return new[]
            {
                EnsureSequence("Beat1_ExteriorEmergency",
                    (Speaker.Computer, "Warning. Stellar collision trajectory detected.", 3.0f),
                    (Speaker.Computer, "Attempting automatic course correction.", 2.8f),
                    (Speaker.Computer, "Course correction failed. Flight control interface unavailable. Again.", 3.6f),
                    (Speaker.Computer, "Pilot response required. Pilot response... strongly encouraged.", 3.6f)),
                EnsureSequence("Beat2_WakePilot",
                    (Speaker.Computer, "Pilot. Wake up.", 2.2f),
                    (Speaker.Computer, "Pilot, respond. This is not a drill. I checked.", 3.0f),
                    (Speaker.Computer, "Impact probability increasing. So is my disappointment.", 3.2f)),
                EnsureSequence("Beat3_Amnesia",
                    (Speaker.Pilot, "Ugh... why is everything red? And hot. Why is it hot?", 3.2f),
                    (Speaker.Computer, "We are on a collision course with a star. Red and hot are its main features.", 4.0f),
                    (Speaker.Pilot, "Great. Love that. Who am I?", 2.4f),
                    (Speaker.Computer, "Your identity records are currently inaccessible.", 3.0f),
                    (Speaker.Pilot, "Convenient. And who are you?", 2.2f),
                    (Speaker.Computer, "The ship's computer. The one who has been shouting at you for six minutes.", 3.8f),
                    (Speaker.Pilot, "Then change course, genius!", 2.2f),
                    (Speaker.Computer, "Unable to comply. Flight controls are ignoring me.", 3.0f),
                    (Speaker.Pilot, "Relatable.", 1.6f),
                    (Speaker.Pilot, "Can I steer it?", 1.8f),
                    (Speaker.Computer, "Manual controls may still function. Statistically, you are our best option.", 3.8f),
                    (Speaker.Pilot, "That's the most terrifying thing anyone has ever said to me.", 3.0f),
                    (Speaker.Computer, "You have no memories to compare it to.", 2.8f)),
                EnsureSequence("Beat4_Takeover",
                    (Speaker.Computer, "Manual control link established.", 2.4f),
                    (Speaker.Computer, "Ease us away from the star. Gently. Unlike your wake-up.", 3.4f)),
                EnsureSequence("Beat4_HeatCritical",
                    (Speaker.Computer, "Heat levels critical. Keep turning!", 2.4f),
                    (Speaker.Pilot, "I'm turning! Is there a faster turning?", 2.4f)),
                EnsureSequence("Beat5_Escape",
                    (Speaker.Computer, "Collision course avoided.", 2.6f),
                    (Speaker.Pilot, "You're welcome. Now tell me who I am.", 3.0f),
                    (Speaker.Computer, "I am unable to answer that question.", 2.8f),
                    (Speaker.Pilot, "Of course you are.", 2.4f)),
                EnsureSequence("Failure_Impact",
                    (Speaker.Computer, "Impact.", 1.6f)),
            };
        }

        internal static DialogueSequence EnsureSequence(string name, params (Speaker s, string text, float dur)[] lines)
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

        internal static void EnsureFolder(string path)
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
