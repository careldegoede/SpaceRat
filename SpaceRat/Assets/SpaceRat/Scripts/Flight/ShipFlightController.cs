using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceRat.Flight
{
    /// <summary>
    /// Simplified manual flight for the Scene 1 escape: pitch (W = nose down, S = nose up)
    /// and yaw (A/D), constant forward speed, sluggish response, control authority that
    /// ramps up after takeover, a star "pull" that drags the nose back toward the sun
    /// (stronger when hot), and small random disturbances. Not orbital mechanics.
    /// Disabled until the director hands over control.
    /// </summary>
    public class ShipFlightController : MonoBehaviour
    {
        [SerializeField] Transform sun;

        [Header("Speed")]
        [SerializeField] float speed = 55f;

        [Header("Steering")]
        [Tooltip("Maximum turn rate at full authority (deg/s).")]
        [SerializeField] float maxTurnRate = 30f;
        [Tooltip("How quickly turn rate follows input (deg/s per second). Lower = more sluggish.")]
        [SerializeField] float turnResponse = 30f;
        [Tooltip("Control authority right after takeover (0-1); ramps to 1.")]
        [Range(0f, 1f)] [SerializeField] float startAuthority = 0.3f;
        [SerializeField] float authorityRampSeconds = 6f;
        [Tooltip("Flight-sim convention: W pushes the nose down.")]
        [SerializeField] bool wPitchesNoseDown = true;

        [Header("Star pull (deg/s toward the sun)")]
        [SerializeField] float pullAtStart = 6f;
        [SerializeField] float pullWhenHot = 18f;

        [Header("Instability")]
        [SerializeField] float disturbanceDegPerSec = 4f;
        [SerializeField] float disturbanceFrequency = 0.6f;

        InputAction _steer;
        Vector2 _turnRate;      // x = yaw, y = pitch (deg/s)
        float _controlTime;

        /// <summary>0..1 heat, set by the collision evaluator; strengthens the star pull.</summary>
        public float Heat { get; set; }

        /// <summary>For automated tests: overrides player input when set.</summary>
        public Vector2? InputOverride { get; set; }

        public float Speed => speed;

        void Awake()
        {
            _steer = new InputAction("Steer", InputActionType.Value);
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            _steer.AddBinding("<Gamepad>/leftStick");
        }

        void OnEnable()
        {
            _controlTime = 0f;
            _turnRate = Vector2.zero;
            _steer.Enable();
        }

        void OnDisable() => _steer.Disable();

        void OnDestroy() => _steer.Dispose();

        void Update()
        {
            float dt = Time.deltaTime;
            _controlTime += dt;

            Vector2 input = InputOverride ?? _steer.ReadValue<Vector2>();
            input = Vector2.ClampMagnitude(input, 1f);

            float authority = Mathf.Lerp(startAuthority, 1f, Mathf.Clamp01(_controlTime / authorityRampSeconds));
            // input.y = +1 for W. Positive pitch about local X = nose down in Unity.
            float pitchSign = wPitchesNoseDown ? 1f : -1f;
            Vector2 target = new Vector2(input.x, input.y * pitchSign) * (maxTurnRate * authority);
            _turnRate = Vector2.MoveTowards(_turnRate, target, turnResponse * dt);

            float t = Time.time * disturbanceFrequency;
            Vector2 noise = new Vector2(Mathf.PerlinNoise(t, 0.37f) - 0.5f, Mathf.PerlinNoise(0.71f, t) - 0.5f)
                            * (2f * disturbanceDegPerSec);

            Vector2 rate = _turnRate + noise;
            transform.rotation *= Quaternion.Euler(rate.y * dt, rate.x * dt, 0f);

            if (sun)
            {
                // Pull the nose back toward the star; keep world-up so the horizon doesn't roll.
                Vector3 toSun = (sun.position - transform.position).normalized;
                float pull = Mathf.Lerp(pullAtStart, pullWhenHot, Heat);
                Vector3 newFwd = Vector3.RotateTowards(transform.forward, toSun, pull * Mathf.Deg2Rad * dt, 0f);
                transform.rotation = Quaternion.LookRotation(newFwd, transform.up);
            }

            transform.position += transform.forward * (speed * dt);
        }
    }
}
