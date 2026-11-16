using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceRat.Flight
{
    /// <summary>
    /// Scene 2 conventional-thruster flight: W/S pitch (W = nose down), A/D yaw, Q/E roll,
    /// Up/Down arrows throttle, hold Left Shift for a short boost that recharges.
    /// Warp is never available. When <see cref="ControlsEnabled"/> is false the ship keeps flying and
    /// can be steered by <see cref="SetAutopilot"/> (used by the scripted malfunction).
    /// </summary>
    public class CruiseFlightController : MonoBehaviour
    {
        [Header("Speed (m/s)")]
        [SerializeField] float minSpeed = 25f;
        [SerializeField] float maxSpeed = 90f;
        [SerializeField] float startSpeed = 60f;
        [SerializeField] float throttleRate = 30f;
        [SerializeField] float speedResponse = 25f;

        [Header("Boost")]
        [SerializeField] float boostMultiplier = 1.6f;
        [SerializeField] float boostMaxSeconds = 4f;
        [SerializeField] float boostRechargeSeconds = 8f;

        [Header("Handling")]
        [SerializeField] float maxTurnRate = 35f;
        [SerializeField] float maxRollRate = 70f;
        [SerializeField] float turnResponse = 70f;
        [SerializeField] bool wPitchesNoseDown = true;

        InputAction _steer, _roll, _throttle, _boost;
        Vector3 _rate;            // pitch, yaw, roll (deg/s)
        float _throttleSpeed;
        float _speed;
        Vector3 _bump;            // decaying positional nudge (asteroid bumps)

        Vector3? _autoTarget;
        float _autoTurnRate;
        float _autoSpeed;

        public bool ControlsEnabled { get; set; }
        public float Speed => _speed;
        public float BoostEnergy { get; private set; } = 1f;
        public bool Boosting { get; private set; }
        public bool BoostAvailable { get; set; } = true;

        void Awake()
        {
            _steer = new InputAction("Steer", InputActionType.Value);
            _steer.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _steer.AddBinding("<Gamepad>/leftStick");

            _roll = new InputAction("Roll", InputActionType.Value);
            _roll.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/e").With("Positive", "<Keyboard>/q");
            _roll.AddCompositeBinding("1DAxis").With("Negative", "<Gamepad>/rightShoulder").With("Positive", "<Gamepad>/leftShoulder");

            _throttle = new InputAction("Throttle", InputActionType.Value);
            _throttle.AddCompositeBinding("1DAxis").With("Negative", "<Keyboard>/downArrow").With("Positive", "<Keyboard>/upArrow");
            _throttle.AddBinding("<Gamepad>/rightStick/y");

            _boost = new InputAction("Boost", InputActionType.Button);
            _boost.AddBinding("<Keyboard>/leftShift");
            _boost.AddBinding("<Gamepad>/buttonSouth");

            _throttleSpeed = _speed = startSpeed;
        }

        void OnEnable() { _steer.Enable(); _roll.Enable(); _throttle.Enable(); _boost.Enable(); }
        void OnDisable() { _steer.Disable(); _roll.Disable(); _throttle.Disable(); _boost.Disable(); }
        void OnDestroy() { _steer.Dispose(); _roll.Dispose(); _throttle.Dispose(); _boost.Dispose(); }

        /// <summary>Takes over steering: turn toward a world point at a rate, approach a speed.</summary>
        public void SetAutopilot(Vector3 worldTarget, float turnRate, float speed)
        {
            _autoTarget = worldTarget;
            _autoTurnRate = turnRate;
            _autoSpeed = speed;
        }

        public void ClearAutopilot() => _autoTarget = null;

        /// <summary>Instant speed override (e.g. scene start drift).</summary>
        public void SetSpeed(float speed) => _throttleSpeed = _speed = speed;

        public void Bump(Vector3 worldImpulse) => _bump += worldImpulse;

        void Update()
        {
            float dt = Time.deltaTime;
            Vector3 targetRate = Vector3.zero;
            bool boostHeld = false;

            if (ControlsEnabled)
            {
                Vector2 steer = Vector2.ClampMagnitude(_steer.ReadValue<Vector2>(), 1f);
                float pitchSign = wPitchesNoseDown ? 1f : -1f;
                targetRate = new Vector3(steer.y * pitchSign * maxTurnRate, steer.x * maxTurnRate, _roll.ReadValue<float>() * maxRollRate);
                _throttleSpeed = Mathf.Clamp(_throttleSpeed + _throttle.ReadValue<float>() * throttleRate * dt, minSpeed, maxSpeed);
                boostHeld = BoostAvailable && _boost.IsPressed();
            }

            _rate = Vector3.MoveTowards(_rate, targetRate, turnResponse * dt);
            transform.rotation *= Quaternion.Euler(_rate.x * dt, _rate.y * dt, _rate.z * dt);

            if (!ControlsEnabled && _autoTarget.HasValue)
            {
                Vector3 to = (_autoTarget.Value - transform.position).normalized;
                Vector3 fwd = Vector3.RotateTowards(transform.forward, to, _autoTurnRate * Mathf.Deg2Rad * dt, 0f);
                transform.rotation = Quaternion.LookRotation(fwd, transform.up);
                _throttleSpeed = _autoSpeed;
            }

            // Boost: drains while held, recharges when released.
            Boosting = boostHeld && BoostEnergy > 0f;
            BoostEnergy = Boosting
                ? Mathf.Max(0f, BoostEnergy - dt / boostMaxSeconds)
                : Mathf.Min(1f, BoostEnergy + (boostHeld ? 0f : dt / boostRechargeSeconds));

            float target = _throttleSpeed * (Boosting ? boostMultiplier : 1f);
            _speed = Mathf.MoveTowards(_speed, target, speedResponse * (Boosting ? 2f : 1f) * dt);

            transform.position += transform.forward * (_speed * dt) + _bump * dt;
            _bump = Vector3.MoveTowards(_bump, Vector3.zero, 40f * dt);
        }
    }
}
