using System;
using UnityEngine;
using UnityEngine.Events;

namespace Shift.Combat
{
    /// <summary>
    /// Target that can be shot in FPS dimension to trigger progression events
    /// (e.g. lowering laser barriers, opening gates, extending platforms).
    /// </summary>
    public class ShootableTarget : MonoBehaviour, IDamageable
    {
        [Header("Target Settings")]
        [SerializeField] private float health = 1f;
        [SerializeField] private GameObject objectToDisable;
        [SerializeField] private GameObject objectToEnable;
        [SerializeField] private UnityEvent onTargetHit;

        [Header("Visual Feedback")]
        [SerializeField] private Color hitColor = Color.green;

        private bool _isTriggered;
        private Renderer _renderer;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
        }

        public void TakeDamage(float amount, Vector3 hitPoint, Vector3 hitNormal)
        {
            if (_isTriggered) return;

            health -= amount;
            if (health <= 0f)
            {
                TriggerTarget();
            }
        }

        private void TriggerTarget()
        {
            _isTriggered = true;

            if (_renderer != null && _renderer.material != null)
            {
                if (_renderer.material.HasProperty("_BaseColor"))
                    _renderer.material.SetColor("_BaseColor", hitColor);
                else
                    _renderer.material.color = hitColor;
            }

            if (objectToDisable != null)
            {
                objectToDisable.SetActive(false);
            }

            if (objectToEnable != null)
            {
                objectToEnable.SetActive(true);
            }

            onTargetHit?.Invoke();
            Debug.Log($"[ShootableTarget] Target {name} activated!");
        }
    }
}

