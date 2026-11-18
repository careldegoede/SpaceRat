using SpaceRat.Characters;
using UnityEngine;

namespace SpaceRat.Environment
{
    /// <summary>
    /// A door or ramp that moves between a closed and an open pose when any of its
    /// <see cref="Interactable"/>s is used (E / gamepad X). Slide = local position offset,
    /// Hinge = local rotation. Put a kinematic Rigidbody on the moving part so its collider
    /// pushes the player correctly.
    /// </summary>
    public class ShipDoor : MonoBehaviour
    {
        public enum Motion { Slide, Hinge }

        [SerializeField] Motion motion = Motion.Slide;
        [Tooltip("Slide: local offset when open.")]
        [SerializeField] Vector3 openOffset = new Vector3(1.25f, 0f, 0f);
        [Tooltip("Hinge: local euler angles when open (closed = pose at Start).")]
        [SerializeField] Vector3 openEuler = new Vector3(-90f, 0f, 0f);
        [SerializeField] float duration = 1.2f;
        [SerializeField] bool startOpen;
        [SerializeField] Interactable[] controls;
        [SerializeField] string openPrompt = "OPEN DOOR";
        [SerializeField] string closePrompt = "CLOSE DOOR";
        [SerializeField] AudioSource moveSound;

        Vector3 _closedPos;
        Quaternion _closedRot;
        float _t;      // 0 = closed, 1 = open
        bool _open;

        public bool IsOpen => _open;
        public bool Locked { get; set; }

        void Awake()
        {
            _closedPos = transform.localPosition;
            _closedRot = transform.localRotation;
            _open = startOpen;
            _t = startOpen ? 1f : 0f;
            Apply();
        }

        void OnEnable()
        {
            if (controls == null) return;
            foreach (var c in controls) if (c) c.Used += OnUsed;
            UpdatePrompts();
        }

        void OnDisable()
        {
            if (controls == null) return;
            foreach (var c in controls) if (c) c.Used -= OnUsed;
        }

        void OnUsed(Interactable _) => SetOpen(!_open);

        public void SetOpen(bool open)
        {
            if (Locked && open) return;
            if (open == _open) return;
            _open = open;
            if (moveSound) moveSound.Play();
            UpdatePrompts();
        }

        void UpdatePrompts()
        {
            if (controls == null) return;
            foreach (var c in controls) if (c) c.Prompt = _open ? closePrompt : openPrompt;
        }

        void Update()
        {
            float target = _open ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;
            _t = Mathf.MoveTowards(_t, target, Time.deltaTime / Mathf.Max(0.01f, duration));
            Apply();
        }

        void Apply()
        {
            float e = Mathf.SmoothStep(0f, 1f, _t);
            if (motion == Motion.Slide)
                transform.localPosition = _closedPos + openOffset * e;
            else
                transform.localRotation = Quaternion.Slerp(_closedRot, Quaternion.Euler(openEuler), e);
        }
    }
}
