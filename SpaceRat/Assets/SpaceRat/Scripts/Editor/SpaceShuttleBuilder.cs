using System.Collections.Generic;
using SpaceRat.Characters;
using SpaceRat.Environment;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;

namespace SpaceRat.EditorTools
{
    /// <summary>
    /// Builds Assets/DDG/SpaceShip/SpaceShuttle.prefab from primitives: shuttle-style exterior,
    /// hollow interior with cockpit, sliding bulkhead door and a hinged rear cargo ramp.
    /// Re-run after tweaking numbers here; it overwrites the prefab, materials and meshes.
    /// Ship origin = ground under the centre of the hull, +Z = forward.
    /// </summary>
    [NoAutoStaticsCleanup] // material cache is rebuilt on every Build()
    public static class SpaceShuttleBuilder
    {
        const string Dir = "Assets/DDG/SpaceShip";

        // Interior dimensions
        const float ZRear = -5f, ZFront = 7.5f, YFloor = 1.0f, YCeil = 3.6f, HalfW = 2f;
        const float ZBulkhead = 4f, ZWindTop = 6.6f;

        static Material hull, belly, inner, floorM, trim, glass, glow, engine;

        [MenuItem("SpaceRat/Build Space Shuttle Prefab")]
        public static void Build()
        {
            EnsureMaterials();

            var root = new GameObject("SpaceShuttle").transform;
            try
            {
                BuildStructure(Group(root, "Structure"), Group(root, "Exterior"));
                BuildExterior(root.Find("Exterior"));
                BuildCockpit(Group(root, "Cockpit"));
                BuildHold(Group(root, "CargoHold"));
                BuildDoors(root.Find("Structure"), root.Find("Exterior"));
                var lights = Group(root, "Lights");
                AddLight(lights, "HoldLight", new Vector3(0, 3.2f, -0.6f), new Color(0.85f, 0.95f, 1f), 8f, 2.2f);
                AddLight(lights, "CockpitLight", new Vector3(0, 3.2f, 5.6f), new Color(0.4f, 0.85f, 1f), 5f, 1.5f);

                PrefabUtility.SaveAsPrefabAsset(root.gameObject, Dir + "/SpaceShuttle.prefab");
                AssetDatabase.SaveAssets();
                Debug.Log("SpaceShuttleBuilder: saved " + Dir + "/SpaceShuttle.prefab");
            }
            finally
            {
                Object.DestroyImmediate(root.gameObject);
            }
        }

        /// <summary>Shared ship materials (also used by <see cref="SparrowInteriorBuilder"/>).</summary>
        internal static Material Hull => hull;
        internal static Material Belly => belly;
        internal static Material Inner => inner;
        internal static Material FloorMat => floorM;
        internal static Material Trim => trim;
        internal static Material Glass => glass;
        internal static Material Glow => glow;

        internal static void EnsureMaterials()
        {
            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/DDG", "SpaceShip");
            if (!AssetDatabase.IsValidFolder(Dir + "/Meshes")) AssetDatabase.CreateFolder(Dir, "Meshes");

            hull = Mat("M_Ship_Hull", new Color(0.86f, 0.87f, 0.88f), 0.2f, 0.5f);
            belly = Mat("M_Ship_Belly", new Color(0.09f, 0.09f, 0.10f), 0.1f, 0.3f);
            inner = Mat("M_Ship_Interior", new Color(0.32f, 0.34f, 0.37f), 0.5f, 0.4f);
            floorM = Mat("M_Ship_Floor", new Color(0.16f, 0.17f, 0.19f), 0.8f, 0.35f);
            trim = Mat("M_Ship_Trim", new Color(0.55f, 0.57f, 0.6f), 0.9f, 0.65f);
            glass = Mat("M_Ship_Glass", new Color(0.3f, 0.6f, 0.75f, 0.25f), 0f, 0.95f);
            Transparent(glass);
            glow = Mat("M_Ship_Glow", new Color(0.1f, 0.85f, 1f), 0f, 0.8f);
            Emit(glow, new Color(0.1f, 0.85f, 1f) * 3f);
            engine = Mat("M_Ship_EngineGlow", new Color(0.4f, 0.7f, 1f), 0f, 0.8f);
            Emit(engine, new Color(0.35f, 0.6f, 1f) * 5f);
        }

        // ------------------------------------------------------------------ parts

        static void BuildStructure(Transform str, Transform ext)
        {
            float len = ZFront - ZRear, zMid = (ZFront + ZRear) * 0.5f;
            float roofLen = ZWindTop - ZRear, roofMid = (ZWindTop + ZRear) * 0.5f;
            float wallH = YCeil - YFloor, wallY = (YFloor + YCeil) * 0.5f;

            Box(str, "Floor", new Vector3(0, YFloor - 0.075f, zMid), new Vector3(HalfW * 2, 0.15f, len), floorM, true);
            Box(str, "Ceiling", new Vector3(0, YCeil + 0.075f, roofMid), new Vector3(HalfW * 2, 0.15f, roofLen), inner, true);
            foreach (int s in new[] { -1, 1 })
                Box(str, s < 0 ? "WallL" : "WallR", new Vector3(s * (HalfW + 0.075f), wallY, zMid), new Vector3(0.15f, wallH + 0.3f, len), inner, true);
            Box(str, "FrontLower", new Vector3(0, 1.5f, ZFront + 0.075f), new Vector3(HalfW * 2, 1.3f, 0.15f), inner, true);
            foreach (int s in new[] { -1, 1 })
                Box(str, "RearJamb", new Vector3(s * 1.9f, wallY, ZRear - 0.075f), new Vector3(0.2f, wallH, 0.15f), trim, true);

            // Bulkhead with door opening x -0.65..0.65, y 1.0..3.3
            Box(str, "BulkheadL", new Vector3(-1.325f, wallY, ZBulkhead), new Vector3(1.35f, wallH, 0.15f), inner, true);
            Box(str, "BulkheadR", new Vector3(1.325f, wallY, ZBulkhead), new Vector3(1.35f, wallH, 0.15f), inner, true);
            Box(str, "BulkheadTop", new Vector3(0, 3.45f, ZBulkhead), new Vector3(1.3f, 0.3f, 0.15f), inner, true);
            Box(str, "DoorFrameGlow", new Vector3(0, 3.32f, ZBulkhead - 0.09f), new Vector3(1.5f, 0.06f, 0.04f), glow, false);

            // Exterior skin over the structure
            foreach (int s in new[] { -1, 1 })
            {
                Box(ext, "SkinSide", new Vector3(s * (HalfW + 0.175f), 2.3f, zMid), new Vector3(0.05f, 2.9f, len), hull, false);
                var bev = Box(ext, "RoofBevel", new Vector3(s * 1.95f, 3.78f, roofMid), new Vector3(0.6f, 0.12f, roofLen), hull, false);
                bev.transform.localRotation = Quaternion.Euler(0, 0, s * -35f);
                Box(ext, "Stripe", new Vector3(s * (HalfW + 0.205f), 2.6f, zMid), new Vector3(0.01f, 0.12f, len - 1f), glow, false);
            }
            Box(ext, "SkinRoof", new Vector3(0, YCeil + 0.175f, roofMid), new Vector3(3.5f, 0.05f, roofLen), hull, false);
            Box(ext, "Belly", new Vector3(0, 0.8f, zMid + 1f), new Vector3(HalfW * 2 + 0.4f, 0.1f, len + 2f), belly, false);
            Box(ext, "RoofSpine", new Vector3(0, YCeil + 0.25f, -0.5f), new Vector3(0.6f, 0.12f, 8f), trim, false);
        }

        static void BuildExterior(Transform ext)
        {
            // Nose
            Box(ext, "NoseBody", new Vector3(0, 1.45f, 8.4f), new Vector3(3.8f, 1.3f, 1.8f), hull, true);
            Prim(ext, PrimitiveType.Sphere, "NoseTip", new Vector3(0, 1.45f, 9.25f), new Vector3(3.8f, 1.3f, 1.9f), hull, false);
            var noseTop = Box(ext, "NoseTopPanel", new Vector3(0, 2.07f, 8.3f), new Vector3(3.6f, 0.08f, 1.7f), belly, false);
            noseTop.transform.localRotation = Quaternion.Euler(8f, 0, 0);
            Prim(ext, PrimitiveType.Sphere, "NoseBelly", new Vector3(0, 1.0f, 8.9f), new Vector3(3.7f, 0.5f, 2.2f), belly, false);
            Box(ext, "WindscreenFrame", new Vector3(0, YCeil + 0.02f, ZWindTop + 0.02f), new Vector3(HalfW * 2 + 0.3f, 0.12f, 0.2f), belly, false);

            // Delta wings, low-mounted at floor-slab height so they never poke into the cabin
            var wingPts = new List<Vector2> { new(2.1f, 6.0f), new(2.9f, 3.0f), new(6.4f, -3.6f), new(6.4f, -5.2f), new(2.1f, -5.2f) };
            MeshObj(ext, "WingR", ExtrudeXZ(wingPts, 0.86f, 0.99f, "Wing_R"), hull, true);
            MeshObj(ext, "WingL", ExtrudeXZ(wingPts.ConvertAll(p => new Vector2(-p.x, p.y)), 0.86f, 0.99f, "Wing_L"), hull, true);
            foreach (int s in new[] { -1, 1 })
            {
                Box(ext, "WingTipGlow", new Vector3(s * 6.42f, 0.925f, -4.4f), new Vector3(0.04f, 0.1f, 1.4f), glow, false);
                Box(ext, "Elevon", new Vector3(s * 4.25f, 0.9f, -5.45f), new Vector3(4.1f, 0.08f, 0.5f), belly, false);
            }

            // Tail fin (profile in z,y; extruded along X)
            var finPts = new List<Vector2> { new(-1.2f, 3.75f), new(-4.8f, 6.6f), new(-5.9f, 6.6f), new(-5.6f, 3.75f) };
            MeshObj(ext, "TailFin", ExtrudeZY(finPts, -0.09f, 0.09f, "TailFin"), hull, true);
            Box(ext, "FinTipGlow", new Vector3(0, 6.62f, -5.35f), new Vector3(0.2f, 0.06f, 1.1f), glow, false);

            // Engine pods
            foreach (int s in new[] { -1, 1 })
            {
                var pod = Prim(ext, PrimitiveType.Cylinder, "EnginePod", new Vector3(s * 2.75f, 2.4f, -4.6f), new Vector3(1.1f, 1.3f, 1.1f), hull, true);
                pod.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var noz = Prim(ext, PrimitiveType.Cylinder, "Nozzle", new Vector3(s * 2.75f, 2.4f, -6.05f), new Vector3(0.9f, 0.2f, 0.9f), belly, false);
                noz.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var g = Prim(ext, PrimitiveType.Cylinder, "NozzleGlow", new Vector3(s * 2.75f, 2.4f, -6.26f), new Vector3(0.7f, 0.02f, 0.7f), engine, false);
                g.transform.localRotation = Quaternion.Euler(90, 0, 0);
                Box(ext, "PodPylon", new Vector3(s * 2.3f, 2.4f, -4.6f), new Vector3(0.3f, 0.25f, 2f), trim, false);
            }

            // Landing gear (ground at y = 0)
            foreach (var p in new[] { new Vector3(0, 0, 7.0f), new Vector3(-1.5f, 0, -3.2f), new Vector3(1.5f, 0, -3.2f) })
            {
                Prim(ext, PrimitiveType.Cylinder, "GearLeg", new Vector3(p.x, 0.55f, p.z), new Vector3(0.14f, 0.28f, 0.14f), trim, false);
                var w = Prim(ext, PrimitiveType.Cylinder, "Wheel", new Vector3(p.x, 0.3f, p.z), new Vector3(0.6f, 0.12f, 0.6f), belly, true);
                w.transform.localRotation = Quaternion.Euler(0, 0, 90);
            }
        }

        static void BuildCockpit(Transform cock)
        {
            // Windscreen from (y 2.15, z 7.5) up to (y 3.6, z 6.6)
            var ws = Box(cock, "Windscreen", new Vector3(0, 2.875f, 7.05f), new Vector3(HalfW * 2, 1.71f, 0.05f), glass, true);
            ws.transform.localRotation = Quaternion.Euler(-31.8f, 0, 0);
            foreach (int s in new[] { -1, 1 })
                Box(cock, "SideWindow", new Vector3(s * (HalfW + 0.21f), 2.75f, 6.0f), new Vector3(0.02f, 0.6f, 1.6f), glass, false);

            Box(cock, "ConsoleBase", new Vector3(0, 1.5f, 7.15f), new Vector3(3.8f, 1.0f, 0.6f), inner, true);
            var desk = Box(cock, "ConsoleDesk", new Vector3(0, 2.05f, 6.85f), new Vector3(3.6f, 0.08f, 0.8f), belly, false);
            desk.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
            foreach (float x in new[] { -1.2f, -0.4f, 0.4f, 1.2f })
            {
                var scr = Box(cock, "Screen", new Vector3(x, 2.1f, 6.87f), new Vector3(0.6f, 0.01f, 0.4f), glow, false);
                scr.transform.localRotation = Quaternion.Euler(-18f, 0, 0);
            }
            foreach (int s in new[] { -1, 1 })
            {
                var seat = Group(cock, s < 0 ? "SeatL" : "SeatR");
                seat.localPosition = new Vector3(s * 0.85f, 0, 5.7f);
                Box(seat, "Base", new Vector3(0, 1.5f, 0), new Vector3(0.65f, 0.14f, 0.65f), belly, true);
                Box(seat, "Back", new Vector3(0, 2.05f, -0.3f), new Vector3(0.65f, 1.0f, 0.12f), belly, false);
                Box(seat, "Headrest", new Vector3(0, 2.65f, -0.3f), new Vector3(0.4f, 0.22f, 0.14f), trim, false);
                Prim(seat, PrimitiveType.Cylinder, "Post", new Vector3(0, 1.2f, 0), new Vector3(0.12f, 0.2f, 0.12f), trim, false);
                Prim(seat, PrimitiveType.Cylinder, "Stick", new Vector3(0, 1.7f, 0.55f), new Vector3(0.05f, 0.2f, 0.05f), trim, false);
            }
            Box(cock, "OverheadPanel", new Vector3(0, YCeil - 0.06f, 5.6f), new Vector3(1.6f, 0.06f, 1.2f), belly, false);
            Box(cock, "OverheadGlow", new Vector3(0, YCeil - 0.1f, 5.6f), new Vector3(1.2f, 0.02f, 0.2f), glow, false);
        }

        static void BuildHold(Transform hold)
        {
            float wallY = (YFloor + YCeil) * 0.5f, wallH = YCeil - YFloor;
            for (float z = -4.2f; z < 3.5f; z += 1.6f)
                foreach (int s in new[] { -1, 1 })
                    Box(hold, "Rib", new Vector3(s * 1.96f, wallY, z), new Vector3(0.08f, wallH, 0.18f), trim, false);
            foreach (int s in new[] { -1, 1 })
            {
                Box(hold, "CeilingLight", new Vector3(s * 0.9f, YCeil - 0.02f, -0.6f), new Vector3(0.12f, 0.03f, 7.5f), glow, false);
                Box(hold, "FloorRail", new Vector3(s * 1.2f, YFloor + 0.01f, -0.6f), new Vector3(0.08f, 0.02f, 8.5f), trim, false);
            }
        }

        static void BuildDoors(Transform str, Transform ext)
        {
            // Sliding bulkhead door (hold side), slides +X behind BulkheadR
            var door = Box(str, "CargoDoor", new Vector3(0, YFloor + 1.15f, ZBulkhead - 0.13f), new Vector3(1.32f, 2.3f, 0.08f), trim, true);
            Box(door.transform, "DoorStripe", new Vector3(0, 0.1f, -0.6f), new Vector3(0.05f, 0.85f, 0.2f), glow, false);
            door.AddComponent<Rigidbody>().isKinematic = true;
            var dHold = Panel(str, "DoorPanel_Hold", new Vector3(-0.95f, 2.1f, ZBulkhead - 0.1f), Vector3.back);
            var dCock = Panel(str, "DoorPanel_Cockpit", new Vector3(-0.95f, 2.1f, ZBulkhead + 0.1f), Vector3.forward);
            ConfigureDoor(door.AddComponent<ShipDoor>(), ShipDoor.Motion.Slide, new Vector3(1.38f, 0, 0), Vector3.zero, 1.0f,
                "OPEN DOOR", "CLOSE DOOR", dHold, dCock);

            // Rear ramp hinged at the floor's rear edge; closed = upright, open = slopes to the ground
            float rampLen = YCeil - YFloor;
            var pivot = Group(str, "RampPivot");
            pivot.localPosition = new Vector3(0, YFloor, ZRear);
            Box(pivot, "Ramp", new Vector3(0, rampLen * 0.5f, -0.07f), new Vector3(3.6f, rampLen, 0.14f), hull, true);
            Box(pivot, "RampInner", new Vector3(0, rampLen * 0.5f, 0.005f), new Vector3(3.4f, rampLen - 0.1f, 0.01f), floorM, false);
            for (float y = 0.3f; y < rampLen - 0.1f; y += 0.35f)
                Box(pivot, "Grip", new Vector3(0, y, 0.015f), new Vector3(3.2f, 0.04f, 0.02f), trim, false);
            Box(pivot, "RampStripe", new Vector3(0, rampLen * 0.5f, -0.145f), new Vector3(3.0f, 0.06f, 0.01f), glow, false);
            pivot.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            float openX = -(90f + Mathf.Asin(YFloor / rampLen) * Mathf.Rad2Deg);
            var rIn = Panel(str, "RampPanel_Inside", new Vector3(-1.93f, 2.0f, -4.3f), Vector3.right);
            var rOut = Panel(ext, "RampPanel_Outside", new Vector3(-2.22f, 1.75f, -4.3f), Vector3.left);
            ConfigureDoor(pivot.gameObject.AddComponent<ShipDoor>(), ShipDoor.Motion.Hinge, Vector3.zero, new Vector3(openX, 0, 0), 2.2f,
                "LOWER RAMP", "RAISE RAMP", rIn, rOut);
        }

        // ------------------------------------------------------------------ helpers

        internal static Transform Group(Transform p, string n)
        {
            var t = new GameObject(n).transform;
            t.SetParent(p, false);
            return t;
        }

        internal static GameObject Box(Transform p, string n, Vector3 pos, Vector3 size, Material m, bool collider)
            => Prim(p, PrimitiveType.Cube, n, pos, size, m, collider);

        internal static GameObject Prim(Transform p, PrimitiveType type, string n, Vector3 pos, Vector3 scale, Material m, bool collider)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = n;
            if (!collider) Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(p, false);
            g.transform.localPosition = pos;
            g.transform.localScale = scale;
            var r = g.GetComponent<MeshRenderer>();
            r.sharedMaterial = m;
            if (m == glow || m == glass || m == engine) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        // 'facing' = direction the glowing button points (towards the player).
        internal static Interactable Panel(Transform p, string n, Vector3 pos, Vector3 facing)
        {
            var g = Box(p, n, pos, new Vector3(0.25f, 0.35f, 0.05f), belly, false);
            g.transform.localRotation = Quaternion.LookRotation(-facing); // button sits on local -Z
            Box(g.transform, "Button", new Vector3(0, 0, -0.6f), new Vector3(0.5f, 0.35f, 0.4f), glow, false);
            var it = g.AddComponent<Interactable>();
            var so = new SerializedObject(it);
            so.FindProperty("range").floatValue = 2.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            return it;
        }

        internal static void ConfigureDoor(ShipDoor d, ShipDoor.Motion motion, Vector3 offset, Vector3 euler, float duration,
            string open, string close, params Interactable[] controls)
        {
            var so = new SerializedObject(d);
            so.FindProperty("motion").enumValueIndex = (int)motion;
            so.FindProperty("openOffset").vector3Value = offset;
            so.FindProperty("openEuler").vector3Value = euler;
            so.FindProperty("duration").floatValue = duration;
            so.FindProperty("openPrompt").stringValue = open;
            so.FindProperty("closePrompt").stringValue = close;
            var arr = so.FindProperty("controls");
            arr.arraySize = controls.Length;
            for (int i = 0; i < controls.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = controls[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void AddLight(Transform p, string n, Vector3 pos, Color c, float range, float intensity)
        {
            var l = new GameObject(n).AddComponent<Light>();
            l.transform.SetParent(p, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.color = c;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        static void MeshObj(Transform p, string n, UnityEngine.Mesh mesh, Material m, bool collider)
        {
            var g = new GameObject(n);
            g.transform.SetParent(p, false);
            g.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.AddComponent<MeshRenderer>().sharedMaterial = m;
            if (collider)
            {
                var mc = g.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = true;
            }
        }

        // Convex polygon given as (x, z), extruded between y0..y1.
        static UnityEngine.Mesh ExtrudeXZ(List<Vector2> pts, float y0, float y1, string name)
            => Extrude(pts.ConvertAll(p => new Vector3(p.x, 0, p.y)), Vector3.up, y0, y1, name);

        // Convex polygon given as (z, y), extruded between x0..x1.
        static UnityEngine.Mesh ExtrudeZY(List<Vector2> pts, float x0, float x1, string name)
            => Extrude(pts.ConvertAll(p => new Vector3(0, p.y, p.x)), Vector3.right, x0, x1, name);

        static UnityEngine.Mesh Extrude(List<Vector3> poly, Vector3 axis, float a0, float a1, string name)
        {
            // Make the polygon counter-clockwise when viewed from +axis.
            Vector3 area = Vector3.zero;
            for (int i = 0; i < poly.Count; i++) area += Vector3.Cross(poly[i], poly[(i + 1) % poly.Count]);
            if (Vector3.Dot(area, axis) < 0) poly.Reverse();

            var v = new List<Vector3>();
            var t = new List<int>();
            int n = poly.Count;

            int top = v.Count;
            foreach (var p in poly) v.Add(p + axis * a1);
            for (int i = 1; i < n - 1; i++) { t.Add(top); t.Add(top + i + 1); t.Add(top + i); }

            int bot = v.Count;
            foreach (var p in poly) v.Add(p + axis * a0);
            for (int i = 1; i < n - 1; i++) { t.Add(bot); t.Add(bot + i); t.Add(bot + i + 1); }

            for (int i = 0; i < n; i++)
            {
                Vector3 p0 = poly[i], p1 = poly[(i + 1) % n];
                int s = v.Count;
                v.Add(p0 + axis * a0); v.Add(p1 + axis * a0); v.Add(p1 + axis * a1); v.Add(p0 + axis * a1);
                t.Add(s); t.Add(s + 2); t.Add(s + 1);
                t.Add(s); t.Add(s + 3); t.Add(s + 2);
            }

            // Unity uses clockwise front faces: flip if the top face points away from +axis.
            Vector3 topN = Vector3.Cross(v[t[1]] - v[t[0]], v[t[2]] - v[t[0]]);
            if (Vector3.Dot(topN, axis) < 0)
                for (int i = 0; i < t.Count; i += 3) (t[i + 1], t[i + 2]) = (t[i + 2], t[i + 1]);

            var mesh = new UnityEngine.Mesh { name = name };
            mesh.SetVertices(v);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            string path = Dir + "/Meshes/" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(path);
            if (existing)
            {
                existing.Clear();
                existing.SetVertices(v);
                existing.SetTriangles(t, 0);
                existing.RecalculateNormals();
                existing.RecalculateBounds();
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Material Mat(string name, Color c, float metallic, float smooth)
        {
            string path = Dir + "/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Smoothness", smooth);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Emit(Material m, Color hdr)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", hdr);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeIndirectEmission;
        }

        static void Transparent(Material m)
        {
            m.SetFloat("_Surface", 1);
            m.SetFloat("_Blend", 0);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0);
            m.SetFloat("_Cull", 0);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }
}
