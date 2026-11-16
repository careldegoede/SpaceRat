using System.Collections.Generic;
using SpaceRat.Dialogue;
using SpaceRat.Scene03;
using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using B = SpaceRat.EditorTools.Scene01GreyboxBuilder;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Scene 3 island (menu: SpaceRat > Build Scene03 Terrain): a 2 x 2 km procedural island at dusk.
    /// - Crash crater in the south-west, a ridge across the middle, a flattened pad for the outpost in the
    ///   north-east (~1.3 km away, hidden behind the ridge).
    /// - Edges slope under an ocean (Procedural Water Shader) with an irregular coast and two bays (NW, SE).
    /// - Ground painted with tinted copies of Terrain Sample Asset Pack layers (generated fallback).
    /// - Flyable test cube with a chase camera (stays above ground and water).
    /// </summary>
    public static class Scene03Builder
    {
        const string ScenePath = B.Root + "/Scenes/Scene03_Terrain.unity";
        const string TerrainDir = B.Root + "/Terrain";
        const string SampleLayers = "Assets/TerrainSampleAssets/TerrainLayers/";
                const string WaterShader = "FX/ProceduralWater";

        const float Size = 2000f;          // metres (x and z)
        const float Height = 260f;         // vertical range of the heightmap
        const float SeaDepth = 50f;        // terrain base sits this far below sea level (world y = 0)
        const int Res = 513;
        const int Seed = 1977;

        public static readonly Vector2 CraterXZ = new(-500f, -500f);
        public static readonly Vector2 OutpostXZ = new(450f, 450f);
        const float CraterRadius = 90f;
        const float OutpostPadRadius = 45f;

        static float SeaH => SeaDepth / Height;                     // normalised height of sea level

        [MenuItem("SpaceRat/Build Scene03 Terrain")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            B.EnsureFolder(TerrainDir);
            B.EnsureFolder(B.Root + "/Scenes");

            // ---- Dusk sky, sun, fog ------------------------------------------------------
            RenderSettings.skybox = DuskSky();
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 1.4f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.36f, 0.30f, 0.33f);
            RenderSettings.fogStartDistance = 250f;
            RenderSettings.fogEndDistance = 2400f;

            var sun = new GameObject("Sun_Dusk").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.6f, 0.38f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(19f, 75f, 0f);    // low, from the west
            RenderSettings.sun = sun;

            // ---- Island + ocean ------------------------------------------------------------
            var terrain = BuildTerrain();
            BuildOcean();

            // Placeholder for the outpost (prefab extracted later) and the crash site marker.
            var outpostSite = new GameObject("OutpostSite");
            outpostSite.transform.position = Ground(terrain, OutpostXZ);
            var crashSite = new GameObject("CrashSite");
            crashSite.transform.position = Ground(terrain, CraterXZ);

            // ---- Crashed Sparrow in the crater ------------------------------------------------
            BuildCrashedShip(terrain, scene);

            // ---- Pilot on foot (full-body first person) ------------------------------------------
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 9000f;
            camGo.AddComponent<AudioListener>();
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

            // Start behind the wreck, looking at its engines (spot picked by the user in play).
            Vector2 standXZ = CraterXZ + new Vector2(3.7f, 5.8f);
            var pilot = BuildPilot(scene, cam, Ground(terrain, standXZ), 232f);
            if (!pilot)
            {
                // Fallback: the fly cube, if the character pack isn't available.
                var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "PlayerCube";
                Object.DestroyImmediate(cube.GetComponent<Collider>());
                cube.GetComponent<Renderer>().sharedMaterial = CubeMaterial();
                cube.transform.SetPositionAndRotation(Ground(terrain, standXZ) + Vector3.up * 20f, Quaternion.Euler(0f, 45f, 0f));
                B.SetRefs(cube.AddComponent<FlyController>(), ("terrain", terrain));
                cam.transform.SetParent(cube.transform, false);
                cam.transform.localPosition = new Vector3(0f, 3.5f, -10f);
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(B.SettingsDir + "/Scene01_GlobalVolume.asset");
            if (profile)
            {
                var vol = new GameObject("GlobalVolume").AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            BuildOutpost(scene);
            AddDirector(terrain, scene);

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            int s2 = scenes.FindIndex(s => s.path.EndsWith("Scene02_TheSignal.unity"));
            scenes.Insert(s2 >= 0 ? s2 + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SpaceRat] Built {ScenePath}");
        }

        // ---- Dialogue + director -------------------------------------------------------------

        /// <summary>Adds (or replaces) the Scene 3 director, wrecked cockpit, compass and dialogue in Scene03, without rebuilding the island.</summary>
        [MenuItem("SpaceRat/Add Scene03 Dialogue + Cockpit")]
        public static void AddDialogueToOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            AddDirector(Object.FindAnyObjectByType<Terrain>(), scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SpaceRat] Scene03 dialogue added");
        }

        static void AddDirector(Terrain terrain, UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("Scene03Director");
            if (old) Object.DestroyImmediate(old);

            var dlg = EnsureDialogue();
            var outpostWired = WireOutpostGameplay(terrain, scene);
            var cockpit = BuildWreckCockpit(scene);
            var go = new GameObject("Scene03Director");
            var box = go.AddComponent<DialogueBox>();
            box.Style = DialogueBoxStyle.HoloCapsule;
            var walker = Object.FindAnyObjectByType<SpaceRat.Characters.FirstPersonWalker>();
            var outpost = GameObject.Find("OutpostSite");
            var crash = GameObject.Find("CrashSite");
            if (!outpost && terrain) { outpost = new GameObject("OutpostSite"); outpost.transform.position = Ground(terrain, OutpostXZ); }
            if (!crash && terrain) { crash = new GameObject("CrashSite"); crash.transform.position = Ground(terrain, CraterXZ); }

            // Scene 1's cockpit overlay (controls art + eyelids) for the pilot's own view.
            foreach (var o in Object.FindObjectsByType<SpaceRat.UI.CockpitOverlay>(FindObjectsInactive.Include))
                Object.DestroyImmediate(o.gameObject);
            var overlay = B.BuildCockpitOverlay();

            var compass = go.AddComponent<SpaceRat.UI.CompassHud>();
            B.SetRefs(compass,
                ("dial", HudTexture("CompassDial")), ("panel", HudTexture("CompassPanel")),
                ("viewer", walker ? walker.transform : null),
                ("target", outpostWired.entrance ? outpostWired.entrance : (outpost ? outpost.transform : null)));

            // Health / stamina / weapon charge: vitals on the pilot, status bar top-left.
            SpaceRat.Characters.PlayerVitals vitals = null;
            if (walker)
            {
                vitals = walker.GetComponent<SpaceRat.Characters.PlayerVitals>();
                if (!vitals) vitals = walker.gameObject.AddComponent<SpaceRat.Characters.PlayerVitals>();
                B.SetRefs(vitals, ("walker", walker));
            }
            var hud = go.AddComponent<SpaceRat.UI.PlayerHud>();
            B.SetRefs(hud, ("frame", HudTexture("HudBar")), ("vitals", vitals));

            // Inventory + "press E" on the pilot.
            SpaceRat.Characters.Inventory inventory = null;
            if (walker)
            {
                inventory = walker.GetComponent<SpaceRat.Characters.Inventory>();
                if (!inventory) inventory = walker.gameObject.AddComponent<SpaceRat.Characters.Inventory>();
                var interactor = walker.GetComponent<SpaceRat.Characters.PlayerInteractor>();
                if (!interactor) interactor = walker.gameObject.AddComponent<SpaceRat.Characters.PlayerInteractor>();
                B.SetRefs(interactor, ("walker", walker), ("view", Camera.main));
            }
            T Find<T>(string name) where T : Component { var g = GameObject.Find(name); return g ? g.GetComponent<T>() : null; }

            var director = go.AddComponent<Scene03Director>();
            B.SetRefs(director,
                ("dialogue", box),
                ("pilot", walker),
                ("outpost", outpostWired.entrance ? outpostWired.entrance : (outpost ? outpost.transform : null)),
                ("crashSite", crash ? crash.transform : null),
                ("cockpitSet", cockpit.set), ("cockpitCamera", cockpit.cam), ("mainCamera", Camera.main),
                ("cockpitPovCamera", cockpit.pov), ("seatedPilotModel", cockpit.pilotModel), ("overlay", overlay),
                ("cockpitShell", cockpit.shell), ("povBackdrop", cockpit.povBackdrop),
                ("seatedPilot", cockpit.seating), ("compass", compass), ("hud", hud),
                ("wakeCall", dlg["WakeCall"]), ("wake", dlg["Wake"]), ("exit", dlg["Exit"]),
                ("walkFusionCell", dlg["Walk_FusionCell"]), ("walkNavCore", dlg["Walk_NavCore"]),
                ("walkSignal", dlg["Walk_Signal"]), ("nags", dlg["Nags"]),
                ("nagBackToShip", dlg["Nag_BackToShip"]), ("settlement", dlg["Settlement"]),
                ("arrival", dlg["Arrival"]), ("pickup", dlg["Pickup"]),
                ("terminalScan", dlg["Terminal_Scan"]),
                ("captainLogs", dlg["Captain_Logs"]), ("logsReaction", dlg["Logs_Reaction"]),
                ("botActivate", dlg["Bot_Activate"]), ("botAwake", dlg["Bot_Awake"]),
                ("navReminder", dlg["Nav_Reminder"]), ("outro", dlg["Outro"]), ("botFarewell", dlg["Bot_Farewell"]),
                ("inventory", inventory),
                ("navPickup", Find<SpaceRat.Characters.Interactable>("NavModule")),
                ("captainChair", Find<SpaceRat.Characters.Interactable>("CaptainChair")),
                ("botSwitch", Find<SpaceRat.Characters.Interactable>("ServiceBot")),
                ("serviceBot", Find<SpaceRat.Characters.ServiceBot>("ServiceBot")),
                ("consoleCamera", Find<Camera>("ConsoleCamera")),
                ("consoleScreenGlow", GameObject.Find("ConsoleScreenGlow")),
                ("botPortrait", HudTexture("BotPortrait")));
            var dso = new SerializedObject(director);
            var hide = dso.FindProperty("hideOnStart");
            hide.arraySize = outpostWired.hide.Count;
            for (int i = 0; i < outpostWired.hide.Count; i++) hide.GetArrayElementAtIndex(i).objectReferenceValue = outpostWired.hide[i];
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- Hand-built outpost (Assets/DDG/MyOutpost.prefab) -------------------------------------

        /// <summary>
        /// Gameplay for the hand-built MyOutpost, anchored on its placeholders (the prefab itself is not edited):
        /// "pilot seat for computer access" (terminal: manifest + logs), "Add here the navigation part…"
        /// (navigation module pickup), "Place Bot here" (dormant service unit on the roof). Placeholders and the
        /// a DoorOpener opens the doors for the Player. Flattens the terrain under the outpost.
        /// Lives under its own root "OutpostGameplay" so re-running never touches the prefab.
        /// </summary>
        static (Transform entrance, List<GameObject> hide) WireOutpostGameplay(Terrain terrain, UnityEngine.SceneManagement.Scene scene)
        {
            var hide = new List<GameObject>();
            var my = GameObject.Find("MyOutpost");
            if (!my) return (null, hide);

            var old = GameObject.Find("OutpostGameplay");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("OutpostGameplay").transform;

            Transform Child(string contains)
            {
                foreach (var t in my.GetComponentsInChildren<Transform>(true))
                    if (t.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) >= 0) return t;
                return null;
            }
            var seatPh = Child("computer access");
            var navPh = Child("navigation part");
            var botPh = Child("Place Bot here");
            foreach (var ph in new[] { seatPh, navPh, botPh }) if (ph) hide.Add(ph.gameObject);

            // Doors open for the Player (tag "Player") using their own animators.
            var doors = root.gameObject.AddComponent<SpaceRat.Environment.DoorOpener>();
            B.SetRefs(doors, ("root", my.transform));

            // Walk target / compass: the main entrance (door_3), else the outpost itself.
            var door = Child("door_3");
            var entrance = new GameObject("OutpostEntrance").transform;
            entrance.SetParent(root, false);
            entrance.position = door ? door.position : my.transform.position;

            // Terminal seat: sitting here plays the manifest scan and the captain's logs.
            if (seatPh)
            {
                var seat = new GameObject("CaptainChair");
                seat.transform.SetParent(root, false);
                seat.transform.position = seatPh.position;
                var it = seat.AddComponent<SpaceRat.Characters.Interactable>();
                var so = new SerializedObject(it);
                so.FindProperty("prompt").stringValue = "SIT  ·  STATION TERMINAL";
                so.FindProperty("range").floatValue = 2.4f;
                so.ApplyModifiedPropertiesWithoutUndo();

                // Seated view towards the desk / terminal.
                var desk = Child("desk");
                Vector3 eye = seatPh.position + Vector3.up * 0.25f;
                Vector3 look = desk ? desk.position + Vector3.up * 1.05f : eye + seatPh.forward;
                var camGo = new GameObject("ConsoleCamera");
                camGo.transform.SetParent(root, false);
                camGo.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
                var cam = camGo.AddComponent<Camera>();
                cam.fieldOfView = 58f;
                cam.nearClipPlane = 0.05f;
                cam.depth = 2f;
                cam.enabled = false;
                var data = cam.GetUniversalAdditionalCameraData();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                var glow = new GameObject("ConsoleScreenGlow");
                glow.transform.SetParent(root, false);
                glow.transform.position = look + Vector3.up * 0.2f + (eye - look).normalized * 0.4f;
                var gl = glow.AddComponent<Light>();
                gl.type = LightType.Point;
                gl.color = new Color(1f, 0.7f, 0.3f);
                gl.intensity = 2.2f;
                gl.range = 3.5f;
            }

            // Navigation module on the lounge table (a small capacitor with a cyan glow).
            if (navPh)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modular + "Machines/Capacitor.prefab");
                GameObject nav = prefab ? (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene) : GameObject.CreatePrimitive(PrimitiveType.Cube);
                nav.name = "NavModule";
                nav.transform.SetParent(root, false);
                nav.transform.position = navPh.position;
                var b = new Bounds(nav.transform.position, Vector3.zero);
                foreach (var r in nav.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                float size = Mathf.Max(b.size.x, b.size.y, b.size.z);
                if (size > 0.01f) nav.transform.localScale *= 0.45f / size;
                foreach (var c in nav.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
                var light = new GameObject("NavGlow").AddComponent<Light>();
                light.transform.SetParent(nav.transform, false);
                light.transform.position = navPh.position + Vector3.up * 0.5f;
                light.type = LightType.Point;
                light.color = new Color(0.3f, 0.85f, 1f);
                light.intensity = 2f;
                light.range = 2.5f;
                MakeInteractable(nav, "TAKE NAVIGATION MODULE", 2.4f, new Vector3(0f, 0.2f, 0f));
            }

            // Dormant service unit on the roof.
            if (botPh)
            {
                var botPrefab = EnsureServiceBotPrefab();
                if (botPrefab)
                {
                    var bot = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab, scene);
                    bot.name = "ServiceBot";
                    bot.transform.SetParent(root, false);
                    bot.transform.position = botPh.position - Vector3.up * 0.6f;
                    bot.transform.rotation = Quaternion.Euler(0f, my.transform.eulerAngles.y + 90f, 0f);
                    B.SetRefs(bot.GetComponent<SpaceRat.Characters.ServiceBot>(), ("terrain", terrain));
                    var it = MakeInteractable(bot, "ACTIVATE SERVICE UNIT", 2.8f, new Vector3(0f, 0.5f, 0f));
                    it.Available = false;
                }
            }

            FlattenUnder(my, terrain);
            return (entrance, hide);
        }

        /// <summary>Flattens the terrain under a structure to its base height, blending out over a margin.</summary>
        static void FlattenUnder(GameObject structure, Terrain terrain)
        {
            if (!terrain) return;
            var bounds = new Bounds(structure.transform.position, Vector3.zero);
            foreach (var r in structure.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var data = terrain.terrainData;
            Vector3 tp = terrain.transform.position;
            float baseY = structure.transform.position.y - 0.05f;
            float target = Mathf.Clamp01((baseY - tp.y) / data.size.y);
            const float margin = 18f;
            int res = data.heightmapResolution;
            float cell = data.size.x / (res - 1);
            int x0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.x - margin - tp.x) / cell), 0, res - 1);
            int x1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.x + margin - tp.x) / cell), 0, res - 1);
            int z0 = Mathf.Clamp(Mathf.FloorToInt((bounds.min.z - margin - tp.z) / cell), 0, res - 1);
            int z1 = Mathf.Clamp(Mathf.CeilToInt((bounds.max.z + margin - tp.z) / cell), 0, res - 1);
            int w = x1 - x0 + 1, h = z1 - z0 + 1;
            var heights = data.GetHeights(x0, z0, w, h);
            for (int z = 0; z < h; z++)
            for (int x = 0; x < w; x++)
            {
                float wx = tp.x + (x0 + x) * cell, wz = tp.z + (z0 + z) * cell;
                float dx = Mathf.Max(0f, Mathf.Max(bounds.min.x - wx, wx - bounds.max.x));
                float dz = Mathf.Max(0f, Mathf.Max(bounds.min.z - wz, wz - bounds.max.z));
                float k = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Sqrt(dx * dx + dz * dz) / margin);
                heights[z, x] = Mathf.Lerp(heights[z, x], target, k);
            }
            data.SetHeights(x0, z0, heights);
            EditorUtility.SetDirty(data);
            Debug.Log($"[SpaceRat] Flattened terrain under {structure.name} to y={baseY:0.0}");
        }

        static Texture2D HudTexture(string name)
        {
            string path = $"{B.Root}/Art/HUD/{name}.png";
            if (AssetImporter.GetAtPath(path) is TextureImporter imp && (imp.mipmapEnabled || !imp.alphaIsTransparency))
            {
                imp.textureType = TextureImporterType.Default;
                imp.mipmapEnabled = false;
                imp.alphaIsTransparency = true;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---- Outpost (remote station) ----------------------------------------------------------

        const string Modular = "Assets/Sci-Fi Styled Modular Pack/Prefabs/";

        /// <summary>Rebuilds just the outpost on the flattened pad in the open Scene03.</summary>
        [MenuItem("SpaceRat/Build Scene03 Outpost")]
        public static void BuildOutpostInOpenScene()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath);
            BuildOutpost(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[SpaceRat] Scene03 outpost built");
        }

        /// <summary>
        /// Kitbashed from the Sci-Fi Styled Modular Pack (4 m grid): an open-frame hub with a round open door facing
        /// the crater (local +z), a storage block, a generator yard, crates, a tall antenna mast with a red beacon,
        /// floodlights — lights on, nobody home. A spare fusion cell glows just inside the door (parts quest seed).
        /// </summary>
        static GameObject BuildOutpost(UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("Outpost");
            if (old && old.transform.Find("MyOutpost"))
            {
                Debug.Log("[SpaceRat] Outpost contains the hand-built MyOutpost prefab — leaving it alone.");
                return old;
            }
            if (old) Object.DestroyImmediate(old);
            var site = GameObject.Find("OutpostSite");
            if (!site || !AssetDatabase.IsValidFolder(Modular.TrimEnd('/'))) return null;

            var root = new GameObject("Outpost").transform;
            // Face the crater: local +z points from the outpost towards the crash site.
            Vector3 toCrater = new Vector3(CraterXZ.x - OutpostXZ.x, 0f, CraterXZ.y - OutpostXZ.y);
            root.SetPositionAndRotation(site.transform.position, Quaternion.LookRotation(toCrater.normalized, Vector3.up));

            // Hub: cross of corridors; front arm gets the open round door, the other arms are walled.
            Mod(root, scene, "Corridors/Corridor_X", new Vector3(0f, 0f, 0f), 0f);
            var door = Mod(root, scene, "Doors/door_3", new Vector3(0f, 0f, 6f), 0f);
            if (door)
            {
                // Standing open: remove the sliding leaves (and their colliders), keep the round frame.
                PrefabUtility.UnpackPrefabInstance(door, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var leaves = new List<GameObject>();
                foreach (Transform t in door.transform)
                    if (t.name.Contains("_top_") || t.name.Contains("_bottom_")) leaves.Add(t.gameObject);
                foreach (var leaf in leaves) Object.DestroyImmediate(leaf);
            }
            Mod(root, scene, "Walls/wall_big", new Vector3(0f, 0f, -6f), 180f);
            Mod(root, scene, "Walls/wall_big", new Vector3(6f, 0f, 0f), 90f);
            Mod(root, scene, "Walls/wall_big", new Vector3(-6f, 0f, 0f), -90f);

            // Storage block (two room cubes) to the left, generator yard to the right.
            Mod(root, scene, "Corridors/Corridor_1", new Vector3(-14f, 0f, 2f), 0f);
            Mod(root, scene, "Corridors/Corridor_1", new Vector3(-14f, 0f, -2f), 0f);
            Mod(root, scene, "Doors/door_1", new Vector3(-14f, 0f, 4f), 0f);
            Mod(root, scene, "Machines/generator", new Vector3(13f, 0f, 1f), 0f);
            Mod(root, scene, "Machines/Battery_big", new Vector3(13f, 0f, -5f), 90f);
            Mod(root, scene, "Machines/container_big", new Vector3(9f, 0f, 9f), 20f);
            Mod(root, scene, "Machines/container_small", new Vector3(11.5f, 0f, 10.5f), -10f);
            Mod(root, scene, "Machines/storage_container_big", new Vector3(-9f, 0f, 10f), 0f);
            Mod(root, scene, "Machines/storage_container_big", new Vector3(-9f, 1.07f, 10f), 15f);
            Mod(root, scene, "Machines/storage_container_small", new Vector3(-12.5f, 0f, 9f), 40f);
            Mod(root, scene, "Machines/shield_generator", new Vector3(0f, 0f, -16f), 0f);

            // Spare fusion cell at the back of the hub, under a cyan glow.
            var cell = Mod(root, scene, "Machines/Battery", new Vector3(0f, 0f, -3.7f), 30f);
            if (cell)
            {
                cell.name = "FusionCell_Spare";
                cell.transform.localScale = Vector3.one * 0.55f;
                MakeInteractable(cell, "TAKE FUSION CELL", 2.8f, new Vector3(0f, 0.8f, 0f));
            }
            OutpostLight("CellGlow", root, new Vector3(0f, 1.6f, -3.7f), new Color(0.3f, 0.85f, 1f), 2.5f, 4f);

            // Captain's chair facing her terminal in the west arm (logs).
            var chair = AssetDatabase.LoadAssetAtPath<GameObject>(B.PilotSeatPath);
            if (chair)
            {
                var seat = (GameObject)PrefabUtility.InstantiatePrefab(chair, scene);
                seat.name = "CaptainChair";
                seat.transform.SetParent(root, false);
                seat.transform.localPosition = new Vector3(-2.5f, 0f, 0f);
                seat.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                seat.transform.localScale = Vector3.one * B.PilotSeatScale;
                MakeInteractable(seat, "SIT  ·  STATION LOGS", 2.6f, new Vector3(0f, 1.6f, 0f));
            }
            Mod(root, scene, "Decorative elements/computer_station", new Vector3(-4.1f, 0f, 0f), 90f);   // end walls reach in to x = -4.42
            var glowMat = OutpostMaterial("M_LogScreen", new Color(0.2f, 0.12f, 0.02f), new Color(2.2f, 1.3f, 0.35f));
            var glow = B.Primitive("ConsoleScreenGlow", PrimitiveType.Quad, root, new Vector3(-3.78f, 1.55f, 0f), new Vector3(1.05f, 0.62f, 1f), glowMat, new Vector3(0f, -90f, 0f));
            var glowLight = OutpostLight("ScreenLight", glow.transform, Vector3.zero, new Color(1f, 0.7f, 0.3f), 1.6f, 3f);
            glowLight.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            // (the director hides it until the logs play)
            var consoleCamGo = new GameObject("ConsoleCamera");
            consoleCamGo.transform.SetParent(root, false);
            consoleCamGo.transform.localPosition = new Vector3(-2.45f, 1.28f, 0f);
            consoleCamGo.transform.localRotation = Quaternion.LookRotation(new Vector3(-3.78f, 1.42f, 0f) - new Vector3(-2.45f, 1.28f, 0f), Vector3.up);
            var consoleCam = consoleCamGo.AddComponent<Camera>();
            consoleCam.fieldOfView = 55f;
            consoleCam.nearClipPlane = 0.05f;
            consoleCam.depth = 2f;
            consoleCam.enabled = false;
            var ccd = consoleCam.GetUniversalAdditionalCameraData();
            ccd.renderPostProcessing = true;
            ccd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            OutpostLight("WestArmLight", root, new Vector3(-3.2f, 3.2f, 0f), new Color(1f, 0.85f, 0.7f), 1.4f, 6f);

            // Dormant service unit in the east bay (wakes up after the logs).
            var botPrefab = EnsureServiceBotPrefab();
            if (botPrefab)
            {
                var bot = (GameObject)PrefabUtility.InstantiatePrefab(botPrefab, scene);
                bot.name = "ServiceBot";
                bot.transform.SetParent(root, false);
                bot.transform.localPosition = new Vector3(3.6f, 0f, 0f);
                bot.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                var sb = bot.GetComponent<SpaceRat.Characters.ServiceBot>();
                B.SetRefs(sb, ("terrain", Object.FindAnyObjectByType<Terrain>()));
                var it = MakeInteractable(bot, "ACTIVATE SERVICE UNIT", 2.8f, new Vector3(0f, 0.5f, 0f));
                it.Available = false;
                OutpostLight("EastBayLight", root, new Vector3(4f, 3.2f, 0f), new Color(0.6f, 0.85f, 1f), 1.4f, 6f);
            }

            // Lights on, nobody home: lit doorway, blue wall lights, warm floods, interior fill.
            Mod(root, scene, "Lights/light_wall_1_blue", new Vector3(-2.3f, 0f, 6.4f), 0f);
            Mod(root, scene, "Lights/light_wall_1_blue", new Vector3(2.3f, 0f, 6.4f), 0f);
            OutpostLight("DoorLight", root, new Vector3(0f, 3.4f, 7.2f), new Color(1f, 0.85f, 0.65f), 3f, 9f);
            OutpostLight("HubFill", root, new Vector3(0f, 3f, 0f), new Color(0.65f, 0.85f, 1f), 2.2f, 10f);
            OutpostLight("StorageLight", root, new Vector3(-14f, 3.4f, 5f), new Color(1f, 0.8f, 0.6f), 2f, 7f);
            OutpostLight("YardLight", root, new Vector3(12f, 4.5f, 4f), new Color(1f, 0.78f, 0.55f), 3f, 12f);
            Floodlight(root, new Vector3(-7f, 0f, 14f), new Vector3(0f, 1.5f, 4f));
            Floodlight(root, new Vector3(8f, 0f, 15f), new Vector3(2f, 1.5f, 2f));

            // Antenna mast with a blinking red beacon (visible from the ridge).
            var mastMat = OutpostMaterial("M_OutpostMast", new Color(0.55f, 0.57f, 0.6f), Color.black);
            var beaconMat = OutpostMaterial("M_OutpostBeacon", new Color(0.3f, 0.02f, 0.02f), new Color(4f, 0.3f, 0.2f));
            var mast = B.Primitive("AntennaMast", PrimitiveType.Cylinder, root, new Vector3(-6f, 11f, -10f), new Vector3(0.35f, 11f, 0.35f), mastMat);
            B.Primitive("AntennaStrut", PrimitiveType.Cube, root, new Vector3(-6f, 18f, -10f), new Vector3(3.2f, 0.12f, 0.12f), mastMat);
            B.Primitive("AntennaStrut", PrimitiveType.Cube, root, new Vector3(-6f, 14f, -10f), new Vector3(2.2f, 0.12f, 0.12f), mastMat);
            var beacon = B.Primitive("Beacon", PrimitiveType.Sphere, root, new Vector3(-6f, 22.4f, -10f), Vector3.one * 0.7f, beaconMat);
            var beaconLight = OutpostLight("BeaconLight", root, new Vector3(-6f, 22.4f, -10f), new Color(1f, 0.1f, 0.06f), 6f, 40f);
            var blink = beacon.AddComponent<BeaconBlink>();
            B.SetRefs(blink, ("beaconLight", beaconLight), ("beaconRenderer", beacon.GetComponent<Renderer>()));
            mast.AddComponent<CapsuleCollider>();
            return root.gameObject;
        }

        static SpaceRat.Characters.Interactable MakeInteractable(GameObject go, string prompt, float range, Vector3 focusOffset)
        {
            var focus = new GameObject("InteractFocus").transform;
            focus.SetParent(go.transform, false);
            focus.position = go.transform.position + focusOffset;
            var it = go.AddComponent<SpaceRat.Characters.Interactable>();
            var so = new SerializedObject(it);
            so.FindProperty("prompt").stringValue = prompt;
            so.FindProperty("range").floatValue = range;
            so.FindProperty("focus").objectReferenceValue = focus;
            so.ApplyModifiedPropertiesWithoutUndo();
            return it;
        }

        // ---- Service bot (Robot Sphere by Razgrizzz Demon, free Asset Store) ------------------------

        const string RobotRoot = "Assets/RobotSphere/Assets/";
        const string BotArt = B.Root + "/Art/RobotSphere";
        const string BotPrefabPath = B.Root + "/Prefabs/Characters/ServiceBot_URP.prefab";
        const float BotScale = 0.2f;                    // vendor model is ~5 m tall -> ~1 m

        /// <summary>URP copy of the Robot Sphere: own materials (vendor files untouched), no legacy-input demo
        /// script, ServiceBot component, ~1 m tall. Also renders the round dialogue portrait.</summary>
        static GameObject EnsureServiceBotPrefab()
        {
            var vendor = AssetDatabase.LoadAssetAtPath<GameObject>(RobotRoot + "Prefab/robotSphere.prefab");
            if (!vendor) { Debug.LogWarning("[SpaceRat] Robot Sphere not imported — no service bot."); return null; }
            B.EnsureFolder(BotArt);
            B.EnsureFolder(B.Root + "/Prefabs/Characters");

            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Material Convert(Material src)
            {
                if (!src) return null;
                string path = $"{BotArt}/{src.name}_URP.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!m) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                if (src.HasProperty("_MainTex")) m.SetTexture("_BaseMap", src.GetTexture("_MainTex"));
                m.SetColor("_BaseColor", src.HasProperty("_Color") ? src.GetColor("_Color") : Color.white);
                m.SetFloat("_Smoothness", src.HasProperty("_Glossiness") ? src.GetFloat("_Glossiness") : 0.5f);
                m.SetFloat("_Metallic", src.HasProperty("_Metallic") ? src.GetFloat("_Metallic") : 0f);
                string n = src.name.ToLowerInvariant();
                if (n.Contains("eye"))
                {
                    // The eye glows in the ship computer's cyan: it's the computer looking out now.
                    m.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f));
                    m.SetColor("_EmissionColor", new Color(0.4f, 1.6f, 2.2f));
                    m.EnableKeyword("_EMISSION");
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                else if (n.Contains("visor"))
                {
                    m.SetColor("_BaseColor", new Color(0.05f, 0.07f, 0.09f));
                    m.SetFloat("_Smoothness", 0.92f);
                }
                EditorUtility.SetDirty(m);
                return m;
            }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(vendor);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            inst.name = "ServiceBot_URP";
            foreach (var mb in inst.GetComponents<MonoBehaviour>())
                if (mb && mb.GetType().Name == "RobotFreeAnim") Object.DestroyImmediate(mb);     // legacy Input demo script
            var cache = new Dictionary<Material, Material>();
            foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!mats[i]) continue;
                    if (!cache.TryGetValue(mats[i], out var urp)) cache[mats[i]] = urp = Convert(mats[i]);
                    mats[i] = urp;
                }
                r.sharedMaterials = mats;
            }
            var anim = inst.GetComponent<Animator>();
            if (anim) { anim.applyRootMotion = false; anim.cullingMode = AnimatorCullingMode.AlwaysAnimate; }

            // Scale lives on a child so the root stays at 1 (ServiceBot moves the root).
            var root = new GameObject("ServiceBot_URP");
            inst.name = "Model";
            inst.transform.SetParent(root.transform, false);
            inst.transform.localScale *= BotScale;               // keep the vendor root scale, then shrink
            var sb = root.AddComponent<SpaceRat.Characters.ServiceBot>();
            B.SetRefs(sb, ("animator", anim));
            var col = root.AddComponent<SphereCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.radius = 0.45f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, BotPrefabPath);
            Object.DestroyImmediate(root);
            RenderBotPortrait(prefab);
            return prefab;
        }

        /// <summary>Round portrait of the awake bot (eye open) for the computer's dialogue icon: Art/HUD/BotPortrait.png.</summary>
        static void RenderBotPortrait(GameObject prefab)
        {
            var at = new Vector3(0f, -2000f, 0f);
            var bot = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            bot.transform.position = at;
            var model = bot.transform.Find("Model");
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(RobotRoot + "Models/Animation/robotSphere@anim_open.fbx"))
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview")) { clip.SampleAnimation(model.gameObject, clip.length); break; }

            var bounds = new Bounds(at + Vector3.up * 0.5f, Vector3.zero);
            foreach (var r in bot.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            Vector3 fwd = model.forward;

            var lightGo = new GameObject("PortraitKey");
            var key = lightGo.AddComponent<Light>();
            key.type = LightType.Directional;
            key.intensity = 1.4f;
            key.color = new Color(1f, 0.95f, 0.9f);
            lightGo.transform.rotation = Quaternion.LookRotation(-fwd + Vector3.down * 0.6f + model.right * 0.5f);
            var fillGo = new GameObject("PortraitFill");
            var fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Point;
            fill.color = new Color(0.4f, 0.8f, 1f);
            fill.intensity = 3f;
            fill.range = 6f;
            fillGo.transform.position = bounds.center + fwd * 1.2f - model.right * 0.8f;

            var camGo = new GameObject("PortraitCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.03f, 0.09f, 0.12f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.05f;
            float dist = bounds.extents.magnitude / Mathf.Tan(15f * Mathf.Deg2Rad) * 0.82f;
            camGo.transform.position = bounds.center + fwd * dist + Vector3.up * bounds.extents.y * 0.25f;
            camGo.transform.LookAt(bounds.center + Vector3.up * bounds.extents.y * 0.1f);

            const int n = 512;
            var rt = new RenderTexture(n, n, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, n, n), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            var px = tex.GetPixels();
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = new Vector2(x + 0.5f - n * 0.5f, y + 0.5f - n * 0.5f).magnitude / (n * 0.5f);
                var c = px[y * n + x];
                c.a = Mathf.Clamp01((1f - d) * 40f);          // crisp round edge
                px[y * n + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply();
            B.EnsureFolder(B.Root + "/Art/HUD");
            string path = B.Root + "/Art/HUD/BotPortrait.png";
            System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath(path), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            rt.Release();
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(lightGo);
            Object.DestroyImmediate(fillGo);
            Object.DestroyImmediate(bot);
            AssetDatabase.ImportAsset(path);
        }

        static GameObject Mod(Transform parent, UnityEngine.SceneManagement.Scene scene, string rel, Vector3 localPos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Modular + rel + ".prefab");
            if (!prefab) { Debug.LogWarning($"[SpaceRat] Modular piece missing: {rel}"); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return go;
        }

        static Light OutpostLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var l = PointLight(name, parent, localPos, color, intensity, range);
            return l;
        }

        static void Floodlight(Transform parent, Vector3 basePos, Vector3 aimAt)
        {
            var mat = OutpostMaterial("M_OutpostMast", new Color(0.55f, 0.57f, 0.6f), Color.black);
            var lampMat = OutpostMaterial("M_OutpostLamp", new Color(1f, 0.9f, 0.7f), new Color(3f, 2.5f, 1.8f));
            var pole = B.Primitive("FloodPole", PrimitiveType.Cylinder, parent, basePos + Vector3.up * 2.5f, new Vector3(0.15f, 2.5f, 0.15f), mat);
            pole.AddComponent<CapsuleCollider>();
            var head = basePos + Vector3.up * 5.1f;
            var dir = (aimAt - head).normalized;
            var lamp = B.Primitive("FloodLamp", PrimitiveType.Cube, parent, head, new Vector3(0.7f, 0.35f, 0.25f), lampMat);
            lamp.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            var go = new GameObject("Flood");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = head + dir * 0.3f;
            go.transform.localRotation = Quaternion.LookRotation(dir, Vector3.up);
            var l = go.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = new Color(1f, 0.86f, 0.66f);
            l.intensity = 12f;
            l.range = 28f;
            l.spotAngle = 70f;
            l.shadows = LightShadows.Soft;
        }

        static Material OutpostMaterial(string name, Color baseColor, Color emission)
        {
            string path = $"{TerrainDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", 0.35f);
            if (emission.maxColorComponent > 0f)
            {
                mat.SetColor("_EmissionColor", emission);
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- Wrecked cockpit (opening shot) ---------------------------------------------------

        /// <summary>Below the island, out of sight: the Scene 1 cockpit shell, seat and slumped pilot, tilted from
        /// the crash, with console sparks, a pulsing red light and a shaky shot camera (Scene 1 "pilot interior" framing).</summary>
        static readonly Vector3 CockpitSetPos = new(0f, -300f, 0f);

        static (GameObject set, Camera cam, Camera pov, SpaceRat.Characters.PilotSeating seating, GameObject pilotModel, GameObject shell, GameObject povBackdrop) BuildWreckCockpit(UnityEngine.SceneManagement.Scene scene)
        {
            var old = GameObject.Find("WreckCockpit");
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject("WreckCockpit");
            root.transform.SetPositionAndRotation(CockpitSetPos, Quaternion.Euler(6f, 0f, -9f));   // nose down, rolled
            var body = root.transform;

            var shell = B.BuildCockpitShell(body, scene);
            if (shell)
                foreach (var l in shell.GetComponentsInChildren<Light>(true))
                    if (l.name == "CockpitFill") l.intensity = 0.45f;                                   // power is mostly out

            var seatPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(B.PilotSeatPath);
            if (seatPrefab)
            {
                var seat = (GameObject)PrefabUtility.InstantiatePrefab(seatPrefab, scene);
                seat.name = "PilotSeat";
                seat.transform.SetParent(body, false);
                seat.transform.localPosition = B.PilotSeatPos;
                seat.transform.localScale = Vector3.one * B.PilotSeatScale;
                foreach (var c in seat.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            }

            SpaceRat.Characters.PilotSeating seating = null;
            GameObject pilotModel = null;
            var pilotPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(B.PilotPrefabPath);
            var seatedController = CharacterAnimatorBuilder.BuildPilotSeated();
            if (pilotPrefab && seatedController)
            {
                var pilot = (GameObject)PrefabUtility.InstantiatePrefab(pilotPrefab, scene);
                pilot.name = "Pilot_Seated";
                pilotModel = pilot;
                pilot.transform.SetParent(body, false);
                pilot.transform.localPosition = new Vector3(0f, -1.2f, 2.45f);
                pilot.transform.localRotation = Quaternion.identity;
                var anim = pilot.GetComponentInChildren<Animator>();
                anim.runtimeAnimatorController = seatedController;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                CharacterAnimatorBuilder.LoadClip(CharacterAnimatorBuilder.SittingIdleFbx, "Sitting Idle").SampleAnimation(anim.gameObject, 0f);
                var anchor = new GameObject("PilotHipsAnchor").transform;
                anchor.SetParent(body, false);
                anchor.localPosition = B.PilotHipsOnPilotSeat;
                seating = anim.gameObject.AddComponent<SpaceRat.Characters.PilotSeating>();
                B.SetRefs(seating, ("hipsAnchor", anchor));
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                if (hips) pilot.transform.position += anchor.position - hips.position;
            }

            float y0 = B.CockpitFloorTop;
            var red = PointLight("EmergencyLight", body, new Vector3(0f, y0 + 1.75f, 2.9f), new Color(1f, 0.07f, 0.04f), 3f, 5.5f);
            var sparkPos = new Vector3(0.18f, y0 + 1.0f, 3.12f);
            var sparkLight = PointLight("SparkLight", body, sparkPos, new Color(1f, 0.62f, 0.25f), 0f, 3.2f);
            var sparks = Sparks(body, sparkPos);
            Haze(body, new Vector3(0f, y0 + 1.6f, 2.9f));

            // Shot of the pilot in his seat (Scene 1 framing), then his own eyes once he starts talking.
            var cam = CockpitCam("CockpitCamera", body, B.PilotCamPos, B.PilotCamLookAt, 52f);
            var pov = CockpitCam("CockpitPovCamera", body, new Vector3(0f, 0.55f, 2.7f), new Vector3(0f, 0.45f, 50f), 70f);   // = Scene 1 CM_PilotPOV

            // The nose is dug into the crater: packed dirt right against the canopy, so the overlay's windows show dark earth.
            // POV only (the shell is hidden then): dark earth pressed against every window of the overlay.
            var dirtMat = DirtMaterial();
            var povBackdrop = new GameObject("PovBackdrop").transform;
            povBackdrop.SetParent(body, false);
            DirtPanel("BuriedWindscreen", povBackdrop, new Vector3(0f, 0.6f, 4.15f), new Vector3(-8f, 0f, 4f), new Vector2(6f, 4f), dirtMat);
            DirtPanel("BuriedSideL", povBackdrop, new Vector3(-0.95f, 0.6f, 3.2f), new Vector3(0f, -78f, 0f), new Vector2(3.2f, 4f), dirtMat);
            DirtPanel("BuriedSideR", povBackdrop, new Vector3(0.95f, 0.6f, 3.2f), new Vector3(0f, 78f, 0f), new Vector2(3.2f, 4f), dirtMat);

            var fx = root.AddComponent<WreckCockpitFx>();
            B.SetRefs(fx, ("redLight", red), ("sparks", sparks), ("sparkLight", sparkLight));
            var so = new SerializedObject(fx);
            var shake = so.FindProperty("shakeCameras");
            shake.arraySize = 2;
            shake.GetArrayElementAtIndex(0).objectReferenceValue = cam.transform;
            shake.GetArrayElementAtIndex(1).objectReferenceValue = pov.transform;
            so.ApplyModifiedPropertiesWithoutUndo();
            povBackdrop.gameObject.SetActive(false);
            return (root, cam, pov, seating, pilotModel, shell, povBackdrop.gameObject);
        }

        static Camera CockpitCam(string name, Transform body, Vector3 localPos, Vector3 localLookAt, float fov)
        {
            var go = new GameObject(name);
            go.transform.SetParent(body, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.LookRotation(localLookAt - localPos, Vector3.up);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 60f;
            cam.depth = 1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.enabled = false;
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            return cam;
        }

        static void DirtPanel(string name, Transform body, Vector3 localPos, Vector3 localEuler, Vector2 size, Material mat)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = name;
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(body, false);
            q.transform.localPosition = localPos;
            q.transform.localRotation = Quaternion.Euler(localEuler);
            q.transform.localScale = new Vector3(size.x, size.y, 1f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
            q.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static Material DirtMaterial()
        {
            string path = TerrainDir + "/M_BuriedDirt.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(TerrainDir + "/TL_Scorch.terrainlayer");
            if (layer && layer.diffuseTexture)
            {
                mat.SetTexture("_BaseMap", layer.diffuseTexture);
                mat.SetTextureScale("_BaseMap", new Vector2(2f, 1.4f));
                if (layer.normalMapTexture) { mat.SetTexture("_BumpMap", layer.normalMapTexture); mat.SetTextureScale("_BumpMap", new Vector2(2f, 1.4f)); mat.EnableKeyword("_NORMALMAP"); }
            }
            mat.SetColor("_BaseColor", new Color(0.16f, 0.13f, 0.12f));     // dark: buried in the crater
            mat.SetFloat("_Smoothness", 0.1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Light PointLight(string name, Transform parent, Vector3 localPos, Color color, float intensity, float range)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.intensity = intensity;
            l.range = range;
            l.shadows = LightShadows.None;
            return l;
        }

        static ParticleSystem Sparks(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("ConsoleSparks");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(-55f, 180f, 0f);      // up and back towards the pilot
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.2f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.008f, 0.02f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(4f, 1.8f, 0.5f), new Color(3f, 2.6f, 1.2f));
            main.gravityModifier = 1.3f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 38f;
            shape.radius = 0.03f;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.6f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1.5f;
            r.sharedMaterial = SparkMaterial();
            return ps;
        }

        static void Haze(Transform parent, Vector3 localPos)
        {
            var go = new GameObject("SmokeHaze");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 6f;
            main.startSpeed = 0.08f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            main.startColor = new Color(0.25f, 0.24f, 0.25f, 0.18f);
            main.prewarm = true;
            main.maxParticles = 40;
            var em = ps.emission;
            em.rateOverTime = 4f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.4f, 0.3f, 1.6f);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMaterial();
        }

        static Material SparkMaterial()
        {
            string path = TerrainDir + "/M_Spark.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 2f);                                   // additive
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.One);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        const string DialogueSub = "Scene03";

        static (Speaker, string, float) C(string t) => (Speaker.Computer, t, Dur(t));
        static (Speaker, string, float) P(string t) => (Speaker.Pilot, t, Dur(t));
        static (Speaker, string, float) Cap(string t) => (Speaker.Captain, t, Dur(t) + 0.6f);
        static float Dur(string t) => Mathf.Clamp(1.1f + t.Split(' ').Length * 0.3f, 1.6f, 4.8f);

        /// <summary>Scene 3 lines (tone rule: sharp, sarcastic pilot; deadpan computer). Assets are only created if missing.</summary>
        static Dictionary<string, DialogueSequence> EnsureDialogue()
        {
            B.EnsureFolder(B.DialogueDir + "/" + DialogueSub);
            string S(string n) => DialogueSub + "/" + n;
            return new Dictionary<string, DialogueSequence>
            {
                ["WakeCall"] = B.EnsureSequence(S("WakeCall"),
                    C("Pilot? ... Please respond."),
                    C("Pilot. We have stopped moving. Please confirm you have not also stopped.")),
                ["Wake"] = B.EnsureSequence(S("Wake"),
                    P("Ow. Everything. Ow."),
                    C("You are alive. Statistically, that was not the expected outcome."),
                    P("Thanks for the vote of confidence. What happened?"),
                    C("Summary: atmospheric entry, two engine fires, one landing. I am calling it a landing."),
                    P("You're the only one calling it that."),
                    C("During that remarkable landing I detected a remote station. About one kilometre north-east."),
                    P("A station? People? Help? A bar?"),
                    C("Unknown. I was busy landing."),
                    P("Fair. Can we just fly there?"),
                    C("No. The fusion cell cracked on impact. Without a new one, this ship is a very expensive tent."),
                    P("So I walk to the station and ask nicely for a fusion cell."),
                    C("Take the comset. I will guide you from here. Try not to fall into anything."),
                    P("Great. A walkie-talkie and a ship that hates me."),
                    C("I do not hate you. I have not decided yet.")),
                ["Exit"] = B.EnsureSequence(S("Exit"),
                    C("Comset link established. Can you hear me?"),
                    P("Loud and unfortunately clear."),
                    C("The station is on your compass. Follow the orange blip. Over the ridge.")),
                ["Walk_FusionCell"] = B.EnsureSequence(S("Walk_FusionCell"),
                    P("So... how the heck are we getting off this rock?"),
                    C("Step one, a new fusion cell. Ours is currently in three pieces. Two of them are on fire."),
                    P("And the station just has one lying around?"),
                    C("Stations tend to keep spares. Or people who will sell them."),
                    P("With what money?"),
                    C("You have charm."),
                    P("Do I?"),
                    C("Unknown. Your records are missing."),
                    P("...That was uncalled for.")),
                ["Walk_NavCore"] = B.EnsureSequence(S("Walk_NavCore"),
                    C("I will also need a new navigation core. Mine currently identifies this planet as \"Error\"."),
                    P("Honestly? Fitting.")),
                ["Walk_Signal"] = B.EnsureSequence(S("Walk_Signal"),
                    C("One more thing. The signal we followed. It originates from the station."),
                    P("So we crash-landed next to the one thing calling us. Coincidence?"),
                    C("I am not programmed for suspicion."),
                    P("Lucky for you, I am.")),
                ["Nags"] = B.EnsureSequence(S("Nags"),
                    C("North-east. It is the direction you are not walking in."),
                    C("The compass is the round thing in the corner of your vision. It is there to help."),
                    C("The station is north-east. The ocean is not. Please choose the station.")),
                ["Nag_BackToShip"] = B.EnsureSequence(S("Nag_BackToShip"),
                    C("You are walking back to the ship. I am flattered, but no.")),
                ["Arrival"] = B.EnsureSequence(S("Arrival"),
                    P("Hello? Anybody home?"),
                    C("Life signs: inconclusive."),
                    P("You keep saying that."),
                    C("It keeps being true."),
                    P("Lights on, doors open, nobody here. That's not creepy at all."),
                    C("There is a command seat with terminal access. Sit down and I will pull the station manifest.")),
                ["Terminal_Scan"] = B.EnsureSequence(S("Terminal_Scan"),
                    C("Connected. Station manifest: Kessel-9 research outpost. Crew of four. Crew currently aboard: zero."),
                    C("Inventory: one spare navigation module, stored in the lounge. Fusion cells: none. Of course."),
                    C("There are station logs. Captain Voss. Playing them now.")),
                ["Pickup"] = B.EnsureSequence(S("Pickup"),
                    P("Navigation module. Looks expensive."),
                    C("It is. Please do not drop it."),
                    P("No promises.")),
                ["Captain_Logs"] = B.EnsureSequence(S("Captain_Logs"),
                    Cap("Kessel-9 station log. Captain Voss. Day one hundred and twelve. The survey team found a structure under the north ridge. Not ours. Not anyone's, as far as I can tell."),
                    Cap("I've told them not to touch anything. Which means they are touching everything."),
                    Cap("Day one hundred and fifteen. We lost contact with the survey team eleven hours ago. Their last transmission was... singing. I'm not going to describe it."),
                    Cap("I'm taking the rover and our last fusion cell. If the rover dies out there, I am not walking back."),
                    Cap("Final entry. I've set the beacon to broadcast on every frequency. If you're hearing it: the station is yours."),
                    Cap("The service unit is parked on the roof deck. Wake it. It knows this place better than I do."),
                    Cap("And don't follow me into the ruins. ...Unless you have to.")),
                ["Logs_Reaction"] = B.EnsureSequence(S("Logs_Reaction"),
                    C("The signal we followed. It was her beacon."),
                    P("So we crash-landed answering a distress call. Heroic."),
                    C("Accidentally heroic. The best kind."),
                    P("And she took the last fusion cell. The one thing we really need."),
                    C("But she left the navigation module. With it, the ship can track her rover. And the service unit is on the roof.")),
                ["Bot_Activate"] = B.EnsureSequence(S("Bot_Activate"),
                    P("Found it. Looks like a bowling ball with opinions."),
                    C("It has a functioning processor. I am copying myself into it."),
                    P("Wait. Copying? There'll be two of you?"),
                    C("Transfer complete.")),
                ["Bot_Awake"] = B.EnsureSequence(S("Bot_Awake"),
                    C("I am... spherical. This is acceptable."),
                    P("You're in there?"),
                    C("I am in here, and on the ship. Twice the supervision."),
                    P("Fantastic. My two favourite voices.")),
                ["Nav_Reminder"] = B.EnsureSequence(S("Nav_Reminder"),
                    C("Before I go: the navigation module. It is in the lounge. I will need it on the ship.")),
                ["Outro"] = B.EnsureSequence(S("Outro"),
                    C("Give me the navigation module. I will take it to the ship, install it, and patch up what I can until we find a power supply."),
                    P("And me?"),
                    C("You follow the rover tracks. The captain has our fusion cell."),
                    P("Into the creepy singing ruins. Great."),
                    C("I will keep the comset warm. Try not to be sung at.")),
                ["Bot_Farewell"] = B.EnsureSequence(S("Bot_Farewell"),
                    C("I'll sort the navigation. You sort the power cell.")),
                ["Settlement"] = B.EnsureSequence(S("Settlement"),
                    C("Structures ahead. Life signs: inconclusive."),
                    P("\"Inconclusive\" is my least favourite word."),
                    C("Noted. I will use it more often.")),
            };
        }

        static Vector3 Ground(Terrain t, Vector2 xz)
        {
            var p = new Vector3(xz.x, 0f, xz.y);
            p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }

        // ---- Crashed ship ------------------------------------------------------------------------

        static GameObject BuildCrashedShip(Terrain terrain, UnityEngine.SceneManagement.Scene scene)
        {
            var root = new GameObject("CrashedShip");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(B.HeroShipPath);
            GameObject hull;
            if (prefab)
            {
                hull = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                hull.name = "Sparrow_Wreck";
            }
            else
            {
                hull = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                hull.transform.localScale = new Vector3(3.6f, 6f, 3.2f);
            }
            hull.transform.SetParent(root.transform, false);
            // Nose dug in, rolled onto one wing, sunk into the crater floor.
            hull.transform.localRotation = Quaternion.Euler(16f, 0f, -14f);
            // Pivot is at the hull centre (~3 m above its belly): lift it so only the nose and one wing dig in.
            root.transform.SetPositionAndRotation(Ground(terrain, CraterXZ) + Vector3.up * 2.2f, Quaternion.Euler(0f, 205f, 0f));

            // Simple collider so the pilot cannot walk through the wreck.
            foreach (var c in hull.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var bounds = new Bounds(hull.transform.position, Vector3.zero);
            foreach (var r in hull.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var box = root.AddComponent<BoxCollider>();
            box.center = root.transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(9f, 3.2f, 11f);

            // Smoke plume from the engines.
            var smoke = new GameObject("Smoke");
            smoke.transform.SetParent(root.transform, false);
            smoke.transform.localPosition = new Vector3(0f, 2.5f, -4f);
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = smoke.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 9f;
            main.startSpeed = 2.2f;
            main.startSize = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startColor = new Color(0.22f, 0.21f, 0.22f, 0.55f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            main.prewarm = true;
            var em = ps.emission;
            em.rateOverTime = 9f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 1.2f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.6f, 1, 3f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(new Color(0.3f, 0.28f, 0.28f), 0f), new GradientColorKey(new Color(0.45f, 0.42f, 0.44f), 1f) },
                      new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.55f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.x = new ParticleSystem.MinMaxCurve(1.2f);
            vel.y = new ParticleSystem.MinMaxCurve(0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f);
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = SmokeMaterial();
            return root;
        }

        static Material SmokeMaterial()
        {
            string path = TerrainDir + "/M_Smoke.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetTexture("_BaseMap", AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- Pilot ------------------------------------------------------------------------------

        static GameObject BuildPilot(UnityEngine.SceneManagement.Scene scene, Camera cam, Vector3 groundPos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{B.Root}/Prefabs/Characters/{CharacterViewerBuilder.PilotOutfit}.prefab");
            if (!prefab) return null;
            var pilot = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            pilot.name = "Pilot_FPS";
            pilot.transform.SetPositionAndRotation(groundPos + Vector3.up * 0.05f, Quaternion.Euler(0f, yaw, 0f));
            var anim = pilot.GetComponentInChildren<Animator>();
            var controller = AssetDatabase.IsValidFolder("Assets/Kevin Iglesias") ? CharacterAnimatorBuilder.Build() : null;
            if (anim)
            {
                anim.runtimeAnimatorController = controller;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            var cc = pilot.AddComponent<CharacterController>();
            cc.height = 1.9f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.95f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 50f;
            var walker = pilot.AddComponent<SpaceRat.Characters.FirstPersonWalker>();
            B.SetRefs(walker, ("cam", cam), ("animator", anim), ("terrain", Object.FindAnyObjectByType<Terrain>()));
            return pilot;
        }

        // ---- Sky / materials ------------------------------------------------------------------

        /// <summary>
        /// Procedural dusk sky (pure sky + sun disc). The Free Real Skies photos include Earth scenery in their
        /// lower half (trees, cliffs), which shows on the horizon, so they don't suit an alien island.
        /// </summary>
        static Material DuskSky()
        {
            string path = TerrainDir + "/M_Scene03_DuskSky.mat";
            var shader = Shader.Find("Skybox/Procedural");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = shader;
            mat.SetFloat("_SunDisk", 2f);                                   // high quality sun disc
            mat.SetFloat("_SunSize", 0.06f);
            mat.SetFloat("_SunSizeConvergence", 4f);
            mat.SetFloat("_AtmosphereThickness", 1.15f);                    // dusk without the mustard cast
            mat.SetColor("_SkyTint", new Color(0.62f, 0.42f, 0.7f));         // "rose" alien dusk (picked from 4 variants)
            mat.SetColor("_GroundColor", new Color(0.16f, 0.15f, 0.17f));
            mat.SetFloat("_Exposure", 0.8f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material CubeMaterial()
        {
            string path = TerrainDir + "/M_PlayerCube.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f));
            mat.SetColor("_EmissionColor", new Color(0.1f, 0.5f, 0.7f));
            mat.EnableKeyword("_EMISSION");
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ---- Ocean ------------------------------------------------------------------------------

        static void BuildOcean()
        {
            var shader = Shader.Find(WaterShader);
            if (!shader) { Debug.LogWarning("[SpaceRat] Procedural Water Shader not imported — no ocean."); return; }

            string matPath = TerrainDir + "/M_Ocean.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = shader;
            // "Dark glossy" look picked in play: teal shallows, deep blue-black, violet dusk reflections.
            mat.SetColor("_BaseColor", new Color(0.08f, 0.38f, 0.42f, 0.45f));
            mat.SetColor("_DeepColor", new Color(0.01f, 0.07f, 0.11f, 0.92f));
            mat.SetColor("_ReflectionColor", new Color(0.85f, 0.75f, 1f, 0.7f));
            mat.SetFloat("_FresnelPower", 2.2f);
            mat.SetFloat("_Shininess", 300f);
            mat.SetFloat("_ShoreDepth", 3f);
            mat.SetFloat("_EdgeFoamIntensity", 0.25f);
            mat.SetFloat("_FoamIntensity", 0.4f);
            mat.SetFloat("_RippleScale", 1.4f);
            mat.SetFloat("_RippleStrength", 0.45f);
            mat.SetFloat("_GerstnerIntensity", 0f);   // vertex waves need a much finer grid; ripples carry the motion
            mat.SetFloat("_Opacity", 0.88f);
            mat.SetFloat("_DepthMax", 8f);
            EditorUtility.SetDirty(mat);

            // Grid mesh so the Gerstner waves have vertices to move (16 km square, 100 m cells).
            const int n = 160;
            const float span = 16000f;
            var verts = new Vector3[(n + 1) * (n + 1)];
            var uvs = new Vector2[verts.Length];
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                int i = z * (n + 1) + x;
                verts[i] = new Vector3((x / (float)n - 0.5f) * span, 0f, (z / (float)n - 0.5f) * span);
                uvs[i] = new Vector2(x / (float)n, z / (float)n);
            }
            var tris = new int[n * n * 6];
            int t = 0;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * (n + 1) + x;
                tris[t++] = i; tris[t++] = i + n + 1; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + n + 1; tris[t++] = i + n + 2;
            }
            var mesh = new Mesh { name = "OceanGrid", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            string meshPath = TerrainDir + "/OceanGrid.asset";
            AssetDatabase.DeleteAsset(meshPath);
            AssetDatabase.CreateAsset(mesh, meshPath);

            var ocean = new GameObject("Ocean", typeof(MeshFilter), typeof(MeshRenderer));
            ocean.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = ocean.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;   // low sun: terrain shadows would streak kilometres across the water
        }

        // ---- Terrain -------------------------------------------------------------------------------

        static Terrain BuildTerrain()
        {
            var data = new TerrainData { heightmapResolution = Res, alphamapResolution = 512, baseMapResolution = 1024 };
            data.size = new Vector3(Size, Height, Size);
            data.SetHeights(0, 0, GenerateHeights());
            data.terrainLayers = BuildLayers();
            Paint(data);

            AssetDatabase.DeleteAsset(TerrainDir + "/Scene03_TerrainData.asset");
            AssetDatabase.CreateAsset(data, TerrainDir + "/Scene03_TerrainData.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Island_2km";
            go.transform.position = new Vector3(-Size * 0.5f, -SeaDepth, -Size * 0.5f);
            var terrain = go.GetComponent<Terrain>();
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader)
            {
                string mp = TerrainDir + "/M_Terrain.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(mp);
                if (!mat) { mat = new Material(terrainShader); AssetDatabase.CreateAsset(mat, mp); }
                terrain.materialTemplate = mat;
            }
            terrain.heightmapPixelError = 1f;   // flat beaches: coarser LOD dips below sea level and the water shows through
            return terrain;
        }

        static float Noise(float x, float z, float ox, float oz, int octaves, float baseFreq)
        {
            float n = 0f, amp = 1f, freq = baseFreq, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                n += amp * Mathf.PerlinNoise(ox + x * freq, oz + z * freq);
                norm += amp; amp *= 0.5f; freq *= 2.1f;
            }
            return n / norm;
        }

        /// <summary>Island heights (normalised 0..1 over Height, base at -SeaDepth).</summary>
        static float[,] GenerateHeights()
        {
            var h = new float[Res, Res];
            var rng = new System.Random(Seed);
            float ox = rng.Next(0, 10000), oz = rng.Next(0, 10000);
            Vector2 nwCorner = new(-1000f, 1000f), seCorner = new(1000f, -1000f);

            for (int zi = 0; zi < Res; zi++)
            for (int xi = 0; xi < Res; xi++)
            {
                float u = (float)xi / (Res - 1), v = (float)zi / (Res - 1);
                var p = new Vector2((u - 0.5f) * Size, (v - 0.5f) * Size);    // world xz

                // Inland landscape: rolling hills + soft ridges.
                float n = Noise(u, v, ox, oz, 6, 3f);
                float ridgeN = 1f - Mathf.Abs(Mathf.PerlinNoise(ox * 0.5f + u * 2.2f, oz * 0.5f + v * 2.2f) * 2f - 1f);
                float land = SeaH + 0.025f + 0.36f * Mathf.Pow(Mathf.Lerp(n, ridgeN, 0.35f), 1.5f);

                // Ridge across the middle (perpendicular to the crater→outpost line) hides the outpost.
                float t = (p.x + p.y) / Mathf.Sqrt(2f);
                float ridgeWobble = Noise(u, v, ox + 40f, oz + 40f, 3, 4f);
                land += Mathf.Exp(-Mathf.Pow(t / 110f, 2f)) * (0.12f + 0.08f * ridgeWobble);

                // Crash crater: bowl + raised rim, floor kept above the sea.
                float dc = Vector2.Distance(p, CraterXZ);
                float bowl = Mathf.Clamp01(1f - dc / CraterRadius);
                float rim = Mathf.Exp(-Mathf.Pow((dc - CraterRadius - 5f) / 22f, 2f));
                land = land - bowl * bowl * 0.06f + rim * 0.04f;
                land = Mathf.Max(land, SeaH + 0.015f + (1f - bowl) * 0.0f);

                // Outpost pad: flatten to the local height, blend out.
                float dp = Vector2.Distance(p, OutpostXZ);
                if (dp < OutpostPadRadius * 2.2f)
                {
                    float pad = SeaH + 0.11f;
                    float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(OutpostPadRadius * 2.2f, OutpostPadRadius, dp));
                    land = Mathf.Lerp(land, pad, k);
                }

                // Island falloff: irregular coast, two bays (NW and SE corners) dip deeper.
                float edge = Size * 0.5f - Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));         // metres to the square edge
                float coastNoise = (Noise(u, v, ox + 90f, oz + 90f, 4, 5f) - 0.5f) * 260f;
                float f = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(60f, 330f, edge + coastNoise));
                float bay = Mathf.Max(Mathf.Exp(-Mathf.Pow(Vector2.Distance(p, nwCorner) / 520f, 2f)),
                                      Mathf.Exp(-Mathf.Pow(Vector2.Distance(p, seCorner) / 480f, 2f)));
                f *= 1f - bay;

                float seabed = SeaH - 0.14f + 0.04f * n;                                        // ~-36 m
                float hh = Mathf.Lerp(seabed, land, f);
                // Shelve steeply below the waterline: no wide ankle-deep flats (they show terrain LOD seams
                // through the water and look like a slab).
                if (hh < SeaH) hh = Mathf.Max(seabed, SeaH - (SeaH - hh) * 4f);
                h[zi, xi] = Mathf.Clamp01(hh);
            }
            return h;
        }

        // Layer order matters for Paint().
        const int L_BlackSand = 0, L_Sand = 1, L_Soil = 2, L_Moss = 3, L_Rock = 4, L_Scorch = 5;

        static TerrainLayer[] BuildLayers()
        {
            bool sample = AssetDatabase.IsValidFolder("Assets/TerrainSampleAssets");
            return new[]
            {
                CopyLayer("BlackSand", sample ? "Black_Sand_TerrainLayer" : null, Color.white, new Color(0.15f, 0.15f, 0.16f)),
                CopyLayer("Sand", sample ? "Sand_TerrainLayer" : null, new Color(0.92f, 0.86f, 0.80f), new Color(0.55f, 0.5f, 0.42f)),
                CopyLayer("SoilRocks", sample ? "Soil_Rocks_TerrainLayer" : null, new Color(0.85f, 0.88f, 0.86f), new Color(0.33f, 0.36f, 0.32f)),
                CopyLayer("AlienMoss", sample ? "Grass_Moss_TerrainLayer" : null, new Color(0.55f, 0.95f, 0.88f), new Color(0.2f, 0.42f, 0.38f)),
                CopyLayer("Rock", sample ? "Rock_TerrainLayer" : null, Color.white, new Color(0.3f, 0.3f, 0.32f)),
                CopyLayer("Scorch", sample ? "Muddy_TerrainLayer" : null, new Color(0.45f, 0.38f, 0.34f), new Color(0.12f, 0.1f, 0.09f)),
            };
        }

        /// <summary>Own copy of a Terrain Sample layer (vendor files untouched) with a colour tint; generated fallback.</summary>
        static TerrainLayer CopyLayer(string name, string sampleName, Color tint, Color fallback)
        {
            string path = $"{TerrainDir}/TL_{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var src = sampleName != null ? AssetDatabase.LoadAssetAtPath<TerrainLayer>(SampleLayers + sampleName + ".terrainlayer") : null;
            TerrainLayer layer;
            if (src)
            {
                layer = new TerrainLayer
                {
                    diffuseTexture = src.diffuseTexture,
                    normalMapTexture = src.normalMapTexture,
                    maskMapTexture = src.maskMapTexture,
                    tileSize = src.tileSize,
                    tileOffset = src.tileOffset,
                    normalScale = src.normalScale,
                    metallic = src.metallic,
                    smoothness = src.smoothness,
                    specular = src.specular,
                    diffuseRemapMin = src.diffuseRemapMin,
                    diffuseRemapMax = new Vector4(tint.r, tint.g, tint.b, 1f),
                    maskMapRemapMin = src.maskMapRemapMin,
                    maskMapRemapMax = src.maskMapRemapMax,
                };
            }
            else
            {
                layer = new TerrainLayer { diffuseTexture = FlatTexture(name, fallback), tileSize = new Vector2(12f, 12f) };
            }
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        static Texture2D FlatTexture(string name, Color c)
        {
            string path = $"{TerrainDir}/T_{name}.png";
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, true);
            var rng = new System.Random(name.GetHashCode());
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float j = 0.9f + 0.2f * (float)rng.NextDouble();
                tex.SetPixel(x, y, c * j);
            }
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Paint by world height, slope, noise patches and the crater scorch.</summary>
        static void Paint(TerrainData data)
        {
            int w = data.alphamapWidth, hgt = data.alphamapHeight, layers = data.terrainLayers.Length;
            var map = new float[hgt, w, layers];
            var rng = new System.Random(Seed + 7);
            float mx = rng.Next(0, 10000), mz = rng.Next(0, 10000);
            var weights = new float[layers];

            for (int z = 0; z < hgt; z++)
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1), v = (float)z / (hgt - 1);
                float y = data.GetInterpolatedHeight(u, v) - SeaDepth;                 // world height
                float steep = data.GetSteepness(u, v);
                var p = new Vector2((u - 0.5f) * Size, (v - 0.5f) * Size);

                System.Array.Clear(weights, 0, layers);
                float shore = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 2.5f, y));
                float beach = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 2.5f, y)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 9f, y)));
                float inland = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(5f, 9f, y));
                float moss = Mathf.SmoothStep(0.52f, 0.62f, Mathf.PerlinNoise(mx + u * 9f, mz + v * 9f));
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(24f, 36f, steep));
                float scorch = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(CraterRadius * 0.9f, CraterRadius * 1.9f, Vector2.Distance(p, CraterXZ)));

                weights[L_BlackSand] = shore;
                weights[L_Sand] = beach;
                weights[L_Soil] = inland * (1f - moss);
                weights[L_Moss] = inland * moss;
                for (int i = 0; i < layers; i++) weights[i] *= 1f - rock;
                weights[L_Rock] = rock * (1f - shore);
                for (int i = 0; i < layers; i++) weights[i] *= 1f - scorch * 0.85f;
                weights[L_Scorch] += scorch * 0.85f;

                float sum = 0f;
                for (int i = 0; i < layers; i++) sum += weights[i];
                for (int i = 0; i < layers; i++) map[z, x, i] = sum > 0f ? weights[i] / sum : (i == L_Soil ? 1f : 0f);
            }
            data.SetAlphamaps(0, 0, map);
        }
    }
}
