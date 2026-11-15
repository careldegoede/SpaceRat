using UnityEngine;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Seats a Humanoid character using muscle values (no animation clip needed):
    /// thighs forward, knees bent, hands resting forward on the controls, and an optional
    /// "slump" (head and chest dropped) for the unconscious pilot in Scene 1.
    /// The hips are placed at this object's position, so put the root on the seat cushion.
    /// Replace with a real sitting animation (e.g. Mixamo "Sitting Idle") later.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SeatedPose : MonoBehaviour
    {
        [Header("Legs")]
        [Tooltip("Upper Leg Front-Back muscle. Negative swings the thigh forward; -0.5 is about horizontal.")]
        [Range(-1f, 1f)] [SerializeField] float thighsForward = -0.5f;
        [Tooltip("Lower Leg Stretch muscle. 0 is about a 90 degree knee bend; -1 fully folded; 1 straight.")]
        [Range(-1f, 1f)] [SerializeField] float kneesStraight = 0f;
        [Range(-1f, 1f)] [SerializeField] float legsApart = 0.15f;

        [Header("Arms")]
        [Range(-1f, 1f)] [SerializeField] float armsDown = -0.35f;
        [Range(-1f, 1f)] [SerializeField] float armsForward = 0.55f;
        [Range(-1f, 1f)] [SerializeField] float elbowsStraight = 0.1f;

        [Header("Slump (0 = alert, 1 = unconscious)")]
        [Range(0f, 1f)] [SerializeField] float slump = 1f;

        [Tooltip("Captured once from the standing pose so re-applying never drifts.")]
        [SerializeField, HideInInspector] float bodyHeight = -1f;
        [SerializeField, HideInInspector] bool hasHips;
        [SerializeField, HideInInspector] Vector3 hipsLocal;

        HumanPoseHandler _handler;
        HumanPose _pose;
        Animator _animator;

        public float Slump
        {
            get => slump;
            set { slump = Mathf.Clamp01(value); Apply(); }
        }

        void Start() => Apply();

        void OnDestroy() => _handler?.Dispose();

        public void Apply()
        {
            if (!_animator) _animator = GetComponent<Animator>();
            if (!_animator || !_animator.avatar || !_animator.avatar.isHuman) return;
            _handler ??= new HumanPoseHandler(_animator.avatar, transform);

            _handler.GetHumanPose(ref _pose);
            if (bodyHeight < 0f) bodyHeight = _pose.bodyPosition.y;
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string m = HumanTrait.MuscleName[i];
                float v = _pose.muscles[i];

                if (m.EndsWith("Upper Leg Front-Back")) v = thighsForward;
                else if (m.EndsWith("Upper Leg In-Out")) v = legsApart;
                else if (m.EndsWith("Lower Leg Stretch")) v = kneesStraight;
                else if (m.EndsWith("Foot Up-Down")) v = 0.1f;
                else if (m.EndsWith("Arm Down-Up")) v = armsDown;
                else if (m.EndsWith("Arm Front-Back")) v = armsForward;
                else if (m.EndsWith("Forearm Stretch")) v = elbowsStraight;
                else if (m.Contains("Stretched") && !m.Contains("Thumb")) v = -0.3f;
                else if (m == "Spine Front-Back") v = Mathf.Lerp(0.05f, -0.3f, slump);
                else if (m == "Chest Front-Back") v = Mathf.Lerp(0f, -0.25f, slump);
                else if (m == "Neck Nod Down-Up") v = Mathf.Lerp(0f, -0.6f, slump);
                else if (m == "Head Nod Down-Up") v = Mathf.Lerp(0f, -0.55f, slump);
                else if (m == "Head Tilt Left-Right") v = Mathf.Lerp(0f, 0.25f, slump);
                else continue;
                _pose.muscles[i] = v;
            }

            // Keep the hips where this object sits (on the seat), facing its forward.
            _pose.bodyPosition = new Vector3(0f, bodyHeight, 0f);
            _pose.bodyRotation = Quaternion.identity;
            _handler.SetHumanPose(ref _pose);

            // SetHumanPose resolves the body slightly differently from pose to pose; pin the hips
            // to where they were first placed so re-applying (game start, waking up) never drifts.
            var hips = _animator.GetBoneTransform(HumanBodyBones.Hips);
            if (!hips) return;
            if (!hasHips) { hipsLocal = hips.localPosition; hasHips = true; }
            else hips.localPosition = hipsLocal;
        }
    }
}
