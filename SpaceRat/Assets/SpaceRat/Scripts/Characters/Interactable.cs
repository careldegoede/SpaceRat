using System;
using UnityEngine;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Something the pilot can use with E (gamepad X): a pickup, a chair, a dormant robot...
    /// <see cref="PlayerInteractor"/> shows "[ E ]  {prompt}" when the pilot is close and facing it.
    /// Listeners hook <see cref="Used"/>; set <see cref="Available"/> false to hide it (e.g. until a story beat).
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [SerializeField] string prompt = "USE";
        [SerializeField] float range = 2.4f;
        [Tooltip("Point the pilot must be near / look at (defaults to this transform).")]
        [SerializeField] Transform focus;
        [SerializeField] bool available = true;

        public string Prompt { get => prompt; set => prompt = value; }
        public float Range => range;
        public Vector3 FocusPoint => (focus ? focus : transform).position;
        public bool Available { get => available && isActiveAndEnabled; set => available = value; }

        public event Action<Interactable> Used;

        public void Use()
        {
            if (Available) Used?.Invoke(this);
        }
    }
}
