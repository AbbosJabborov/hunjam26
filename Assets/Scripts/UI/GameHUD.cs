using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Shift.Core;
using Shift.Player;
using Shift.Combat;
using Shift.Cameras;

namespace Shift.UI
{
    /// <summary>
    /// Pure TextMeshPro UGUI Controller for SHIFT.
    /// Manages HUD elements, timer, PB records, dimension state, health, crosshair,
    /// damage feedback, and stage clearance overlays using modern TextMeshPro (TMP_Text).
    /// All references are optional and null-safe for complete authoring freedom in the Unity Editor.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        [Header("Speedrun Timer & PB")]
        [Tooltip("Displays real-world run time (MM:SS.ms)")]
        [SerializeField] private TMP_Text timerText;

        [Tooltip("Displays Personal Best time")]
        [SerializeField] private TMP_Text bestTimeText;

        [Tooltip("Optional badge/object shown when current run beats PB")]
        [SerializeField] private GameObject newRecordBadge;

        [Header("Dimension Indicator")]
        [Tooltip("Displays dimension name / status text")]
        [SerializeField] private TMP_Text dimensionText;

        [Tooltip("Badge or background image tinted by dimension")]
        [SerializeField] private Image dimensionBadge;

        [Tooltip("Optional container visible only in 2D Platformer mode")]
        [SerializeField] private GameObject platformerContainer;

        [Tooltip("Optional container visible only in FPS mode")]
        [SerializeField] private GameObject fpsContainer;

        [SerializeField] private Color platformerColor = new Color(0.18f, 0.16f, 0.15f, 1f); // Charcoal Ink #2E2926
        [SerializeField] private Color fpsColor = new Color(0.66f, 0.16f, 0.16f, 1f); // Cinnabar Red #A82828
        [SerializeField] private string platformerLabel = "[ SHIFT ] 2D PARCHMENT";
        [SerializeField] private string fpsLabel = "[ SHIFT ] 3D INK VOID (SLOW-MO)";

        [Header("Ink Flood Atmosphere")]
        [Tooltip("Wet ink vignette that eats into the screen during 3D FPS mode")]
        [SerializeField] private CanvasGroup inkVignetteGroup;

        [Header("Player Health")]
        [Tooltip("Health slider bar")]
        [SerializeField] private Slider healthSlider;
        [Tooltip("Alternative: Image with ImageType.Filled")]
        [SerializeField] private Image healthFillImage;
        [Tooltip("Displays numerical health (e.g. '100 / 100')")]
        [SerializeField] private TMP_Text healthText;

        [Tooltip("Full-screen red damage flash overlay")]
        [SerializeField] private Image damageFlashOverlay;
        [SerializeField] private CanvasGroup damageFlashGroup;
        [SerializeField] private float damageFlashDuration = 0.3f;

        [Header("Combat Crosshair & Hit Feedback")]
        [Tooltip("Crosshair root object; automatically displayed only during FPS mode")]
        [SerializeField] private GameObject crosshairRoot;
        [SerializeField] private CanvasGroup crosshairGroup;
        [SerializeField] private bool showCrosshairOnlyInFPS = true;

        [Header("Platformer Crosshair (Canvas Scaled)")]
        [Tooltip("UI crosshair that follows mouse in 2D platformer mode, perfectly scaled with CanvasScaler")]
        [SerializeField] private RectTransform platformerCrosshair;

        [Header("Platformer Hardware Cursor Fallback")]
        [Tooltip("Custom cursor texture used as fallback if UI crosshair is not assigned")]
        [SerializeField] private Texture2D platformerCursorTexture;
        [SerializeField] private Vector2 cursorHotspot = new Vector2(32f, 32f);

        [Tooltip("Hit marker indicator (X or flash on successful bullet impact)")]
        [SerializeField] private GameObject hitMarkerRoot;
        [SerializeField] private CanvasGroup hitMarkerGroup;
        [SerializeField] private Image hitMarkerImage;
        [SerializeField] private float hitMarkerDuration = 0.12f;

        [Header("Stage Cleared (Completion) Panel")]
        [SerializeField] private GameObject stageClearedPanel;
        [SerializeField] private CanvasGroup stageClearedGroup;
        [SerializeField] private TMP_Text clearedFinalTimeText;
        [SerializeField] private TMP_Text clearedBestTimeText;
        [SerializeField] private GameObject clearedNewRecordBadge;
        [SerializeField] private Button retryButton;

        [Header("Player Death (Game Over) Panel")]
        [SerializeField] private GameObject deathPanel;
        [SerializeField] private CanvasGroup deathGroup;
        [SerializeField] private Button deathRestartButton;

        private PlayerHealth _playerHealth;
        private PlayerCombat _playerCombat;
        private float _lastKnownHealth = 100f;
        private float _hitMarkerTimer = 0f;
        private float _damageFlashTimer = 0f;
        private Color _damageFlashColor = Color.red;

        private Canvas _canvas;
        private RectTransform _canvasRect;
        private bool _isLevelEnded;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _canvas = GetComponent<Canvas>();
            _canvasRect = GetComponent<RectTransform>();

            if (platformerCrosshair == null)
            {
                Transform found = transform.Find("Platformer_Crosshair");
                if (found != null)
                {
                    platformerCrosshair = found.GetComponent<RectTransform>();
                }
            }
            if (platformerCrosshair != null)
            {
                platformerCrosshair.gameObject.SetActive(false);
            }

            if (damageFlashOverlay != null)
            {
                _damageFlashColor = damageFlashOverlay.color;
                SetDamageFlashAlpha(0f);
            }
            if (damageFlashGroup != null)
            {
                damageFlashGroup.alpha = 0f;
            }

            if (inkVignetteGroup != null)
            {
                inkVignetteGroup.alpha = 0f;
            }

            if (hitMarkerRoot != null) hitMarkerRoot.SetActive(false);
            if (hitMarkerGroup != null) hitMarkerGroup.alpha = 0f;

            if (stageClearedPanel != null) stageClearedPanel.SetActive(false);
            if (stageClearedGroup != null) stageClearedGroup.alpha = 0f;

            if (deathPanel != null) deathPanel.SetActive(false);
            if (deathGroup != null) deathGroup.alpha = 0f;

            if (retryButton != null)
            {
                retryButton.onClick.AddListener(HandleRestartClicked);
            }
            if (deathRestartButton != null)
            {
                deathRestartButton.onClick.AddListener(HandleRestartClicked);
            }
        }

        private void Start()
        {
            FindPlayerReferences();

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateDimensionDisplay(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                UpdateDimensionDisplay(DimensionType.Platformer);
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelCompleted += HandleLevelCompleted;
                LevelManager.Instance.OnLevelRestarted += HandleLevelRestarted;
                UpdateBestTimeDisplay(LevelManager.Instance.BestTime);
            }

            if (_playerHealth != null)
            {
                UpdateHealthDisplay(_playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            }

            UpdateCursorMode(DimensionManager.Instance != null ? DimensionManager.Instance.CurrentDimension : DimensionType.Platformer);
        }

        private void OnDisable()
        {
            ResetCursorToDefault();
        }

        private void OnDestroy()
        {
            ResetCursorToDefault();

            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelCompleted -= HandleLevelCompleted;
                LevelManager.Instance.OnLevelRestarted -= HandleLevelRestarted;
            }

            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= HandleHealthChanged;
                _playerHealth.OnPlayerDied -= HandlePlayerDied;
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnHitTarget -= ShowHitMarker;
            }

            if (Instance == this) Instance = null;
        }

        private void FindPlayerReferences()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                _playerHealth = player.GetComponent<PlayerHealth>();
                if (_playerHealth != null)
                {
                    _lastKnownHealth = _playerHealth.CurrentHealth;
                    _playerHealth.OnHealthChanged += HandleHealthChanged;
                    _playerHealth.OnPlayerDied += HandlePlayerDied;
                }

                _playerCombat = player.GetComponent<PlayerCombat>();
                if (_playerCombat != null)
                {
                    _playerCombat.OnHitTarget += ShowHitMarker;
                }
            }
        }

        private void Update()
        {
            UpdateTimerDisplay();
            UpdateCombatFeedback();
            UpdateCrosshairVisibility();
        }

        private void LateUpdate()
        {
            UpdatePlatformerCrosshair();
        }

        private void UpdatePlatformerCrosshair()
        {
            if (platformerCrosshair == null)
            {
                Transform found = transform.Find("Platformer_Crosshair");
                if (found != null)
                {
                    platformerCrosshair = found.GetComponent<RectTransform>();
                }
                else
                {
                    return;
                }
            }

            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;
            if (isFps || _isLevelEnded)
            {
                if (platformerCrosshair.gameObject.activeSelf)
                {
                    platformerCrosshair.gameObject.SetActive(false);
                }
                return;
            }

            if (!platformerCrosshair.gameObject.activeSelf)
            {
                platformerCrosshair.gameObject.SetActive(true);
            }
            Cursor.visible = false;

            if (Mouse.current == null) return;
            Vector2 mousePos = Mouse.current.position.ReadValue();

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
                if (_canvas == null) _canvas = GetComponent<Canvas>();
            }

            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                platformerCrosshair.position = new Vector3(mousePos.x, mousePos.y, 0f);
            }
            else
            {
                RectTransform parentRect = platformerCrosshair.parent as RectTransform;
                if (parentRect == null && _canvasRect != null) parentRect = _canvasRect;

                if (parentRect != null)
                {
                    Camera cam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
                    if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, mousePos, cam, out Vector2 localPoint))
                    {
                        platformerCrosshair.anchoredPosition = localPoint;
                    }
                    else
                    {
                        platformerCrosshair.position = new Vector3(mousePos.x, mousePos.y, 0f);
                    }
                }
                else
                {
                    platformerCrosshair.position = new Vector3(mousePos.x, mousePos.y, 0f);
                }
            }
        }

        // --- TIMER & RECORDS ---

        private void UpdateTimerDisplay()
        {
            if (GameTimer.Instance == null) return;

            string formatted = GameTimer.Instance.GetFormattedTime();
            SetText(timerText, formatted);

            if (newRecordBadge != null && LevelManager.Instance != null)
            {
                float best = LevelManager.Instance.BestTime;
                bool isRecord = best > 0f && GameTimer.Instance.ElapsedTime < best;
                newRecordBadge.SetActive(isRecord);
            }
        }

        private void UpdateBestTimeDisplay(float bestTime)
        {
            string formatted = LevelManager.FormatTime(bestTime);
            string label = bestTime > 0f ? $"PB: {formatted}" : "PB: --:--.--";
            SetText(bestTimeText, label);
        }

        // --- DIMENSION DISPLAY ---

        private void HandleDimensionChanged(DimensionType dimension)
        {
            UpdateDimensionDisplay(dimension);
            UpdateCursorMode(dimension);
        }

        public void UpdateCursorMode(DimensionType dimension)
        {
            if (_isLevelEnded)
            {
                ResetCursorToDefault();
                return;
            }

            if (dimension == DimensionType.FPS)
            {
                if (platformerCrosshair != null) platformerCrosshair.gameObject.SetActive(false);
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
                Cursor.lockState = CursorLockMode.None;

                if (platformerCrosshair == null)
                {
                    Transform found = transform.Find("Platformer_Crosshair");
                    if (found != null) platformerCrosshair = found.GetComponent<RectTransform>();
                }

                if (platformerCrosshair != null)
                {
                    Cursor.visible = false;
                    platformerCrosshair.gameObject.SetActive(true);
                    UpdatePlatformerCrosshair();
                }
                else if (platformerCursorTexture != null)
                {
                    Vector2 hotspot = cursorHotspot.sqrMagnitude > 0f
                        ? cursorHotspot
                        : new Vector2(platformerCursorTexture.width / 2f, platformerCursorTexture.height / 2f);
                    Cursor.SetCursor(platformerCursorTexture, hotspot, CursorMode.Auto);
                    Cursor.visible = true;
                }
                else
                {
                    Cursor.visible = true;
                }
            }
        }

        public void ResetCursorToDefault()
        {
            if (platformerCrosshair != null) platformerCrosshair.gameObject.SetActive(false);
            Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void UpdateDimensionDisplay(DimensionType dimension)
        {
            bool isFps = dimension == DimensionType.FPS;

            string label = isFps ? fpsLabel : platformerLabel;
            Color color = isFps ? fpsColor : platformerColor;

            SetText(dimensionText, label);

            if (dimensionText != null) dimensionText.color = color;
            if (dimensionBadge != null) dimensionBadge.color = color;

            if (platformerContainer != null) platformerContainer.SetActive(!isFps);
            if (fpsContainer != null) fpsContainer.SetActive(isFps);
        }

        // --- HEALTH & DAMAGE FEEDBACK ---

        private void HandleHealthChanged(float current, float max)
        {
            if (current < _lastKnownHealth)
            {
                TriggerDamageFlash();
            }
            _lastKnownHealth = current;
            UpdateHealthDisplay(current, max);
        }

        public void UpdateHealthDisplay(float current, float max)
        {
            float fill = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (healthSlider != null)
            {
                healthSlider.minValue = 0f;
                healthSlider.maxValue = max;
                healthSlider.value = current;
            }

            if (healthFillImage != null)
            {
                healthFillImage.fillAmount = fill;
            }

            string text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            SetText(healthText, text);
        }

        public void TriggerDamageFlash()
        {
            _damageFlashTimer = damageFlashDuration;
        }

        private void SetDamageFlashAlpha(float alpha)
        {
            if (damageFlashOverlay != null)
            {
                Color c = _damageFlashColor;
                c.a = alpha * _damageFlashColor.a;
                damageFlashOverlay.color = c;
            }
            if (damageFlashGroup != null)
            {
                damageFlashGroup.alpha = alpha;
            }
        }

        // --- COMBAT & HIT MARKER ---

        public void ShowHitMarker()
        {
            _hitMarkerTimer = hitMarkerDuration;
            if (hitMarkerRoot != null) hitMarkerRoot.SetActive(true);
        }

        private void UpdateCombatFeedback()
        {
            // Damage Flash decay
            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer -= Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(_damageFlashTimer / Mathf.Max(0.01f, damageFlashDuration));
                SetDamageFlashAlpha(alpha);
            }
            else
            {
                SetDamageFlashAlpha(0f);
            }

            // Hit Marker decay
            if (_hitMarkerTimer > 0f)
            {
                _hitMarkerTimer -= Time.unscaledDeltaTime;
                float progress = Mathf.Clamp01(_hitMarkerTimer / Mathf.Max(0.01f, hitMarkerDuration));

                if (hitMarkerGroup != null)
                {
                    hitMarkerGroup.alpha = progress;
                }
                if (hitMarkerImage != null)
                {
                    Color c = hitMarkerImage.color;
                    c.a = progress;
                    hitMarkerImage.color = c;
                }
            }
            else
            {
                if (hitMarkerRoot != null && hitMarkerRoot.activeSelf)
                {
                    hitMarkerRoot.SetActive(false);
                }
                if (hitMarkerGroup != null) hitMarkerGroup.alpha = 0f;
            }
        }

        private void UpdateCrosshairVisibility()
        {
            if (!showCrosshairOnlyInFPS) return;

            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;

            // Check smooth camera transition if applicable
            float targetAlpha = isFps ? 1f : 0f;
            if (CameraManager.Instance != null && CameraManager.Instance.IsTransitioning)
            {
                targetAlpha = CameraManager.Instance.TransitionProgress;
            }

            if (crosshairRoot != null)
            {
                bool shouldBeActive = targetAlpha > 0.05f;
                if (crosshairRoot.activeSelf != shouldBeActive)
                {
                    crosshairRoot.SetActive(shouldBeActive);
                }
            }

            if (crosshairGroup != null)
            {
                crosshairGroup.alpha = targetAlpha;
            }

            if (inkVignetteGroup != null)
            {
                inkVignetteGroup.alpha = targetAlpha;
            }
        }

        // --- LEVEL COMPLETION & DEATH SCREENS ---

        private void HandleLevelCompleted(float finalTime)
        {
            _isLevelEnded = true;
            ResetCursorToDefault();

            if (stageClearedPanel != null) stageClearedPanel.SetActive(true);
            if (stageClearedGroup != null)
            {
                stageClearedGroup.alpha = 1f;
                stageClearedGroup.blocksRaycasts = true;
                stageClearedGroup.interactable = true;
            }

            string finalStr = LevelManager.FormatTime(finalTime);
            SetText(clearedFinalTimeText, $"Time: {finalStr}");

            if (LevelManager.Instance != null)
            {
                string bestStr = LevelManager.FormatTime(LevelManager.Instance.BestTime);
                SetText(clearedBestTimeText, $"Personal Best: {bestStr}");

                if (clearedNewRecordBadge != null)
                {
                    clearedNewRecordBadge.SetActive(LevelManager.Instance.IsNewBestTime);
                }
            }
        }

        private void HandlePlayerDied()
        {
            _isLevelEnded = true;
            ResetCursorToDefault();

            if (deathPanel != null) deathPanel.SetActive(true);
            if (deathGroup != null)
            {
                deathGroup.alpha = 1f;
                deathGroup.blocksRaycasts = true;
                deathGroup.interactable = true;
            }
        }

        private void HandleLevelRestarted()
        {
            _isLevelEnded = false;
            if (stageClearedPanel != null) stageClearedPanel.SetActive(false);
            if (stageClearedGroup != null) stageClearedGroup.alpha = 0f;

            if (deathPanel != null) deathPanel.SetActive(false);
            if (deathGroup != null) deathGroup.alpha = 0f;

            UpdateCursorMode(DimensionManager.Instance != null ? DimensionManager.Instance.CurrentDimension : DimensionType.Platformer);
        }

        private void HandleRestartClicked()
        {
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.RestartLevel();
            }
        }

        // --- UTILITIES ---

        private static void SetText(TMP_Text tmp, string value)
        {
            if (tmp != null) tmp.text = value;
        }
    }
}
