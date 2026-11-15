using UnityEngine;

namespace SpaceRat.Flight
{
    /// <summary>
    /// Scripted, deterministic drift toward the sun used during cinematics.
    /// Moves the ship root along a heading that bends toward the sun, and tumbles
    /// only the visual body child so cameras following the root stay stable.
    /// The tumble is mostly roll plus pitch/yaw wobble, so the nose stays roughly
    /// on the heading (keeps the sun in view from the cockpit).
    /// Disable this before handing the ship to the flight controller.
    /// </summary>
    public class ShipCinematicMotion : MonoBehaviour
    {
        [SerializeField] Transform sun;
        [SerializeField] Transform tumbleBody;

        [Header("Travel")]
        [SerializeField] float speed = 40f;
        [Tooltip("Degrees per second the heading bends toward the sun (drifting off course).")]
        [SerializeField] float pullTowardSun = 1.2f;

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

        public float Speed { get => speed; set => speed = value; }

        void Awake() => _rollRate = _targetRollRate = rollDegreesPerSecond;

        /// <summary>Eases the roll rate toward a new value (e.g. calmer for cockpit shots).</summary>
        public void SetRollRate(float degreesPerSecond) => _targetRollRate = degreesPerSecond;

        void Update()
        {
            float dt = Time.deltaTime;
            _time += dt;

            if (sun)
            {
                Vector3 toSun = (sun.position - transform.position).normalized;
                Quaternion target = Quaternion.LookRotation(toSun, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, pullTowardSun * dt);
            }
            transform.position += transform.forward * (speed * dt);

            if (tumbleBody)
            {
                _rollRate = Mathf.MoveTowards(_rollRate, _targetRollRate, rollRateEase * dt);
                _roll += _rollRate * dt;
                float w = _time * wobbleFrequency * Mathf.PI * 2f;
                float pitch = Mathf.Sin(w) * wobbleAmplitude.x;
                float yaw = Mathf.Sin(w * 0.73f + 1.3f) * wobbleAmplitude.y;
                tumbleBody.localRotation = Quaternion.Euler(pitch, yaw, _roll);
            }
        }
    }
}
