using System;
using UnityEngine;

namespace Shift.Core
{
    /// <summary>
    /// Central manager for dimension state (Platformer vs FPS),
    /// time scale manipulation, and dimension switch events.
    /// </summary>
    public class DimensionManager : MonoBehaviour
    {
        public static DimensionManager Instance { get; private set; }

        [Header("Dimension State")]
        [SerializeField] private DimensionType currentDimension = DimensionType.Platformer;
        [SerializeField] private float switchCooldown = 0.25f;

        [Header("Time Manipulation")]
        [Range(0.05f, 1f)]
        [SerializeField] private float fpsTimeScale = 0.25f;
        [SerializeField] private float platformerTimeScale = 1.0f;
        private const float DefaultFixedDeltaTime = 0.02f;

        private float _lastSwitchUnscaledTime = -999f;

        public DimensionType CurrentDimension => currentDimension;
        public bool IsPlatformer => currentDimension == DimensionType.Platformer;
        public bool IsFPS => currentDimension == DimensionType.FPS;

        public event Action<DimensionType> OnDimensionChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            ApplyDimensionSettings(currentDimension);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Time.timeScale = 1.0f;
                Time.fixedDeltaTime = DefaultFixedDeltaTime;
                Instance = null;
            }
        }

        /// <summary>
        /// Attempts to toggle dimension between Platformer and FPS.
        /// Respects the switch lockout cooldown.
        /// </summary>
        public bool SwitchDimension()
        {
            if (Time.unscaledTime - _lastSwitchUnscaledTime < switchCooldown)
            {
                return false;
            }

            DimensionType targetDimension = currentDimension == DimensionType.Platformer
                ? DimensionType.FPS
                : DimensionType.Platformer;

            SetDimension(targetDimension);
            return true;
        }

        /// <summary>
        /// Sets a specific dimension and updates time scale and subscribers.
        /// </summary>
        public void SetDimension(DimensionType newDimension)
        {
            _lastSwitchUnscaledTime = Time.unscaledTime;
            currentDimension = newDimension;

            ApplyDimensionSettings(newDimension);
            OnDimensionChanged?.Invoke(currentDimension);
        }

        private void ApplyDimensionSettings(DimensionType dimension)
        {
            float targetTimeScale = dimension == DimensionType.FPS ? fpsTimeScale : platformerTimeScale;
            Time.timeScale = targetTimeScale;
            Time.fixedDeltaTime = DefaultFixedDeltaTime * targetTimeScale;
        }
    }
}

