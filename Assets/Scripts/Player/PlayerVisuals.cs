using UnityEngine;
using Shift.Core;
using Shift.Cameras;

namespace Shift.Player
{
    /// <summary>
    /// Coordinates the hand-drawn 2D paper character sprite and animation states:
    /// - Idle, Running, Jumping, Sliding, and Pendulum Swinging.
    /// - Sprite orientation (facing right/left based on velocity and input).
    /// - Visibility coordination between 2D Platformer and 3D FPS dimensions.
    /// </summary>
    public class PlayerVisuals : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Animator animator;

        [Header("Animation State Hashes")]
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int IsGroundedHash = Animator.StringToHash("IsGrounded");
        private static readonly int IsSlidingHash = Animator.StringToHash("IsSliding");
        private static readonly int IsSwingingHash = Animator.StringToHash("IsSwinging");
        private static readonly int IsWallSlidingHash = Animator.StringToHash("IsWallSliding");

        [Header("Wall Slide Facing")]
        [Tooltip("When true, flips the sprite when on a right-side wall. Default false assumes unflipped faces right wall.")]
        [SerializeField] private bool flipWallSlideOnRight = false;

        public Vector3 FacingDirection => (spriteRenderer != null && spriteRenderer.flipX) ? Vector3.left : Vector3.right;

        private PlayerController _playerController;
        private PlayerMovement _playerMovement;
        private PlayerGrapple _playerGrapple;
        private Rigidbody _rb;

        private void Awake()
        {
            _playerController = GetComponentInParent<PlayerController>();
            if (_playerController != null)
            {
                _playerMovement = _playerController.GetComponent<PlayerMovement>();
                _playerGrapple = _playerController.GetComponent<PlayerGrapple>();
                _rb = _playerController.GetComponent<Rigidbody>();
            }

            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (animator == null) animator = GetComponent<Animator>();
        }

        private void Start()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateVisibility(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                UpdateVisibility(DimensionType.Platformer);
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
            UpdateVisibility(dimension);
        }

        private void Update()
        {
            UpdateVisibilityDynamic();
            UpdateAnimationParameters();
            UpdateSpriteOrientation();
        }

        private void UpdateVisibilityDynamic()
        {
            if (spriteRenderer == null) return;

            if (CameraManager.Instance != null && CameraManager.Instance.IsTransitioning)
            {
                // Visible during camera swoop until camera slips into head pivot
                spriteRenderer.enabled = CameraManager.Instance.TransitionProgress < 0.85f;
            }
            else
            {
                bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;
                spriteRenderer.enabled = !isFps;
            }
        }

        private void UpdateVisibility(DimensionType dimension)
        {
            if (spriteRenderer == null) return;
            spriteRenderer.enabled = (dimension == DimensionType.Platformer);
        }

        private void UpdateAnimationParameters()
        {
            if (animator == null) return;

            Vector3 vel = _rb != null ? _rb.linearVelocity : Vector3.zero;
            float horizontalSpeed = Mathf.Abs(vel.x);
            bool isGrounded = _playerMovement != null && _playerMovement.IsGrounded;
            bool isSliding = _playerMovement != null && _playerMovement.IsSliding;
            bool isSwinging = _playerGrapple != null && (_playerGrapple.IsSwinging || _playerGrapple.IsExtending);
            bool isWallSliding = _playerMovement != null && _playerMovement.IsWallSliding;

            // Set Animator parameters if defined
            animator.SetFloat(SpeedHash, horizontalSpeed);
            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(IsSlidingHash, isSliding);
            animator.SetBool(IsSwingingHash, isSwinging);
            animator.SetBool(IsWallSlidingHash, isWallSliding);

            // Direct fallback state playback to ensure animation updates even if controller transitions aren't wired
            if (isWallSliding)
            {
                PlayStateIfDifferent("WallSlide");
            }
            else if (isSwinging)
            {
                PlayStateIfDifferent("Swing");
            }
            else if (isSliding)
            {
                PlayStateIfDifferent("Slide");
            }
            else if (!isGrounded)
            {
                PlayStateIfDifferent("Jumping");
            }
            else if (horizontalSpeed > 0.5f)
            {
                PlayStateIfDifferent("Running");
            }
            else
            {
                PlayStateIfDifferent("Idle");
            }
        }

        private string _currentStateName = "";
        private void PlayStateIfDifferent(string stateName)
        {
            if (_currentStateName != stateName)
            {
                _currentStateName = stateName;
                animator.CrossFadeInFixedTime(stateName, 0.05f);
            }
        }

        public void TriggerShootAnimation()
        {
            if (animator != null)
            {
                PlayStateIfDifferent("Shooting");
            }
        }

        private void UpdateSpriteOrientation()
        {
            if (spriteRenderer == null) return;

            // 1. When wall sliding, orient towards or away from the wall
            if (_playerMovement != null && _playerMovement.IsWallSliding)
            {
                // WallNormalX < 0 means wall is to the player's right (+X).
                // WallNormalX > 0 means wall is to the player's left (-X).
                bool wallIsOnRight = _playerMovement.WallNormalX < 0f;
                spriteRenderer.flipX = wallIsOnRight ? flipWallSlideOnRight : !flipWallSlideOnRight;
                return;
            }

            // 2. When grapple is extending towards anchor, face target anchor
            if (_playerGrapple != null && _playerGrapple.IsExtending)
            {
                float dirX = _playerGrapple.GrappleAnchor.x - transform.position.x;
                if (Mathf.Abs(dirX) > 0.1f)
                {
                    spriteRenderer.flipX = dirX < 0f;
                    return;
                }
            }

            Vector3 vel = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector2 moveInput = Vector2.zero;
            if (_playerController != null && _playerController.InputReader != null)
            {
                moveInput = _playerController.InputReader.MoveInput;
            }

            // 3. Dynamic orientation based on velocity or active steering input
            if (Mathf.Abs(vel.x) > 0.15f)
            {
                spriteRenderer.flipX = vel.x < 0f;
            }
            else if (Mathf.Abs(moveInput.x) > 0.1f)
            {
                spriteRenderer.flipX = moveInput.x < 0f;
            }
        }

        public void Setup(SpriteRenderer sr, Animator anim)
        {
            spriteRenderer = sr;
            animator = anim;
        }
    }
}

