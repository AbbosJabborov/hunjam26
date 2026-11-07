using UnityEngine;
using Shift.Core;
using Shift.Player;

namespace Shift.Cameras
{
    /// <summary>
    /// Coordinates the dual-camera system with smooth cinematic transitions:
    /// - Orthographic side-scroller camera for Platformer mode.
    /// - Perspective first-person camera with mouse-look for FPS mode.
    /// - Smooth camera swoop (zoom in/out, arc, rotation, FOV interpolation) when switching dimensions.
    /// Ensures seamless switching, audio listener management, and cursor lock.
    /// </summary>
    public class CameraManager : MonoBehaviour
    {
        public static CameraManager Instance { get; private set; }

        [Header("Cameras")]
        [SerializeField] private Camera platformerCamera;
        [SerializeField] private Camera fpsCamera;

        [Header("Targets")]
        [SerializeField] private Transform playerTransform;
        [SerializeField] private Transform fpsCameraPivot; // Player head height

        [Header("Platformer Camera Settings")]
        [SerializeField] private Vector3 platformerOffset = new Vector3(0f, 1.5f, -15f);
        [SerializeField] private float platformerFollowSmoothTime = 0.12f;
        [SerializeField] private float orthographicSize = 7.5f;
        [SerializeField] private float horizontalLookAhead = 2.5f;

        [Header("Platformer Vertical Follow")]
        [Tooltip("When true, camera Y remains locked to ground elevation during jumps to prevent bobbing and exposed void.")]
        [SerializeField] private bool platformerLockVerticalOnJump = true;
        [Tooltip("Vertical deadzone above/below grounded height before camera follows airborne player.")]
        [SerializeField] private float platformerVerticalDeadzone = 4.0f;
        [Tooltip("Smooth time for vertical elevation changes between ledges.")]
        [SerializeField] private float platformerVerticalSmoothTime = 0.35f;

        [Header("FPS Camera Settings")]
        [SerializeField] private float defaultFpsFov = 80f;
        [SerializeField] private float mouseSensitivity = 2.0f;
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        [Header("Smooth Transition Settings")]
        [Tooltip("Real-time duration of camera swoop between dimensions (unscaled time)")]
        [SerializeField] private float transitionDuration = 0.32f;
        [Tooltip("Subtle vertical arc height during swoop in meters")]
        [SerializeField] private float swoopArcHeight = 0.25f;
        [SerializeField] private AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private AudioListener _platformerAudioListener;
        private AudioListener _fpsAudioListener;
        private Vector3 _platformerFollowPosition;
        private Vector3 _platformerVelocity;
        private Rigidbody _playerRb;
        private PlayerMovement _playerMovement;
        private float _platformerGroundedY;
        private float _platformerTargetY;
        private float _platformerVerticalVelocity;
        private float _currentLookAheadX;
        private float _fpsPitch;
        private float _fpsYaw = 90f; // Default facing +X (down the track towards goal)

        // 0.0f = Pure Platformer, 1.0f = Pure FPS
        private float _transitionProgress = 0f;
        private float _targetProgress = 0f;

        public bool IsTransitioning => Mathf.Abs(_transitionProgress - _targetProgress) > 0.0001f;
        public float TransitionProgress => _transitionProgress;

        public Camera ActiveCamera => (_transitionProgress > 0f || (DimensionManager.Instance != null && DimensionManager.Instance.IsFPS))
            ? fpsCamera
            : platformerCamera;

        public Camera FPSCamera => fpsCamera;
        public Camera PlatformerCamera => platformerCamera;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializeCameras();
        }

        private void Start()
        {
            if (playerTransform != null)
            {
                _playerMovement = playerTransform.GetComponent<PlayerMovement>();
                _platformerGroundedY = playerTransform.position.y;
                _platformerTargetY = _platformerGroundedY;
                _platformerFollowPosition = GetInstantPlatformerPosition();
                if (platformerCamera != null)
                {
                    platformerCamera.transform.position = _platformerFollowPosition;
                    platformerCamera.transform.rotation = Quaternion.identity;
                }
            }

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateCameraStates(DimensionManager.Instance.CurrentDimension, instant: true);
            }
            else
            {
                UpdateCameraStates(DimensionType.Platformer, instant: true);
            }
        }

        private void OnDestroy()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void InitializeCameras()
        {
            if (platformerCamera != null)
            {
                platformerCamera.orthographic = true;
                platformerCamera.orthographicSize = orthographicSize;
                _platformerAudioListener = platformerCamera.GetComponent<AudioListener>();
            }

            if (fpsCamera != null)
            {
                fpsCamera.orthographic = false;
                fpsCamera.fieldOfView = defaultFpsFov;
                fpsCamera.nearClipPlane = 0.05f;
                _fpsAudioListener = fpsCamera.GetComponent<AudioListener>();
            }

            if (playerTransform != null)
            {
                _playerRb = playerTransform.GetComponent<Rigidbody>();
            }
        }

        private void HandleDimensionChanged(DimensionType newDimension)
        {
            UpdateCameraStates(newDimension, instant: false);
        }

        public void UpdateCameraStates(DimensionType dimension, bool instant = false)
        {
            bool isFps = dimension == DimensionType.FPS;
            _targetProgress = isFps ? 1f : 0f;

            if (instant)
            {
                _transitionProgress = _targetProgress;
            }

            if (isFps)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                // Smart initial FPS yaw: face +X (90 deg) towards goal, or -X (270 deg) if moving backwards
                if (_playerRb != null && _playerRb.linearVelocity.x < -0.5f)
                {
                    _fpsYaw = 270f;
                }
                else
                {
                    _fpsYaw = 90f;
                }
                _fpsPitch = 0f;

                if (playerTransform != null)
                {
                    playerTransform.rotation = Quaternion.Euler(0f, _fpsYaw, 0f);
                }

                // Activate FPS camera to perform the zoom-in swoop
                if (fpsCamera != null)
                {
                    fpsCamera.enabled = true;
                    if (_fpsAudioListener != null) _fpsAudioListener.enabled = true;

                    // If starting transition from platformer side-view, align FPS camera with platformer camera
                    if (!instant && _transitionProgress <= 0.01f)
                    {
                        fpsCamera.transform.position = _platformerFollowPosition;
                        fpsCamera.transform.rotation = Quaternion.identity;
                        fpsCamera.fieldOfView = CalculatePlatformerMatchingFov();
                    }
                }

                // Disable platformer camera rendering during swoop
                if (platformerCamera != null)
                {
                    platformerCamera.enabled = false;
                    if (_platformerAudioListener != null) _platformerAudioListener.enabled = false;
                }
            }
            else
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                // Keep FPS camera active to perform the zoom-out and rotate swoop back to ortho
                if (fpsCamera != null)
                {
                    fpsCamera.enabled = true;
                    if (_fpsAudioListener != null) _fpsAudioListener.enabled = true;
                }

                if (platformerCamera != null)
                {
                    platformerCamera.enabled = false;
                    if (_platformerAudioListener != null) _platformerAudioListener.enabled = false;
                }
            }

            if (instant)
            {
                ApplyInstantState(isFps);
            }
        }

        private void ApplyInstantState(bool isFps)
        {
            if (isFps)
            {
                UpdateFPSCamera();
                if (platformerCamera != null) platformerCamera.enabled = false;
                if (fpsCamera != null)
                {
                    fpsCamera.enabled = true;
                    fpsCamera.fieldOfView = defaultFpsFov;
                }
                if (_platformerAudioListener != null) _platformerAudioListener.enabled = false;
                if (_fpsAudioListener != null) _fpsAudioListener.enabled = true;
            }
            else
            {
                _platformerFollowPosition = GetInstantPlatformerPosition();
                if (platformerCamera != null)
                {
                    platformerCamera.transform.position = _platformerFollowPosition;
                    platformerCamera.transform.rotation = Quaternion.identity;
                    platformerCamera.enabled = true;
                }
                if (fpsCamera != null) fpsCamera.enabled = false;
                if (_platformerAudioListener != null) _platformerAudioListener.enabled = true;
                if (_fpsAudioListener != null) _fpsAudioListener.enabled = false;
            }
        }

        /// <summary>
        /// Calculates the perspective FOV (in degrees) that matches the orthographic view
        /// vertical height at the player's plane distance.
        /// </summary>
        public float CalculatePlatformerMatchingFov()
        {
            float dist = Mathf.Abs(platformerOffset.z);
            if (dist < 0.01f) dist = 15f;
            float halfAngleRad = Mathf.Atan(orthographicSize / dist);
            return 2f * halfAngleRad * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Applies an upward recoil pitch kick when weapon is fired.
        /// </summary>
        public void ApplyRecoil(float pitchKick = 1.2f)
        {
            _fpsPitch = Mathf.Clamp(_fpsPitch - pitchKick, minPitch, maxPitch);
        }

        /// <summary>
        /// Applies mouse delta rotation for FPS camera look.
        /// Uses unscaled delta time so aiming remains crisp during slow-motion.
        /// </summary>
        public void ApplyMouseLook(Vector2 lookDelta)
        {
            if (DimensionManager.Instance == null || !DimensionManager.Instance.IsFPS) return;

            float mouseX = lookDelta.x * mouseSensitivity * 0.1f;
            float mouseY = lookDelta.y * mouseSensitivity * 0.1f;

            _fpsYaw += mouseX;
            _fpsPitch -= mouseY;
            _fpsPitch = Mathf.Clamp(_fpsPitch, minPitch, maxPitch);

            // Rotate player body horizontally to match yaw
            if (playerTransform != null)
            {
                playerTransform.rotation = Quaternion.Euler(0f, _fpsYaw, 0f);
            }
        }

        private void LateUpdate()
        {
            if (playerTransform == null) return;

            // Continuously track platformer follow position so it stays smooth
            UpdatePlatformerTracking();

            bool transitioning = Mathf.Abs(_transitionProgress - _targetProgress) > 0.0001f;
            if (transitioning)
            {
                // Advance transition progress using unscaled delta time for responsiveness
                float step = (1f / Mathf.Max(transitionDuration, 0.01f)) * Time.unscaledDeltaTime;
                _transitionProgress = Mathf.MoveTowards(_transitionProgress, _targetProgress, step);

                float curveT = transitionCurve != null
                    ? transitionCurve.Evaluate(_transitionProgress)
                    : Mathf.SmoothStep(0f, 1f, _transitionProgress);

                // Positions: from side-view platformer position to player head pivot
                Vector3 platPos = _platformerFollowPosition;
                Vector3 fpsPos = fpsCameraPivot != null ? fpsCameraPivot.position : (playerTransform.position + Vector3.up * 1.7f);
                Vector3 blendedPos = Vector3.Lerp(platPos, fpsPos, curveT);

                // Add slight upward swoop arc
                if (swoopArcHeight > 0.001f)
                {
                    blendedPos.y += Mathf.Sin(curveT * Mathf.PI) * swoopArcHeight;
                }

                // Rotations: from Quaternion.identity (looking +Z) to FPS pitch/yaw
                Quaternion platRot = Quaternion.identity;
                Quaternion fpsRot = Quaternion.Euler(_fpsPitch, _fpsYaw, 0f);
                Quaternion blendedRot = Quaternion.Slerp(platRot, fpsRot, curveT);

                // Field of view: from matching ortho FOV (~53.13 deg) to FPS FOV (80 deg)
                float platFov = CalculatePlatformerMatchingFov();
                float blendedFov = Mathf.Lerp(platFov, defaultFpsFov, curveT);

                if (fpsCamera != null)
                {
                    fpsCamera.transform.position = blendedPos;
                    fpsCamera.transform.rotation = blendedRot;
                    fpsCamera.fieldOfView = blendedFov;
                }

                // If transition back to Platformer just completed (reached 0)
                if (_transitionProgress <= 0.0001f)
                {
                    if (platformerCamera != null)
                    {
                        platformerCamera.transform.position = _platformerFollowPosition;
                        platformerCamera.transform.rotation = Quaternion.identity;
                        platformerCamera.enabled = true;
                        if (_platformerAudioListener != null) _platformerAudioListener.enabled = true;
                    }

                    if (fpsCamera != null)
                    {
                        fpsCamera.enabled = false;
                        if (_fpsAudioListener != null) _fpsAudioListener.enabled = false;
                    }
                }
            }
            else
            {
                if (_targetProgress >= 0.999f)
                {
                    // Fully in FPS mode
                    UpdateFPSCamera();
                }
                else
                {
                    // Fully in Platformer mode
                    if (platformerCamera != null)
                    {
                        platformerCamera.transform.position = _platformerFollowPosition;
                        platformerCamera.transform.rotation = Quaternion.identity;
                        if (!platformerCamera.enabled) platformerCamera.enabled = true;
                        if (_platformerAudioListener != null && !_platformerAudioListener.enabled)
                            _platformerAudioListener.enabled = true;
                    }

                    if (fpsCamera != null && fpsCamera.enabled)
                    {
                        fpsCamera.enabled = false;
                        if (_fpsAudioListener != null) _fpsAudioListener.enabled = false;
                    }
                }
            }

            // Synchronize Parallax in the exact same frame for 0-latency jitter-free scrolling
            if (Shift.Environment.ParallaxBackground.Instance != null)
            {
                Camera activeCam = ActiveCamera;
                if (activeCam != null)
                {
                    Shift.Environment.ParallaxBackground.Instance.UpdateParallax(activeCam.transform.position, Time.time);
                }
            }
        }

        private Vector3 GetInstantPlatformerPosition()
        {
            if (playerTransform == null) return platformerOffset;

            float lookAheadX = 0f;
            if (_playerRb != null)
            {
                lookAheadX = Mathf.Clamp(_playerRb.linearVelocity.x * 0.2f, -horizontalLookAhead, horizontalLookAhead);
            }

            float yPos = platformerLockVerticalOnJump ? _platformerGroundedY : playerTransform.position.y;

            return new Vector3(
                playerTransform.position.x + platformerOffset.x + lookAheadX,
                yPos + platformerOffset.y,
                playerTransform.position.z + platformerOffset.z
            );
        }

        private void UpdatePlatformerTracking()
        {
            if (playerTransform == null) return;

            if (_playerRb == null) _playerRb = playerTransform.GetComponent<Rigidbody>();
            if (_playerMovement == null) _playerMovement = playerTransform.GetComponent<PlayerMovement>();

            // 1. Horizontal Tracking with smoothed look-ahead to eliminate physics velocity jitter
            float targetLookAhead = 0f;
            if (_playerRb != null)
            {
                targetLookAhead = Mathf.Clamp(_playerRb.linearVelocity.x * 0.2f, -horizontalLookAhead, horizontalLookAhead);
            }
            _currentLookAheadX = Mathf.MoveTowards(_currentLookAheadX, targetLookAhead, Time.unscaledDeltaTime * 12f);

            float targetX = playerTransform.position.x + platformerOffset.x + _currentLookAheadX;

            // 2. Vertical Tracking: Lock to ground level when jumping so houses and camera don't bounce
            float playerY = playerTransform.position.y;
            bool isGroundedOrWall = _playerMovement == null || _playerMovement.IsGrounded || _playerMovement.IsTouchingWall;

            if (isGroundedOrWall)
            {
                _platformerGroundedY = playerY;
                _platformerTargetY = _platformerGroundedY;
            }
            else if (!platformerLockVerticalOnJump)
            {
                _platformerTargetY = playerY;
            }
            else
            {
                // Airborne (jumping/falling): Only adjust targetY if player exceeds vertical deadzone
                float deltaY = playerY - _platformerGroundedY;
                if (deltaY > platformerVerticalDeadzone)
                {
                    _platformerTargetY = playerY - platformerVerticalDeadzone;
                }
                else if (deltaY < -platformerVerticalDeadzone)
                {
                    _platformerTargetY = playerY + platformerVerticalDeadzone;
                }
            }

            // Smooth X and Y independently: fast responsive X, calm grounded Y
            float smoothedX = Mathf.SmoothDamp(
                _platformerFollowPosition.x,
                targetX,
                ref _platformerVelocity.x,
                platformerFollowSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            float smoothedY = Mathf.SmoothDamp(
                _platformerFollowPosition.y,
                _platformerTargetY + platformerOffset.y,
                ref _platformerVerticalVelocity,
                platformerVerticalSmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            _platformerFollowPosition = new Vector3(
                smoothedX,
                smoothedY,
                playerTransform.position.z + platformerOffset.z
            );
        }

        private void UpdateFPSCamera()
        {
            if (fpsCamera == null || fpsCameraPivot == null) return;

            fpsCamera.transform.position = fpsCameraPivot.position;
            fpsCamera.transform.rotation = Quaternion.Euler(_fpsPitch, _fpsYaw, 0f);
            fpsCamera.fieldOfView = defaultFpsFov;
        }

        public void SetTargets(Transform player, Transform headPivot)
        {
            playerTransform = player;
            fpsCameraPivot = headPivot;
            if (playerTransform != null)
            {
                _playerRb = playerTransform.GetComponent<Rigidbody>();
                _playerMovement = playerTransform.GetComponent<PlayerMovement>();
                _platformerGroundedY = playerTransform.position.y;
                _platformerTargetY = _platformerGroundedY;
                _platformerFollowPosition = GetInstantPlatformerPosition();
            }
        }
    }
}
