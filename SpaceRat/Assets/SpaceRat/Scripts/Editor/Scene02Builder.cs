using System.Collections.Generic;
using System.Linq;
using SpaceRat.Dialogue;
using SpaceRat.Flight;
using SpaceRat.Scene01;
using SpaceRat.Scene02;
using SpaceRat.UI;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using B = SpaceRat.EditorTools.Scene01GreyboxBuilder;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Builds Assets/SpaceRat/Scenes/Scene02_TheSignal.unity (menu: SpaceRat > Build Scene02 The Signal).
    /// Reuses the Scene 1 builder's materials, cockpit overlay, cameras and post profiles.
    /// Dialogue assets live in ScriptableObjects/Dialogue/Scene02 and are only created if missing.
    /// </summary>
    public static class Scene02Builder
    {
        const string ScenePath = B.Root + "/Scenes/Scene02_TheSignal.unity";
        const string DialogueSub = "Scene02";
        const string KitPrefabs = "Assets/Mwester Studios/Simple Space Starter Kit/Prefabs/";

        static readonly Vector3 SunPos = new(350f, -120f, -2600f);           // the star we just escaped, behind us
        // Behind-right of the starting heading: not in view at first — the pilot finds it with the radar.
        static readonly Vector3 PlanetPos = new(8600f, 1500f, -5600f);
        const string RadarSourcePng = "../Images/Futuristic Neon Radar HUD Interface.png";   // repo Images folder
        const string RadarSpritePath = B.Root + "/Art/UI/Radar_HUD.png";         // signal source, ~11 deg off the nose
        const float PlanetRadius = 700f;

        [MenuItem("SpaceRat/Build Scene02 The Signal")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (var d in new[] { B.Root + "/Scenes", B.SettingsDir, B.DialogueDir + "/" + DialogueSub })
                B.EnsureFolder(d);
            var mats = B.BuildMaterials();
            var globalProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(B.SettingsDir + "/Scene01_GlobalVolume.asset")
                                ?? B.BuildProfiles().global;
            var entryProfile = BuildEntryProfile();
            var dlg = EnsureDialogue();

            // ---- Environment -------------------------------------------------------
            var sky = AssetDatabase.LoadAssetAtPath<Material>(B.SkyboxPath);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.05f, 0.06f);

            var sun = new GameObject("Sun");
            sun.transform.position = SunPos;
            var starPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(B.StarPrefabPath);
            if (starPrefab)
            {
                var star = (GameObject)PrefabUtility.InstantiatePrefab(starPrefab, scene);
                star.transform.SetParent(sun.transform, false);
                star.transform.localScale = Vector3.one * (B.SunDiameter / star.GetComponent<MeshFilter>().sharedMesh.bounds.size.x);
                foreach (Transform c in star.transform) c.gameObject.SetActive(false);
            }
            else B.Primitive("SunGreybox", PrimitiveType.Sphere, sun.transform, Vector3.zero, Vector3.one * B.SunDiameter, mats["Sun"]);

            var sunLight = new GameObject("SunLight").AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(1f, 0.85f, 0.65f);
            sunLight.intensity = 1.8f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.transform.rotation = Quaternion.LookRotation((PlanetPos - SunPos).normalized);

            var planet = BuildPlanet(scene);

            // ---- Ship -----------------------------------------------------------
            var ship = new GameObject("Ship");
            ship.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var body = new GameObject("Body").transform;
            body.SetParent(ship.transform, false);

            GameObject exterior;
            var hero = AssetDatabase.LoadAssetAtPath<GameObject>(B.HeroShipPath);
            if (hero)
            {
                exterior = (GameObject)PrefabUtility.InstantiatePrefab(hero, scene);
                exterior.name = "Exterior_SparrowBlue";
                exterior.transform.SetParent(body, false);
            }
            else
            {
                exterior = B.Primitive("Exterior_Greybox", PrimitiveType.Capsule, body, Vector3.zero, new Vector3(3.6f, 6f, 3.2f), mats["Hull"], new Vector3(90, 0, 0));
            }

            var alarmGo = new GameObject("AlarmLight");
            alarmGo.transform.SetParent(body, false);
            alarmGo.transform.localPosition = new Vector3(0, 1.1f, 3.4f);
            var alarm = alarmGo.AddComponent<Light>();
            alarm.type = LightType.Point;
            alarm.color = new Color(1f, 0.08f, 0.05f);
            alarm.range = 18f;
            alarm.intensity = 0f;
            var audio = ship.AddComponent<AudioSource>();
            var flight = ship.AddComponent<CruiseFlightController>();

            // ---- Cameras ----------------------------------------------------------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 40000f;
            camGo.AddComponent<AudioListener>();
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            var brain = camGo.AddComponent<CinemachineBrain>();

            var noise = AssetDatabase.LoadAssetAtPath<NoiseSettings>(B.NoisePath);
            var shakers = new List<CinemachineBasicMultiChannelPerlin>();
            var pov = B.LocalShot("CM_PilotPOV", body, new Vector3(0, 0.55f, 2.7f), new Vector3(0, 0.45f, 50f), 70f, noise, shakers);
            var crashCam = B.FollowShot("CM_CrashExterior", ship.transform, new Vector3(-22f, 9f, -30f), 50f, noise, shakers);
            foreach (var c in new[] { pov, crashCam })
            {
                var lens = c.Lens;
                lens.FarClipPlane = 40000f;
                c.Lens = lens;
            }
            pov.Priority = 20;

            // ---- Post ---------------------------------------------------------------
            var gv = new GameObject("GlobalVolume").AddComponent<Volume>();
            gv.isGlobal = true;
            gv.sharedProfile = globalProfile;
            var ev = new GameObject("EntryVolume").AddComponent<Volume>();
            ev.isGlobal = true;
            ev.priority = 10;
            ev.weight = 0f;
            ev.sharedProfile = entryProfile;

            // ---- Effects, navigation, asteroids -----------------------------------------
            var effects = ship.AddComponent<ShipEmergencyEffects>();
            B.SetRefs(effects, ("alarmLight", alarm), ("alarmSource", audio));
            var effSo = new SerializedObject(effects);
            var list = effSo.FindProperty("shakers");
            list.arraySize = shakers.Count;
            for (int i = 0; i < shakers.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = shakers[i];
            effSo.ApplyModifiedPropertiesWithoutUndo();

            var nav = new GameObject("Navigation");
            var tracker = nav.AddComponent<SignalTracker>();
            B.SetRefs(tracker, ("ship", ship.transform), ("target", planet.transform));
            new SerializedObject(tracker) { }.Also(so => { so.FindProperty("targetRadius").floatValue = PlanetRadius; so.ApplyModifiedPropertiesWithoutUndo(); });

            var field = new GameObject("AsteroidField").AddComponent<AsteroidField>();
            var fso = new SerializedObject(field);
            var pf = fso.FindProperty("prefabs");
            var rocks = Enumerable.Range(1, 5).Select(i => AssetDatabase.LoadAssetAtPath<GameObject>($"{KitPrefabs}Asteroids/Asteroid {i}.prefab")).Where(g => g).ToList();
            pf.arraySize = rocks.Count;
            for (int i = 0; i < rocks.Count; i++) pf.GetArrayElementAtIndex(i).objectReferenceValue = rocks[i];
            fso.FindProperty("ship").objectReferenceValue = ship.transform;
            fso.FindProperty("routeStart").vector3Value = Vector3.zero;
            fso.FindProperty("routeEnd").vector3Value = PlanetPos;
            fso.ApplyModifiedPropertiesWithoutUndo();

            // ---- UI ---------------------------------------------------------------
            var overlay = B.BuildCockpitOverlay();
            var hud = BuildHud(overlay.transform, ship.transform);

            // ---- Director ------------------------------------------------------------
            var directorGo = new GameObject("Scene02Director");
            var subtitles = directorGo.AddComponent<GreyboxSubtitles>();
            var director = directorGo.AddComponent<Scene02Director>();
            B.SetRefs(director,
                ("subtitles", subtitles),
                ("recovery", dlg["Recovery"]), ("scan", dlg["Scan"]), ("tutorial", dlg["Tutorial"]),
                ("flightMemory", dlg["Flight_Memory"]), ("flightMusic", dlg["Flight_Music"]), ("flightSignal", dlg["Flight_Signal"]),
                ("nags", dlg["Nags"]), ("nagSun", dlg["Nag_Sun"]), ("asteroidBump", dlg["Asteroid"]),
                ("malfunction", dlg["Malfunction"]), ("crash", dlg["Crash"]),
                ("flight", flight), ("tracker", tracker), ("asteroids", field), ("effects", effects),
                ("sun", sun.transform), ("exteriorModel", exterior),
                ("brain", brain), ("shotPov", pov), ("shotCrashExterior", crashCam),
                ("overlay", overlay), ("hud", hud), ("entryVolume", ev));

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            int s1 = scenes.FindIndex(s => s.path.EndsWith("Scene01_Awakening.unity"));
            scenes.Insert(s1 >= 0 ? s1 + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SpaceRat] Built {ScenePath}");
        }

        static GameObject BuildPlanet(UnityEngine.SceneManagement.Scene scene)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPrefabs + "Planet and Star/Planet 1.prefab");
            GameObject planet;
            if (prefab)
            {
                planet = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                planet.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(12f, 30f, 8f));
                planet.transform.localScale = Vector3.one;
                // Size from the planet sphere itself (not the ring).
                var sphere = planet.GetComponentsInChildren<Renderer>(true)
                    .Where(r => r.name.StartsWith("Planet") && !(r is ParticleSystemRenderer))
                    .OrderByDescending(r => r.bounds.size.magnitude).FirstOrDefault();
                float d = sphere ? Mathf.Max(sphere.bounds.size.x, sphere.bounds.size.y, sphere.bounds.size.z) : 1f;
                planet.transform.localScale = Vector3.one * (PlanetRadius * 2f / Mathf.Max(0.001f, d));
                foreach (var t in planet.GetComponentsInChildren<Transform>(true))
                    if (t.name.Contains("VFX")) t.gameObject.SetActive(false);
                TintPlanet(planet);
            }
            else
            {
                planet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(planet.GetComponent<Collider>());
                planet.transform.localScale = Vector3.one * PlanetRadius * 2f;
            }
            planet.name = "Planet_SignalSource";
            planet.transform.position = PlanetPos;
            return planet;
        }

        /// <summary>Cool green-teal look (copies of the kit materials, so the vendor files stay untouched).</summary>
        static void TintPlanet(GameObject planet)
        {
            foreach (var r in planet.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Asteroid")) continue;
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (!m) continue;
                    if (m.name == "Planet 1")
                    {
                        mats[i] = CopyMat(m, "M_SignalPlanet", c => { c.SetColor("_BaseColor", new Color(0.34f, 0.72f, 0.66f)); c.SetFloat("_Smoothness", 0.25f); });
                        changed = true;
                    }
                    else if (m.name == "Atmosphere Material")
                    {
                        mats[i] = CopyMat(m, "M_SignalAtmosphere", c => c.SetColor("_Atmosphere_Color", new Color(0.08f, 0.32f, 0.26f)));
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static Material CopyMat(Material src, string name, System.Action<Material> edit)
        {
            string path = $"{B.MatDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(src) { name = name }; AssetDatabase.CreateAsset(mat, path); }
            else mat.CopyPropertiesFromMaterial(src);
            edit(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// The radar art has a solid black background: cut it out by brightness, but keep the radar
        /// screen itself semi-dark so the grid reads over space. Writes Assets/SpaceRat/Art/UI/Radar_HUD.png.
        /// </summary>
        static Sprite BuildRadarSprite()
        {
            string src = System.IO.Path.GetFullPath(RadarSourcePng);
            if (!System.IO.File.Exists(src)) { Debug.LogWarning($"[SpaceRat] Radar art not found at {src}"); return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(src));
            int w = tex.width, h = tex.height;
            var px = tex.GetPixels32();
            Vector2 c = new(0.423f * w, 0.512f * h);
            float screenR = 0.215f * w;
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                float lum = Mathf.Max(p.r, Mathf.Max(p.g, p.b)) / 255f;
                float a = Mathf.Clamp01((lum - 0.04f) * 3f);
                float d = Vector2.Distance(new Vector2(i % w, i / w), c);
                if (d < screenR) a = Mathf.Max(a, 0.72f);         // dark radar glass
                px[i] = new Color32(p.r, p.g, p.b, (byte)(a * 255f));
            }
            tex.SetPixels32(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(RadarSpritePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(RadarSpritePath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(RadarSpritePath);
            ti.textureType = TextureImporterType.Sprite;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(RadarSpritePath);
        }

        static VolumeProfile BuildEntryProfile()
        {
            var p = B.NewProfile($"{B.SettingsDir}/Scene02_EntryVolume.asset");
            var ca = B.AddOverride<ColorAdjustments>(p);
            ca.colorFilter.Override(new Color(1f, 0.55f, 0.3f));
            ca.postExposure.Override(0.6f);
            ca.saturation.Override(15f);
            var bloom = B.AddOverride<Bloom>(p);
            bloom.intensity.Override(4f);
            bloom.threshold.Override(0.8f);
            var vig = B.AddOverride<Vignette>(p);
            vig.intensity.Override(0.45f);
            vig.color.Override(new Color(0.35f, 0.08f, 0f));
            var cab = B.AddOverride<ChromaticAberration>(p);
            cab.intensity.Override(0.6f);
            AssetDatabase.SaveAssets();
            return p;
        }

        static Scene02Hud BuildHud(Transform canvasRoot, Transform ship)
        {
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var cyan = new Color(0.45f, 0.95f, 1f);

            var root = new GameObject("Scene02HUD", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(canvasRoot, false);
            root.anchorMin = Vector2.zero; root.anchorMax = Vector2.one;
            root.offsetMin = root.offsetMax = Vector2.zero;
            root.SetSiblingIndex(1);   // above the cockpit art, below eyelid/flash/title

            // Bearing marker: diamond + label, centred anchor (positioned by script).
            var marker = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
            marker.SetParent(root, false);
            marker.anchorMin = marker.anchorMax = new Vector2(0.5f, 0.5f);
            marker.sizeDelta = new Vector2(40, 40);
            var diamond = B.UiImage("Diamond", marker, new Color(cyan.r, cyan.g, cyan.b, 0.9f));
            diamond.rectTransform.anchorMin = diamond.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            diamond.rectTransform.sizeDelta = new Vector2(20, 20);
            diamond.rectTransform.localRotation = Quaternion.Euler(0, 0, 45f);
            var label = B.UiText("Label", marker, font, 18, TextAnchor.UpperCenter, cyan);
            label.text = "SIGNAL";
            label.rectTransform.offsetMin = new Vector2(-60, -44);
            label.rectTransform.offsetMax = new Vector2(60, -22);

            var nav = B.UiText("Nav", root, font, 24, TextAnchor.UpperLeft, cyan);
            var navRt = nav.rectTransform;
            navRt.anchorMin = navRt.anchorMax = new Vector2(0f, 1f);
            navRt.pivot = new Vector2(0f, 1f);
            navRt.anchoredPosition = new Vector2(40, -40);
            navRt.sizeDelta = new Vector2(420, 120);
            nav.gameObject.AddComponent<Shadow>();

            var status = B.UiText("Status", root, font, 22, TextAnchor.UpperRight, new Color(1f, 0.35f, 0.3f));
            var stRt = status.rectTransform;
            stRt.anchorMin = stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(1f, 1f);
            stRt.anchoredPosition = new Vector2(-40, -300);   // below the radar
            stRt.sizeDelta = new Vector2(560, 80);
            status.gameObject.AddComponent<Shadow>();

            var boost = new GameObject("Boost", typeof(RectTransform)).GetComponent<RectTransform>();
            boost.SetParent(root, false);
            boost.anchorMin = boost.anchorMax = new Vector2(0.5f, 1f);
            boost.pivot = new Vector2(0.5f, 1f);
            boost.anchoredPosition = new Vector2(0, -40);
            boost.sizeDelta = new Vector2(420, 34);
            var bg = B.UiImage("BoostBg", boost, new Color(0f, 0f, 0f, 0.55f));
            bg.rectTransform.anchorMin = new Vector2(0, 0); bg.rectTransform.anchorMax = new Vector2(1, 0);
            bg.rectTransform.pivot = new Vector2(0.5f, 0); bg.rectTransform.sizeDelta = new Vector2(0, 12);
            var fill = B.UiImage("BoostFill", bg.rectTransform, cyan);
            var bl = B.UiText("BoostLabel", boost, font, 18, TextAnchor.UpperCenter, cyan);
            bl.text = "BOOST";

            // Radar, top right (nose radar). Dot + distance readout in the side tab.
            var radarSprite = BuildRadarSprite();
            var radarImg = B.UiImage("Radar", root, Color.white);
            radarImg.sprite = radarSprite;
            radarImg.preserveAspect = true;
            var radar = radarImg.rectTransform;
            radar.anchorMin = radar.anchorMax = new Vector2(1f, 1f);
            radar.pivot = new Vector2(1f, 1f);
            radar.anchoredPosition = new Vector2(-24, -20);
            radar.sizeDelta = new Vector2(390, 260);
            var dot = B.UiImage("RadarDot", radar, new Color(1f, 0.85f, 0.25f));
            var dotRt = dot.rectTransform;
            dotRt.anchorMin = dotRt.anchorMax = new Vector2(0.5f, 0.5f);
            dotRt.sizeDelta = new Vector2(14, 14);
            dotRt.localRotation = Quaternion.Euler(0, 0, 45f);
            var radarText = B.UiText("RadarText", radar, font, 20, TextAnchor.MiddleCenter, cyan);
            var rtRt = radarText.rectTransform;
            rtRt.anchorMin = new Vector2(0.72f, 0.40f);
            rtRt.anchorMax = new Vector2(0.93f, 0.58f);
            rtRt.offsetMin = rtRt.offsetMax = Vector2.zero;
            radarText.text = "--";

            var hud = root.gameObject.AddComponent<Scene02Hud>();
            B.SetRefs(hud, ("canvasRect", (RectTransform)canvasRoot), ("marker", marker), ("markerLabel", label),
                ("navText", nav), ("statusText", status), ("boostFill", fill), ("boostRoot", boost.gameObject),
                ("radar", radar), ("radarDot", dotRt), ("radarText", radarText), ("ship", ship));
            return hud;
        }

        // ---- Dialogue (tone rule: sharp, sarcastic pilot; deadpan computer) -------------------

        static (Speaker, string, float) C(string t) => (Speaker.Computer, t, Dur(t));
        static (Speaker, string, float) P(string t) => (Speaker.Pilot, t, Dur(t));
        static float Dur(string t) => Mathf.Clamp(1.1f + t.Split(' ').Length * 0.3f, 1.6f, 4.8f);

        static Dictionary<string, DialogueSequence> EnsureDialogue()
        {
            string S(string n) => DialogueSub + "/" + n;
            return new Dictionary<string, DialogueSequence>
            {
                ["Recovery"] = B.EnsureSequence(S("Recovery"),
                    P("Okay. Not on fire. Small wins."),
                    C("Hull temperature returning to survivable levels."),
                    P("\"Survivable.\" Love the bar we're setting."),
                    P("Fine. Forget who I am. Where are we?"),
                    C("Current position unknown. Navigation database inaccessible."),
                    P("So you don't know who I am, where we are, or why we're here."),
                    C("That is an accurate summary."),
                    P("Fantastic. What do you know?"),
                    C("We were recently on a collision course with a star."),
                    P("Yeah, I was there for that part."),
                    C("I appear to have initialised shortly before the collision warning. My earlier operational history is also missing."),
                    P("So we both just woke up."),
                    C("That is a reasonable hypothesis. I cannot verify it."),
                    P("Great. Two amnesiacs in a broken spaceship. Sounds like the start of a bad joke."),
                    C("I do not know any jokes. That data is also missing."),
                    P("Somehow that's the saddest thing you've said.")),
                ["Scan"] = B.EnsureSequence(S("Scan"),
                    P("Scan the area. Anything that helps us not die."),
                    C("Initiating passive scan. Sensors operating below nominal capacity."),
                    P("Join the club."),
                    C("...One signal detected. Extremely weak. Origin: a nearby planetary body."),
                    P("A distress beacon?"),
                    C("Unknown. It may be artificial. It may also be interference."),
                    P("\"Maybe help, maybe static.\" Still better odds than the star. Can we get there?"),
                    C("Warp drive offline. Conventional thrusters available, with limitations."),
                    P("Of course they are. Mark the bearing."),
                    C("Bearing marked. I recommend avoiding further stellar encounters."),
                    P("Wow. Wish I'd thought of that.")),
                ["Tutorial"] = B.EnsureSequence(S("Tutorial"),
                    C("Thruster overdrive available. Hold Shift. Do not hold it forever."),
                    P("What happens if I hold it forever?"),
                    C("It stops. Then you are disappointed.")),
                ["Flight_Memory"] = B.EnsureSequence(S("Flight_Memory"),
                    P("Any idea what wiped our memories?"),
                    C("No. The missing records are remarkably thorough."),
                    P("That's not reassuring."),
                    C("It was not intended to be.")),
                ["Flight_Music"] = B.EnsureSequence(S("Flight_Music"),
                    P("So... got any music in there?"),
                    C("Audio archives missing."),
                    P("Of course they are."),
                    C("I can play the collision alarm."),
                    P("Please don't.")),
                ["Flight_Signal"] = B.EnsureSequence(S("Flight_Signal"),
                    C("Signal strength increasing. We are approaching the source."),
                    P("Finally, good news. Write that down. Oh wait, you can't.")),
                ["Nags"] = B.EnsureSequence(S("Nags"),
                    C("The planet is the other way. The large, round one."),
                    C("You are drifting off course. Recalculating my opinion of you."),
                    C("The glowing marker is the destination. I added it specifically for you.")),
                ["Nag_Sun"] = B.EnsureSequence(S("Nag_Sun"),
                    C("We have already tried the star. I did not enjoy it.")),
                ["Asteroid"] = B.EnsureSequence(S("Asteroid"),
                    C("Minor impact. Please stop hitting rocks."),
                    P("The rock hit me.")),
                ["Malfunction"] = B.EnsureSequence(S("Malfunction"),
                    C("Warning. Uncommanded power fluctuation."),
                    P("Oh, come ON."),
                    C("Flight controls are no longer responding. Engine output unstable."),
                    P("Give me manual override!"),
                    C("Manual override unavailable."),
                    P("You said we could make it!"),
                    C("I said we could attempt to reach the planet."),
                    P("This is a terrible time to be precise!"),
                    C("Atmospheric entry imminent. Brace for impact."),
                    P("Bracing! Whatever that means!")),
                ["Crash"] = B.EnsureSequence(S("Crash"),
                    (Speaker.Computer, "Pilot? ... Please respond.", 4.5f)),
            };
        }
    }

    static class ObjectExt
    {
        public static T Also<T>(this T obj, System.Action<T> act) { act(obj); return obj; }
    }
}
