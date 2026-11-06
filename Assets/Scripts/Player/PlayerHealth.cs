using System;
using UnityEngine;
using Shift.Combat;

namespace Shift.Player
{
    /// <summary>
    /// Tracks player health, processes incoming damage, and handles death.
    /// Implements IDamageable so enemies can attack the player.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;
        [SerializeField] private bool godMode = false;

        [Header("Pit Fall Death")]
        [SerializeField] private float fallDeathY = -8f;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0f;

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action OnPlayerDied;

        private void Awake()
        {
            currentHealth = maxHealth;
        }

        private void Update()
        {
            if (IsAlive && transform.position.y < fallDeathY)
            {
                TakeDamage(maxHealth * 10f, transform.position, Vector3.up);
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (godMode || !IsAlive) return;

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive) return;

            currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            OnPlayerDied?.Invoke();
            Debug.Log("[PlayerHealth] Player died!");
        }
    }
}

