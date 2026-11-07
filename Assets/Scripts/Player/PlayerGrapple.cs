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
        [SerializeField] private float swingForce = 35f; // Pumping momentum along swing arc
        [SerializeField] private float swingGravityBoost = 1.6f; // Extra gravity at top of swing for swoop
        [SerializeField] private float reelSpeed = 12f; // Shorten/lengthen rope with W/S
        [SerializeField] private float releaseJumpBoost = 12f; // Slingshot boost on Space release
        [SerializeField] private float minRopeLength = 2.5f;
        [SerializeField] private LayerMask grappleLayer = ~0;

        [Header("Visual Cable")]
        [SerializeField] private LineRenderer cableRenderer;
        [SerializeField] private Transform cableOrigin;
        [SerializeField] private Color cableColor = new Color(0.1f, 1f, 0.9f); // Neon web cyan
        [SerializeField] private float cableWidth = 0.22f;

        private PlayerController _playerController;
        private Rigidbody _rb;
        private PlayerInputReader _input;
        private Collider[] _playerColliders;

        private bool _isSwinging;
        private Vector3 _grappleAnchor;
        private float _currentRopeLength;
        private float _lastMoveDirX = 1f;
        private Coroutine _missTracerRoutine;

        public bool IsSwinging => _isSwinging;
        public Vector3 GrappleAnchor => _grappleAnchor;
        public float CurrentRopeLength => _currentRopeLength;

        private void Awake()
        {
            _playerController = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody>();
            _input = GetComponent<PlayerInputReader>();
            _playerColliders = GetComponentsInChildren<Collider>(true);

            SetupCableRenderer();
        }

        private void SetupCableRenderer()
        {
            if (cableRenderer == null)
            {
                var lineObj = new GameObject("GrappleCable");
                lineObj.transform.SetParent(transform);
                cableRenderer = lineObj.AddComponent<LineRenderer>();
                cableRenderer.useWorldSpace = true;
                cableRenderer.startWidth = cableWidth;
                cableRenderer.endWidth = cableWidth * 0.7f;
                cableRenderer.positionCount = 2;

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit") 
                             ?? Shader.Find("Unlit/Color") 
                             ?? Shader.Find("Sprites/Default");
                Material mat = new Material(shader);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", cableColor);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", cableColor);
                mat.color = cableColor;
                cableRenderer.material = mat;
                cableRenderer.enabled = false;
            }
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

            if (_isSwinging)
            {
                StopSwing();
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

                if (grapplePressed && !_isSwinging)
                {
                    HandleGrapplePressed();
                }
                else if (grappleReleased && _isSwinging)
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
            if (_isSwinging)
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
            if (_isSwinging)
            {
                StopSwing();
            }
        }

        private void HandleJumpWhileSwinging()
        {
            if (!_isSwinging) return;

            // Spider-Man jump release: launches off the swing arc with extra lift and forward punch
            Vector3 vel = _rb.linearVelocity;
            Vector3 forwardBoost = vel.normalized * releaseJumpBoost;

            // Add strong upward boost if jumping during swing
            vel += forwardBoost * 0.7f + Vector3.up * (releaseJumpBoost * 0.8f);
            _rb.linearVelocity = vel;

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

            Vector3 rayOrigin = transform.position + Vector3.up * 1.0f;
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
                StartSwing(validHit.point);
                AudioManager.Instance?.PlayGrapple();
                Debug.Log($"[PlayerGrapple] Web attached to {validHit.collider.name} at {validHit.point}! Swinging!");
            }
            else
            {
                // Show miss tracer
                Vector3 missPoint = rayOrigin + rayDir * maxDistance;
                if (_missTracerRoutine != null) StopCoroutine(_missTracerRoutine);
                _missTracerRoutine = StartCoroutine(ShowMissTracer(rayOrigin, missPoint));
            }
        }

        private void StartSwing(Vector3 anchorPoint)
        {
            if (_missTracerRoutine != null) StopCoroutine(_missTracerRoutine);

            _isSwinging = true;
            _grappleAnchor = anchorPoint;

            Vector3 toPlayer = transform.position - anchorPoint;
            _currentRopeLength = Mathf.Max(minRopeLength, toPlayer.magnitude);

            // Give an initial swing velocity boost if stationary to get into the arc
            Vector3 vel = _rb.linearVelocity;
            if (vel.magnitude < 6f)
            {
                vel += new Vector3(_lastMoveDirX * 8f, -2f, 0f);
                _rb.linearVelocity = vel;
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
            if (cableRenderer != null)
            {
                cableRenderer.enabled = false;
            }
        }

        private void ApplyPendulumPhysics()
        {
            Vector3 playerPos = transform.position;
            Vector3 toPlayer = playerPos - _grappleAnchor;
            float currentDist = toPlayer.magnitude;

            if (currentDist < 0.01f) return;

            Vector3 rHat = toPlayer / currentDist; // Direction pointing outward from anchor to player
            Vector3 vel = _rb.linearVelocity;

            // 1. Extra swoop gravity while swinging for that deep pendulum swoop
            vel += Physics.gravity * ((swingGravityBoost - 1f) * Time.fixedDeltaTime);

            // 2. Rope constraint (acts as a taut cord)
            if (currentDist >= _currentRopeLength)
            {
                // Remove outward velocity pushing away from anchor (converts into pendulum arc!)
                float outwardSpeed = Vector3.Dot(vel, rHat);
                if (outwardSpeed > 0f)
                {
                    vel -= rHat * outwardSpeed;
                }

                // Elastic position correction: keep player along the circular arc
                Vector3 circlePos = _grappleAnchor + rHat * _currentRopeLength;
                _rb.position = Vector3.Lerp(_rb.position, circlePos, 25f * Time.fixedDeltaTime);
            }

            // 3. Tangential Swing Pumping (Spider-Man momentum acceleration)
            // Calculate tangent perpendicular to rope in the XY plane
            Vector3 tangent = new Vector3(-rHat.y, rHat.x, 0f).normalized;

            Vector2 input = _input != null ? _input.MoveInput : Vector2.zero;
            if (Mathf.Abs(input.x) > 0.05f)
            {
                // Direction player wants to swing
                float pushDir = Mathf.Sign(input.x);
                float alignment = Vector3.Dot(tangent, Vector3.right * pushDir);
                Vector3 pumpTangent = tangent * Mathf.Sign(alignment);

                // Accelerate tangentially along the arc
                vel += pumpTangent * (swingForce * Time.fixedDeltaTime);
            }

            // Always enforce Z plane lock in platformer mode
            bool isPlatformer = DimensionManager.Instance == null || DimensionManager.Instance.IsPlatformer;
            if (isPlatformer)
            {
                vel.z = 0f;
            }

            _rb.linearVelocity = vel;
        }

        private void UpdateCableVisual()
        {
            if (cableRenderer == null || !cableRenderer.enabled) return;

            Vector3 startPos = cableOrigin != null ? cableOrigin.position : transform.position + Vector3.up * 1.0f;
            cableRenderer.SetPosition(0, startPos);
            cableRenderer.SetPosition(1, _grappleAnchor);
        }

        private IEnumerator ShowMissTracer(Vector3 start, Vector3 end)
        {
            if (cableRenderer == null) yield break;

            cableRenderer.enabled = true;
            cableRenderer.SetPosition(0, start);
            cableRenderer.SetPosition(1, end);

            yield return new WaitForSecondsRealtime(0.12f);

            if (!_isSwinging)
            {
                cableRenderer.enabled = false;
            }
        }
    }
}
