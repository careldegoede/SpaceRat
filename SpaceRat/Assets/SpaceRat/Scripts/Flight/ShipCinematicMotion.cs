using UnityEngine;

namespace SpaceRat.Flight
{
    /// <summary>
    /// Scripted, deterministic drift toward the sun used during cinematics.
    /// Moves the ship root along a heading that bends toward the sun, and tumbles
    /// only the visual body child so cameras following the root stay stable.
    /// The tumble is mostly roll plus pitch/yaw wobble, so the nose stays roughly
    /// on the heading (keeps the sun in view from the cockpit).
    ///
    /// The drift slows as it nears <see cref="holdDistance"/> so manual control
    /// always starts from a consistent distance, however long the dialogue runs.
    /// Set <see cref="DriveRoot"/> false when the flight controller takes over;
    /// the body tumble keeps running (scaled down) for an unstable feel.
    /// </summary>
    public class ShipCinematicMotion : MonoBehaviour
    {
        [SerializeField] Transform sun;
        [SerializeField] Transform tumbleBody;

        [Header("Travel")]
        [SerializeField] float speed = 40f;
        [Tooltip("Degrees per second the heading bends toward the sun (drifting off course).")]
        [SerializeField] float pullTowardSun = 1.2f;
        [Tooltip("Distance from the sun centre where the cinematic drift parks the ship.")]
        [SerializeField] float holdDistance = 1450f;
        [Tooltip("Distance over which the drift eases from full speed down to holdSpeed.")]
        [SerializeField] float slowRange = 500f;
        [SerializeField] float holdSpeed = 3f;

        [Header("Tumble (visual body only)")]
        [SerializeField] float rollDegreesPerSecond = 38f;
        [SerializeField] Vector2 wobbleAmplitude = new(10f, 14f);
        [SerializeField] float wobbleFrequency = 0.3f;
        [Tooltip("How fast roll rate changes when SetRollRate is called (deg/s per second).")]
        [SerializeField] float rollRateEase = 20f;

        float _time;
        float _roll;
        float _rollRate;
        float _targetRollRate;
        float _wobbleScale = 1f;
        float _targetWobbleScale = 1f;
        bool _levelling;

        /// <summary>When false, only the body tumble runs; the root is left to the flight controller.</summary>
        public bool DriveRoot { get; set; } = true;

        public Transform Sun => sun;

        void Awake() => _rollRate = _targetRollRate = rollDegreesPerSecond;

        /// <summary>Eases the roll rate toward a new value (e.g. calmer for cockpit shots).</summary>
        public void SetRollRate(float degreesPerSecond) => _targetRollRate = degreesPerSecond;

        /// <summary>Eases the pitch/yaw wobble amplitude multiplier (1 = full).</summary>
        public void SetWobbleScale(float scale) => _targetWobbleScale = Mathf.Max(0f, scale);

        /// <summary>Stops the roll and eases the body back to wings-level (for manual control).</summary>
        public void LevelOut()
        {
            _levelling = true;
            _targetRollRate = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;

            if (DriveRoot)
            {
                if (sun)
                {
                    Vector3 toSun = (sun.position - transform.position).normalized;
                    Quaternion target = Quaternion.LookRotation(toSun, Vector3.up);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, target, pullTowardSun * dt);
                }

                float v = speed;
                if (sun)
                {
                    float dist = Vector3.Distance(transform.position, sun.position);
                    v = Mathf.Max(holdSpeed, speed * Mathf.Clamp01((dist - holdDistance) / slowRange));
                }
                transform.position += transform.forward * (v * dt);
            }

            if (tumbleBody)
            {
                _rollRate = Mathf.MoveTowards(_rollRate, _targetRollRate, rollRateEase * dt);
                _wobbleScale = Mathf.MoveTowards(_wobbleScale, _targetWobbleScale, 0.5f * dt);
                _roll += _rollRate * dt;
                if (_levelling && Mathf.Abs(_rollRate) < 0.01f)
                    _roll = Mathf.MoveTowardsAngle(_roll, 0f, 45f * dt);
                float w = _time * wobbleFrequency * Mathf.PI * 2f;
                float pitch = Mathf.Sin(w) * wobbleAmplitude.x * _wobbleScale;
                float yaw = Mathf.Sin(w * 0.73f + 1.3f) * wobbleAmplitude.y * _wobbleScale;
                tumbleBody.localRotation = Quaternion.Euler(pitch, yaw, _roll);
            }
        }
    }
}
