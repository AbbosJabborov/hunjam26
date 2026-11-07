using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Shift.Core;
using Shift.Cameras;
using Shift.Combat;
using Shift.Player;

namespace Shift.UI
{
    /// <summary>
    /// Displays and animates the hand-drawn paper weapon in the screen-space HUD during FPS mode:
    /// - Operates via Unity UI Image independent of 3D world geometry.
    /// - Plays a 4-frame firing animation cycle with muzzle flash, recoil kick, and recovery.
    /// - Adds subtle weapon sway and walking bobbing for tactile game feel.
    /// - Smoothly fades into view as the camera swoops into first-person mode.
    /// </summary>
    public class FPSWeaponHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Image weaponImage;
        [SerializeField] private RectTransform weaponRect;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Hand-Drawn Weapon Sprites")]
        [SerializeField] private Sprite readySprite;
        [SerializeField] private Sprite firingSprite;

        [Header("Weapon Sway & Bob Settings")]
        [SerializeField] private float swayAmount = 15f;
        [SerializeField] private float maxSway = 40f;
        [SerializeField] private float swaySmoothTime = 0.08f;
        [SerializeField] private float bobFrequency = 8f;
        [SerializeField] private float bobAmountX = 8f;
        [SerializeField] private float bobAmountY = 12f;

        [Header("Recoil Kick Offsets")]
        [SerializeField] private Vector2 recoilKickOffset = new Vector2(8f, 38f);
        [SerializeField] private float recoilKickAngle = -3.5f;

        private PlayerCombat _playerCombat;
        private PlayerInputReader _inputReader;
        private Rigidbody _playerRb;
        private Vector2 _restPosition;
        private Vector2 _currentSwayVelocity;
        private Vector2 _currentSwayOffset;
        private float _bobTimer;
        private Coroutine _fireCycleRoutine;

        private void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (weaponRect == null && weaponImage != null) weaponRect = weaponImage.rectTransform;
            if (weaponRect != null) _restPosition = weaponRect.anchoredPosition;
        }

        private void Start()
        {
            FindPlayerReferences();

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateHUDState(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                UpdateHUDState(DimensionType.Platformer);
            }

            if (readySprite != null && weaponImage != null)
            {
                weaponImage.sprite = readySprite;
            }
        }

        private void OnDestroy()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnWeaponFired -= HandleWeaponFired;
            }
        }

        private void FindPlayerReferences()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                _playerRb = player.GetComponent<Rigidbody>();
                _inputReader = player.InputReader;
                _playerCombat = player.GetComponent<PlayerCombat>();
                if (_playerCombat != null)
                {
                    _playerCombat.OnWeaponFired += HandleWeaponFired;
                }
            }
        }

        private void HandleDimensionChanged(DimensionType dimension)
        {
            UpdateHUDState(dimension);
        }

        private void UpdateHUDState(DimensionType dimension)
        {
            if (canvasGroup == null) return;

            bool isFps = dimension == DimensionType.FPS;
            canvasGroup.alpha = isFps ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;

            if (isFps && readySprite != null && weaponImage != null)
            {
                weaponImage.sprite = readySprite;
            }
        }

        private void Update()
        {
            if (canvasGroup == null || weaponRect == null) return;

            // Handle smooth fade-in during camera swoop
            if (CameraManager.Instance != null && CameraManager.Instance.IsTransitioning)
            {
                float t = CameraManager.Instance.TransitionProgress;
                // Fade in during the final portion of the zoom-in (t >= 0.7)
                canvasGroup.alpha = Mathf.Clamp01((t - 0.7f) / 0.3f);
            }
            else
            {
                bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;
                canvasGroup.alpha = isFps ? 1f : 0f;
            }

            if (canvasGroup.alpha > 0.01f)
            {
                UpdateSwayAndBob();
            }
        }

        private void UpdateSwayAndBob()
        {
            if (_inputReader == null || _playerRb == null)
            {
                FindPlayerReferences();
                if (_inputReader == null) return;
            }

            // Mouse look sway (lagging behind mouse motion)
            Vector2 look = _inputReader.LookInput;
            float targetSwayX = Mathf.Clamp(-look.x * swayAmount * 0.1f, -maxSway, maxSway);
            float targetSwayY = Mathf.Clamp(-look.y * swayAmount * 0.1f, -maxSway, maxSway);

            _currentSwayOffset = Vector2.SmoothDamp(
                _currentSwayOffset,
                new Vector2(targetSwayX, targetSwayY),
                ref _currentSwayVelocity,
                swaySmoothTime,
                Mathf.Infinity,
                Time.unscaledDeltaTime
            );

            // Walking bobbing
            Vector3 horizVel = _playerRb != null ? new Vector3(_playerRb.linearVelocity.x, 0f, _playerRb.linearVelocity.z) : Vector3.zero;
            float moveSpeed = horizVel.magnitude;

            Vector2 bobOffset = Vector2.zero;
            if (moveSpeed > 0.5f)
            {
                _bobTimer += Time.unscaledDeltaTime * bobFrequency * Mathf.Clamp01(moveSpeed / 4f);
                bobOffset.x = Mathf.Sin(_bobTimer * 0.5f) * bobAmountX;
                bobOffset.y = Mathf.Abs(Mathf.Sin(_bobTimer)) * bobAmountY;
            }
            else
            {
                _bobTimer = 0f;
            }

            // Apply sway + bob to weapon rect if not overriding with firing kick
            if (_fireCycleRoutine == null)
            {
                weaponRect.anchoredPosition = _restPosition + _currentSwayOffset + bobOffset;
                weaponRect.localEulerAngles = new Vector3(0f, 0f, _currentSwayOffset.x * 0.15f);
            }
        }

        private void HandleWeaponFired()
        {
            if (_fireCycleRoutine != null) StopCoroutine(_fireCycleRoutine);
            _fireCycleRoutine = StartCoroutine(FireCycleRoutine());
        }

        /// <summary>
        /// Executes the 4-frame hand-drawn paper firing animation cycle:
        /// 1. Muzzle blast frame + upward recoil kick.
        /// 2. Recoil apex frame (weapon kicked up & rotated).
        /// 3. Recovery frame (settling back down).
        /// 4. Rest frame (ready idle).
        /// Uses unscaled delta time so weapon firing feels snappy in slow-motion.
        /// </summary>
        private IEnumerator FireCycleRoutine()
        {
            if (weaponImage == null || weaponRect == null) yield break;

            // --- FRAME 1: Muzzle Flash & Initial Recoil Snap ---
            if (firingSprite != null) weaponImage.sprite = firingSprite;
            weaponRect.anchoredPosition = _restPosition + _currentSwayOffset + recoilKickOffset;
            weaponRect.localEulerAngles = new Vector3(0f, 0f, recoilKickAngle);
            yield return new WaitForSecondsRealtime(0.05f);

            // --- FRAME 2: Recoil Apex ---
            if (readySprite != null) weaponImage.sprite = readySprite;
            weaponRect.anchoredPosition = _restPosition + _currentSwayOffset + (recoilKickOffset * 0.65f);
            weaponRect.localEulerAngles = new Vector3(0f, 0f, recoilKickAngle * 0.6f);
            yield return new WaitForSecondsRealtime(0.05f);

            // --- FRAME 3: Recovery Settling ---
            weaponRect.anchoredPosition = _restPosition + _currentSwayOffset + (recoilKickOffset * 0.25f);
            weaponRect.localEulerAngles = new Vector3(0f, 0f, recoilKickAngle * 0.2f);
            yield return new WaitForSecondsRealtime(0.06f);

            // --- FRAME 4: Rest / Ready Idle ---
            weaponRect.anchoredPosition = _restPosition + _currentSwayOffset;
            weaponRect.localEulerAngles = Vector3.zero;

            _fireCycleRoutine = null;
        }

        public void SetupSprites(Sprite ready, Sprite firing)
        {
            readySprite = ready;
            firingSprite = firing;
            if (weaponImage != null && readySprite != null)
            {
                weaponImage.sprite = readySprite;
            }
        }
    }
}

