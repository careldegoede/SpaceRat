using UnityEngine;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Animated seated pilot: the Animator plays a sitting idle (Mixamo "Sitting Idle"); this component
    /// (1) moves the character once so its hips land on <see cref="hipsAnchor"/> (the chair cushion), and
    /// (2) layers an "unconscious" slump on top of the animation — spine, neck and head bend forward —
    /// which eases out when <see cref="Slump"/> is set to 0 (pilot wakes up).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class PilotSeating : MonoBehaviour
    {
        [SerializeField] Transform hipsAnchor;

        [Header("Slump (0 = awake, 1 = unconscious)")]
        [Range(0f, 1f)] [SerializeField] float slump = 1f;
        [SerializeField] float wakeSeconds = 1.5f;
        [SerializeField] float spineBend = 12f;
        [SerializeField] float chestBend = 10f;
        [SerializeField] float neckBend = 25f;
        [SerializeField] float headBend = 20f;
        [SerializeField] float headTilt = 12f;

        Animator _animator;
        Transform _hips, _spine, _chest, _neck, _head;
        float _target;
        bool _aligned;

        /// <summary>Target slump; the visible slump eases toward it.</summary>
        public float Slump
        {
            get => _target;
            set => _target = Mathf.Clamp01(value);
        }

        void Awake()
        {
            _animator = GetComponent<Animator>();
            _target = slump;
            _hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            _chest = _animator.GetBoneTransform(HumanBodyBones.Chest);
            _neck = _animator.GetBoneTransform(HumanBodyBones.Neck);
            _head = _animator.GetBoneTransform(HumanBodyBones.Head);
        }

        void LateUpdate()
        {
            if (!_aligned && hipsAnchor && _hips)
            {
                // After the first animated frame, shift the whole character so the hips sit on the cushion.
                transform.position += hipsAnchor.position - _hips.position;
                _aligned = true;
            }

            slump = Mathf.MoveTowards(slump, _target, Time.deltaTime / Mathf.Max(0.01f, wakeSeconds));
            if (slump <= 0.001f) return;

            Vector3 right = transform.right;
            Vector3 forward = transform.forward;
            Bend(_spine, right, spineBend);
            Bend(_chest, right, chestBend);
            Bend(_neck, right, neckBend);
            Bend(_head, right, headBend);
            if (_head) _head.rotation = Quaternion.AngleAxis(headTilt * slump, forward) * _head.rotation;
        }

        void Bend(Transform bone, Vector3 axis, float degrees)
        {
            if (bone) bone.rotation = Quaternion.AngleAxis(degrees * slump, axis) * bone.rotation;
        }
    }
}
