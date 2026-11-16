using UnityEngine;

namespace SpaceRat.Characters
{
    /// <summary>
    /// The outpost service unit (Robot Sphere). Dormant = closed ball. <see cref="Activate"/> opens it
    /// (the ship computer copies itself in), after which it follows the pilot as a companion: walks when
    /// he moves, idles open when he stops, stays on the ground. Reusable in later scenes.
    /// Animator bools (vendor controller): Open_Anim, Walk_Anim, Roll_Anim.
    /// </summary>
    public class ServiceBot : MonoBehaviour
    {
        [SerializeField] Animator animator;
        [SerializeField] Transform follow;
        [SerializeField] Terrain terrain;
        [Tooltip("Where it likes to be, relative to the pilot (x right, z forward).")]
        [SerializeField] Vector3 followOffset = new(1.6f, 0f, -1.8f);
        [SerializeField] float stopDistance = 0.6f;
        [SerializeField] float walkSpeed = 3.4f;
        [SerializeField] float catchUpSpeed = 8f;
        [SerializeField] float turnSpeed = 360f;
        [SerializeField] float hoverHeight = 0f;

        public bool Active { get; private set; }
        public bool Following { get; set; }
        public Transform FollowTarget { get => follow; set => follow = value; }

        Transform _sendTarget;

        /// <summary>Stop following and roll off to a destination (e.g. back to the ship).</summary>
        public void SendTo(Transform destination)
        {
            Following = false;
            _sendTarget = destination;
        }

        static readonly int Open = Animator.StringToHash("Open_Anim");
        static readonly int Walk = Animator.StringToHash("Walk_Anim");
        static readonly int Roll = Animator.StringToHash("Roll_Anim");

        void Awake()
        {
            if (!animator) animator = GetComponentInChildren<Animator>();
            SetAnim(false, false);
        }

        /// <summary>Wake the bot up (opens the shell).</summary>
        public void Activate()
        {
            Active = true;
            SetAnim(true, false);
        }

        void Update()
        {
            if (Active && _sendTarget) { RollTowards(_sendTarget.position); return; }
            if (!Active || !Following || !follow) return;
            Vector3 flatFwd = Vector3.ProjectOnPlane(follow.forward, Vector3.up).normalized;
            if (flatFwd.sqrMagnitude < 0.01f) flatFwd = Vector3.forward;
            Vector3 right = Vector3.Cross(Vector3.up, flatFwd);
            Vector3 goal = follow.position + right * followOffset.x + flatFwd * followOffset.z;

            Vector3 to = goal - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            bool moving = dist > stopDistance;
            if (moving)
            {
                float speed = dist > 6f ? catchUpSpeed : walkSpeed;
                Vector3 step = to.normalized * Mathf.Min(dist, speed * Time.deltaTime);
                transform.position += step;
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
            else
            {
                // Idle: face the same way as the pilot.
                var look = Quaternion.LookRotation(flatFwd, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * 0.4f * Time.deltaTime);
            }
            StickToGround();
            SetAnim(true, moving);
        }

        void RollTowards(Vector3 goal)
        {
            Vector3 to = goal - transform.position;
            to.y = 0f;
            if (to.magnitude < 1f) { _sendTarget = null; SetAnim(true, false); return; }
            transform.position += to.normalized * catchUpSpeed * Time.deltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to.normalized, Vector3.up), turnSpeed * Time.deltaTime);
            StickToGround();
            if (animator && animator.runtimeAnimatorController)
            {
                animator.SetBool(Open, false);
                animator.SetBool(Walk, false);
                animator.SetBool(Roll, true);
            }
        }

        void StickToGround()
        {
            var p = transform.position;
            float ground;
            if (Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out var hit, 6f, ~0, QueryTriggerInteraction.Ignore)
                && !hit.collider.transform.IsChildOf(transform) && (!follow || !hit.collider.transform.IsChildOf(follow)))
                ground = hit.point.y;
            else if (terrain)
                ground = terrain.SampleHeight(p) + terrain.transform.position.y;
            else return;
            p.y = Mathf.Lerp(p.y, ground + hoverHeight, 1f - Mathf.Exp(-Time.deltaTime * 12f));
            transform.position = p;
        }

        void SetAnim(bool open, bool walk)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            animator.SetBool(Open, open);
            animator.SetBool(Walk, walk);
            animator.SetBool(Roll, false);
        }
    }
}
