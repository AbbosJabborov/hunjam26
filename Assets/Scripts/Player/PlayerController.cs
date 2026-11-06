using UnityEngine;
using Shift.Core;
using Shift.Cameras;

namespace Shift.Player
{
    /// <summary>
    /// Central player component coordinating physics, input, and dimension state.
    /// Operates on a single persistent Rigidbody across both Platformer and FPS dimensions.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(CapsuleCollider))]
    public class PlayerController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader inputReader;
        [SerializeField] private Transform headPivot;
        [SerializeField] private GameObject visualRoot;

        [Header("Dimension 2D Plane")]
        [SerializeField] private float platformerZPosition = -1.92f;

        private Rigidbody _rb;
        private CapsuleCollider _collider;

        public Rigidbody Rigidbody => _rb != null ? _rb : (_rb = GetComponent<Rigidbody>());
        public CapsuleCollider Collider => _collider != null ? _collider : (_collider = GetComponent<CapsuleCollider>());
        public Transform HeadPivot => headPivot;
        public PlayerInputReader InputReader => inputReader != null ? inputReader : (inputReader = GetComponent<PlayerInputReader>());

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _collider = GetComponent<CapsuleCollider>();

            if (inputReader == null)
            {
                inputReader = GetComponent<PlayerInputReader>();
                if (inputReader == null)
                {
                    inputReader = gameObject.AddComponent<PlayerInputReader>();
                }
            }

            ConfigurePhysicsBody();
        }

        private void Start()
        {
            if (headPivot == null)
            {
                // Create a default head pivot if none assigned
                var head = new GameObject("HeadPivot");
                head.transform.SetParent(transform);
                head.transform.localPosition = new Vector3(0f, 0.75f, 0f);
                headPivot = head.transform;
            }

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetTargets(transform, headPivot);
            }

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                ApplyDimensionConstraints(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                ApplyDimensionConstraints(DimensionType.Platformer);
            }

            if (inputReader != null)
            {
                inputReader.OnSwitchDimensionPressed += HandleSwitchDimensionPressed;
            }
        }

        private void OnDestroy()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }

            if (inputReader != null)
            {
                inputReader.OnSwitchDimensionPressed -= HandleSwitchDimensionPressed;
            }
        }

        private void ConfigurePhysicsBody()
        {
            _rb.mass = 1.0f;
            _rb.useGravity = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            // Configure CapsuleCollider defaults
            _collider.height = 2.0f;
            _collider.radius = 0.4f;
            _collider.center = new Vector3(0f, 1.0f, 0f);
        }

        private void Update()
        {
            // Pass mouse look to camera manager when in FPS mode
            if (DimensionManager.Instance != null && DimensionManager.Instance.IsFPS)
            {
                if (inputReader != null && CameraManager.Instance != null)
                {
                    CameraManager.Instance.ApplyMouseLook(inputReader.LookInput);
                }
            }

            // Smooth body visuals: keep visible during camera swoop until camera enters the head
            if (visualRoot != null)
            {
                if (CameraManager.Instance != null && CameraManager.Instance.IsTransitioning)
                {
                    visualRoot.SetActive(CameraManager.Instance.TransitionProgress < 0.85f);
                }
                else
                {
                    visualRoot.SetActive(DimensionManager.Instance == null || !DimensionManager.Instance.IsFPS);
                }
            }
        }

        private void HandleSwitchDimensionPressed()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.SwitchDimension();
            }
        }

        private void HandleDimensionChanged(DimensionType newDimension)
        {
            ApplyDimensionConstraints(newDimension);
        }

        private void ApplyDimensionConstraints(DimensionType dimension)
        {
            if (dimension == DimensionType.Platformer)
            {
                // Lock Z position and freeze all rotations in platformer
                _rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionZ;

                // Snap Z position to 2D level plane
                Vector3 currentPos = transform.position;
                currentPos.z = platformerZPosition;
                transform.position = currentPos;

                // Reset rotation facing right or forward
                transform.rotation = Quaternion.identity;

                // Zero out any residual Z velocity
                Vector3 vel = _rb.linearVelocity;
                vel.z = 0f;
                _rb.linearVelocity = vel;
            }
            else // FPS
            {
                // In FPS mode, freeze X/Z rotations (pitch/roll), allow horizontal Y rotation and all position axes
                _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }
    }
}

