using System.Collections.Generic;
using System.Linq;
using SpaceRat.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Scene tools for <see cref="WaypointRoad"/>, same feel as FenceLayout:
    /// Ctrl+click adds a waypoint (near the road = insert, otherwise extends the nearer end),
    /// Ctrl+Shift+click deletes one, click a waypoint to select it and drag its handle.
    /// The mesh rebuilds live.
    /// </summary>
    [CustomEditor(typeof(WaypointRoad))]
    class WaypointRoadEditor : Editor
    {
        WaypointRoad _road;
        int _selected = -1;

        void OnEnable()
        {
            _road = (WaypointRoad)target;
            Undo.undoRedoPerformed += OnUndoRedo;
        }

        void OnDisable() => Undo.undoRedoPerformed -= OnUndoRedo;

        void OnUndoRedo()
        {
            if (_road) Rebuild(_road);
        }

        // ------------------------------------------------------------ inspector

        public override void OnInspectorGUI()
        {
            EditorGUILayout.HelpBox(
                "Ctrl+click on the ground: add a waypoint (click near the road to insert one)\n" +
                "Ctrl+Shift+click: delete the nearest waypoint\n" +
                "Click a waypoint sphere, then drag its arrows to move it",
                MessageType.Info);

            EditorGUI.BeginChangeCheck();
            DrawDefaultInspector();
            if (EditorGUI.EndChangeCheck()) Rebuild(_road);

            var world = WorldPoints(_road);
            float length = 0f;
            var line = world.Count >= 2 ? PaintedRoadBuilder.CatmullRom(world, IsClosed(_road), 1f) : world;
            for (int i = 1; i < line.Count; i++) length += Vector3.Distance(line[i - 1], line[i]);
            EditorGUILayout.LabelField("Waypoints", $"{_road.points.Count}   (length ≈ {length:F0} m)");

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_road.points.Count < 2))
            {
                if (GUILayout.Button("Paint Gravel Under Road"))
                {
                    if (!PaintedRoadBuilder.PaintGravelUnder(world, IsClosed(_road), _road.width, _road.gravelShoulder))
                        EditorUtility.DisplayDialog("Waypoint Road", "No terrain found under the road's first waypoint.", "OK");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("Rebuild Mesh")) Rebuild(_road);
                    if (GUILayout.Button("Reverse Direction"))
                    {
                        Undo.RecordObject(_road, "Reverse Road");
                        _road.points.Reverse();
                        (_road.startEnd, _road.endEnd) = (_road.endEnd, _road.startEnd);
                        Rebuild(_road);
                    }
                    if (GUILayout.Button("Snap All To Ground"))
                    {
                        Undo.RecordObject(_road, "Snap Road To Ground");
                        for (int i = 0; i < _road.points.Count; i++)
                            _road.points[i] = _road.transform.InverseTransformPoint(Snap(_road, _road.transform.TransformPoint(_road.points[i])));
                        Rebuild(_road);
                    }
                }
            }
            using (new EditorGUI.DisabledScope(_road.points.Count == 0))
            {
                if (GUILayout.Button("Clear Waypoints") &&
                    EditorUtility.DisplayDialog("Waypoint Road", "Remove all waypoints from this road?", "Clear", "Cancel"))
                {
                    Undo.RecordObject(_road, "Clear Road");
                    _road.points.Clear();
                    _selected = -1;
                    Rebuild(_road);
                }
            }
        }

        // ------------------------------------------------------------ scene view

        void OnSceneGUI()
        {
            var e = Event.current;
            var t = _road.transform;
            var world = WorldPoints(_road);

            // Centre line + waypoints.
            if (world.Count >= 2)
            {
                Handles.color = new Color(1f, 0.85f, 0.2f, 0.9f);
                Handles.DrawAAPolyLine(3f, PaintedRoadBuilder.CatmullRom(world, IsClosed(_road), 1f).ToArray());
            }
            for (int i = 0; i < world.Count; i++)
            {
                float size = HandleUtility.GetHandleSize(world[i]) * 0.12f;
                Handles.color = i == _selected ? Color.cyan : i == 0 ? Color.green : i == world.Count - 1 ? Color.red : Color.white;
                if (Handles.Button(world[i], Quaternion.identity, size, size * 1.3f, Handles.SphereHandleCap))
                {
                    _selected = i;
                    Repaint();
                }
                Handles.Label(world[i] + Vector3.up * size * 4f, i.ToString());
            }

            if (_selected >= 0 && _selected < world.Count)
            {
                EditorGUI.BeginChangeCheck();
                var moved = Handles.PositionHandle(world[_selected], Quaternion.identity);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(_road, "Move Road Waypoint");
                    if (_road.snapPointsToGround) moved = Snap(_road, moved);
                    _road.points[_selected] = t.InverseTransformPoint(moved);
                    Rebuild(_road);
                }
            }

            // Ctrl / Ctrl+Shift clicks.
            int id = GUIUtility.GetControlID(FocusType.Passive);
            if (!e.control) return;
            HandleUtility.AddDefaultControl(id); // stop the click from changing the selection
            if (e.type == EventType.MouseMove) SceneView.RepaintAll();

            if (!Raycast(_road, HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit)) return;
            Handles.color = e.shift ? Color.red : Color.green;
            Handles.DrawWireDisc(hit, Vector3.up, _road.width * 0.5f);

            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (e.shift) DeleteNearest(world, hit);
            else AddPoint(world, hit);
            e.Use();
        }

        void AddPoint(List<Vector3> world, Vector3 hit)
        {
            Undo.RecordObject(_road, "Add Road Waypoint");
            var local = _road.transform.InverseTransformPoint(hit);
            int index = world.Count;

            if (world.Count >= 2)
            {
                // Near an existing segment → insert into it.
                int segs = IsClosed(_road) ? world.Count : world.Count - 1;
                float best = _road.width; int bestSeg = -1;
                for (int i = 0; i < segs; i++)
                {
                    float d = DistXZ(hit, world[i], world[(i + 1) % world.Count]);
                    if (d < best) { best = d; bestSeg = i; }
                }
                if (bestSeg >= 0) index = bestSeg + 1;
                else if (!_road.closed && FlatDist(hit, world[0]) < FlatDist(hit, world[^1])) index = 0; // extend the nearer end
            }
            _road.points.Insert(index, local);
            _selected = index;
            Rebuild(_road);
        }

        void DeleteNearest(List<Vector3> world, Vector3 hit)
        {
            if (world.Count == 0) return;
            int nearest = 0;
            for (int i = 1; i < world.Count; i++) if (FlatDist(hit, world[i]) < FlatDist(hit, world[nearest])) nearest = i;
            if (FlatDist(hit, world[nearest]) > Mathf.Max(_road.width * 2f, HandleUtility.GetHandleSize(hit))) return;
            Undo.RecordObject(_road, "Delete Road Waypoint");
            _road.points.RemoveAt(nearest);
            _selected = -1;
            Rebuild(_road);
        }

        // ------------------------------------------------------------ mesh

        static void Rebuild(WaypointRoad road)
        {
            var mf = road.GetComponent<MeshFilter>();
            var mr = road.GetComponent<MeshRenderer>();
            string meshName = "WaypointRoad_" + road.GetEntityId();
            var mesh = mf.sharedMesh;
            if (!mesh || mesh.name != meshName || AssetDatabase.Contains(mesh)) mesh = new UnityEngine.Mesh { name = meshName };

            System.Func<Vector3, float?> ground = null;
            if (road.followTerrain)
                ground = p =>
                {
                    var terrain = PaintedRoadBuilder.TerrainUnder(p);
                    return terrain ? terrain.SampleHeight(p) + terrain.transform.position.y : (float?)null;
                };
            PaintedRoadBuilder.BuildRoadMesh(WorldPoints(road), IsClosed(road), road.width, road.startEnd, road.endEnd, ground, road.transform, mesh);

            mf.sharedMesh = mesh;
            if (!mr.sharedMaterial || road.material) mr.sharedMaterial = road.material ? road.material : PaintedRoadBuilder.RoadMat();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = road.GetComponent<MeshCollider>();
            if (!col) col = road.gameObject.AddComponent<MeshCollider>();
            col.sharedMesh = null;
            col.sharedMesh = mesh.vertexCount > 0 ? mesh : null;

            EditorUtility.SetDirty(road);
            if (!Application.isPlaying) EditorSceneManager.MarkSceneDirty(road.gameObject.scene);
        }

        // ------------------------------------------------------------ helpers

        static bool IsClosed(WaypointRoad r) => r.closed && r.points.Count >= 3;

        static List<Vector3> WorldPoints(WaypointRoad r) => r.points.Select(p => r.transform.TransformPoint(p)).ToList();

        static float FlatDist(Vector3 a, Vector3 b) => Vector2.Distance(PaintedRoadBuilder.XZ(a), PaintedRoadBuilder.XZ(b));

        static float DistXZ(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector2 p2 = PaintedRoadBuilder.XZ(p), a2 = PaintedRoadBuilder.XZ(a), ab = PaintedRoadBuilder.XZ(b) - a2;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p2 - a2, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p2, a2 + ab * t);
        }

        /// <summary>Scene raycast that ignores this road's own collider.</summary>
        static bool Raycast(WaypointRoad road, Ray ray, out Vector3 point)
        {
            foreach (var h in Physics.RaycastAll(ray, 10000f).OrderBy(h => h.distance))
            {
                if (h.collider.transform.IsChildOf(road.transform)) continue;
                point = h.point;
                return true;
            }
            point = default;
            return false;
        }

        static Vector3 Snap(WaypointRoad road, Vector3 p) =>
            Raycast(road, new Ray(p + Vector3.up * 500f, Vector3.down), out var hit) ? hit : p;

        [MenuItem("SpaceRat/Roads/Rebuild All Waypoint Roads")]
        static void RebuildAll()
        {
            var roads = Object.FindObjectsByType<WaypointRoad>(FindObjectsInactive.Include);
            foreach (var r in roads) Rebuild(r);
            Debug.Log($"[Roads] Rebuilt {roads.Length} waypoint road(s).");
        }

        // ------------------------------------------------------------ create

        [MenuItem("GameObject/SpaceRat/Waypoint Road", false, 10)]
        [MenuItem("SpaceRat/Roads/New Waypoint Road")]
        static void Create(MenuCommand cmd)
        {
            var go = new GameObject("Waypoint Road", typeof(WaypointRoad), typeof(MeshCollider));
            GameObjectUtility.SetParentAndAlign(go, cmd.context as GameObject);
            go.GetComponent<MeshRenderer>().sharedMaterial = PaintedRoadBuilder.RoadMat();
            Undo.RegisterCreatedObjectUndo(go, "Create Waypoint Road");
            Selection.activeGameObject = go;
            Debug.Log("[Roads] Waypoint Road created: Ctrl+click on the ground in the Scene view to add waypoints.");
        }
    }
}
