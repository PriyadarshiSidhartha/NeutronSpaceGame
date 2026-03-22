using System;
using System.Collections;
using UnityEngine;

namespace SpaceShooter.Player
{
    /// <summary>
    /// Manages player health and regenerating shields.
    /// Shields absorb damage first. After taking damage, shields regenerate after a delay.
    /// Fires C# events that the UIManager subscribes to for the HUD.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IHittable
    {
        [Header("Health")]
        [SerializeField] private int maxHealth = 100;

        [Header("Shields")]
        [SerializeField] private int maxShields = 50;
        [SerializeField] private float shieldRegenDelay = 3f;   // seconds before shield regen starts
        [SerializeField] private float shieldRegenRate = 10f;  // shield points per second

        [Header("Invincibility Frames")]
        [SerializeField] private float invincibilityDuration = 1f;

        // ── Runtime state ─────────────────────────────────────────────────────
        private int _currentHealth;
        private float _currentShields;
        private bool _isInvincible;
        private bool _isDead;
        private Coroutine _shieldRegenCoroutine;

        // ── Events ────────────────────────────────────────────────────────────
        public event Action<int, int> OnHealthChanged;   // (current, max)
        public event Action<float, float> OnShieldChanged;   // (current, max)
        public event Action OnPlayerDeath;

        // ── Properties ───────────────────────────────────────────────────────
        public int MaxHealth => maxHealth;
        public float MaxShields => maxShields;
        public int CurrentHealth => _currentHealth;
        public float CurrentShields => _currentShields;

        // ── Unity lifecycle ───────────────────────────────────────────────────
        private void Awake()
        {
            _currentHealth = maxHealth;
            _currentShields = maxShields;
        }

        private void Update()
        {
            // Passive shield regen handled by coroutine — nothing additional needed here
        }

        // ── IHittable ─────────────────────────────────────────────────────────
        public void TakeDamage(int damage)
        {
            if (_isInvincible || _isDead) return;

            // Stop any ongoing regen
            if (_shieldRegenCoroutine != null)
                StopCoroutine(_shieldRegenCoroutine);

            // Shields absorb first
            if (_currentShields > 0f)
            {
                _currentShields -= damage;
                if (_currentShields < 0f)
                {
                    // Overflow damage bleeds into health
                    int overflow = Mathf.Abs(Mathf.RoundToInt(_currentShields));
                    _currentShields = 0f;
                    ApplyHealthDamage(overflow);
                }
                OnShieldChanged?.Invoke(_currentShields, maxShields);
            }
            else
            {
                ApplyHealthDamage(damage);
            }

            // Start shield regen countdown
            _shieldRegenCoroutine = StartCoroutine(ShieldRegenRoutine());

            // Brief invincibility after a hit
            StartCoroutine(InvincibilityRoutine());
        }

        // ── Internal helpers ──────────────────────────────────────────────────
        private void ApplyHealthDamage(int damage)
        {
            _currentHealth = Mathf.Max(0, _currentHealth - damage);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);

            if (_currentHealth <= 0)
                Die();
        }

        private void Die()
        {
            if (_isDead) return;
            _isDead = true;
            OnPlayerDeath?.Invoke();
        }

        private IEnumerator ShieldRegenRoutine()
        {
            yield return new WaitForSeconds(shieldRegenDelay);

            while (_currentShields < maxShields)
            {
                _currentShields = Mathf.Min(_currentShields + shieldRegenRate * Time.deltaTime, maxShields);
                OnShieldChanged?.Invoke(_currentShields, maxShields);
                yield return null;
            }
        }

        private IEnumerator InvincibilityRoutine()
        {
            _isInvincible = true;
            yield return new WaitForSeconds(invincibilityDuration);
            _isInvincible = false;
        }

        // ── Public API ────────────────────────────────────────────────────────
        public void Heal(int amount)
        {
            _currentHealth = Mathf.Min(_currentHealth + amount, maxHealth);
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            _isDead = false;
            _isInvincible = false;
            _currentHealth = maxHealth;
            _currentShields = maxShields;
            OnHealthChanged?.Invoke(_currentHealth, maxHealth);
            OnShieldChanged?.Invoke(_currentShields, maxShields);
        }
    }
}
