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
        [SerializeField] private float jumpForce = 14f;
        [SerializeField] private float fallGravityMultiplier = 1.6f;
        [SerializeField] private float lowJumpMultiplier = 1.5f;
        [SerializeField] private float coyoteTime = 0.12f;
        [SerializeField] private float jumpBufferTime = 0.14f;

        [Header("FPS Movement")]
        [SerializeField] private float fpsMoveSpeed = 4.0f;
        [SerializeField] private float fpsAcceleration = 25f;

        [Header("Ground Detection")]
        [SerializeField] private Transform groundCheckPoint;
        [SerializeField] private float groundCheckRadius = 0.22f;
        [SerializeField] private LayerMask groundLayer = ~0; // Default all

        [Header("Sliding (Platformer)")]
        [SerializeField] private float slideSpeedBoost = 1.3f;
        [SerializeField] private float slideDuration = 0.6f;

        [Header("Wall Jump & Slide")]
        [SerializeField] private float wallCheckDistance = 0.7f;
        [SerializeField] private float wallSlideSpeed = 3.2f;
        [SerializeField] private float wallJumpHorizontalForce = 14f;
        [SerializeField] private float wallJumpVerticalForce = 15f;
        [SerializeField] private float wallJumpInputLockout = 0.20f;

        private PlayerController _playerController;
        private Rigidbody _rb;
        private PlayerInputReader _input;
        private PlayerGrapple _playerGrapple;

        private bool _isGrounded;
        private bool _isTouchingWall;
        private bool _isWallSliding;
        private float _wallNormalX;
        private float _lastWallNormalX;
        private float _wallCoyoteTimer;
        private float _wallJumpLockoutTimer;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _isSliding;
        private float _slideTimer;
        private float _slideDirection;

        public bool IsGrounded => _isGrounded;
        public bool IsTouchingWall => _isTouchingWall;
        public bool IsWallSliding => _isWallSliding;
        public float WallNormalX => _wallNormalX;
        public bool IsSliding => _isSliding;
        public Vector3 Velocity => (_rb != null ? _rb : GetComponent<Rigidbody>()).linearVelocity;

        public void ClearJumpBuffer()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
        }

        public event System.Action<Vector3> OnLanded;
        public event System.Action<Vector3> OnJumped;
        public event System.Action<Vector3, Vector3> OnWallJumped;
        public event System.Action<Vector3> OnSlideStarted;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputReader>();
            _playerGrapple = GetComponent<PlayerGrapple>();

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

            if (_wallCoyoteTimer > 0f)
            {
                _wallCoyoteTimer -= Time.unscaledDeltaTime;
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
                else if ((_isTouchingWall || _wallCoyoteTimer > 0f) && !_isGrounded)
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
            // Lower center spherecast (starts inside lower capsule, casts strictly downwards)
            float castRadius = 0.22f; // Inset narrower than capsule (0.4) so vertical walls are never clipped
            Vector3 castOrigin = transform.position + Vector3.up * 0.35f;
            float castDist = 0.45f; // Reaches 0.10m below feet

            bool foundGround = false;
            Vector3 landPoint = transform.position;

            // 1. SphereCastAll strictly downwards
            RaycastHit[] sHits = Physics.SphereCastAll(castOrigin, castRadius, Vector3.down, castDist, groundLayer, QueryTriggerInteraction.Ignore);
            foreach (var hit in sHits)
            {
                if (hit.collider.gameObject == gameObject || hit.collider.transform.IsChildOf(transform))
                    continue;

                // Ground MUST face upwards (normal.y > 0.55f, max ~56° slope)
                // Vertical walls have normal.y ≈ 0 and will NEVER trigger ground status!
                if (hit.normal.y > 0.55f)
                {
                    foundGround = true;
                    landPoint = hit.point;
                    break;
                }
            }

            // 2. Fallback downward raycasts (center, left, right) for narrow beams/thin platform edges
            if (!foundGround)
            {
                Vector3[] rayOrigins = new Vector3[]
                {
                    transform.position + Vector3.up * 0.2f,
                    transform.position + Vector3.up * 0.2f + Vector3.left * 0.2f,
                    transform.position + Vector3.up * 0.2f + Vector3.right * 0.2f
                };

                for (int i = 0; i < rayOrigins.Length; i++)
                {
                    RaycastHit[] rHits = Physics.RaycastAll(rayOrigins[i], Vector3.down, 0.35f, groundLayer, QueryTriggerInteraction.Ignore);
                    foreach (var rayHit in rHits)
                    {
                        if (rayHit.collider.gameObject == gameObject || rayHit.collider.transform.IsChildOf(transform))
                            continue;

                        if (rayHit.normal.y > 0.55f)
                        {
                            foundGround = true;
                            landPoint = rayHit.point;
                            break;
                        }
                    }
                    if (foundGround) break;
                }
            }

            if (!foundGround && _isGrounded)
            {
                // Left the ground
            }
            else if (foundGround && !_isGrounded)
            {
                OnLanded?.Invoke(landPoint);
            }

            _isGrounded = foundGround;
        }

        private void CheckWall()
        {
            _isTouchingWall = false;
            _wallNormalX = 0f;

            if (_isGrounded)
            {
                _isWallSliding = false;
                _wallCoyoteTimer = 0f;
                return;
            }

            float checkDist = Mathf.Max(wallCheckDistance, 0.65f); // Reaches comfortably beyond 0.4m capsule
            Vector3[] checkOrigins = new Vector3[]
            {
                transform.position + Vector3.up * 0.4f,
                transform.position + Vector3.up * 1.0f,
                transform.position + Vector3.up * 1.5f
            };

            RaycastHit closestHit = default;
            float closestDist = float.MaxValue;
            bool hitFound = false;

            foreach (var origin in checkOrigins)
            {
                // Check right (+X)
                RaycastHit[] hitsRight = Physics.RaycastAll(origin, Vector3.right, checkDist, groundLayer, QueryTriggerInteraction.Ignore);
                foreach (var hitRight in hitsRight)
                {
                    if (hitRight.collider.gameObject == gameObject || hitRight.collider.transform.IsChildOf(transform))
                        continue;

                    // Ensure it's a vertical/steep wall surface
                    if (Mathf.Abs(hitRight.normal.x) > 0.5f && hitRight.distance < closestDist)
                    {
                        closestHit = hitRight;
                        closestDist = hitRight.distance;
                        hitFound = true;
                    }
                }

                // Check left (-X)
                RaycastHit[] hitsLeft = Physics.RaycastAll(origin, Vector3.left, checkDist, groundLayer, QueryTriggerInteraction.Ignore);
                foreach (var hitLeft in hitsLeft)
                {
                    if (hitLeft.collider.gameObject == gameObject || hitLeft.collider.transform.IsChildOf(transform))
                        continue;

                    if (Mathf.Abs(hitLeft.normal.x) > 0.5f && hitLeft.distance < closestDist)
                    {
                        closestHit = hitLeft;
                        closestDist = hitLeft.distance;
                        hitFound = true;
                    }
                }
            }

            if (hitFound)
            {
                _isTouchingWall = true;
                _wallNormalX = closestHit.normal.x;
                _lastWallNormalX = closestHit.normal.x;
                _wallCoyoteTimer = 0.18f; // Forgiving wall jump window!
            }
        }

        private void HandleJumpInput()
        {
            if (_playerGrapple != null && (_playerGrapple.IsSwinging || _playerGrapple.IsExtending))
            {
                // Grapple jump is handled by PlayerGrapple.HandleJumpWhileSwinging()
                return;
            }

            _jumpBufferTimer = jumpBufferTime;

            if (_coyoteTimer > 0f)
            {
                ExecuteJump();
            }
            else if ((_isTouchingWall || _wallCoyoteTimer > 0f) && !_isGrounded)
            {
                ExecuteWallJump();
            }
        }

        private void ExecuteJump()
        {
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _wallCoyoteTimer = 0f;
            _isWallSliding = false;

            Vector3 vel = _rb.linearVelocity;
            vel.y = jumpForce;
            _rb.linearVelocity = vel;

            AudioManager.Instance?.PlayJump();
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
            _wallCoyoteTimer = 0f;
            _isWallSliding = false;
            _wallJumpLockoutTimer = wallJumpInputLockout;

            float normalX = _wallNormalX != 0f ? _wallNormalX : _lastWallNormalX;
            if (normalX == 0f) normalX = -1f;

            Vector3 vel = _rb.linearVelocity;
            vel.x = Mathf.Sign(normalX) * wallJumpHorizontalForce;
            vel.y = wallJumpVerticalForce;
            vel.z = 0f;
            _rb.linearVelocity = vel;

            AudioManager.Instance?.PlayJump();
            OnWallJumped?.Invoke(transform.position, new Vector3(normalX, 0f, 0f));

            if (_isSliding) EndSlide();
        }

        private void FixedUpdatePlatformer()
        {
            if (_playerGrapple != null && _playerGrapple.IsSwinging)
            {
                // Yield physics control to PlayerGrapple arcade swinging
                return;
            }

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

            // Wall slide and vertical movement
            if (_isTouchingWall && !_isGrounded)
            {
                // Wall slide activates when downward velocity begins or apex of jump
                if (vel.y <= 0.1f)
                {
                    _isWallSliding = true;
                    vel.y = -wallSlideSpeed;

                    // If player is steering into the wall, zero out vel.x so they don't jam or stutter
                    // If player steers away, allow them to naturally unstick/peel off the wall
                    bool steeringIntoWall = (input.x > 0.1f && _wallNormalX < 0f) || (input.x < -0.1f && _wallNormalX > 0f);
                    if (steeringIntoWall)
                    {
                        vel.x = 0f;
                    }
                }
                else
                {
                    _isWallSliding = false;
                    // Apply low jump gravity while rising along wall
                    vel += Vector3.up * (Physics.gravity.y * (lowJumpMultiplier - 1f) * Time.fixedDeltaTime);
                }
            }
            else
            {
                _isWallSliding = false;

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
