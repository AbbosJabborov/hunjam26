using UnityEngine;
using Shift.Core;

namespace Shift.Environment
{
    public enum DimensionFilter
    {
        Both,
        PlatformerOnly,
        FPSOnly
    }

    /// <summary>
    /// Controls object visibility and collision based on the active dimension.
    /// Allows designing dimension-specific platforms, combat barricades, and traversal puzzles.
    /// </summary>
    public class DimensionObject : MonoBehaviour
    {
        [Header("Dimension Filter")]
        [SerializeField] private DimensionFilter activeDimension = DimensionFilter.PlatformerOnly;

        [Header("Behavior")]
        [SerializeField] private bool controlVisibility = true;
        [SerializeField] private bool controlCollision = true;

        private Renderer[] _renderers;
        private Collider[] _colliders;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
        }

        private void Start()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateObjectState(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                UpdateObjectState(DimensionType.Platformer);
            }
        }

        private void OnDestroy()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }
        }

        private void HandleDimensionChanged(DimensionType dimension)
        {
            UpdateObjectState(dimension);
        }

        public void UpdateObjectState(DimensionType dimension)
        {
            if (_colliders == null || _colliders.Length == 0) _colliders = GetComponentsInChildren<Collider>(true);
            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);

            bool shouldBeActive = false;

            switch (activeDimension)
            {
                case DimensionFilter.Both:
                    shouldBeActive = true;
                    break;
                case DimensionFilter.PlatformerOnly:
                    shouldBeActive = (dimension == DimensionType.Platformer);
                    break;
                case DimensionFilter.FPSOnly:
                    shouldBeActive = (dimension == DimensionType.FPS);
                    break;
            }

            // Update colliders
            if (controlCollision && _colliders != null)
            {
                for (int i = 0; i < _colliders.Length; i++)
                {
                    if (_colliders[i] != null)
                    {
                        _colliders[i].enabled = shouldBeActive;
                    }
                }
            }

            // Update renderers
            if (controlVisibility && _renderers != null)
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (_renderers[i] != null)
                    {
                        _renderers[i].enabled = shouldBeActive;
                    }
                }
            }
        }
    }
}
