using UnityEngine;
using UnityEngine.UI;
using Shift.Core;
using Shift.Player;
using Shift.Combat;

namespace Shift.UI
{
    /// <summary>
    /// Displays speedrun timer, dimension indicator, player health,
    /// FPS crosshair, and level completion screen.
    /// Provides built-in GUI drawing so HUD renders immediately with zero prefab setup.
    /// </summary>
    public class GameHUD : MonoBehaviour
    {
        public static GameHUD Instance { get; private set; }

        [Header("UI Styling")]
        [SerializeField] private Color platformerColor = new Color(0.2f, 0.8f, 1f); // Neon cyan
        [SerializeField] private Color fpsColor = new Color(1f, 0.4f, 0.2f); // Neon orange
        [SerializeField] private bool showCrosshairInFPS = true;

        private PlayerHealth _playerHealth;
        private PlayerCombat _playerCombat;
        private GUIStyle _timerStyle;
        private GUIStyle _bestTimeStyle;
        private GUIStyle _dimensionStyle;
        private GUIStyle _healthStyle;
        private GUIStyle _completionStyle;
        private GUIStyle _newRecordStyle;
        private GUIStyle _subStyle;
        private Texture2D _crosshairTexture;
        private Texture2D _hitMarkerTexture;
        private Texture2D _damageFlashTexture;
        private float _hitMarkerTimer;
        private float _damageFlashTimer;
        private float _lastKnownHealth = 100f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateCrosshairTexture();
            CreateDamageFlashTexture();
        }

        private void Start()
        {
            _playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (_playerHealth != null)
            {
                _lastKnownHealth = _playerHealth.CurrentHealth;
                _playerHealth.OnHealthChanged += HandleHealthChanged;
            }

            _playerCombat = UnityEngine.Object.FindAnyObjectByType<PlayerCombat>();
            if (_playerCombat != null)
            {
                _playerCombat.OnHitTarget += ShowHitMarker;
            }
        }

        private void OnDestroy()
        {
            if (_playerHealth != null)
            {
                _playerHealth.OnHealthChanged -= HandleHealthChanged;
            }
            if (_playerCombat != null)
            {
                _playerCombat.OnHitTarget -= ShowHitMarker;
            }
            if (Instance == this) Instance = null;
        }

        private void HandleHealthChanged(float current, float max)
        {
            if (current < _lastKnownHealth)
            {
                _damageFlashTimer = 0.25f; // Flash on damage
            }
            _lastKnownHealth = current;
        }

        public void ShowHitMarker()
        {
            _hitMarkerTimer = 0.1f;
        }

        private void Update()
        {
            if (_hitMarkerTimer > 0f)
            {
                _hitMarkerTimer -= Time.unscaledDeltaTime;
            }
            if (_damageFlashTimer > 0f)
            {
                _damageFlashTimer -= Time.unscaledDeltaTime;
            }
        }

        private void CreateDamageFlashTexture()
        {
            _damageFlashTexture = new Texture2D(2, 2);
            Color[] colors = new Color[4] { Color.red, Color.red, Color.red, Color.red };
            _damageFlashTexture.SetPixels(colors);
            _damageFlashTexture.Apply();
        }

        private void CreateCrosshairTexture()
        {
            _crosshairTexture = new Texture2D(2, 2);
            Color[] colors = new Color[4] { Color.white, Color.white, Color.white, Color.white };
            _crosshairTexture.SetPixels(colors);
            _crosshairTexture.Apply();

            _hitMarkerTexture = new Texture2D(2, 2);
            Color[] hitColors = new Color[4] { Color.red, Color.red, Color.red, Color.red };
            _hitMarkerTexture.SetPixels(hitColors);
            _hitMarkerTexture.Apply();
        }

        private void InitStyles()
        {
            if (_timerStyle != null) return;

            _timerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            _timerStyle.normal.textColor = Color.white;

            _bestTimeStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperRight
            };
            _bestTimeStyle.normal.textColor = new Color(1f, 0.85f, 0.2f); // Gold

            _newRecordStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _newRecordStyle.normal.textColor = Color.yellow;

            _dimensionStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            _healthStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            _healthStyle.normal.textColor = new Color(0.2f, 1f, 0.4f);

            _completionStyle = new GUIStyle(GUI.skin.box)
            {
                fontSize = 36,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            _completionStyle.normal.textColor = Color.yellow;

            _subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Normal,
                alignment = TextAnchor.MiddleCenter
            };
            _subStyle.normal.textColor = Color.white;
        }

        private void OnGUI()
        {
            InitStyles();

            // 1. Speedrun Timer (Top Right)
            string timerText = GameTimer.Instance != null ? GameTimer.Instance.GetFormattedTime() : "00:00.00";
            Rect timerRect = new Rect(Screen.width - 240, 20, 220, 40);
            GUI.Label(timerRect, timerText, _timerStyle);

            // 2. Dimension Indicator (Top Center)
            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;
            string dimText = isFps ? "[ SHIFT ] FPS (0.25x SLOW-MO)" : "[ SHIFT ] 2D PLATFORMER";
            _dimensionStyle.normal.textColor = isFps ? fpsColor : platformerColor;
            Rect dimRect = new Rect((Screen.width - 340) * 0.5f, 20, 340, 36);
            GUI.Box(dimRect, dimText, _dimensionStyle);

            // 3. Player Health (Top Left)
            if (_playerHealth == null) _playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            float hp = _playerHealth != null ? _playerHealth.CurrentHealth : 100f;
            float maxHp = _playerHealth != null ? _playerHealth.MaxHealth : 100f;
            Rect hpRect = new Rect(25, 20, 200, 35);
            GUI.Label(hpRect, $"HP: {Mathf.CeilToInt(hp)} / {Mathf.CeilToInt(maxHp)}", _healthStyle);

            // 4. FPS Crosshair (Center of screen when in FPS mode)
            if (isFps && showCrosshairInFPS && _crosshairTexture != null)
            {
                float chSize = 4f;
                float chLength = 12f;
                float cx = Screen.width * 0.5f;
                float cy = Screen.height * 0.5f;

                // Center dot
                GUI.DrawTexture(new Rect(cx - chSize * 0.5f, cy - chSize * 0.5f, chSize, chSize), _crosshairTexture);
                // Horizontal reticle
                GUI.DrawTexture(new Rect(cx - chLength - 4f, cy - 1f, chLength, 2f), _crosshairTexture);
                GUI.DrawTexture(new Rect(cx + 4f, cy - 1f, chLength, 2f), _crosshairTexture);
                // Vertical reticle
                GUI.DrawTexture(new Rect(cx - 1f, cy - chLength - 4f, 2f, chLength), _crosshairTexture);
                GUI.DrawTexture(new Rect(cx - 1f, cy + 4f, 2f, chLength), _crosshairTexture);

                // Hit marker (X indicator on successful shot)
                if (_hitMarkerTimer > 0f && _hitMarkerTexture != null)
                {
                    float hmOffset = 6f;
                    float hmSize = 6f;
                    GUI.DrawTexture(new Rect(cx - hmOffset - hmSize, cy - hmOffset - hmSize, hmSize, hmSize), _hitMarkerTexture);
                    GUI.DrawTexture(new Rect(cx + hmOffset, cy - hmOffset - hmSize, hmSize, hmSize), _hitMarkerTexture);
                    GUI.DrawTexture(new Rect(cx - hmOffset - hmSize, cy + hmOffset, hmSize, hmSize), _hitMarkerTexture);
                    GUI.DrawTexture(new Rect(cx + hmOffset, cy + hmOffset, hmSize, hmSize), _hitMarkerTexture);
                }
            }

            // 5. Level Completion Overlay
            if (LevelManager.Instance != null && LevelManager.Instance.IsCompleted)
            {
                float panelW = 480;
                float panelH = 220;
                Rect panelRect = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.5f, panelW, panelH);
                GUI.Box(panelRect, "\nSTAGE CLEARED!", _completionStyle);

                Rect textRect1 = new Rect(panelRect.x, panelRect.y + 90, panelW, 35);
                GUI.Label(textRect1, $"Final Time: {GameTimer.Instance?.GetFormattedTime()}", _subStyle);

                Rect textRect2 = new Rect(panelRect.x, panelRect.y + 140, panelW, 35);
                GUI.Label(textRect2, "Press [ R ] to Retry / Speedrun", _subStyle);
            }

            // 6. Death Overlay
            if (_playerHealth != null && !_playerHealth.IsAlive)
            {
                float panelW = 400;
                float panelH = 150;
                Rect panelRect = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.5f, panelW, panelH);
                _completionStyle.normal.textColor = Color.red;
                GUI.Box(panelRect, "\nYOU DIED", _completionStyle);
            }
        }
    }
}
