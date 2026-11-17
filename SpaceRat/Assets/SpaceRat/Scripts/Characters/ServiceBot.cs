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
        [Tooltip("Personal space: never closer than this to the pilot (slides around him instead of through him).")]
        [SerializeField] float keepAway = 2f;

        public bool Active { get; private set; }
        public bool Following { get; set; }
        public Transform FollowTarget { get => follow; set => follow = value; }

        Transform _sendTarget;

        /// <summary>True while the fly-away exit is running.</summary>
        public bool Flying { get; private set; }

        /// <summary>
        /// Exit: lift off on the spot with <paramref name="carry"/> hung under the bot, then climb and
        /// fly off towards the destination; deactivates when far away.
        /// </summary>
        public void FlyAway(Transform destination, GameObject carry)
        {
            Following = false;
            _sendTarget = null;
            StartCoroutine(FlyRoutine(destination, carry));
        }

        System.Collections.IEnumerator FlyRoutine(Transform destination, GameObject carry)
        {
            Flying = true;
            Vector3 Dir()
            {
                Vector3 d = (destination ? destination.position : transform.position + transform.forward * 100f) - transform.position;
                d.y = 0f;
                return d.sqrMagnitude > 0.01f ? d.normalized : transform.forward;
            }

            SetAnim(true, false);

            // 1) Float up, bobbing, with the part hung under it.
            Vector3 start = transform.position;
            if (carry)
            {
                carry.SetActive(true);
                foreach (var c in carry.GetComponentsInChildren<Collider>()) c.enabled = false;
                carry.transform.SetParent(transform, true);   // keep its world scale
                carry.transform.localPosition = new Vector3(0f, -0.35f, 0f);
                carry.transform.localRotation = Quaternion.identity;
            }
            for (float t = 0f; t < 2.2f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 2.2f);
                transform.position = start + Vector3.up * (k * 2.6f + Mathf.Sin(t * 5f) * 0.06f);
                transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
                yield return null;
            }

            // 2) Fly off towards the destination, climbing and speeding up.
            float speed = 2f;
            for (float t = 0f; t < 9f; t += Time.deltaTime)
            {
                speed = Mathf.Min(speed + 9f * Time.deltaTime, 28f);
                Vector3 v = Dir() * speed + Vector3.up * Mathf.Lerp(2.5f, 0.6f, t / 9f);
                transform.position += v * Time.deltaTime;
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(Dir(), Vector3.up), turnSpeed * 0.5f * Time.deltaTime);
                yield return null;
            }
            Flying = false;
            gameObject.SetActive(false);
        }

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
            if (Flying) return;
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
            float speed = dist > 6f ? catchUpSpeed : walkSpeed;
            Vector3 step = to.normalized * Mathf.Min(dist, speed * Time.deltaTime);
            if (moving && Blocked(step)) moving = false;   // spot is on his far side: wait beside him
            if (moving)
            {
                transform.position = KeepClear(transform.position + step);
                var look = Quaternion.LookRotation(to.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * Time.deltaTime);
            }
            else
            {
                // Idle: face the same way as the pilot.
                var look = Quaternion.LookRotation(flatFwd, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * 0.4f * Time.deltaTime);
            }
            transform.position = KeepClear(transform.position);
            StickToGround();
            SetAnim(true, moving);
        }

        /// <summary>True if the step would take the bot into the pilot's personal space.</summary>
        bool Blocked(Vector3 step)
        {
            if (!follow) return false;
            Vector3 next = transform.position + step - follow.position;
            next.y = 0f;
            return next.magnitude < keepAway;
        }

        /// <summary>Pushes a position out of the pilot's personal-space circle (flat), so the bot goes around him.</summary>
        Vector3 KeepClear(Vector3 pos)
        {
            if (!follow) return pos;
            Vector3 away = pos - follow.position;
            away.y = 0f;
            float d = away.magnitude;
            if (d >= keepAway) return pos;
            Vector3 dir = d > 0.01f ? away / d : -follow.forward;
            Vector3 fixedPos = follow.position + dir * keepAway;
            fixedPos.y = pos.y;
            return fixedPos;
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
