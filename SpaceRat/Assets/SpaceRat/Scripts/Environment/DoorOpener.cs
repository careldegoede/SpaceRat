using System.Collections.Generic;
using UnityEngine;

namespace SpaceRat.Environment
{
    /// <summary>
    /// Opens the Sci-Fi Modular Pack doors under <see cref="root"/> when the Player (tag "Player") comes near
    /// and closes them again when he leaves. The doors' own Animators (frame_door_* controllers) have a
    /// "character_nearby" bool; this sets it by distance, so no trigger colliders or prefab edits are needed.
    /// </summary>
    public class DoorOpener : MonoBehaviour
    {
        [SerializeField] Transform root;
        [SerializeField] float openDistance = 4f;
        [SerializeField] float closeDistance = 5.5f;
        [SerializeField] string playerTag = "Player";

        static readonly int Nearby = Animator.StringToHash("character_nearby");

        readonly List<Animator> _doors = new();
        Transform _player;

        /// <summary>While locked, doors stay shut (and close if open).</summary>
        public bool Locked { get; set; }

        /// <summary>True while the Player is within the building's footprint (ground floor).</summary>
        public bool PlayerInside { get; private set; }

        void Start()
        {
            if (!root) root = transform;
            foreach (var a in root.GetComponentsInChildren<Animator>(true))
            {
                if (!a.runtimeAnimatorController) continue;
                foreach (var p in a.parameters)
                    if (p.nameHash == Nearby) { _doors.Add(a); break; }
            }
            // Footprint in the building's own (rotated) frame, from the meshes' local bounds.
            bool any = false;
            var local = new Bounds();
            var toLocal = root.worldToLocalMatrix;
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var m = toLocal * mf.transform.localToWorldMatrix;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var lp = m.MultiplyPoint3x4(corner);
                    if (!any) { local = new Bounds(lp, Vector3.zero); any = true; } else local.Encapsulate(lp);
                }
            }
            _localFootprint = local;
            _hasFootprint = any;
        }

        Bounds _localFootprint;
        bool _hasFootprint;

        /// <summary>
        /// Inside = within the footprint (building frame, ground floor) and more than 2 m past the nearest
        /// door towards the middle of the building — so standing just outside the entrance never counts.
        /// </summary>
        bool IsInside(Vector3 worldPos)
        {
            if (!_hasFootprint) return false;
            var lp = root.InverseTransformPoint(worldPos);
            var fp = _localFootprint;
            if (lp.x < fp.min.x || lp.x > fp.max.x || lp.z < fp.min.z || lp.z > fp.max.z) return false;
            if (lp.y > 3.2f) return false;                                   // roof deck is "outside"
            Animator nearest = null;
            float best = float.MaxValue;
            foreach (var d in _doors)
            {
                if (!d) continue;
                float dist = Vector3.Distance(worldPos, d.transform.position);
                if (dist < best) { best = dist; nearest = d; }
            }
            if (!nearest) return true;
            var doorL = root.InverseTransformPoint(nearest.transform.position);
            var inward = new Vector3(fp.center.x - doorL.x, 0f, fp.center.z - doorL.z).normalized;
            return Vector3.Dot(new Vector3(lp.x - doorL.x, 0f, lp.z - doorL.z), inward) > 2f;
        }

        void Update()
        {
            if (!_player)
            {
                var go = GameObject.FindWithTag(playerTag);
                if (!go) return;
                _player = go.transform;
            }
            Vector3 p = _player.position;
            PlayerInside = IsInside(p);
            foreach (var door in _doors)
            {
                if (!door || !door.isActiveAndEnabled) continue;
                float d = Vector3.Distance(p, door.transform.position);
                bool open = door.GetBool(Nearby);
                if (Locked) { if (open) door.SetBool(Nearby, false); continue; }
                if (!open && d < openDistance) door.SetBool(Nearby, true);
                else if (open && d > closeDistance) door.SetBool(Nearby, false);
            }
        }
    }
}
