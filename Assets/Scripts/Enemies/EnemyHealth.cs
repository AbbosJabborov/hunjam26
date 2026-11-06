using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Shift.Combat;

namespace Shift.Enemies
{
    /// <summary>
    /// Manages enemy health, damage reception, hit feedback flash, and death.
    /// Retains health and state seamlessly across dimension switches.
    /// </summary>
    public class EnemyHealth : MonoBehaviour, IDamageable
    {
        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float currentHealth = 100f;

        [Header("Hit Feedback")]
        [SerializeField] private float flashDuration = 0.1f;
        [SerializeField] private Color flashColor = Color.red;

        public float CurrentHealth => currentHealth;
        public float MaxHealth => maxHealth;
        public bool IsAlive => currentHealth > 0f;

        public event Action<float, float> OnHealthChanged; // (current, max)
        public event Action OnDeath;

        private Renderer[] _renderers;
        private List<Color> _originalColors = new List<Color>();
        private Coroutine _flashCoroutine;

        private void Awake()
        {
            currentHealth = maxHealth;
            CacheRenderers();
        }

        private void CacheRenderers()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _originalColors.Clear();
            foreach (var r in _renderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_BaseColor"))
                {
                    _originalColors.Add(r.material.GetColor("_BaseColor"));
                }
                else if (r != null && r.material != null && r.material.HasProperty("_Color"))
                {
                    _originalColors.Add(r.material.color);
                }
                else
                {
                    _originalColors.Add(Color.white);
                }
            }
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (!IsAlive) return;

            if (_renderers == null) CacheRenderers();

            currentHealth = Mathf.Max(0f, currentHealth - amount);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);

            if (Application.isPlaying && gameObject.activeInHierarchy)
            {
                if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
                _flashCoroutine = StartCoroutine(FlashRoutine());
            }

            if (currentHealth <= 0f)
            {
                Die();
            }
        }

        private IEnumerator FlashRoutine()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null)
                {
                    if (_renderers[i].material.HasProperty("_BaseColor"))
                        _renderers[i].material.SetColor("_BaseColor", flashColor);
                    else if (_renderers[i].material.HasProperty("_Color"))
                        _renderers[i].material.color = flashColor;
                }
            }

            yield return new WaitForSecondsRealtime(flashDuration);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null && i < _originalColors.Count)
                {
                    if (_renderers[i].material.HasProperty("_BaseColor"))
                        _renderers[i].material.SetColor("_BaseColor", _originalColors[i]);
                    else if (_renderers[i].material.HasProperty("_Color"))
                        _renderers[i].material.color = _originalColors[i];
                }
            }
        }

        public void ResetHealth()
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
        }

        private void Die()
        {
            OnDeath?.Invoke();

            if (Application.isPlaying)
            {
                // Spawn dramatic death burst cubes
                for (int i = 0; i < 5; i++)
                {
                    var fragment = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fragment.name = "EnemyFragment";
                    fragment.transform.position = transform.position + UnityEngine.Random.insideUnitSphere * 0.5f;
                    fragment.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.2f, 0.35f);

                    var r = fragment.GetComponent<Renderer>();
                    if (r != null)
                    {
                        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                        mat.color = flashColor;
                        r.material = mat;
                    }

                    var col = fragment.GetComponent<Collider>();
                    if (col != null) col.enabled = false;

                    var rb = fragment.AddComponent<Rigidbody>();
                    rb.linearVelocity = (UnityEngine.Random.insideUnitSphere + Vector3.up).normalized * UnityEngine.Random.Range(4f, 8f);

                    Destroy(fragment, 1.0f);
                }

                Destroy(gameObject);
            }
            else
            {
                DestroyImmediate(gameObject);
            }
        }
    }
}
