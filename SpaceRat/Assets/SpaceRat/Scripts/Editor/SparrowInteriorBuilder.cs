using System.Collections.Generic;
using SpaceRat.Environment;
using Unity.Scripting.LifecycleManagement;
using UnityEditor;
using UnityEngine;
using S = SpaceRat.EditorTools.SpaceShuttleBuilder;

namespace SpaceRat.EditorTools
{
    using Mesh = UnityEngine.Mesh;

    /// <summary>
    /// Builds Assets/DDG/Sparrow/Sparrow_{blue,grey,orange}_Interior.prefab: a 1.4x copy of the URP Sparrow with a
    /// walk-in interior (cargo hold + cockpit, sliding bulkhead door), a doorway cut into the rear
    /// panel with a double sliding door, and a fold-down ramp. The vendor/URP Sparrow assets are not
    /// modified. Root origin = ground; the hull sits on its fins.
    /// </summary>
    [NoAutoStaticsCleanup] // only a constant colour list
    public static class SparrowInteriorBuilder
    {
        const string SrcPrefab = "Assets/SpaceRat/Prefabs/Ship/Sparrow_blue_URP.prefab"; // hull geometry source
        const string Dir = "Assets/DDG/Sparrow";
        const float Scale = 1.4f;

        // Interior box, in hull space (after scaling). Measured by raycasting the scaled hull.
        const float YFloor = -2.25f, YCeil = 0.25f, HalfW = 1.15f;
        const float ZBulk = 3.0f, ZCockFront = 5.5f, ZNose = 6.3f, NoseHalfW = 0.8f;
        const float DoorHalfW = 0.55f, DoorTop = -0.05f;
        const float FloorAboveGround = 1.8f, RampLen = 3.0f;

        [MenuItem("SpaceRat/Build Sparrow Interior")]
        public static void Build()
        {
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefab);
            if (!src) { Debug.LogError("SparrowInteriorBuilder: missing " + SrcPrefab + " (run Convert Sparrow Fighter to URP)."); return; }
            var srcMf = src.GetComponentInChildren<MeshFilter>();
            var srcMat = srcMf.GetComponent<MeshRenderer>().sharedMaterial;

            if (!AssetDatabase.IsValidFolder(Dir)) AssetDatabase.CreateFolder("Assets/DDG", "Sparrow");
            S.EnsureMaterials();

            // 1. Scaled copy of the hull mesh (bake the source transform + 1.4x).
            var toRoot = Matrix4x4.Scale(Vector3.one * Scale) * src.transform.worldToLocalMatrix * srcMf.transform.localToWorldMatrix;
            var srcMesh = srcMf.sharedMesh;
            var verts = srcMesh.vertices;
            var norms = srcMesh.normals;
            for (int i = 0; i < verts.Length; i++) verts[i] = toRoot.MultiplyPoint3x4(verts[i]);
            var nm = toRoot.inverse.transpose;
            for (int i = 0; i < norms.Length; i++) norms[i] = nm.MultiplyVector(norms[i]).normalized;

            var hullMesh = new Mesh { name = "Sparrow_Hull_Interior", indexFormat = srcMesh.indexFormat };
            hullMesh.vertices = verts;
            hullMesh.normals = norms;
            hullMesh.tangents = srcMesh.tangents;
            hullMesh.uv = srcMesh.uv;
            if (srcMesh.uv2 != null && srcMesh.uv2.Length == verts.Length) hullMesh.uv2 = srcMesh.uv2;
            var tris = new List<int>(srcMesh.GetTriangles(0));

            // 2. Measure the rear panel position by raycasting the scaled hull.
            float zHull = MeasureRear(verts, tris, out float zBayEnd, out string fitReport);

            // 3. Cut the doorway: remove hull triangles whose centre is inside the cut box.
            var cutMin = new Vector3(-DoorHalfW - 0.2f, YFloor - 0.1f, zHull - 0.8f);
            var cutMax = new Vector3(DoorHalfW + 0.2f, DoorTop + 0.05f, zHull + 0.6f);
            var kept = new List<int>(tris.Count);
            int removed = 0;
            for (int i = 0; i < tris.Count; i += 3)
            {
                var c = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;
                bool inside = c.x > cutMin.x && c.x < cutMax.x && c.y > cutMin.y && c.y < cutMax.y && c.z > cutMin.z && c.z < cutMax.z;
                if (inside) { removed++; continue; }
                kept.Add(tris[i]); kept.Add(tris[i + 1]); kept.Add(tris[i + 2]);
            }
            hullMesh.SetTriangles(kept, 0);
            hullMesh.RecalculateBounds();
            SaveMesh(hullMesh, Dir + "/Sparrow_Hull_Interior.asset");
            hullMesh = AssetDatabase.LoadAssetAtPath<Mesh>(Dir + "/Sparrow_Hull_Interior.asset");

            // 4. One prefab per colour. All three vendor meshes are identical, so they share the cut hull.
            foreach (var colour in Colours)
            {
                var colourSrc = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefabFor(colour));
                var mat = colourSrc ? colourSrc.GetComponentInChildren<MeshRenderer>().sharedMaterial : srcMat;
                string path = $"{Dir}/Sparrow_{colour}_Interior.prefab";

                var root = new GameObject($"Sparrow_{colour}_Interior").transform;
                try
                {
                    var ship = S.Group(root, "Ship");
                    ship.localPosition = new Vector3(0, FloorAboveGround - YFloor, 0);

                    var hull = new GameObject("Hull");
                    hull.transform.SetParent(ship, false);
                    hull.AddComponent<MeshFilter>().sharedMesh = hullMesh;
                    hull.AddComponent<MeshRenderer>().sharedMaterial = mat;
                    hull.AddComponent<MeshCollider>().sharedMesh = hullMesh;

                    float zRoomRear = zHull + 0.15f;
                    BuildRoom(S.Group(ship, "Interior"), zRoomRear);
                    BuildRearExit(S.Group(ship, "RearExit"), zHull, zRoomRear, zBayEnd);

                    PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
                }
                finally
                {
                    Object.DestroyImmediate(root.gameObject);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"SparrowInteriorBuilder: saved {Colours.Length} prefabs in {Dir} (rear panel z {zHull:F2}, bay end z {zBayEnd:F2}, removed {removed} tris).\n{fitReport}");
        }

        static readonly string[] Colours = { "blue", "grey", "orange" };
        static string SrcPrefabFor(string colour) => $"Assets/SpaceRat/Prefabs/Ship/Sparrow_{colour}_URP.prefab";

        // ------------------------------------------------------------------ interior

        static void BuildRoom(Transform t, float zRear)
        {
            var inner = S.Inner; var floorM = S.FloorMat; var trim = S.Trim; var glass = S.Glass; var glow = S.Glow; var dark = S.Belly;
            float wallH = YCeil - YFloor, wallY = (YFloor + YCeil) * 0.5f;
            float mainLen = ZCockFront - zRear, mainMid = (ZCockFront + zRear) * 0.5f;
            float noseLen = ZNose - ZCockFront, noseMid = (ZNose + ZCockFront) * 0.5f;

            // Floor / walls / ceiling
            S.Box(t, "Floor", new Vector3(0, YFloor - 0.05f, mainMid), new Vector3(HalfW * 2, 0.1f, mainLen), floorM, true);
            S.Box(t, "FloorNose", new Vector3(0, YFloor - 0.05f, noseMid), new Vector3(NoseHalfW * 2, 0.1f, noseLen), floorM, true);
            foreach (int s in new[] { -1, 1 })
            {
                S.Box(t, "Wall", new Vector3(s * (HalfW + 0.05f), wallY, mainMid), new Vector3(0.1f, wallH, mainLen), inner, true);
                S.Box(t, "WallNose", new Vector3(s * (NoseHalfW + 0.05f), wallY, noseMid), new Vector3(0.1f, wallH, noseLen), inner, true);
                S.Box(t, "NoseStep", new Vector3(s * (HalfW + NoseHalfW) * 0.5f, wallY, ZCockFront), new Vector3(HalfW - NoseHalfW + 0.1f, wallH, 0.1f), inner, true);
            }
            float holdLen = ZBulk - zRear, holdMid = (ZBulk + zRear) * 0.5f;
            S.Box(t, "CeilingHold", new Vector3(0, YCeil + 0.05f, holdMid), new Vector3(HalfW * 2, 0.1f, holdLen), inner, true);
            // Cockpit: glass roof and windscreen so you can see out through the canopy
            S.Box(t, "CockpitGlassRoof", new Vector3(0, YCeil + 0.03f, (ZBulk + ZCockFront) * 0.5f), new Vector3(HalfW * 2, 0.04f, ZCockFront - ZBulk), glass, true);
            S.Box(t, "NoseGlassRoof", new Vector3(0, YCeil + 0.03f, noseMid), new Vector3(NoseHalfW * 2, 0.04f, noseLen), glass, true);
            S.Box(t, "NoseFrontLower", new Vector3(0, YFloor + 0.55f, ZNose + 0.05f), new Vector3(NoseHalfW * 2, 1.1f, 0.1f), inner, true);
            S.Box(t, "Windscreen", new Vector3(0, (YFloor + 1.1f + YCeil) * 0.5f, ZNose + 0.05f), new Vector3(NoseHalfW * 2, YCeil - YFloor - 1.1f, 0.04f), glass, true);
            S.Box(t, "RoofFrame", new Vector3(0, YCeil + 0.02f, ZBulk + 0.05f), new Vector3(HalfW * 2, 0.08f, 0.1f), dark, false);

            // Rear wall with doorway
            float sideW = HalfW - DoorHalfW;
            foreach (int s in new[] { -1, 1 })
                S.Box(t, "RearWall", new Vector3(s * (DoorHalfW + sideW * 0.5f), wallY, zRear), new Vector3(sideW, wallH, 0.1f), inner, true);
            S.Box(t, "RearWallTop", new Vector3(0, (DoorTop + YCeil) * 0.5f, zRear), new Vector3(DoorHalfW * 2, YCeil - DoorTop, 0.1f), inner, true);

            // Bulkhead between hold and cockpit, with sliding door (slides +X into the wall)
            foreach (int s in new[] { -1, 1 })
                S.Box(t, "Bulkhead", new Vector3(s * (DoorHalfW + sideW * 0.5f), wallY, ZBulk), new Vector3(sideW, wallH, 0.1f), inner, true);
            S.Box(t, "BulkheadTop", new Vector3(0, (DoorTop + YCeil) * 0.5f, ZBulk), new Vector3(DoorHalfW * 2, YCeil - DoorTop, 0.1f), inner, true);
            S.Box(t, "BulkheadGlow", new Vector3(0, DoorTop + 0.04f, ZBulk - 0.06f), new Vector3(DoorHalfW * 2 + 0.2f, 0.04f, 0.02f), glow, false);
            var door = S.Box(t, "CockpitDoor", new Vector3(0, (YFloor + DoorTop) * 0.5f, ZBulk - 0.1f), new Vector3(DoorHalfW * 2 + 0.04f, DoorTop - YFloor, 0.06f), trim, true);
            S.Box(door.transform, "Stripe", new Vector3(-0.35f, 0.05f, -0.6f), new Vector3(0.04f, 0.5f, 0.2f), glow, false);
            door.AddComponent<Rigidbody>().isKinematic = true;
            var pHold = S.Panel(t, "DoorPanel_Hold", new Vector3(-0.85f, -1.0f, ZBulk - 0.08f), Vector3.back);
            var pCock = S.Panel(t, "DoorPanel_Cockpit", new Vector3(-0.85f, -1.0f, ZBulk + 0.08f), Vector3.forward);
            S.ConfigureDoor(door.AddComponent<ShipDoor>(), ShipDoor.Motion.Slide, new Vector3(DoorHalfW * 2 + 0.02f, 0, 0), Vector3.zero, 0.9f,
                "OPEN DOOR", "CLOSE DOOR", pHold, pCock);

            // Hold details (empty hold)
            for (float z = zRear + 0.8f; z < ZBulk - 0.3f; z += 1.4f)
                foreach (int s in new[] { -1, 1 })
                    S.Box(t, "Rib", new Vector3(s * (HalfW - 0.04f), wallY, z), new Vector3(0.08f, wallH, 0.16f), trim, false);
            foreach (int s in new[] { -1, 1 })
            {
                S.Box(t, "CeilingLight", new Vector3(s * 0.55f, YCeil - 0.02f, holdMid), new Vector3(0.1f, 0.03f, holdLen - 0.6f), glow, false);
                S.Box(t, "FloorRail", new Vector3(s * 0.7f, YFloor + 0.01f, holdMid), new Vector3(0.06f, 0.02f, holdLen - 0.4f), trim, false);
            }

            // Cockpit: one pilot seat + console in the nose
            var seat = S.Group(t, "PilotSeat");
            seat.localPosition = new Vector3(0, YFloor, 4.5f);
            S.Box(seat, "Base", new Vector3(0, 0.5f, 0), new Vector3(0.65f, 0.14f, 0.65f), dark, true);
            S.Box(seat, "Back", new Vector3(0, 1.05f, -0.3f), new Vector3(0.65f, 1.0f, 0.12f), dark, false);
            S.Box(seat, "Headrest", new Vector3(0, 1.65f, -0.3f), new Vector3(0.4f, 0.22f, 0.14f), trim, false);
            S.Prim(seat, PrimitiveType.Cylinder, "Post", new Vector3(0, 0.2f, 0), new Vector3(0.12f, 0.2f, 0.12f), trim, false);
            foreach (int s in new[] { -1, 1 })
                S.Prim(seat, PrimitiveType.Cylinder, "Stick", new Vector3(s * 0.42f, 0.75f, 0.2f), new Vector3(0.05f, 0.18f, 0.05f), trim, false);
            S.Box(t, "ConsoleBase", new Vector3(0, YFloor + 0.45f, 6.05f), new Vector3(NoseHalfW * 2, 0.9f, 0.5f), inner, true);
            var desk = S.Box(t, "ConsoleDesk", new Vector3(0, YFloor + 0.95f, 5.85f), new Vector3(NoseHalfW * 2 - 0.1f, 0.06f, 0.6f), dark, false);
            desk.transform.localRotation = Quaternion.Euler(-20f, 0, 0);
            foreach (float x in new[] { -0.45f, 0.0f, 0.45f })
            {
                var scr = S.Box(t, "Screen", new Vector3(x, YFloor + 0.99f, 5.86f), new Vector3(0.38f, 0.01f, 0.3f), glow, false);
                scr.transform.localRotation = Quaternion.Euler(-20f, 0, 0);
            }

            // Lights
            S.AddLight(t, "HoldLight", new Vector3(0, YCeil - 0.4f, holdMid), new Color(0.85f, 0.95f, 1f), 6f, 1.8f);
            S.AddLight(t, "CockpitLight", new Vector3(0, YCeil - 0.4f, 4.6f), new Color(0.4f, 0.85f, 1f), 4f, 1.2f);
        }

        static void BuildRearExit(Transform t, float zHull, float zRoomRear, float zBayEnd)
        {
            const float BayHalfW = 0.9f;
            var trim = S.Trim; var dark = S.Belly; var glow = S.Glow; var floorM = S.FloorMat;
            float doorH = DoorTop - YFloor;

            // Frame: covers the jagged cut edge and lines the short tunnel to the room's rear wall.
            float tunnelZ = (zHull - 0.12f + zRoomRear) * 0.5f, tunnelLen = zRoomRear - (zHull - 0.12f) + 0.1f;
            foreach (int s in new[] { -1, 1 })
            {
                S.Box(t, "FramePost", new Vector3(s * (DoorHalfW + 0.2f), (YFloor + DoorTop) * 0.5f + 0.05f, tunnelZ), new Vector3(0.4f, doorH + 0.4f, tunnelLen), trim, true);
                S.Box(t, "FrameGlow", new Vector3(s * (DoorHalfW + 0.02f), (YFloor + DoorTop) * 0.5f, zHull - 0.13f), new Vector3(0.03f, doorH - 0.2f, 0.02f), glow, false);
            }
            S.Box(t, "FrameLintel", new Vector3(0, DoorTop + 0.15f, tunnelZ), new Vector3(DoorHalfW * 2 + 0.8f, 0.3f, tunnelLen), trim, true);
            S.Box(t, "Sill", new Vector3(0, YFloor - 0.08f, tunnelZ), new Vector3(DoorHalfW * 2 + 0.8f, 0.16f, tunnelLen), dark, true);

            // Double sliding door just inside the room (halves slide sideways behind the rear wall).
            float halfDoorW = DoorHalfW + 0.03f;
            var controls = new List<SpaceRat.Characters.Interactable>
            {
                S.Panel(t, "HatchPanel_Inside", new Vector3(-HalfW + 0.03f, -1.0f, zRoomRear + 0.7f), Vector3.right),
                S.Panel(t, "HatchPanel_Outside", new Vector3(-BayHalfW - 0.3f, -1.0f, zBayEnd - 0.05f), Vector3.back),
            };
            // Post holding the outside panel, beside the ramp at the bay mouth.
            S.Box(t, "HatchPost", new Vector3(-BayHalfW - 0.3f, (YFloor - 1.15f) * 0.5f, zBayEnd + 0.03f), new Vector3(0.12f, YFloor * -1f - 1.15f, 0.08f), trim, false);

            // Walkway through the recessed rear bay, from the doorway to the bay mouth.
            float bayLen = (zHull - 0.12f) - zBayEnd;
            S.Box(t, "BayWalkway", new Vector3(0, YFloor - 0.05f, zBayEnd + bayLen * 0.5f), new Vector3(BayHalfW * 2, 0.1f, bayLen), dark, true);
            foreach (int s in new[] { -1, 1 })
                S.Box(t, "BayEdgeGlow", new Vector3(s * (BayHalfW - 0.05f), YFloor + 0.005f, zBayEnd + bayLen * 0.5f), new Vector3(0.04f, 0.01f, bayLen), glow, false);
            foreach (int s in new[] { -1, 1 })
            {
                var half = S.Box(t, s < 0 ? "RearDoorL" : "RearDoorR", new Vector3(s * halfDoorW * 0.5f, (YFloor + DoorTop) * 0.5f, zRoomRear + 0.09f),
                    new Vector3(halfDoorW, doorH, 0.06f), trim, true);
                S.Box(half.transform, "Stripe", new Vector3(-s * 0.42f, 0, -0.6f), new Vector3(0.06f, 0.8f, 0.2f), glow, false);
                half.AddComponent<Rigidbody>().isKinematic = true;
                S.ConfigureDoor(half.AddComponent<ShipDoor>(), ShipDoor.Motion.Slide, new Vector3(s * halfDoorW, 0, 0), Vector3.zero, 0.9f,
                    "OPEN HATCH", "CLOSE HATCH", controls.ToArray());
            }

            // Fold-down ramp hinged at the bay mouth; closed = upright gate over the bay, open = slopes to the ground.
            var pivot = S.Group(t, "RampPivot");
            pivot.localPosition = new Vector3(0, YFloor, zBayEnd);
            S.Box(pivot, "Ramp", new Vector3(0, RampLen * 0.5f, -0.06f), new Vector3(BayHalfW * 2, RampLen, 0.12f), dark, true);
            S.Box(pivot, "RampInner", new Vector3(0, RampLen * 0.5f, 0.005f), new Vector3(BayHalfW * 2 - 0.2f, RampLen - 0.1f, 0.01f), floorM, false);
            for (float y = 0.25f; y < RampLen - 0.1f; y += 0.3f)
                S.Box(pivot, "Grip", new Vector3(0, y, 0.015f), new Vector3(BayHalfW * 2 - 0.4f, 0.04f, 0.03f), trim, false);
            S.Box(pivot, "RampStripe", new Vector3(0, RampLen * 0.5f, -0.125f), new Vector3(0.08f, RampLen - 0.4f, 0.01f), glow, false);
            pivot.gameObject.AddComponent<Rigidbody>().isKinematic = true;
            float openX = -(90f + Mathf.Asin(Mathf.Clamp01(FloorAboveGround / RampLen)) * Mathf.Rad2Deg);
            S.ConfigureDoor(pivot.gameObject.AddComponent<ShipDoor>(), ShipDoor.Motion.Hinge, Vector3.zero, new Vector3(openX, 0, 0), 1.8f,
                "OPEN HATCH", "CLOSE HATCH", controls.ToArray());
        }

        // ------------------------------------------------------------------ measuring

        // Finds the rear panel z on the centre-line and checks the interior box fits inside the hull.
        static float MeasureRear(Vector3[] verts, List<int> tris, out float zBayEnd, out string report)
        {
            var tmpMesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            tmpMesh.vertices = verts;
            tmpMesh.SetTriangles(tris, 0);
            var go = new GameObject("__SparrowMeasure") { hideFlags = HideFlags.HideAndDontSave };
            go.transform.position = new Vector3(0, -5000, 0);
            go.AddComponent<MeshCollider>().sharedMesh = tmpMesh;
            Physics.SyncTransforms();
            bool back = Physics.queriesHitBackfaces;
            Physics.queriesHitBackfaces = true;
            Vector3 o = go.transform.position;
            float zHull = -4.3f;
            zBayEnd = zHull - 0.5f;
            var sb = new System.Text.StringBuilder("Fit check (points that are NOT enclosed by the hull):");
            try
            {
                if (Physics.Raycast(o + new Vector3(0, -1.2f, -2f), Vector3.back, out var hit, 10f))
                    zHull = hit.point.z - o.z;

                // The rear panel sits at the back of a recessed bay; walk backwards while the bay
                // still has a floor and a roof above the centre-line to find the bay mouth.
                zBayEnd = zHull - 0.5f;
                for (float z = zHull - 0.2f; z > zHull - 4f; z -= 0.05f)
                {
                    var p = o + new Vector3(0, -1.0f, z);
                    if (!Physics.Raycast(p, Vector3.down, 2.5f) || !Physics.Raycast(p, Vector3.up, 3f)) break;
                    zBayEnd = z;
                }
                zBayEnd += 0.1f; // keep the hinge just inside the mouth
                sb.Append($" [bay {zHull:F2} -> {zBayEnd:F2}]");

                int bad = 0;
                float zRear = zHull + 0.15f;
                foreach (float z in new[] { zRear, zRear + 1f, 0f, ZBulk, ZCockFront, ZNose })
                {
                    float hw = z > ZCockFront ? NoseHalfW : HalfW;
                    foreach (var p in new[] { new Vector3(-hw, YFloor - 0.1f, z), new Vector3(hw, YFloor - 0.1f, z), new Vector3(-hw, YCeil + 0.1f, z), new Vector3(hw, YCeil + 0.1f, z) })
                    {
                        if (Enclosed(o + p)) continue;
                        bad++;
                        sb.Append($" ({p.x:F2},{p.y:F2},{p.z:F2})");
                    }
                }
                if (bad == 0) sb.Append(" none - interior fits.");
            }
            finally
            {
                Physics.queriesHitBackfaces = back;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(tmpMesh);
            }
            report = sb.ToString();
            return zHull;
        }

        static bool Enclosed(Vector3 p)
        {
            foreach (var d in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward })
                if (!Physics.Raycast(p, d, 20f)) return false;
            return true;
        }

        static void SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                EditorUtility.SetDirty(existing);
                Object.DestroyImmediate(mesh);
            }
            else
            {
                AssetDatabase.CreateAsset(mesh, path);
            }
        }
    }
}
