using System.Collections;
using UnityEngine;
using Shift.Core;
using Shift.Cameras;
using Shift.Player;

namespace Shift.Enemies
{
    /// <summary>
    /// Coordinates hand-drawn 2D paper sprite visuals for enemy entities in 3D world space:
    /// - Platformer Mode: displays side-facing animated sprite (Enemy_idle / Enemy_slash), flips towards player.
    /// - FPS Mode: displays front-facing animated sprite (Enemy_front_idle / Enemy_front_attack), billboarding to face the active camera.
    /// Preserves existing enemy entity, health, AI, and collision across dimensions.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyVisuals : MonoBehaviour
    {
        [Header("Animator Controllers")]
        [SerializeField] private RuntimeAnimatorController sideController;
        [SerializeField] private RuntimeAnimatorController frontController;
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Transform visualTransform;

        [Header("Visual Transform Settings")]
        [SerializeField] private bool lockVerticalBillboard = true; // Stay upright in FPS mode
        [SerializeField] private float spriteScale = 0.6f;
        [SerializeField] private Vector3 visualOffset = new Vector3(0f, -1.0f, 0f);

        [Header("Hit Flash")]
        [SerializeField] private Color flashColor = new Color(1f, 0.2f, 0.2f, 1f);
        [SerializeField] private float flashDuration = 0.08f;

        [Header("Legacy Sprites (Optional Fallback)")]
        [SerializeField] private Sprite platformerSprite;
        [SerializeField] private Sprite fpsSprite;

        private static readonly int AttackTriggerHash = Animator.StringToHash("Attack");
        private const string SideIdleState = "Enemy_idle";
        private const string SideAttackState = "Enemy_slash";
        private const string FrontIdleState = "Enemy_front_idle";
        private const string FrontAttackState = "Enemy_front_attack";

        private EnemyHealth _health;
        private Transform _playerTransform;
        private Coroutine _flashRoutine;
        private Coroutine _attackRoutine;
        private Color _baseColor = Color.white;
        private bool _isFps;

        public Animator Animator => animator;
        public SpriteRenderer SpriteRenderer => spriteRenderer;
        public Transform VisualTransform => visualTransform;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();

            // Setup visual transform hierarchy
            if (visualTransform == null)
            {
                var child = transform.Find("Visuals");
                if (child != null)
                {
                    visualTransform = child;
                }
                else
                {
                    visualTransform = transform;
                }
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            // Apply visual offset and scale if visualTransform is a dedicated child
            if (visualTransform != null && visualTransform != transform)
            {
                visualTransform.localPosition = visualOffset;
                visualTransform.localScale = Vector3.one * spriteScale;
            }
            else
            {
                transform.localScale = Vector3.one * spriteScale;
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
            _isFps = (dimension == DimensionType.FPS);

            if (_attackRoutine != null)
            {
                StopCoroutine(_attackRoutine);
                _attackRoutine = null;
            }

            if (_isFps)
            {
                if (animator != null && frontController != null && animator.runtimeAnimatorController != frontController)
                {
                    animator.runtimeAnimatorController = frontController;
                }
                else if (spriteRenderer != null && fpsSprite != null)
                {
                    spriteRenderer.sprite = fpsSprite;
                }

                if (spriteRenderer != null)
                {
                    spriteRenderer.flipX = false;
                }
            }
            else
            {
                if (animator != null && sideController != null && animator.runtimeAnimatorController != sideController)
                {
                    animator.runtimeAnimatorController = sideController;
                }
                else if (spriteRenderer != null && platformerSprite != null)
                {
                    spriteRenderer.sprite = platformerSprite;
                }

                transform.rotation = Quaternion.identity;
                if (visualTransform != null)
                {
                    visualTransform.localRotation = Quaternion.identity;
                }

                UpdatePlatformerFacing();
            }
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null) return;

            if (_isFps)
            {
                // Perspective Mode: rotate enemy to face the player directly
                if (_playerTransform == null) FindPlayer();
                if (_playerTransform != null)
                {
                    Vector3 toPlayer = _playerTransform.position - transform.position;
                    if (lockVerticalBillboard)
                    {
                        toPlayer.y = 0f;
                    }

                    if (toPlayer.sqrMagnitude > 0.001f)
                    {
                        // In Unity SpriteRenderer front normal points along -Z, so +Z forward looks away from player
                        transform.rotation = Quaternion.LookRotation(-toPlayer.normalized, Vector3.up);
                    }
                }

                if (visualTransform != null && visualTransform != transform)
                {
                    visualTransform.localRotation = Quaternion.identity;
                }

                spriteRenderer.flipX = false;
            }
            else
            {
                // Platformer Mode: Flat to 2D plane (no 3D rotation), flip towards player horizontally
                transform.rotation = Quaternion.identity;
                if (visualTransform != null && visualTransform != transform)
                {
                    visualTransform.localRotation = Quaternion.identity;
                }
                UpdatePlatformerFacing();
            }
        }

        private void UpdatePlatformerFacing()
        {
            if (spriteRenderer == null) return;

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            // Default side sprite has head facing LEFT.
            // When player is to the RIGHT of enemy, flipX = true to face right towards player.
            // When player is to the LEFT of enemy, flipX = false to face left towards player.
            bool playerIsRight = _playerTransform.position.x > transform.position.x;
            spriteRenderer.flipX = playerIsRight;
        }

        /// <summary>
        /// Plays the animated attack/slash clip matching the active dimension.
        /// </summary>
        public void PlayAttack()
        {
            if (animator == null) return;

            // Trigger the transition in the active AnimatorController
            animator.SetTrigger(AttackTriggerHash);

            // Also explicitly play the state to guarantee prompt playback
            string attackState = _isFps ? FrontAttackState : SideAttackState;
            animator.Play(attackState, 0, 0f);

            if (_attackRoutine != null) StopCoroutine(_attackRoutine);
            _attackRoutine = StartCoroutine(ReturnToIdleRoutine(_isFps ? 0.42f : 0.58f));
        }

        public void PlayAttackAnimation()
        {
            PlayAttack();
        }

        private IEnumerator ReturnToIdleRoutine(float clipDuration)
        {
            yield return new WaitForSeconds(clipDuration);

            if (animator != null)
            {
                string idleState = _isFps ? FrontIdleState : SideIdleState;
                animator.Play(idleState, 0, 0f);
            }

            _attackRoutine = null;
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

        public void SetControllers(RuntimeAnimatorController side, RuntimeAnimatorController front, Animator anim, SpriteRenderer sr, Transform vTrans = null)
        {
            sideController = side;
            frontController = front;
            animator = anim;
            spriteRenderer = sr;
            visualTransform = vTrans != null ? vTrans : (anim != null ? anim.transform : transform);
            if (spriteRenderer != null) _baseColor = spriteRenderer.color;
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
