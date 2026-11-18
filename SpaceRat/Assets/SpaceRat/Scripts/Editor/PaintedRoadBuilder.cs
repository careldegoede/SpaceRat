using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Turns lines painted on the terrain with the "RoadGuide" layer into gravel road meshes, then
    /// repaints the guide strokes (and a strip under every road) with the "Gravel" layer.
    /// Workflow: SpaceRat/Roads/Setup Road Paint Layers → paint with RoadGuide (magenta) →
    /// SpaceRat/Roads/Build Roads From Painted Lines. EasyRoads3D Free refuses API calls, so the roads are
    /// our own meshes (using its dirt-road material when installed). At a junction the straightest pair of
    /// branches becomes one continuous road and the other branches end tucked under its edge.
    /// </summary>
    static class PaintedRoadBuilder
    {
        const string Folder = "Assets/DDG/Roads";
        const string GuideLayer = "RoadGuide";
        const string GravelLayer = "Gravel";
        const string RoadPrefix = "PaintedRoad_";
        const string BaseGrassLayer = "Assets/EasyRoads3D scenes/Terrain Assets/layer_Grass ER.terrainlayer";
        const string RoadMaterial = "Materials/roads/dirt material"; // EasyRoads3D Resources (optional)
        const string RootName = "Painted Roads";

        const float RoadWidth = 5f;
        const float MarkerSpacing = 10f;
        const float GravelShoulder = 1.5f; // full gravel this far past the road edge, then fades over 2 m
        const float SampleStep = 1f;       // mesh ring spacing along the road
        const int Across = 5;              // vertices across the road (more = follows terrain better)
        const float Lift = 0.06f;          // height above terrain
        const float BranchLift = 0.03f;    // junction ends sit lower so they tuck under the through road
        const float DeadEndFade = 4f;      // metres of alpha fade at free road ends

        // ---------------------------------------------------------------- menu: setup

        [MenuItem("SpaceRat/Roads/Setup Road Paint Layers")]
        static void Setup()
        {
            var terrain = ActiveTerrain();
            if (!terrain) return;
            Directory.CreateDirectory(Folder);

            var guideTex = EnsureTexture("T_RoadGuide", 8, (x, y) => new Color(1f, 0f, 1f), true);
            var gravelTex = EnsureTexture("T_Gravel", 512, GravelPixel, false);
            var guide = EnsureLayer(GuideLayer, guideTex, 4f);
            var gravel = EnsureLayer(GravelLayer, gravelTex, 4f);

            var data = terrain.terrainData;
            Undo.RegisterCompleteObjectUndo(data, "Setup road paint layers");
            var layers = data.terrainLayers.Where(l => l).ToList();
            if (layers.Count == 0)
            {
                var grass = AssetDatabase.LoadAssetAtPath<TerrainLayer>(BaseGrassLayer);
                if (grass) layers.Add(grass);
                else Debug.LogWarning("[Roads] Terrain had no layers and the grass base layer was not found; RoadGuide becomes the base layer.");
            }
            if (!layers.Contains(guide)) layers.Add(guide);
            if (!layers.Contains(gravel)) layers.Add(gravel);
            data.terrainLayers = layers.ToArray();
            EditorUtility.SetDirty(data);
            Debug.Log($"[Roads] Terrain '{terrain.name}' layers: {string.Join(", ", layers.Select(l => l.name))}. Paint road lines with '{GuideLayer}'.");
        }

        // ---------------------------------------------------------------- menu: build

        [MenuItem("SpaceRat/Roads/Build Roads From Painted Lines")]
        static void Build()
        {
            var terrain = ActiveTerrain();
            if (!terrain) return;
            var data = terrain.terrainData;
            int gi = LayerIndex(data, GuideLayer), vi = LayerIndex(data, GravelLayer);
            if (gi < 0 || vi < 0)
            {
                EditorUtility.DisplayDialog("Painted Roads", "Run SpaceRat/Roads/Setup Road Paint Layers first, then paint with the RoadGuide layer.", "OK");
                return;
            }

            int w = data.alphamapWidth, h = data.alphamapHeight;
            var maps = data.GetAlphamaps(0, 0, w, h);
            var mask = new bool[w, h];
            int painted = 0;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (maps[y, x, gi] > 0.5f) { mask[x, y] = true; painted++; }
            if (painted == 0)
            {
                EditorUtility.DisplayDialog("Painted Roads", "No RoadGuide paint found on the terrain.", "OK");
                return;
            }

            var mat = RoadMat();

            // Trace the strokes into a graph of pixel polylines.
            Thin(mask, w, h);
            int skeleton = 0;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) if (mask[x, y]) skeleton++;
            float strokeWidthPx = skeleton > 0 ? (float)painted / skeleton : 3f;
            float pxSize = data.size.x / w;
            var graph = Graph.Trace(mask, w, h);
            graph.Prune(Mathf.Max(4f, strokeWidthPx * 1.5f));
            int junctions = graph.ResolveJunctions((RoadWidth * 0.5f - 0.5f) / pxSize);
            graph.MergeDegreeTwo();

            // Create the road meshes.
            var root = GameObject.Find(RootName);
            if (!root)
            {
                root = new GameObject(RootName);
                Undo.RegisterCreatedObjectUndo(root, "Build painted roads");
            }
            int next = root.transform.Cast<Transform>().Count(c => c.name.StartsWith(RoadPrefix));
            var worldLines = new List<(List<Vector3> pts, bool closed)>();
            int loops = 0;
            foreach (var e in graph.Edges)
            {
                var px = Smooth(e.Points, e.Closed, 4);
                var world = Resample(px.Select(p => PixelToWorld(terrain, p, w, h)).ToList(), e.Closed, MarkerSpacing);
                if (world.Count < (e.Closed ? 3 : 2)) continue;
                var go = BuildRoad(terrain, world, e.Closed, e.TrimA, e.TrimB, mat, RoadPrefix + (next++).ToString("000"));
                go.transform.SetParent(root.transform, true);
                Undo.RegisterCreatedObjectUndo(go, "Build painted roads");
                if (e.Closed) loops++;
                worldLines.Add((world, e.Closed));
            }

            // Repaint: guide strokes → gravel, plus a gravel strip under every road.
            Undo.RegisterCompleteObjectUndo(new Object[] { data }.Concat(data.alphamapTextures).ToArray(), "Paint gravel roads");
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float g = maps[y, x, gi];
                if (g <= 0f) continue;
                maps[y, x, gi] = 0f;
                maps[y, x, vi] += g;
            }
            float inner = (RoadWidth * 0.5f + GravelShoulder) / pxSize, outer = inner + 2f / pxSize;
            foreach (var (line, closed) in worldLines)
            {
                var pts = line.Select(p => WorldToPixel(terrain, p, w, h)).ToList();
                int n = pts.Count;
                for (int i = 0; i < n - 1; i++) StampSegment(maps, w, h, vi, pts[i], pts[i + 1], inner, outer);
                if (closed) StampSegment(maps, w, h, vi, pts[n - 1], pts[0], inner, outer);
            }
            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);
            EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);

            Debug.Log($"[Roads] Built {worldLines.Count} gravel road(s) ({loops} loop(s), {junctions} junction(s)) from {painted} guide pixels " +
                      $"(stroke ≈ {strokeWidthPx * pxSize:F1} m). Road material: {mat.name}.");
        }

        // ---------------------------------------------------------------- menu: clear / demo

        [MenuItem("SpaceRat/Roads/Remove Painted Roads")]
        static void Clear()
        {
            var root = GameObject.Find(RootName);
            if (!root) { Debug.Log("[Roads] No painted roads in the scene."); return; }
            var scene = root.scene;
            Undo.DestroyObjectImmediate(root);
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Roads] Removed painted roads. Terrain gravel paint is unchanged (use Undo to revert it).");
        }

        [MenuItem("SpaceRat/Roads/Paint Demo Guide Lines")]
        static void PaintDemo()
        {
            var terrain = ActiveTerrain();
            if (!terrain) return;
            var data = terrain.terrainData;
            int gi = LayerIndex(data, GuideLayer);
            if (gi < 0) { Setup(); gi = LayerIndex(data, GuideLayer); if (gi < 0) return; }

            int w = data.alphamapWidth, h = data.alphamapHeight;
            var maps = data.GetAlphamaps(0, 0, w, h);
            Undo.RegisterCompleteObjectUndo(new Object[] { data }.Concat(data.alphamapTextures).ToArray(), "Paint demo road guides");
            Vector3 c = terrain.transform.position + data.size * 0.5f;
            const float radius = 3f;
            // Winding main road west → east.
            Stroke(terrain, maps, gi, radius, Curve(t => c + new Vector3(Mathf.Lerp(-220f, 220f, t), 0f, Mathf.Sin(t * Mathf.PI * 2f) * 45f)));
            // T junction: branch north off the main road.
            Stroke(terrain, maps, gi, radius, Curve(t => c + new Vector3(Mathf.Lerp(-60f, -20f, t), 0f, Mathf.Lerp(Mathf.Sin(0.3636f * Mathf.PI * 2f) * 45f, 200f, t))));
            // Crossroad: straight road crossing the main road.
            Stroke(terrain, maps, gi, radius, Curve(t => c + new Vector3(Mathf.Lerp(110f, 140f, t), 0f, Mathf.Lerp(-180f, 160f, t))));
            // A loop track in the south-west.
            Stroke(terrain, maps, gi, radius, Curve(t => c + new Vector3(-150f + Mathf.Cos(t * Mathf.PI * 2f) * 50f, 0f, -170f + Mathf.Sin(t * Mathf.PI * 2f) * 35f)));
            data.SetAlphamaps(0, 0, maps);
            EditorUtility.SetDirty(data);
            Debug.Log("[Roads] Painted demo guide lines (winding road, T junction, crossroad, loop).");
        }

        static IEnumerable<Vector3> Curve(System.Func<float, Vector3> f)
        {
            for (int i = 0; i <= 400; i++) yield return f(i / 400f);
        }

        static void Stroke(Terrain t, float[,,] maps, int layer, float radius, IEnumerable<Vector3> points)
        {
            var d = t.terrainData;
            int w = d.alphamapWidth, h = d.alphamapHeight;
            float r = radius / (d.size.x / w);
            foreach (var p in points)
            {
                var c = WorldToPixel(t, p, w, h);
                for (int y = Mathf.FloorToInt(c.y - r - 1); y <= Mathf.CeilToInt(c.y + r + 1); y++)
                for (int x = Mathf.FloorToInt(c.x - r - 1); x <= Mathf.CeilToInt(c.x + r + 1); x++)
                {
                    if (x < 0 || y < 0 || x >= w || y >= h) continue;
                    float dist = Vector2.Distance(new Vector2(x, y), c);
                    float v = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(r - 1f, r + 1f, dist));
                    SetWeight(maps, x, y, layer, v);
                }
            }
        }

        // ---------------------------------------------------------------- road mesh

        static GameObject BuildRoad(Terrain t, List<Vector3> markers, bool closed, bool branchStart, bool branchEnd, Material mat, string name)
        {
            var centre = CatmullRom(markers, closed, SampleStep);
            if (closed) centre.Add(centre[0]); // duplicate ring closes the UVs
            int rings = centre.Count;
            var dist = new float[rings];
            for (int i = 1; i < rings; i++) dist[i] = dist[i - 1] + Vector2.Distance(XZ(centre[i - 1]), XZ(centre[i]));
            float total = dist[rings - 1];

            var verts = new Vector3[rings * Across];
            var uvs = new Vector2[verts.Length];
            var colors = new Color[verts.Length];
            float ty = t.transform.position.y;
            for (int i = 0; i < rings; i++)
            {
                Vector3 prev = centre[i > 0 ? i - 1 : closed ? rings - 2 : 0];
                Vector3 nxt = centre[i < rings - 1 ? i + 1 : closed ? 1 : rings - 1];
                var fwd = nxt - prev; fwd.y = 0f; fwd.Normalize();
                var right = Vector3.Cross(Vector3.up, fwd);

                float lift = Lift, alpha = 1f;
                if (!closed)
                {
                    float fromStart = dist[i], fromEnd = total - dist[i];
                    if (branchStart) lift = Mathf.Min(lift, Mathf.Lerp(BranchLift, Lift, fromStart / 3f));
                    else alpha = Mathf.Min(alpha, Mathf.Clamp01(fromStart / DeadEndFade));
                    if (branchEnd) lift = Mathf.Min(lift, Mathf.Lerp(BranchLift, Lift, fromEnd / 3f));
                    else alpha = Mathf.Min(alpha, Mathf.Clamp01(fromEnd / DeadEndFade));
                }

                for (int j = 0; j < Across; j++)
                {
                    float s = j / (Across - 1f);
                    var p = centre[i] + right * ((s - 0.5f) * RoadWidth);
                    p.y = t.SampleHeight(p) + ty + lift;
                    int k = i * Across + j;
                    verts[k] = p;
                    uvs[k] = new Vector2(s, dist[i] / RoadWidth);
                    colors[k] = new Color(1f, 1f, 1f, alpha);
                }
            }

            var tris = new List<int>((rings - 1) * (Across - 1) * 6);
            for (int i = 0; i < rings - 1; i++)
            for (int j = 0; j < Across - 1; j++)
            {
                int a = i * Across + j, b = a + 1, c = a + Across, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            var mesh = new UnityEngine.Mesh { name = name };
            if (verts.Length > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
            return go;
        }

        static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);

        /// <summary>Uniform Catmull-Rom through the markers, sampled roughly every <paramref name="step"/> metres.</summary>
        static List<Vector3> CatmullRom(List<Vector3> m, bool closed, float step)
        {
            int n = m.Count;
            Vector3 P(int i) => closed ? m[((i % n) + n) % n] : m[Mathf.Clamp(i, 0, n - 1)];
            var result = new List<Vector3>();
            int segments = closed ? n : n - 1;
            for (int k = 0; k < segments; k++)
            {
                Vector3 p0 = P(k - 1), p1 = P(k), p2 = P(k + 1), p3 = P(k + 2);
                int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(p1, p2) / step));
                for (int s = 0; s < samples; s++)
                {
                    float u = s / (float)samples, u2 = u * u, u3 = u2 * u;
                    result.Add(0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (-p0 + 3f * p1 - 3f * p2 + p3) * u3));
                }
            }
            if (!closed) result.Add(m[n - 1]);
            return result;
        }

        /// <summary>EasyRoads3D's dirt-road material if installed, otherwise a URP Lit gravel material.</summary>
        static Material RoadMat()
        {
            var er = Resources.Load<Material>(RoadMaterial);
            if (er && er.shader.isSupported) return er;
            string path = $"{Folder}/M_GravelRoad.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat) return mat;
            Directory.CreateDirectory(Folder);
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_GravelRoad" };
            mat.SetTexture("_BaseMap", EnsureTexture("T_Gravel", 512, GravelPixel, false));
            mat.SetColor("_BaseColor", new Color(0.85f, 0.8f, 0.72f));
            mat.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        // ---------------------------------------------------------------- painting helpers

        static void StampSegment(float[,,] maps, int w, int h, int layer, Vector2 a, Vector2 b, float inner, float outer)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - outer)), x1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + outer));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - outer)), y1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + outer));
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float dist = DistToSegment(new Vector2(x, y), a, b);
                if (dist >= outer) continue;
                SetWeight(maps, x, y, layer, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, dist)));
            }
        }

        /// <summary>Raises one layer's weight to at least <paramref name="v"/>, scaling the others to keep the sum at 1.</summary>
        static void SetWeight(float[,,] maps, int x, int y, int layer, float v)
        {
            float old = maps[y, x, layer];
            if (v <= old) return;
            int n = maps.GetLength(2);
            float others = 1f - old;
            float scale = others > 1e-5f ? (1f - v) / others : 0f;
            for (int l = 0; l < n; l++) maps[y, x, l] = l == layer ? v : maps[y, x, l] * scale;
        }

        static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        // ---------------------------------------------------------------- geometry helpers

        static Vector3 PixelToWorld(Terrain t, Vector2 p, int w, int h)
        {
            var d = t.terrainData;
            var pos = t.transform.position + new Vector3((p.x + 0.5f) / w * d.size.x, 0f, (p.y + 0.5f) / h * d.size.z);
            pos.y = t.SampleHeight(pos) + t.transform.position.y;
            return pos;
        }

        static Vector2 WorldToPixel(Terrain t, Vector3 p, int w, int h)
        {
            var d = t.terrainData;
            var local = p - t.transform.position;
            return new Vector2(local.x / d.size.x * w - 0.5f, local.z / d.size.z * h - 0.5f);
        }

        static List<Vector2> Smooth(List<Vector2> pts, bool closed, int radius)
        {
            int n = pts.Count;
            if (n < 3) return new List<Vector2>(pts);
            var result = new List<Vector2>(n);
            for (int i = 0; i < n; i++)
            {
                if (!closed && (i == 0 || i == n - 1)) { result.Add(pts[i]); continue; }
                Vector2 sum = Vector2.zero; int count = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int j = i + k;
                    if (closed) j = ((j % n) + n) % n;
                    else if (j < 0 || j >= n) continue;
                    sum += pts[j]; count++;
                }
                result.Add(sum / count);
            }
            return result;
        }

        /// <summary>Evenly spaced points along a polyline (heights re-sampled from the input).</summary>
        static List<Vector3> Resample(List<Vector3> pts, bool closed, float spacing)
        {
            var src = new List<Vector3>(pts);
            if (closed) src.Add(pts[0]);
            var cum = new float[src.Count];
            for (int i = 1; i < src.Count; i++) cum[i] = cum[i - 1] + Vector3.Distance(src[i - 1], src[i]);
            float length = cum[^1];
            if (length < 1f) return new List<Vector3>();
            int segments = Mathf.Max(closed ? 3 : 1, Mathf.RoundToInt(length / spacing));
            var result = new List<Vector3>();
            int k = 1;
            int count = closed ? segments : segments + 1;
            for (int i = 0; i < count; i++)
            {
                float d = length * i / segments;
                while (k < src.Count - 1 && cum[k] < d) k++;
                float seg = cum[k] - cum[k - 1];
                float t = seg > 1e-5f ? (d - cum[k - 1]) / seg : 0f;
                result.Add(Vector3.Lerp(src[k - 1], src[k], Mathf.Clamp01(t)));
            }
            return result;
        }

        // ---------------------------------------------------------------- skeleton

        /// <summary>Zhang–Suen thinning followed by removal of staircase elbow pixels.</summary>
        static void Thin(bool[,] m, int w, int h)
        {
            bool Get(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && m[x, y];
            var remove = new List<Vector2Int>();
            bool changed = true;
            while (changed)
            {
                changed = false;
                for (int pass = 0; pass < 2; pass++)
                {
                    remove.Clear();
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        if (!m[x, y]) continue;
                        bool p2 = Get(x, y + 1), p3 = Get(x + 1, y + 1), p4 = Get(x + 1, y), p5 = Get(x + 1, y - 1);
                        bool p6 = Get(x, y - 1), p7 = Get(x - 1, y - 1), p8 = Get(x - 1, y), p9 = Get(x - 1, y + 1);
                        int b = (p2 ? 1 : 0) + (p3 ? 1 : 0) + (p4 ? 1 : 0) + (p5 ? 1 : 0) + (p6 ? 1 : 0) + (p7 ? 1 : 0) + (p8 ? 1 : 0) + (p9 ? 1 : 0);
                        if (b < 2 || b > 6) continue;
                        int a = 0;
                        bool[] seq = { p2, p3, p4, p5, p6, p7, p8, p9, p2 };
                        for (int i = 0; i < 8; i++) if (!seq[i] && seq[i + 1]) a++;
                        if (a != 1) continue;
                        if (pass == 0 ? (p2 && p4 && p6) || (p4 && p6 && p8) : (p2 && p4 && p8) || (p2 && p6 && p8)) continue;
                        remove.Add(new Vector2Int(x, y));
                    }
                    foreach (var p in remove) m[p.x, p.y] = false;
                    if (remove.Count > 0) changed = true;
                }
            }

            // Elbows: a pixel whose only neighbours are one horizontal and one vertical 4-neighbour.
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if (!m[x, y]) continue;
                bool n = Get(x, y + 1), s = Get(x, y - 1), e = Get(x + 1, y), wv = Get(x - 1, y);
                bool diag = Get(x + 1, y + 1) || Get(x + 1, y - 1) || Get(x - 1, y - 1) || Get(x - 1, y + 1);
                if (diag) continue;
                if ((n ^ s) && (e ^ wv) && (n ? 1 : 0) + (s ? 1 : 0) + (e ? 1 : 0) + (wv ? 1 : 0) == 2) m[x, y] = false;
            }
        }

        // ---------------------------------------------------------------- graph

        class Edge
        {
            public List<Vector2> Points;
            public int A, B; // node ids at Points[0] / Points[^1]; -1 for closed loops
            public bool Closed;
            public bool TrimA, TrimB; // that end was cut back at a junction
            public float Length
            {
                get { float l = 0; for (int i = 1; i < Points.Count; i++) l += Vector2.Distance(Points[i - 1], Points[i]); return l; }
            }
        }

        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup] // Nbrs is a constant table
        class Graph
        {
            public readonly List<Vector2> Nodes = new();
            public readonly List<Edge> Edges = new();

            static readonly Vector2Int[] Nbrs =
            {
                new(0, 1), new(1, 0), new(0, -1), new(-1, 0), // 4-neighbours first
                new(1, 1), new(1, -1), new(-1, -1), new(-1, 1),
            };

            public static Graph Trace(bool[,] m, int w, int h)
            {
                var g = new Graph();
                bool Get(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < w && p.y < h && m[p.x, p.y];
                int Degree(Vector2Int p) { int d = 0; foreach (var o in Nbrs) if (Get(p + o)) d++; return d; }

                var nid = new int[w, h];
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) nid[x, y] = -1;

                // Junction clusters (degree ≥ 3) and endpoints (degree 1) become nodes.
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var p = new Vector2Int(x, y);
                    if (!m[x, y] || nid[x, y] >= 0) continue;
                    int deg = Degree(p);
                    if (deg == 1)
                    {
                        nid[x, y] = g.Nodes.Count;
                        g.Nodes.Add(p);
                    }
                    else if (deg >= 3)
                    {
                        int id = g.Nodes.Count;
                        var queue = new Queue<Vector2Int>();
                        queue.Enqueue(p); nid[x, y] = id;
                        Vector2 sum = Vector2.zero; int count = 0;
                        while (queue.Count > 0)
                        {
                            var q = queue.Dequeue();
                            sum += q; count++;
                            foreach (var o in Nbrs)
                            {
                                var r = q + o;
                                if (Get(r) && nid[r.x, r.y] < 0 && Degree(r) >= 3) { nid[r.x, r.y] = id; queue.Enqueue(r); }
                            }
                        }
                        g.Nodes.Add(sum / count);
                    }
                }

                var visited = new bool[w, h];
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (nid[x, y] < 0) continue;
                    var start = new Vector2Int(x, y);
                    foreach (var o in Nbrs)
                    {
                        var q = start + o;
                        if (!Get(q) || nid[q.x, q.y] >= 0 || visited[q.x, q.y]) continue;
                        g.Walk(m, w, h, nid, visited, start, q);
                    }
                }

                // Loops with no nodes on them.
                for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    if (!m[x, y] || visited[x, y] || nid[x, y] >= 0) continue;
                    var s = new Vector2Int(x, y);
                    var pts = new List<Vector2> { s };
                    visited[x, y] = true;
                    var cur = s;
                    while (true)
                    {
                        bool moved = false;
                        foreach (var o in Nbrs)
                        {
                            var r = cur + o;
                            if (Get(r) && !visited[r.x, r.y] && nid[r.x, r.y] < 0) { visited[r.x, r.y] = true; pts.Add(r); cur = r; moved = true; break; }
                        }
                        if (!moved) break;
                    }
                    if (pts.Count >= 8) g.Edges.Add(new Edge { Points = pts, A = -1, B = -1, Closed = true });
                }
                return g;
            }

            void Walk(bool[,] m, int w, int h, int[,] nid, bool[,] visited, Vector2Int start, Vector2Int first)
            {
                bool Get(Vector2Int p) => p.x >= 0 && p.y >= 0 && p.x < w && p.y < h && m[p.x, p.y];
                int startId = nid[start.x, start.y];
                var pts = new List<Vector2> { Nodes[startId], first };
                visited[first.x, first.y] = true;
                var prev = start;
                var cur = first;
                int end = -1;
                while (end < 0)
                {
                    // Arrive at a node if one is adjacent (not straight back into the start cluster).
                    foreach (var o in Nbrs)
                    {
                        var r = cur + o;
                        if (r == prev || !Get(r) || nid[r.x, r.y] < 0) continue;
                        if (nid[r.x, r.y] == startId && pts.Count < 4) continue;
                        end = nid[r.x, r.y];
                        break;
                    }
                    if (end >= 0) break;

                    bool moved = false;
                    foreach (var o in Nbrs)
                    {
                        var r = cur + o;
                        if (!Get(r) || visited[r.x, r.y] || nid[r.x, r.y] >= 0) continue;
                        visited[r.x, r.y] = true;
                        pts.Add(r);
                        prev = cur; cur = r; moved = true;
                        break;
                    }
                    if (!moved)
                    {
                        end = Nodes.Count; // dead end: becomes an endpoint
                        Nodes.Add(cur);
                        pts.RemoveAt(pts.Count - 1);
                    }
                }
                pts.Add(Nodes[end]);
                Edges.Add(new Edge { Points = pts, A = startId, B = end });
            }

            int[] Degrees()
            {
                var deg = new int[Nodes.Count];
                foreach (var e in Edges)
                {
                    if (e.Closed) continue;
                    deg[e.A]++; deg[e.B]++;
                }
                return deg;
            }

            /// <summary>Drops short spurs left by thinning and tiny isolated scribbles.</summary>
            public void Prune(float minSpur)
            {
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    var deg = Degrees();
                    for (int i = Edges.Count - 1; i >= 0; i--)
                    {
                        var e = Edges[i];
                        if (e.Closed) continue;
                        bool spur = (deg[e.A] == 1 && deg[e.B] >= 3) || (deg[e.B] == 1 && deg[e.A] >= 3);
                        bool speck = deg[e.A] == 1 && deg[e.B] == 1 && e.Length < minSpur * 2f;
                        if ((spur && e.Length < minSpur) || speck) { Edges.RemoveAt(i); changed = true; }
                    }
                    if (MergeDegreeTwo()) changed = true;
                }
            }

            /// <summary>Joins the two edges at every node with exactly two edges.</summary>
            public bool MergeDegreeTwo()
            {
                bool any = false;
                bool changed = true;
                while (changed)
                {
                    changed = false;
                    var deg = Degrees();
                    for (int n = 0; n < Nodes.Count && !changed; n++)
                    {
                        if (deg[n] != 2) continue;
                        var at = Edges.Where(e => !e.Closed && (e.A == n || e.B == n)).ToList();
                        if (at.Count == 1) // one edge with both ends here: a loop
                        {
                            var loop = at[0];
                            loop.Points.RemoveAt(loop.Points.Count - 1);
                            loop.Closed = true; loop.A = loop.B = -1;
                        }
                        else
                        {
                            var e1 = at[0]; var e2 = at[1];
                            if (e1.B != n) Reverse(e1);
                            if (e2.A != n) Reverse(e2);
                            e1.Points.AddRange(e2.Points.Skip(1));
                            e1.B = e2.B;
                            e1.TrimB = e2.TrimB;
                            Edges.Remove(e2);
                        }
                        changed = any = true;
                    }
                }
                return any;
            }

            /// <summary>
            /// At each junction keeps the straightest pair of branches as one through road (merged later)
            /// and cuts the other branches back so they end at the through road's edge.
            /// </summary>
            public int ResolveJunctions(float trimPx)
            {
                var deg = Degrees();
                int count = 0;
                for (int n = 0; n < deg.Length; n++)
                {
                    if (deg[n] < 3) continue;
                    count++;
                    var at = Edges.Where(e => !e.Closed && (e.A == n || e.B == n)).ToList();
                    foreach (var e in at) if (e.A != n) Reverse(e); // all start at the junction
                    var dirs = at.Select(e => Direction(e.Points, 8f)).ToList();
                    int bi = 0, bj = 1; float best = 2f;
                    for (int i = 0; i < at.Count; i++)
                    for (int j = i + 1; j < at.Count; j++)
                    {
                        float dot = Vector2.Dot(dirs[i], dirs[j]); // -1 = perfectly straight through
                        if (dot < best) { best = dot; bi = i; bj = j; }
                    }
                    for (int i = 0; i < at.Count; i++)
                    {
                        if (i == bi || i == bj) continue;
                        var e = at[i];
                        Trim(e.Points, trimPx);
                        e.TrimA = true;
                        e.A = Nodes.Count; // detach: new endpoint at the cut
                        Nodes.Add(e.Points[0]);
                    }
                }
                return count;
            }

            static Vector2 Direction(List<Vector2> pts, float along)
            {
                float l = 0;
                for (int i = 1; i < pts.Count; i++)
                {
                    l += Vector2.Distance(pts[i - 1], pts[i]);
                    if (l >= along) return (pts[i] - pts[0]).normalized;
                }
                return (pts[^1] - pts[0]).normalized;
            }

            /// <summary>Removes the first <paramref name="dist"/> pixels of length from the start of the polyline.</summary>
            static void Trim(List<Vector2> pts, float dist)
            {
                var origin = pts[0];
                for (int i = 1; i < pts.Count; i++)
                {
                    if (Vector2.Distance(pts[i], origin) < dist) continue;
                    var a = pts[i - 1]; var b = pts[i];
                    // point on segment a-b at distance 'dist' from origin
                    float lo = 0f, hi = 1f;
                    for (int k = 0; k < 12; k++)
                    {
                        float mid = (lo + hi) * 0.5f;
                        if (Vector2.Distance(Vector2.Lerp(a, b, mid), origin) < dist) lo = mid; else hi = mid;
                    }
                    pts.RemoveRange(0, i);
                    pts.Insert(0, Vector2.Lerp(a, b, hi));
                    return;
                }
                if (pts.Count > 2) pts.RemoveRange(0, pts.Count - 2); // branch shorter than the trim: keep its tail
            }

            static void Reverse(Edge e)
            {
                e.Points.Reverse();
                (e.A, e.B) = (e.B, e.A);
                (e.TrimA, e.TrimB) = (e.TrimB, e.TrimA);
            }
        }

        // ---------------------------------------------------------------- assets

        static Terrain ActiveTerrain()
        {
            var t = Selection.activeGameObject ? Selection.activeGameObject.GetComponent<Terrain>() : null;
            if (!t) t = Terrain.activeTerrain;
            if (!t) EditorUtility.DisplayDialog("Painted Roads", "No terrain found in the open scene.", "OK");
            return t;
        }

        static int LayerIndex(TerrainData d, string name)
        {
            var layers = d.terrainLayers;
            for (int i = 0; i < layers.Length; i++) if (layers[i] && layers[i].name == "TL_" + name) return i;
            return -1;
        }

        static TerrainLayer EnsureLayer(string name, Texture2D tex, float tile)
        {
            string path = $"{Folder}/TL_{name}.terrainlayer";
            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
            if (layer) return layer;
            layer = new TerrainLayer { diffuseTexture = tex, tileSize = new Vector2(tile, tile), smoothness = 0f, metallic = 0f };
            AssetDatabase.CreateAsset(layer, path);
            return layer;
        }

        static Texture2D EnsureTexture(string name, int size, System.Func<int, int, Color> pixel, bool point)
        {
            string path = $"{Folder}/{name}.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing) return existing;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = pixel(x, y);
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.wrapMode = TextureWrapMode.Repeat;
            if (point) { importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; }
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Seamless procedural gravel: layered value noise plus pebble speckles.</summary>
        static Color GravelPixel(int x, int y)
        {
            const int size = 512;
            float n = 0f, amp = 0.5f, total = 0f;
            foreach (int cells in new[] { 8, 32, 64, 128 })
            {
                n += ValueNoise(x * cells / (float)size, y * cells / (float)size, cells, cells) * amp;
                total += amp; amp *= 0.6f;
            }
            n /= total;
            float pebble = ValueNoise(x / 3f, y / 3f, size / 3, 7);
            var dark = new Color(0.30f, 0.27f, 0.23f);
            var light = new Color(0.62f, 0.58f, 0.52f);
            var c = Color.Lerp(dark, light, Mathf.SmoothStep(0.2f, 0.8f, n));
            if (pebble > 0.78f) c = Color.Lerp(c, new Color(0.75f, 0.72f, 0.66f), (pebble - 0.78f) * 4f);
            else if (pebble < 0.15f) c *= 0.75f;
            return c;
        }

        static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx); fy = fy * fy * (3f - 2f * fy);
            float H(int ix, int iy)
            {
                ix = ((ix % period) + period) % period; iy = ((iy % period) + period) % period;
                uint hsh = (uint)(ix * 374761393 + iy * 668265263 + seed * 982451653);
                hsh = (hsh ^ (hsh >> 13)) * 1274126177u;
                return ((hsh ^ (hsh >> 16)) & 0xffff) / 65535f;
            }
            return Mathf.Lerp(Mathf.Lerp(H(x0, y0), H(x0 + 1, y0), fx), Mathf.Lerp(H(x0, y0 + 1), H(x0 + 1, y0 + 1), fx), fy);
        }
    }
}
