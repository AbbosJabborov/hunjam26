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
            bool isSwinging = _playerGrapple != null && _playerGrapple.IsSwinging;

            // Set Animator parameters if defined
            animator.SetFloat(SpeedHash, horizontalSpeed);
            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(IsSlidingHash, isSliding);
            animator.SetBool(IsSwingingHash, isSwinging);

            // Direct fallback state playback to ensure animation updates even if controller transitions aren't wired
            if (isSwinging)
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

        private void UpdateSpriteOrientation()
        {
            if (spriteRenderer == null) return;

            Vector3 vel = _rb != null ? _rb.linearVelocity : Vector3.zero;

            // Flip sprite based on velocity along X axis
            if (vel.x > 0.15f)
            {
                spriteRenderer.flipX = false; // Facing right (+X)
            }
            else if (vel.x < -0.15f)
            {
                spriteRenderer.flipX = true; // Facing left (-X)
            }
        }

        public void Setup(SpriteRenderer sr, Animator anim)
        {
            spriteRenderer = sr;
            animator = anim;
        }
    }
}

