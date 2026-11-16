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
            RenderSettings.ambientIntensity = 1.0f;
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
            sun.transform.rotation = Quaternion.Euler(13f, 75f, 0f);    // low, from the west
            RenderSettings.sun = sun;

            // ---- Island + ocean ------------------------------------------------------------
            var terrain = BuildTerrain();
            BuildOcean();

            // Placeholder for the outpost (prefab extracted later) and the crash site marker.
            var outpostSite = new GameObject("OutpostSite");
            outpostSite.transform.position = Ground(terrain, OutpostXZ);
            var crashSite = new GameObject("CrashSite");
            crashSite.transform.position = Ground(terrain, CraterXZ);

            // ---- Player cube + chase camera ----------------------------------------------------
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "PlayerCube";
            Object.DestroyImmediate(cube.GetComponent<Collider>());
            cube.GetComponent<Renderer>().sharedMaterial = CubeMaterial();
            Vector2 startXZ = CraterXZ + new Vector2(70f, 70f);            // on the crater rim, facing the ridge
            var start = Ground(terrain, startXZ) + Vector3.up * 20f;
            cube.transform.SetPositionAndRotation(start, Quaternion.Euler(0f, 45f, 0f));
            var fly = cube.AddComponent<FlyController>();
            B.SetRefs(fly, ("terrain", terrain));

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.farClipPlane = 9000f;
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
            lens.FarClipPlane = 9000f;
            chase.Lens = lens;
            var follow = chase.gameObject.AddComponent<CinemachineFollow>();
            follow.FollowOffset = new Vector3(0f, 3.5f, -10f);
            var ts = follow.TrackerSettings;
            ts.BindingMode = BindingMode.LockToTargetWithWorldUp;
            ts.PositionDamping = new Vector3(0.4f, 0.4f, 0.4f);
            follow.TrackerSettings = ts;
            chase.gameObject.AddComponent<CinemachineRotationComposer>().TargetOffset = new Vector3(0f, 1f, 4f);

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

        static Vector3 Ground(Terrain t, Vector2 xz)
        {
            var p = new Vector3(xz.x, 0f, xz.y);
            p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
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
            mat.SetColor("_BaseColor", new Color(0.10f, 0.42f, 0.40f, 0.50f));   // teal-green shallows
            mat.SetColor("_DeepColor", new Color(0.02f, 0.10f, 0.13f, 0.80f));
            mat.SetColor("_ReflectionColor", new Color(0.65f, 0.75f, 0.85f, 0.25f)); // soft, cool reflections
            mat.SetFloat("_DepthMax", 14f);
            mat.SetFloat("_RippleScale", 0.25f);                                 // broad swells, not corduroy
            mat.SetFloat("_RippleStrength", 0.3f);
            mat.SetFloat("_Opacity", 0.9f);
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
            terrain.heightmapPixelError = 4f;
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
                land = land - bowl * bowl * 0.09f + rim * 0.05f;
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
                h[zi, xi] = Mathf.Clamp01(Mathf.Lerp(seabed, land, f));
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
