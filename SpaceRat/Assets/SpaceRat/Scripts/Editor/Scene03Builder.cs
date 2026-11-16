using System.Collections.Generic;
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
    /// Scene 3 terrain test (menu: SpaceRat > Build Scene03 Terrain): a procedurally generated 2 x 2 km
    /// Unity Terrain (hills, ridges, a central crash crater), two generated ground layers painted by
    /// slope, and a flyable cube with a chase camera.
    /// </summary>
    public static class Scene03Builder
    {
        const string ScenePath = B.Root + "/Scenes/Scene03_Terrain.unity";
        const string TerrainDir = B.Root + "/Terrain";
        const float Size = 2000f;          // metres (x and z)
        const float Height = 260f;         // max terrain height
        const int Res = 513;               // heightmap resolution
        const int Seed = 1977;

        [MenuItem("SpaceRat/Build Scene03 Terrain")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            B.EnsureFolder(TerrainDir);
            B.EnsureFolder(B.Root + "/Scenes");

            // ---- Sky & light -------------------------------------------------------
            var sky = AssetDatabase.LoadAssetAtPath<Material>(B.SkyboxPath);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.12f, 0.2f, 0.19f);
            RenderSettings.fogStartDistance = 300f;
            RenderSettings.fogEndDistance = 1800f;

            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.9f, 0.78f);
            sun.intensity = 1.6f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, -35f, 0f);

            // ---- Terrain -------------------------------------------------------------
            var terrain = BuildTerrain();

            // ---- Player cube + chase camera -----------------------------------------------
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PlayerCube";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            var cubeMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            cubeMat.SetColor("_BaseColor", new Color(0.2f, 0.85f, 1f));
            cubeMat.SetColor("_EmissionColor", new Color(0.1f, 0.5f, 0.7f));
            cubeMat.EnableKeyword("_EMISSION");
            AssetDatabase.DeleteAsset(TerrainDir + "/M_PlayerCube.mat");
            AssetDatabase.CreateAsset(cubeMat, TerrainDir + "/M_PlayerCube.mat");
            cube.GetComponent<Renderer>().sharedMaterial = cubeMat;
            var start = new Vector3(0f, 0f, -300f);   // looking north toward the crash crater
            start.y = terrain.SampleHeight(start) + terrain.transform.position.y + 25f;
            cube.transform.SetPositionAndRotation(start, Quaternion.identity);
            var fly = cube.AddComponent<FlyController>();
            B.SetRefs(fly, ("terrain", terrain));

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = sky ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.farClipPlane = 5000f;
            camGo.AddComponent<AudioListener>();
            var urp = cam.GetUniversalAdditionalCameraData();
            urp.renderPostProcessing = true;
            urp.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            camGo.AddComponent<CinemachineBrain>();

            var chase = new GameObject("CM_Chase").AddComponent<CinemachineCamera>();
            chase.Target.TrackingTarget = cube.transform;
            var lens = LensSettings.Default;
            lens.FieldOfView = 60f;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 5000f;
            chase.Lens = lens;
            var follow = chase.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 3.5f, -10f);
            var ts = follow.TrackerSettings;
            ts.BindingMode = BindingMode.LockToTargetWithWorldUp;
            ts.PositionDamping = new Vector3(0.4f, 0.4f, 0.4f);
            follow.TrackerSettings = ts;
            var compose = chase.gameObject.AddComponent<CinemachineRotationComposer>();
            compose.TargetOffset = new Vector3(0f, 1f, 4f);

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(B.SettingsDir + "/Scene01_GlobalVolume.asset");
            if (profile)
            {
                var vol = new GameObject("GlobalVolume").AddComponent<Volume>();
                vol.isGlobal = true;
                vol.sharedProfile = profile;
            }

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == ScenePath);
            int s2 = scenes.FindIndex(s => s.path.EndsWith("Scene02_TheSignal.unity"));
            scenes.Insert(s2 >= 0 ? s2 + 1 : scenes.Count, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log($"[SpaceRat] Built {ScenePath}");
        }

        static Terrain BuildTerrain()
        {
            var data = new TerrainData { heightmapResolution = Res, alphamapResolution = 512, baseMapResolution = 1024 };
            data.size = new Vector3(Size, Height, Size);
            data.SetHeights(0, 0, GenerateHeights());

            var dirt = Layer("Ground", GroundTexture("T_AlienGround", new Color(0.30f, 0.42f, 0.36f), new Color(0.18f, 0.27f, 0.23f), 11), 18f, 0.15f);
            var rock = Layer("Rock", GroundTexture("T_AlienRock", new Color(0.32f, 0.31f, 0.33f), new Color(0.14f, 0.15f, 0.17f), 29), 9f, 0.3f);
            data.terrainLayers = new[] { dirt, rock };
            PaintBySlope(data);

            AssetDatabase.DeleteAsset(TerrainDir + "/Scene03_TerrainData.asset");
            AssetDatabase.CreateAsset(data, TerrainDir + "/Scene03_TerrainData.asset");

            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain_2km";
            go.transform.position = new Vector3(-Size * 0.5f, 0f, -Size * 0.5f);
            var terrain = go.GetComponent<Terrain>();
            var terrainShader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (terrainShader)
            {
                var mat = new Material(terrainShader);
                AssetDatabase.DeleteAsset(TerrainDir + "/M_Terrain.mat");
                AssetDatabase.CreateAsset(mat, TerrainDir + "/M_Terrain.mat");
                terrain.materialTemplate = mat;
            }
            terrain.heightmapPixelError = 4f;
            return terrain;
        }

        /// <summary>Fractal hills + ridges, flattened toward the edges' average, with a crater in the middle.</summary>
        static float[,] GenerateHeights()
        {
            var h = new float[Res, Res];
            var rng = new System.Random(Seed);
            float ox = rng.Next(0, 10000), oz = rng.Next(0, 10000);
            for (int z = 0; z < Res; z++)
            for (int x = 0; x < Res; x++)
            {
                float u = (float)x / (Res - 1), v = (float)z / (Res - 1);
                float n = 0f, amp = 1f, freq = 3f, norm = 0f;
                for (int o = 0; o < 6; o++)
                {
                    n += amp * Mathf.PerlinNoise(ox + u * freq, oz + v * freq);
                    norm += amp; amp *= 0.5f; freq *= 2.1f;
                }
                n /= norm;
                float ridge = 1f - Mathf.Abs(Mathf.PerlinNoise(ox * 0.5f + u * 2.2f, oz * 0.5f + v * 2.2f) * 2f - 1f);
                float height = Mathf.Lerp(n, ridge, 0.35f);
                height = Mathf.Pow(height, 1.6f) * 0.85f;

                // Crash crater at the centre: bowl + raised rim.
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * Size;   // metres from centre
                float bowl = Mathf.Clamp01(1f - d / 90f);
                float rim = Mathf.Exp(-Mathf.Pow((d - 95f) / 22f, 2f));
                height = Mathf.Max(0f, height - bowl * bowl * 0.12f + rim * 0.06f);
                h[z, x] = height;
            }
            return h;
        }

        static void PaintBySlope(TerrainData data)
        {
            int w = data.alphamapWidth, hgt = data.alphamapHeight;
            var map = new float[hgt, w, 2];
            for (int z = 0; z < hgt; z++)
            for (int x = 0; x < w; x++)
            {
                float u = (float)x / (w - 1), v = (float)z / (hgt - 1);
                float steep = data.GetSteepness(u, v);                     // degrees
                float rock = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(18f, 32f, steep));
                map[z, x, 0] = 1f - rock;
                map[z, x, 1] = rock;
            }
            data.SetAlphamaps(0, 0, map);
        }

        static TerrainLayer Layer(string name, Texture2D tex, float tile, float smoothness)
        {
            string path = $"{TerrainDir}/TL_{name}.terrainlayer";
            AssetDatabase.DeleteAsset(path);
            var layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile), smoothness = smoothness };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        /// <summary>Generated tileable noise texture (no external art needed).</summary>
        static Texture2D GroundTexture(string name, Color light, Color dark, int seed)
        {
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGB24, true);
            float s = seed * 13.37f;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                // Tileable: sample noise on a torus via periodic coordinates.
                float a = Tile(u, v, 4f, s) * 0.6f + Tile(u, v, 12f, s + 7f) * 0.3f + Tile(u, v, 32f, s + 3f) * 0.1f;
                tex.SetPixel(x, y, Color.Lerp(dark, light, Mathf.SmoothStep(0.2f, 0.8f, a)));
            }
            tex.Apply();
            string path = $"{TerrainDir}/{name}.png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Repeat;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static float Tile(float u, float v, float f, float seed)
        {
            float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
            float r = f / (Mathf.PI * 2f);
            // Blend of two periodic samples gives a seamless tile.
            return 0.5f * (Mathf.PerlinNoise(seed + Mathf.Cos(a) * r, seed + Mathf.Sin(b) * r)
                         + Mathf.PerlinNoise(seed + 50f + Mathf.Sin(a) * r, seed + 50f + Mathf.Cos(b) * r));
        }
    }
}
