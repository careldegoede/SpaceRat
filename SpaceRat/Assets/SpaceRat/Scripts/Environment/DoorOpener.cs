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

        void Start()
        {
            if (!root) root = transform;
            foreach (var a in root.GetComponentsInChildren<Animator>(true))
            {
                if (!a.runtimeAnimatorController) continue;
                foreach (var p in a.parameters)
                    if (p.nameHash == Nearby) { _doors.Add(a); break; }
            }
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
            foreach (var door in _doors)
            {
                if (!door || !door.isActiveAndEnabled) continue;
                float d = Vector3.Distance(p, door.transform.position);
                bool open = door.GetBool(Nearby);
                if (!open && d < openDistance) door.SetBool(Nearby, true);
                else if (open && d > closeDistance) door.SetBool(Nearby, false);
            }
        }
    }
}
