using System.Collections;
using UnityEngine;
using Shift.Core;
using Shift.Cameras;
using Shift.Player;

namespace Shift.Enemies
{
    /// <summary>
    /// Coordinates hand-drawn 2D paper sprite visuals for enemy entities in 3D world space:
    /// - Platformer Mode: displays side-facing sprite, flips towards player.
    /// - FPS Mode: displays front-facing sprite, billboarding to face the active camera.
    /// Preserves existing enemy entity, health, AI, and collision across dimensions.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyVisuals : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite platformerSprite;
        [SerializeField] private Sprite fpsSprite;
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Billboard Settings")]
        [SerializeField] private bool lockVerticalBillboard = true; // Stay upright
        [SerializeField] private float spriteScale = 1.0f;

        [Header("Hit Flash")]
        [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float flashDuration = 0.08f;

        private EnemyHealth _health;
        private Transform _playerTransform;
        private Coroutine _flashRoutine;
        private Color _baseColor = Color.white;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            transform.localScale = Vector3.one * spriteScale;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (spriteRenderer != null)
            {
                _baseColor = spriteRenderer.color;
            }
        }

        private void Start()
        {
            FindPlayer();

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateVisualDimension(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                UpdateVisualDimension(DimensionType.Platformer);
            }

            if (_health != null)
            {
                _health.OnHealthChanged += HandleHealthChanged;
            }
        }

        private void OnDestroy()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }

            if (_health != null)
            {
                _health.OnHealthChanged -= HandleHealthChanged;
            }
        }

        private void FindPlayer()
        {
            var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                _playerTransform = pc.transform;
            }
        }

        private void HandleDimensionChanged(DimensionType dimension)
        {
            UpdateVisualDimension(dimension);
        }

        public void UpdateVisualDimension(DimensionType dimension)
        {
            if (spriteRenderer == null) return;

            bool isFps = dimension == DimensionType.FPS;

            if (isFps)
            {
                if (fpsSprite != null) spriteRenderer.sprite = fpsSprite;
                spriteRenderer.flipX = false;
            }
            else
            {
                if (platformerSprite != null) spriteRenderer.sprite = platformerSprite;
                transform.rotation = Quaternion.identity;
                UpdatePlatformerFacing();
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null) return;

            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;

            if (isFps)
            {
                // FPS Mode: Billboard behavior - face active camera
                Camera cam = CameraManager.Instance != null ? CameraManager.Instance.ActiveCamera : Camera.main;
                if (cam != null)
                {
                    Vector3 toCam = cam.transform.position - transform.position;
                    if (lockVerticalBillboard)
                    {
                        toCam.y = 0f;
                    }

                    if (toCam.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(-toCam.normalized, Vector3.up);
                    }
                }
            }
            else
            {
                // Platformer Mode: Fixed plane facing, track player horizontally
                UpdatePlatformerFacing();
            }
        }

        private void UpdatePlatformerFacing()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            // Face towards player along X
            bool playerIsLeft = _playerTransform.position.x < transform.position.x;
            spriteRenderer.flipX = playerIsLeft;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (!gameObject.activeInHierarchy) return;

            if (_flashRoutine != null) StopCoroutine(_flashRoutine);
            _flashRoutine = StartCoroutine(HitFlashRoutine());
        }

        private IEnumerator HitFlashRoutine()
        {
            if (spriteRenderer == null) yield break;

            spriteRenderer.color = flashColor;
            yield return new WaitForSeconds(flashDuration);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = _baseColor;
            }
        }

        public void SetSprites(Sprite platformer, Sprite fps, SpriteRenderer sr)
        {
            platformerSprite = platformer;
            fpsSprite = fps;
            spriteRenderer = sr;
            if (spriteRenderer != null) _baseColor = spriteRenderer.color;
        }
    }
}
