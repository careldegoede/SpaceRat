using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceRat.Scene03
{
    /// <summary>
    /// Simple free-fly for the Scene 3 terrain test:
    /// A/D turn (yaw), W/S up/down, Up/Down arrows forward/back, hold Shift to go faster.
    /// Never sinks below the terrain.
    /// </summary>
    public class FlyController : MonoBehaviour
    {
        [SerializeField] float turnRate = 75f;          // deg/s
        [SerializeField] float climbSpeed = 15f;        // m/s
        [SerializeField] float forwardSpeed = 30f;      // m/s
        [SerializeField] float fastMultiplier = 3f;
        [SerializeField] float response = 6f;           // velocity smoothing
        [SerializeField] float groundClearance = 1f;
        [SerializeField] Terrain terrain;

        InputAction _turn, _climb, _move, _fast;
        Vector3 _velocity;
        float _yawRate;

        void Awake()
        {
            _turn = Axis("Turn", "<Keyboard>/a", "<Keyboard>/d");
            _climb = Axis("Climb", "<Keyboard>/s", "<Keyboard>/w");
            _move = Axis("Move", "<Keyboard>/downArrow", "<Keyboard>/upArrow");
            _fast = new InputAction("Fast", InputActionType.Button);
            _fast.AddBinding("<Keyboard>/leftShift");
        }

        static InputAction Axis(string name, string negative, string positive)
        {
            var a = new InputAction(name, InputActionType.Value);
            a.AddCompositeBinding("1DAxis").With("Negative", negative).With("Positive", positive);
            return a;
        }

        void OnEnable() { _turn.Enable(); _climb.Enable(); _move.Enable(); _fast.Enable(); }
        void OnDisable() { _turn.Disable(); _climb.Disable(); _move.Disable(); _fast.Disable(); }
        void OnDestroy() { _turn.Dispose(); _climb.Dispose(); _move.Dispose(); _fast.Dispose(); }

        void Update()
        {
            float dt = Time.deltaTime;
            float k = 1f - Mathf.Exp(-response * dt);
            float boost = _fast.IsPressed() ? fastMultiplier : 1f;

            _yawRate = Mathf.Lerp(_yawRate, _turn.ReadValue<float>() * turnRate, k);
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + _yawRate * dt, 0f);

            Vector3 target = transform.forward * (_move.ReadValue<float>() * forwardSpeed * boost)
                           + Vector3.up * (_climb.ReadValue<float>() * climbSpeed * boost);
            _velocity = Vector3.Lerp(_velocity, target, k);
            Vector3 p = transform.position + _velocity * dt;

            if (terrain)
            {
                float ground = terrain.SampleHeight(p) + terrain.transform.position.y + groundClearance;
                if (p.y < ground) { p.y = ground; _velocity.y = Mathf.Max(0f, _velocity.y); }
            }
            transform.position = p;
        }
    }
}
