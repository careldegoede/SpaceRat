using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceRat.Characters
{
    /// <summary>
    /// The pilot's health, stamina and weapon charge (all 0..1 for the HUD).
    /// Stamina drains while running and refills while walking or standing; once empty, running is
    /// locked until it has recovered past <see cref="recoverThreshold"/> (so you can't stutter-sprint).
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        [SerializeField] FirstPersonWalker walker;

        [Header("Health")]
        [SerializeField] float maxHealth = 100f;

        [Header("Stamina (per second)")]
        [SerializeField] float maxStamina = 100f;
        [SerializeField] float runDrain = 18f;
        [SerializeField] float walkRegen = 12f;
        [SerializeField] float restRegen = 20f;
        [Tooltip("After running dry, running unlocks again at this fraction.")]
        [SerializeField, Range(0f, 1f)] float recoverThreshold = 0.3f;
        [Tooltip("Pause before stamina starts refilling after a run.")]
        [SerializeField] float regenDelay = 0.6f;

        [Header("Weapon")]
        [SerializeField] bool hasWeapon;
        [SerializeField, Range(0f, 1f)] float weaponCharge = 1f;

        float _health, _stamina, _regenAt;
        bool _exhausted;

        public float Health01 => _health / maxHealth;
        public float Stamina01 => _stamina / maxStamina;
        public float WeaponCharge01 => weaponCharge;
        public bool HasWeapon { get => hasWeapon; set => hasWeapon = value; }
        public bool Exhausted => _exhausted;
        public bool IsDead => _health <= 0f;

        /// <summary>Raised with the damage amount (HUD flashes the portrait).</summary>
        public event Action<float> Damaged;

        void Awake()
        {
            if (!walker) walker = GetComponent<FirstPersonWalker>();
            _health = maxHealth;
            _stamina = maxStamina;
        }

        void Update()
        {
            float dt = Time.deltaTime;

            // Dev cheat (editor / development builds only): F5 refills stamina.
            if ((Application.isEditor || Debug.isDebugBuild) && Application.isFocused
                && Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
                RefillStamina();
            bool running = walker && walker.IsRunning;
            if (running)
            {
                _stamina = Mathf.Max(0f, _stamina - runDrain * dt);
                _regenAt = Time.time + regenDelay;
                if (_stamina <= 0f) _exhausted = true;
            }
            else if (Time.time >= _regenAt)
            {
                bool moving = walker && walker.IsMoving;
                _stamina = Mathf.Min(maxStamina, _stamina + (moving ? walkRegen : restRegen) * dt);
            }
            if (_exhausted && Stamina01 >= recoverThreshold) _exhausted = false;
            if (walker) walker.RunAllowed = !_exhausted;
        }

        public void Damage(float amount)
        {
            if (amount <= 0f || IsDead) return;
            _health = Mathf.Max(0f, _health - amount);
            Damaged?.Invoke(amount);
        }

        /// <summary>Full stamina, not exhausted (dev cheat / pickups later).</summary>
        public void RefillStamina()
        {
            _stamina = maxStamina;
            _exhausted = false;
        }

        public void Heal(float amount) => _health = Mathf.Min(maxHealth, _health + Mathf.Max(0f, amount));

        public void SetWeaponCharge(float charge01) => weaponCharge = Mathf.Clamp01(charge01);
    }
}
