using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceRat.Characters
{
    /// <summary>
    /// Full-body first-person walking for the pilot: WASD move, mouse look, Shift run, gravity on a
    /// CharacterController. The camera sits at the head bone (head collapsed so you never see inside it)
    /// and you can look down at your own body. Animator states Idle / Walk / Run are cross-faded by speed.
    /// Click to capture the mouse, Escape to release it.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class FirstPersonWalker : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] Animator animator;

        [Header("Movement (m/s)")]
        [SerializeField] float walkSpeed = 3.2f;
        [SerializeField] float runSpeed = 7.5f;
        [SerializeField] float acceleration = 12f;
        [SerializeField] float gravity = -18f;

        [Header("Water")]
        [Tooltip("Optional: with a terrain set, the pilot can wade only this deep below the water level.")]
        [SerializeField] Terrain terrain;
        [SerializeField] float waterLevel = 0f;
        [SerializeField] float maxWadeDepth = 0.4f;

        [Header("Look")]
        [SerializeField] float mouseSensitivity = 0.12f;
        [SerializeField] Vector2 pitchLimits = new(-75f, 80f);
        [SerializeField] float fieldOfView = 72f;
        [SerializeField] Vector3 eyeOffset = new(0f, 0.08f, 0.1f);   // from the head bone, in body space

        [Header("Animation")]
        [SerializeField] string idleState = "Idle";
        [SerializeField] string walkState = "Walk";
        [SerializeField] string runState = "Run";
        [Tooltip("Animation playback speed per m/s of ground speed (keeps feet roughly in sync).")]
        [SerializeField] float walkAnimRate = 0.42f;
        [SerializeField] float runAnimRate = 0.22f;

        CharacterController _cc;
        InputAction _move, _run, _look;
        Vector3 _velocity;
        float _pitch;
        string _currentState;
        Transform _head;
        Vector3 _headScale;

        public bool InputEnabled { get; set; } = true;

        void Awake()
        {
            _cc = GetComponent<CharacterController>();
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");
            _run = new InputAction("Run", InputActionType.Button);
            _run.AddBinding("<Keyboard>/leftShift");
            _run.AddBinding("<Gamepad>/leftStickPress");
            _look = new InputAction("Look", InputActionType.Value);
            _look.AddBinding("<Mouse>/delta");

            if (animator && animator.isHuman)
            {
                _head = animator.GetBoneTransform(HumanBodyBones.Head);
                if (_head) _headScale = _head.localScale;
            }
            if (cam) { cam.fieldOfView = fieldOfView; cam.nearClipPlane = 0.03f; }
        }

        void OnEnable() { _move.Enable(); _run.Enable(); _look.Enable(); }
        void OnDisable()
        {
            _move.Disable(); _run.Disable(); _look.Disable();
            if (_head) _head.localScale = _headScale;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        void OnDestroy() { _move.Dispose(); _run.Dispose(); _look.Dispose(); }

        void Update()
        {
            float dt = Time.deltaTime;
            HandleCursor();

            // Look: yaw the body, pitch the camera.
            if (InputEnabled && Cursor.lockState == CursorLockMode.Locked)
            {
                Vector2 d = _look.ReadValue<Vector2>() * mouseSensitivity;
                transform.Rotate(0f, d.x, 0f, Space.World);
                _pitch = Mathf.Clamp(_pitch - d.y, pitchLimits.x, pitchLimits.y);
            }

            // Move relative to facing.
            Vector2 input = InputEnabled ? Vector2.ClampMagnitude(_move.ReadValue<Vector2>(), 1f) : Vector2.zero;
            bool running = InputEnabled && _run.IsPressed() && input.y > 0.1f;
            float speed = running ? runSpeed : walkSpeed;
            Vector3 wish = (transform.forward * input.y + transform.right * input.x) * speed;
            Vector3 horizontal = Vector3.MoveTowards(new Vector3(_velocity.x, 0f, _velocity.z), wish, acceleration * dt);
            if (terrain && horizontal.sqrMagnitude > 0f)
            {
                // Don't walk into the sea: block moves whose ground is deeper than wading depth.
                Vector3 next = transform.position + horizontal * Mathf.Max(dt, 0.1f);
                float ground = terrain.SampleHeight(next) + terrain.transform.position.y;
                if (ground < waterLevel - maxWadeDepth) horizontal = Vector3.zero;
            }
            _velocity = new Vector3(horizontal.x, _cc.isGrounded ? -2f : _velocity.y + gravity * dt, horizontal.z);
            _cc.Move(_velocity * dt);

            UpdateAnimation(horizontal.magnitude, running);
        }

        void LateUpdate()
        {
            if (!cam) return;
            if (_head)
            {
                _head.localScale = Vector3.one * 0.001f;     // never see the inside of your own head
                Quaternion body = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
                cam.transform.SetPositionAndRotation(_head.position + body * eyeOffset, body * Quaternion.Euler(_pitch, 0f, 0f));
            }
            else
            {
                cam.transform.SetPositionAndRotation(transform.position + Vector3.up * 1.7f, Quaternion.Euler(_pitch, transform.eulerAngles.y, 0f));
            }
        }

        void UpdateAnimation(float groundSpeed, bool running)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            string state = groundSpeed < 0.25f ? idleState : (running && groundSpeed > walkSpeed + 0.5f ? runState : walkState);
            if (state != _currentState)
            {
                animator.CrossFadeInFixedTime(state, 0.2f);
                _currentState = state;
            }
            animator.speed = state == walkState ? Mathf.Max(0.6f, groundSpeed * walkAnimRate)
                           : state == runState ? Mathf.Max(0.8f, groundSpeed * runAnimRate)
                           : 1f;
        }

        void HandleCursor()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && Application.isFocused)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>For tests / scripted moments: set look angles directly.</summary>
        public void SetLook(float yaw, float pitch)
        {
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            _pitch = Mathf.Clamp(pitch, pitchLimits.x, pitchLimits.y);
        }
    }
}
