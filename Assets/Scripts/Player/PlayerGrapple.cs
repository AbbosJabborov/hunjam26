using System.Collections;
using UnityEngine;
using Shift.Core;
using Shift.Cameras;
using UnityEngine.InputSystem;

namespace Shift.Player
{
    /// <summary>
    /// Spider-Man style pendulum swinging and grappling hook (E key).
    /// Creates a physics pendulum tether around the anchor point,
    /// allowing the player to swoop down, pump momentum along the arc,
    /// and launch into the air upon release or jump!
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerGrapple : MonoBehaviour
    {
        [Header("Swing & Grapple Settings")]
        [SerializeField] private float maxDistance = 40f;
        [SerializeField] private float swingForce = 45f; // Pumping momentum along swing arc
        [SerializeField] private float maxSwingSpeed = 22f; // Maximum swing velocity cap
        [SerializeField] private float swingGravityScale = 0.55f; // Counter-balance doubled project gravity
        [SerializeField] private float swingGravityBoost = 1.0f; // Kept for serialization compatibility
        [SerializeField] private float reelSpeed = 12f; // Shorten/lengthen rope with W/S (optional)
        [SerializeField] private float releaseJumpBoost = 12f; // Slingshot boost on Space release
        [SerializeField] private float minRopeLength = 2.0f;
        [SerializeField] private LayerMask grappleLayer = ~0;

        [Header("Visual Cable & Rope")]
        [SerializeField] private LineRenderer cableRenderer;
        [SerializeField] private Transform cableOrigin;
        [SerializeField] private Material ropeMaterial;
        [SerializeField] private float ropeWidth = 0.32f;
        [SerializeField] private float ropeTileRate = 0.75f; // Texture repeats per world unit
        [SerializeField] private Color cableColor = Color.white;
        [SerializeField] private float cableWidth = 0.22f;

        [Header("Hook Visual")]
        [SerializeField] private Sprite hookSprite;
        [SerializeField] private float hookScale = 0.12f;

        [Header("Projectile & Extension")]
        [SerializeField] private float grappleFlySpeed = 75f; // Speed of the grapple line traveling to target
        [SerializeField] private float minFlyDuration = 0.04f;
        [SerializeField] private float maxFlyDuration = 0.22f;
        [SerializeField] private int cableSegments = 16;
        [SerializeField] private float launchWaveAmplitude = 0.35f;

        [Header("Rope Physics & Ragdoll")]
        [SerializeField] private float slackSagMultiplier = 0.5f; // How much the rope sags down when loose
        [SerializeField] private float maxSag = 1.6f; // Maximum downward sag in meters
        [SerializeField] private float velocityBowMultiplier = 0.025f; // How much rope flexes behind movement
        [SerializeField] private float impactTwangStrength = 0.30f; // Vibration intensity when hook strikes surface

        private float _smoothedSag;
        private Vector3 _smoothedVelBow;
        private float _impactVibrationTimer;

        private Transform _hookRoot;
        private Transform _hookChild;
        private SpriteRenderer _hookRenderer;

        private PlayerController _playerController;
        private Rigidbody _rb;
        private PlayerInputReader _input;
        private Collider[] _playerColliders;
        private PlayerVisuals _playerVisuals;

        private bool _isSwinging;
        private bool _isExtending;
        private Coroutine _extendRoutine;
        private Vector3 _grappleAnchor;
        private float _currentRopeLength;
        private float _lastMoveDirX = 1f;
        private Coroutine _missTracerRoutine;

        public bool IsSwinging => _isSwinging;
        public bool IsExtending => _isExtending;
        public Vector3 GrappleAnchor => _grappleAnchor;
        public float CurrentRopeLength => _currentRopeLength;

        public Vector3 GetCableOrigin()
        {
            float facing = 1f;
            if (_playerVisuals != null)
            {
                facing = _playerVisuals.FacingDirection.x;
            }
            else
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null && sr.flipX) facing = -1f;
            }

            if (cableOrigin != null)
            {
                Vector3 local = cableOrigin.localPosition;
                // If swinging, raise cable origin to match the raised hand in Swing animation
                float yPos = _isSwinging ? Mathf.Max(local.y, 2.15f) : local.y;
                float xPos = _isSwinging ? Mathf.Abs(local.x) * 0.45f : Mathf.Abs(local.x);
                Vector3 orientedLocal = new Vector3(xPos * facing, yPos, 0f);
                return transform.TransformPoint(orientedLocal);
            }

            // Fallback default gun tip / hand position
            float fallbackY = _isSwinging ? 2.15f : 1.40f;
            float fallbackX = _isSwinging ? 0.20f : 0.45f;
            return transform.position + new Vector3(facing * fallbackX, fallbackY, 0f);
        }

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputReader>();
            _playerColliders = GetComponentsInChildren<Collider>(true);
            _playerVisuals = GetComponentInChildren<PlayerVisuals>();

            SetupCableRenderer();
            SetupHookVisual();
        }

        private void SetupCableRenderer()
        {
            if (cableRenderer == null)
            {
                var lineObj = new GameObject("GrappleCable");
                lineObj.transform.SetParent(transform);
                cableRenderer = lineObj.AddComponent<LineRenderer>();
            }

            cableRenderer.useWorldSpace = true;
            cableRenderer.textureMode = LineTextureMode.Tile;
            cableRenderer.alignment = LineAlignment.View;
            cableRenderer.startWidth = ropeWidth;
            cableRenderer.endWidth = ropeWidth;
            cableRenderer.positionCount = cableSegments;

            if (ropeMaterial == null)
            {
                #if UNITY_EDITOR
                ropeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_GrappleRope.mat");
                #endif
            }

            if (ropeMaterial != null)
            {
                cableRenderer.material = ropeMaterial;
                cableRenderer.material.mainTextureScale = new Vector2(ropeTileRate, 1f);
            }
            cableRenderer.enabled = false;
        }

        private void SetupHookVisual()
        {
            if (_hookRoot == null)
            {
                var rootObj = new GameObject("GrappleHookRoot");
                rootObj.transform.SetParent(null);
                _hookRoot = rootObj.transform;
            }

            if (_hookChild == null)
            {
                var childObj = new GameObject("HookSprite");
                childObj.transform.SetParent(_hookRoot);
                _hookChild = childObj.transform;
            }

            _hookRenderer = _hookChild.GetComponent<SpriteRenderer>();
            if (_hookRenderer == null)
            {
                _hookRenderer = _hookChild.gameObject.AddComponent<SpriteRenderer>();
            }

            if (hookSprite == null)
            {
                #if UNITY_EDITOR
                var sprites = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/hook_temporary.png");
                foreach (var a in sprites)
                {
                    if (a is Sprite s) { hookSprite = s; break; }
                }
                #endif
            }

            if (hookSprite != null)
            {
                _hookRenderer.sprite = hookSprite;
            }

            _hookRenderer.sortingOrder = 10;
            _hookChild.localScale = Vector3.one * hookScale;

            // Offset child so claw tip (97, 70) is centered at root origin (0, 0)
            _hookChild.localPosition = new Vector3(1.59f * hookScale, 1.86f * hookScale, 0f);

            _hookRoot.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
            }
        }

        private void OnDisable()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }

            if (_hookRoot != null)
            {
                Destroy(_hookRoot.gameObject);
            }

            if (_isSwinging)
            {
                StopSwing();
            }

            if (_hookRoot != null)
            {
                _hookRoot.gameObject.SetActive(false);
            }
        }

        private void Start()
        {
            if (_input == null) _input = _playerController.InputReader;

            if (_input != null)
            {
                _input.OnJumpPressed += HandleJumpWhileSwinging;
            }

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
            }
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.OnJumpPressed -= HandleJumpWhileSwinging;
            }

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }

            if (_hookRoot != null)
            {
                Destroy(_hookRoot.gameObject);
            }
        }

        private void HandleDimensionChanged(DimensionType dimension)
        {
            // Grapple hook cannot exist in 3D perspective mode
            if (dimension == DimensionType.FPS && _isSwinging)
            {
                StopSwing();
            }
        }

        private void Update()
        {
            if (_input != null && Mathf.Abs(_input.MoveInput.x) > 0.1f)
            {
                _lastMoveDirX = Mathf.Sign(_input.MoveInput.x);
            }

            bool isPlatformer = DimensionManager.Instance == null || DimensionManager.Instance.IsPlatformer;

            if (isPlatformer)
            {
                var mouse = Mouse.current;
                var kb = Keyboard.current;

                // Left Mouse Button is the primary grappling hook control; 'E' supported as secondary
                bool grapplePressed = (mouse != null && mouse.leftButton.wasPressedThisFrame)
                                   || (kb != null && kb.eKey.wasPressedThisFrame);
                bool grappleReleased = (mouse != null && mouse.leftButton.wasReleasedThisFrame)
                                    || (kb != null && kb.eKey.wasReleasedThisFrame);

                // Ignore mouse click if pointer is over UI elements
                if (grapplePressed && mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    if (UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                    {
                        grapplePressed = false;
                    }
                }

                if (grapplePressed && !_isSwinging && !_isExtending)
                {
                    HandleGrapplePressed();
                }
                else if (grappleReleased && (_isSwinging || _isExtending))
                {
                    HandleGrappleReleased();
                }

                // Reel in/out while swinging (W/S keys)
                if (_isSwinging && kb != null)
                {
                    if (kb.wKey.isPressed)
                    {
                        _currentRopeLength = Mathf.Max(minRopeLength, _currentRopeLength - reelSpeed * Time.unscaledDeltaTime);
                    }
                    else if (kb.sKey.isPressed)
                    {
                        _currentRopeLength = Mathf.Min(maxDistance, _currentRopeLength + reelSpeed * Time.unscaledDeltaTime);
                    }
                }
            }
            else
            {
                // In perspective mode, grappling hook cannot be used; immediately release if swinging
                if (_isSwinging)
                {
                    StopSwing();
                }
            }

            if (_isSwinging)
            {
                UpdateCableVisual();
            }
        }

        private void FixedUpdate()
        {
            if (_isSwinging)
            {
                ApplyPendulumPhysics();
            }
        }

        public void HandleGrapplePressed()
        {
            if (_isSwinging || _isExtending)
            {
                StopSwing();
                return;
            }

            // Grappling hook is strictly restricted to 2D platformer mode
            if (DimensionManager.Instance != null && !DimensionManager.Instance.IsPlatformer)
            {
                return;
            }

            TryLaunchSwing();
        }

        public void HandleGrappleReleased()
        {
            if (_isSwinging || _isExtending)
            {
                StopSwing();
            }
        }

        private void HandleJumpWhileSwinging()
        {
            if (_isExtending)
            {
                StopSwing();
                return;
            }

            if (!_isSwinging) return;

            Vector3 vel = _rb.linearVelocity;

            // Preserve horizontal swing momentum with a controlled cap (prevents rocketing across the level)
            float swingDirX = Mathf.Abs(vel.x) > 0.5f ? Mathf.Sign(vel.x) : _lastMoveDirX;
            float currentSpeedX = Mathf.Abs(vel.x);
            vel.x = Mathf.Clamp(currentSpeedX + 1.5f, 8f, 16f) * swingDirX;

            // Upward jump: provides a clean jump similar to regular jump (14),
            // strictly capped so it never skyrockets to triple jump height
            float baseJumpLift = Mathf.Clamp(releaseJumpBoost, 10f, 13f);
            if (vel.y > 0f)
            {
                // Riding upward arc: modest boost capped at 15.5f (regular ground jump is 14)
                vel.y = Mathf.Clamp(vel.y * 0.4f + baseJumpLift, 12f, 15.5f);
            }
            else
            {
                // Moving downward or level: cancel downward drop and provide clean jump lift
                vel.y = baseJumpLift;
            }

            vel.z = 0f;
            _rb.linearVelocity = vel;

            // Clear ground jump buffer so jump isn't double-consumed if near ground/walls
            if (_playerController != null)
            {
                var movement = _playerController.GetComponent<PlayerMovement>();
                movement?.ClearJumpBuffer();
            }

            AudioManager.Instance?.PlayJump();
            StopSwing();
        }

        private void TryLaunchSwing()
        {
            // Grappling hook is strictly disabled in 3D perspective mode
            if (DimensionManager.Instance != null && !DimensionManager.Instance.IsPlatformer)
            {
                return;
            }

            Vector3 rayOrigin = GetCableOrigin();
            Vector3 rayDir = Vector3.zero;

            // In 2D Platformer mode:
            // 1. Mouse cursor aiming (tracks custom crosshair)
            var mouse = Mouse.current;
            Camera platCam = CameraManager.Instance != null ? CameraManager.Instance.PlatformerCamera : Camera.main;

            if (mouse != null && platCam != null)
            {
                Vector2 mouseScreenPos = mouse.position.ReadValue();
                Ray mouseRay = platCam.ScreenPointToRay(mouseScreenPos);
                Plane zPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));

                if (zPlane.Raycast(mouseRay, out float enter))
                {
                    Vector3 worldPos = mouseRay.GetPoint(enter);
                    Vector3 dirToMouse = worldPos - rayOrigin;
                    dirToMouse.z = 0f;

                    // Shoot in mouse direction if cursor is not directly on top of the player's core
                    if (dirToMouse.sqrMagnitude > 0.25f)
                    {
                        rayDir = dirToMouse.normalized;
                    }
                }
            }

            // 2. Fallback: diagonal up-forward in movement direction (great for natural swinging if mouse unavailable)
            if (rayDir == Vector3.zero)
            {
                float aimX = _input != null && Mathf.Abs(_input.MoveInput.x) > 0.1f ? Mathf.Sign(_input.MoveInput.x) : _lastMoveDirX;
                rayDir = new Vector3(aimX * 0.8f, 0.75f, 0f).normalized;
            }

            // Immediately orient player sprite to face shot direction
            if (Mathf.Abs(rayDir.x) > 0.05f)
            {
                if (_playerVisuals != null)
                {
                    var sr = _playerVisuals.GetComponent<SpriteRenderer>();
                    if (sr != null) sr.flipX = rayDir.x < 0f;
                }
                rayOrigin = GetCableOrigin();
            }

            if (_playerColliders == null || _playerColliders.Length == 0)
            {
                _playerColliders = GetComponentsInChildren<Collider>(true);
            }

            RaycastHit[] hits = Physics.RaycastAll(rayOrigin, rayDir, maxDistance, grappleLayer, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit validHit = default;
            bool hitFound = false;

            foreach (var h in hits)
            {
                bool isPlayer = false;
                if (h.collider.gameObject == gameObject || h.collider.transform.IsChildOf(transform))
                {
                    isPlayer = true;
                }
                else
                {
                    for (int i = 0; i < _playerColliders.Length; i++)
                    {
                        if (_playerColliders[i] == h.collider)
                        {
                            isPlayer = true;
                            break;
                        }
                    }
                }

                if (!isPlayer)
                {
                    validHit = h;
                    hitFound = true;
                    break;
                }
            }

            if (hitFound)
            {
                if (_extendRoutine != null) StopCoroutine(_extendRoutine);
                _extendRoutine = StartCoroutine(ExtendGrappleRoutine(rayOrigin, validHit.point, validHit.normal, true));
            }
            else
            {
                Vector3 missPoint = rayOrigin + rayDir * maxDistance;
                if (_extendRoutine != null) StopCoroutine(_extendRoutine);
                _extendRoutine = StartCoroutine(ExtendGrappleRoutine(rayOrigin, missPoint, Vector3.zero, false));
            }
        }

        private void StartSwing(Vector3 anchorPoint)
        {
            if (_extendRoutine != null)
            {
                StopCoroutine(_extendRoutine);
                _extendRoutine = null;
            }
            if (_missTracerRoutine != null) StopCoroutine(_missTracerRoutine);

            _isExtending = false;
            _isSwinging = true;
            _grappleAnchor = anchorPoint;
            _impactVibrationTimer = 0.25f;
            _smoothedSag = 0f;

            Vector3 toPlayer = transform.position - anchorPoint;
            toPlayer.z = 0f;
            _currentRopeLength = Mathf.Max(minRopeLength, toPlayer.magnitude);

            Vector3 vel = _rb.linearVelocity;
            vel.z = 0f;

            // Direct arcade launch kick:
            // If player is moving slowly, give an immediate impulse in the direction they are facing or moving
            float aimX = _input != null && Mathf.Abs(_input.MoveInput.x) > 0.1f 
                         ? Mathf.Sign(_input.MoveInput.x) 
                         : _lastMoveDirX;

            if (Mathf.Abs(vel.x) < 6f)
            {
                vel.x = aimX * 10f;
            }

            // Damp excessive falling downward speed into horizontal swing entry
            if (vel.y < -6f)
            {
                vel.y = -2f;
            }

            _rb.linearVelocity = vel;

            if (_hookRoot != null)
            {
                _hookRoot.position = anchorPoint;
                _hookRoot.gameObject.SetActive(true);
            }

            if (cableRenderer != null)
            {
                cableRenderer.enabled = true;
                UpdateCableVisual();
            }
        }

        private void StopSwing()
        {
            _isSwinging = false;
            _isExtending = false;
            if (_extendRoutine != null)
            {
                StopCoroutine(_extendRoutine);
                _extendRoutine = null;
            }
            if (_missTracerRoutine != null)
            {
                StopCoroutine(_missTracerRoutine);
                _missTracerRoutine = null;
            }
            if (cableRenderer != null)
            {
                cableRenderer.enabled = false;
            }
            if (_hookRoot != null)
            {
                _hookRoot.gameObject.SetActive(false);
            }
        }

        private void ApplyPendulumPhysics()
        {
            Vector3 playerPos = transform.position;
            Vector3 toPlayer = playerPos - _grappleAnchor;
            toPlayer.z = 0f;
            float currentDist = toPlayer.magnitude;

            if (currentDist < 0.05f) return;

            Vector3 rHat = toPlayer / currentDist; // Direction pointing outward from anchor to player
            Vector3 vel = _rb.linearVelocity;
            vel.z = 0f;

            // 1. Counteract doubled scene gravity so swinging feels floaty and agile
            // Unity's physics engine automatically applies Physics.gravity every fixed step.
            // Net gravity desired = Physics.gravity * swingGravityScale.
            // Offset to add = Physics.gravity * (swingGravityScale - 1f) * fixedDeltaTime.
            Vector3 gravityOffset = Physics.gravity * ((swingGravityScale - 1f) * Time.fixedDeltaTime);
            vel += gravityOffset;

            // 2. Rope distance constraint (taut cord)
            if (currentDist >= _currentRopeLength)
            {
                // Remove outward velocity pushing away from anchor (converts into pendulum arc!)
                float outwardSpeed = Vector3.Dot(vel, rHat);
                if (outwardSpeed > 0f)
                {
                    vel -= rHat * outwardSpeed;
                }

                // Smooth positional correction: keep player along the circular arc
                Vector3 circlePos = _grappleAnchor + rHat * _currentRopeLength;
                circlePos.z = playerPos.z;
                _rb.position = Vector3.Lerp(_rb.position, circlePos, 35f * Time.fixedDeltaTime);
            }

            // 3. Responsive Arcade Tangent Pumping (A / D or Left / Right)
            Vector2 input = _input != null ? _input.MoveInput : Vector2.zero;
            if (Mathf.Abs(input.x) > 0.05f)
            {
                float pushDir = Mathf.Sign(input.x);

                // Calculate tangent perpendicular to rope in the XY plane
                Vector3 tangent = new Vector3(-rHat.y, rHat.x, 0f).normalized;

                // Direct tangent so it points in the direction of the input (right or left)
                if (Vector3.Dot(tangent, Vector3.right * pushDir) < 0f)
                {
                    tangent = -tangent;
                }

                // Instant, responsive arcade acceleration along the arc!
                vel += tangent * (swingForce * Mathf.Abs(input.x) * Time.fixedDeltaTime);
            }
            else
            {
                // Gentle air drag along horizontal axis when coasting
                vel.x = Mathf.MoveTowards(vel.x, 0f, 1.2f * Time.fixedDeltaTime);
            }

            // 4. Subtle swoop assist: when swinging upwards below anchor, give a slight lift assist
            if (vel.y > 0.1f && playerPos.y < _grappleAnchor.y)
            {
                vel.y += 2.5f * Time.fixedDeltaTime;
            }

            // 5. Cap maximum velocity for snappy, controllable feel
            if (vel.magnitude > maxSwingSpeed)
            {
                vel = vel.normalized * maxSwingSpeed;
            }

            // Always enforce Z plane lock in platformer mode
            vel.z = 0f;
            _rb.linearVelocity = vel;
        }

        private void UpdateCableVisual()
        {
            if (cableRenderer == null || !cableRenderer.enabled) return;

            Vector3 startPos = GetCableOrigin();
            startPos.z = transform.position.z;

            Vector3 worldRingPos = _grappleAnchor;

            if (_hookRoot != null && _hookRoot.gameObject.activeSelf)
            {
                _hookRoot.position = _grappleAnchor;

                Vector3 pullDir = (startPos - _grappleAnchor).normalized;
                if (pullDir.sqrMagnitude > 0.001f)
                {
                    float pullAngle = Mathf.Atan2(pullDir.y, pullDir.x) * Mathf.Rad2Deg;
                    float targetAngle = pullAngle - 69.5f;
                    _hookRoot.rotation = Quaternion.Euler(0f, 0f, targetAngle);
                }

                if (_hookChild != null)
                {
                    Vector3 ringOffsetLocal = new Vector3(-0.13f * hookScale, 2.05f * hookScale, 0f);
                    worldRingPos = _hookChild.TransformPoint(ringOffsetLocal);
                    worldRingPos.z = startPos.z;
                }
            }

            // Calculate dynamic rope ragdoll physics
            float dist = Vector3.Distance(startPos, worldRingPos);
            float slack = Mathf.Max(0f, _currentRopeLength - dist);

            // 1. Gravity catenary droop when loose/slack
            float targetSag = Mathf.Min(slack * slackSagMultiplier, maxSag);
            _smoothedSag = Mathf.Lerp(_smoothedSag, targetSag, 12f * Time.deltaTime);
            Vector3 sagVector = Vector3.down * _smoothedSag;

            // 2. Inertial trailing flex against player swing velocity
            Vector3 vel = _rb != null ? _rb.linearVelocity : Vector3.zero;
            Vector3 targetVelBow = -vel * velocityBowMultiplier;
            _smoothedVelBow = Vector3.Lerp(_smoothedVelBow, targetVelBow, 15f * Time.deltaTime);

            // 3. Post-impact latch vibration ripple that dampens out
            float impactWave = 0f;
            if (_impactVibrationTimer > 0f)
            {
                _impactVibrationTimer -= Time.deltaTime;
                impactWave = Mathf.Sin(Time.time * 35f) * (_impactVibrationTimer / 0.25f) * impactTwangStrength;
            }

            Vector3 physicalOffset = sagVector + _smoothedVelBow;
            RenderCable(startPos, worldRingPos, physicalOffset, impactWave, 4f);
        }

        private void RenderCable(Vector3 start, Vector3 end, Vector3 physicalMidOffset, float waveMagnitude, float waveFrequency)
        {
            if (cableRenderer == null || !cableRenderer.enabled) return;

            int count = cableRenderer.positionCount;
            if (count < 2) count = 2;

            Vector3 lineVec = end - start;
            float lineLen = lineVec.magnitude;
            Vector3 lineDir = lineLen > 0.001f ? lineVec / lineLen : Vector3.right;
            Vector3 perpDir = new Vector3(-lineDir.y, lineDir.x, 0f);

            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                Vector3 pt = Vector3.Lerp(start, end, t);

                // Smooth parabolic envelope (0 at endpoints, 1 at midpoint)
                float envelope = Mathf.Sin(t * Mathf.PI);

                // Physical ragdoll offset: catenary slack sag + aerodynamic trailing bow
                pt += physicalMidOffset * envelope;

                // High-frequency vibration / shockwave ripple
                if (waveMagnitude > 0.001f)
                {
                    float sin = Mathf.Sin(t * Mathf.PI * waveFrequency);
                    pt += perpDir * (sin * envelope * waveMagnitude);
                }

                cableRenderer.SetPosition(i, pt);
            }

            if (cableRenderer.material != null)
            {
                cableRenderer.material.mainTextureScale = new Vector2(ropeTileRate, 1f);
            }
        }

        private IEnumerator ExtendGrappleRoutine(Vector3 startOrigin, Vector3 targetAnchor, Vector3 hitNormal, bool isHit)
        {
            _isExtending = true;
            _isSwinging = false;

            float dist = Vector3.Distance(startOrigin, targetAnchor);
            float duration = Mathf.Clamp(dist / grappleFlySpeed, minFlyDuration, maxFlyDuration);
            float elapsed = 0f;

            Vector3 flyDir = (targetAnchor - startOrigin).normalized;
            float flyAngle = Mathf.Atan2(flyDir.y, flyDir.x) * Mathf.Rad2Deg;
            float flightHookAngle = flyAngle + 110.5f; // Claws leading forward in flight

            if (cableRenderer != null)
            {
                cableRenderer.enabled = true;
            }
            if (_hookRoot != null)
            {
                _hookRoot.gameObject.SetActive(true);
            }

            AudioManager.Instance?.PlayGrapple();

            while (elapsed < duration)
            {
                if (!_isExtending) yield break;

                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);

                Vector3 handPos = GetCableOrigin();
                handPos.z = transform.position.z;

                Vector3 currentTip = Vector3.Lerp(handPos, targetAnchor, progress);
                currentTip.z = handPos.z;

                if (_hookRoot != null)
                {
                    _hookRoot.position = currentTip;
                    _hookRoot.rotation = Quaternion.Euler(0f, 0f, flightHookAngle);
                }

                Vector3 worldRingPos = currentTip;
                if (_hookChild != null)
                {
                    Vector3 ringOffsetLocal = new Vector3(-0.13f * hookScale, 2.05f * hookScale, 0f);
                    worldRingPos = _hookChild.TransformPoint(ringOffsetLocal);
                    worldRingPos.z = handPos.z;
                }

                float waveAmp = (1f - progress) * launchWaveAmplitude;
                RenderCable(handPos, worldRingPos, Vector3.zero, waveAmp, 2.5f);

                yield return null;
            }

            if (!_isExtending) yield break;

            _isExtending = false;

            if (isHit)
            {
                StartSwing(targetAnchor);
            }
            else
            {
                yield return new WaitForSeconds(0.06f);
                if (!_isSwinging)
                {
                    StopSwing();
                }
            }
        }
    }
}
