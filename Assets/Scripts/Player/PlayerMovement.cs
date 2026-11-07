using UnityEngine;
using Shift.Core;

namespace Shift.Player
{
    /// <summary>
    /// Handles both 2D Platformer movement and FPS movement on the shared Rigidbody.
    /// Provides responsive platformer acceleration, coyote time, jump buffering,
    /// and camera-relative slow-motion FPS movement.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Platformer Movement")]
        [SerializeField] private float platformerRunSpeed = 12f;
        [SerializeField] private float platformerAcceleration = 70f;
        [SerializeField] private float platformerDeceleration = 60f;
        [SerializeField] private float platformerAirAcceleration = 35f;

        [Header("Platformer Jump")]
        [SerializeField] private float jumpForce = 13f;
        [SerializeField] private float fallGravityMultiplier = 2.5f;
        [SerializeField] private float lowJumpMultiplier = 2.0f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("FPS Movement")]
        [SerializeField] private float fpsMoveSpeed = 4.0f;
        [SerializeField] private float fpsAcceleration = 25f;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckRadius = 0.35f;
        [SerializeField] private LayerMask groundLayer = ~0; // Default all

        [Header("Sliding (Platformer)")]
        [SerializeField] private float slideSpeedBoost = 1.3f;
        [SerializeField] private float slideDuration = 0.6f;

        [Header("Wall Jump & Slide")]
        [SerializeField] private float wallCheckDistance = 0.65f;
        [SerializeField] private float wallSlideMaxFallSpeed = 4.0f;
        [SerializeField] private float wallJumpHorizontalForce = 13f;
        [SerializeField] private float wallJumpVerticalForce = 13f;
        [SerializeField] private float wallJumpInputLockout = 0.18f;

        private PlayerController _playerController;
        private Rigidbody _rb;
        private PlayerInputReader _input;

        private bool _isGrounded;
        private bool _isTouchingWall;
        private float _wallNormalX;
        private float _wallJumpLockoutTimer;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _isSliding;
        private float _slideTimer;
        private float _slideDirection;

        public bool IsGrounded => _isGrounded;
        public bool IsTouchingWall => _isTouchingWall;
        public bool IsSliding => _isSliding;
        public Vector3 Velocity => (_rb != null ? _rb : GetComponent<Rigidbody>()).linearVelocity;

        public event System.Action<Vector3> OnLanded;
        public event System.Action<Vector3> OnJumped;
        public event System.Action<Vector3, Vector3> OnWallJumped;
        public event System.Action<Vector3> OnSlideStarted;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputReader>();

            if (groundCheckPoint == null)
            {
                var check = new GameObject("GroundCheck");
                check.transform.SetParent(transform);
                check.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                groundCheckPoint = check.transform;
            }
        }

        private void Start()
        {
            if (_input == null)
            {
                _input = _playerController.InputReader;
            }

            if (_input != null)
            {
                _input.OnJumpPressed += HandleJumpInput;
            }

            // Exclude Player layer from ground check if Player layer is set
            if (gameObject.layer != 0)
            {
                groundLayer &= ~(1 << gameObject.layer);
            }
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnJumpPressed -= HandleJumpInput;
            }
        }

        private void Update()
        {
            UpdateTimers();
        }

        private void FixedUpdate()
        {
            CheckGround();
            CheckWall();

            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;

            if (isFps)
            {
                FixedUpdateFPS();
            }
            else
            {
                FixedUpdatePlatformer();
            }
        }

        private void UpdateTimers()
        {
            if (_wallJumpLockoutTimer > 0f)
            {
                _wallJumpLockoutTimer -= Time.unscaledDeltaTime;
            }

            // Coyote timer: unscaled time ensures it feels consistent across time scales
            if (_isGrounded)
            {
                _coyoteTimer = coyoteTime;
            }
            else
            {
                _coyoteTimer -= Time.unscaledDeltaTime;
            }

            // Jump buffer timer
            if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= Time.unscaledDeltaTime;
            }

            // Check if buffered jump can execute (ground jump or wall jump)
            if (_jumpBufferTimer > 0f)
            {
                if (_coyoteTimer > 0f)
                {
                    ExecuteJump();
                }
                else if (_isTouchingWall && !_isGrounded)
                {
                    ExecuteWallJump();
                }
            }

            // Slide timer
            if (_isSliding)
            {
                _slideTimer -= Time.deltaTime;
                if (_slideTimer <= 0f || !_isGrounded)
                {
                    EndSlide();
                }
            }
            else if (_isGrounded && _input != null && _input.SlideHeld)
            {
                StartSlide();
            }
        }

        private void CheckGround()
        {
            Vector3 origin = groundCheckPoint != null ? groundCheckPoint.position : transform.position;
            
            // Sphere cast downwards ignoring triggers
            Collider[] hits = Physics.OverlapSphere(origin, groundCheckRadius, groundLayer, QueryTriggerInteraction.Ignore);
            
            bool foundGround = false;
            foreach (var hit in hits)
            {
                if (hit.gameObject != gameObject && !hit.transform.IsChildOf(transform))
                {
                    foundGround = true;
                    break;
                }
            }

            if (!foundGround && _isGrounded)
            {
                // Left the ground
            }
            else if (foundGround && !_isGrounded)
            {
                OnLanded?.Invoke(origin);
            }

            _isGrounded = foundGround;
        }

        private void CheckWall()
        {
            _isTouchingWall = false;
            _wallNormalX = 0f;

            if (_isGrounded) return;

            int mask = groundLayer & ~(1 << gameObject.layer);
            Vector3[] checkOrigins = new Vector3[]
            {
                transform.position + Vector3.up * 0.6f,
                transform.position + Vector3.up * 1.4f
            };

            foreach (var origin in checkOrigins)
            {
                // Check right (+X)
                if (Physics.Raycast(origin, Vector3.right, out RaycastHit hitRight, wallCheckDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    if (hitRight.collider.gameObject != gameObject && !hitRight.transform.IsChildOf(transform))
                    {
                        _isTouchingWall = true;
                        _wallNormalX = hitRight.normal.x;
                        return;
                    }
                }

                // Check left (-X)
                if (Physics.Raycast(origin, Vector3.left, out RaycastHit hitLeft, wallCheckDistance, mask, QueryTriggerInteraction.Ignore))
                {
                    if (hitLeft.collider.gameObject != gameObject && !hitLeft.transform.IsChildOf(transform))
                    {
                        _isTouchingWall = true;
                        _wallNormalX = hitLeft.normal.x;
                        return;
                    }
                }
            }
        }

        private void HandleJumpInput()
        {
            _jumpBufferTimer = jumpBufferTime;
        }

        private void ExecuteJump()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;

            Vector3 vel = _rb.linearVelocity;
            vel.y = jumpForce;
            _rb.linearVelocity = vel;

            OnJumped?.Invoke(transform.position);

            if (_isSliding)
            {
                EndSlide();
            }
        }

        private void ExecuteWallJump()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _wallJumpLockoutTimer = wallJumpInputLockout;

            Vector3 vel = _rb.linearVelocity;
            vel.x = Mathf.Sign(_wallNormalX) * wallJumpHorizontalForce;
            vel.y = wallJumpVerticalForce;
            vel.z = 0f;
            _rb.linearVelocity = vel;

            OnWallJumped?.Invoke(transform.position, new Vector3(_wallNormalX, 0f, 0f));

            if (_isSliding) EndSlide();
        }

        private void FixedUpdatePlatformer()
        {
            Vector2 input = _input != null ? _input.MoveInput : Vector2.zero;
            Vector3 vel = _rb.linearVelocity;

            // Only apply horizontal input acceleration if not in wall-jump lockout
            if (_wallJumpLockoutTimer <= 0f)
            {
                float targetSpeed = input.x * platformerRunSpeed;

                if (_isSliding)
                {
                    targetSpeed = _slideDirection * platformerRunSpeed * slideSpeedBoost;
                }

                float currentSpeedX = vel.x;
                float accel;

                if (Mathf.Abs(input.x) > 0.01f)
                {
                    accel = _isGrounded ? platformerAcceleration : platformerAirAcceleration;
                }
                else
                {
                    accel = platformerDeceleration;
                }

                vel.x = Mathf.MoveTowards(currentSpeedX, targetSpeed, accel * Time.fixedDeltaTime);
            }

            // Wall slide fall speed clamp
            if (_isTouchingWall && !_isGrounded && vel.y < -wallSlideMaxFallSpeed)
            {
                vel.y = -wallSlideMaxFallSpeed;
            }
            else
            {
                // Gravity adjustments for crisp, responsive platformer jumps
                if (vel.y < 0f)
                {
                    // Falling: apply higher gravity for snappy descent
                    vel += Vector3.up * (Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
                }
                else if (vel.y > 0f && (_input == null || !_input.JumpHeld))
                {
                    // Low jump multiplier: releasing jump early cuts upward velocity
                    vel += Vector3.up * (Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime);
                }
            }

            // Lock Z velocity
            vel.z = 0f;

            _rb.linearVelocity = vel;
        }

        private void FixedUpdateFPS()
        {
            Vector2 input = _input != null ? _input.MoveInput : Vector2.zero;
            Vector3 vel = _rb.linearVelocity;

            // Camera-oriented horizontal movement in 3D
            Vector3 moveDir = (transform.forward * input.y + transform.right * input.x).normalized;
            Vector3 targetHorizontalVel = moveDir * fpsMoveSpeed;

            Vector3 currentHorizontalVel = new Vector3(vel.x, 0f, vel.z);
            Vector3 newHorizontalVel = Vector3.MoveTowards(
                currentHorizontalVel, 
                targetHorizontalVel, 
                fpsAcceleration * Time.fixedDeltaTime
            );

            vel.x = newHorizontalVel.x;
            vel.z = newHorizontalVel.z;

            // Gravity still applies normally
            if (vel.y < 0f)
            {
                vel += Vector3.up * (Physics.gravity.y * (fallGravityMultiplier - 1f) * Time.fixedDeltaTime);
            }

            _rb.linearVelocity = vel;
        }

        private void StartSlide()
        {
            Vector2 input = _input != null ? _input.MoveInput : Vector2.zero;
            if (Mathf.Abs(input.x) < 0.1f) return;

            _isSliding = true;
            _slideTimer = slideDuration;
            _slideDirection = Mathf.Sign(input.x);

            OnSlideStarted?.Invoke(transform.position);

            // Slightly reduce capsule height during slide
            if (_playerController != null && _playerController.Collider != null)
            {
                _playerController.Collider.height = 1.0f;
                _playerController.Collider.center = new Vector3(0f, 0.5f, 0f);
            }
        }

        private void EndSlide()
        {
            _isSliding = false;

            // Restore capsule height
            if (_playerController != null && _playerController.Collider != null)
            {
                _playerController.Collider.height = 2.0f;
                _playerController.Collider.center = new Vector3(0f, 1.0f, 0f);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (groundCheckPoint != null)
            {
                Gizmos.color = _isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(groundCheckPoint.position, groundCheckRadius);
            }
        }
    }
}
