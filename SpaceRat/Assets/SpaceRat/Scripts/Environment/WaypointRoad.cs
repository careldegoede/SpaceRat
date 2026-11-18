using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Environment
{
    /// <summary>
    /// A road drawn through waypoints (Ctrl+click in the Scene view; see WaypointRoadEditor).
    /// The mesh is generated in the editor and saved with the scene; this component only holds the settings.
    /// Points are local to this transform.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class WaypointRoad : MonoBehaviour
    {
        public enum RoadEnd
        {
            [Tooltip("Fades out over a few metres.")] Fade,
            [Tooltip("Hard square end.")] Square,
            [Tooltip("Tucks under another road it meets (no fade, sits slightly lower).")] Junction,
        }

        public List<Vector3> points = new();
        [Min(1f)] public float width = 5f;
        public bool closed;
        [Tooltip("Mesh hugs the terrain. Off = straight between waypoint heights (bridges, ramps).")]
        public bool followTerrain = true;
        [Tooltip("Dragged / added waypoints drop onto the ground below them.")]
        public bool snapPointsToGround = true;
        public RoadEnd startEnd = RoadEnd.Fade;
        public RoadEnd endEnd = RoadEnd.Fade;
        [Tooltip("Extra gravel painted on the terrain past each road edge (Paint Gravel button).")]
        [Min(0f)] public float gravelShoulder = 1.5f;
        [Tooltip("Empty = EasyRoads3D dirt material if installed, otherwise M_GravelRoad.")]
        public Material material;
    }
}
