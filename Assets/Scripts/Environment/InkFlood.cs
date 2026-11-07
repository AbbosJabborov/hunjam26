using UnityEngine;
using Shift.Player;

namespace Shift.Environment
{
    /// <summary>
    /// The Ink Flood (墨潮): Rising Pitch-Black Void Sea.
    /// Floats at the base of the track with churning organic wave motion.
    /// Swallows anything that falls below its surface into flat blackness.
    /// Provides continuous rising droplet particles and immediate submersion death.
    /// </summary>
    public class InkFlood : MonoBehaviour
    {
        public static InkFlood Instance { get; private set; }

        [Header("Height & Rising Settings")]
        [SerializeField] private float baseHeight = -6.5f;
        [SerializeField] private float riseSpeed = 0.06f;
        [SerializeField] private float maxHeight = -2.5f; // Does not rise above the safe track
        [SerializeField] private bool autoRise = true;

        [Header("Wave Motion")]
        [SerializeField] private float waveFrequency = 1.8f;
        [SerializeField] private float waveAmplitude = 0.35f;

        [Header("Submersion Threshold")]
        [SerializeField] private float killOffset = 0.2f;

        private PlayerController _player;
        private PlayerHealth _playerHealth;
        private float _currentBaseY;
        private float _currentSurfaceY;
        private bool _hasSubmergedPlayer;

        public float SurfaceY => _currentSurfaceY;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _currentBaseY = baseHeight;
            _currentSurfaceY = _currentBaseY;
        }

        private void Start()
        {
            FindPlayer();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void FindPlayer()
        {
            _player = Object.FindAnyObjectByType<PlayerController>();
            if (_player != null)
            {
                _playerHealth = _player.GetComponent<PlayerHealth>();
            }
        }

        private void Update()
        {
            // Rising tide
            if (autoRise && _currentBaseY < maxHeight)
            {
                _currentBaseY += riseSpeed * Time.deltaTime;
            }

            // Churning wave motion
            float wave = Mathf.Sin(Time.time * waveFrequency) * waveAmplitude;
            _currentSurfaceY = _currentBaseY + wave;

            Vector3 pos = transform.position;
            pos.y = _currentSurfaceY;
            transform.position = pos;

            // Player submersion check
            CheckPlayerSubmersion();
        }

        private void CheckPlayerSubmersion()
        {
            if (_hasSubmergedPlayer) return;

            if (_player == null || _playerHealth == null)
            {
                FindPlayer();
                if (_player == null || _playerHealth == null) return;
            }

            if (!_playerHealth.IsAlive) return;

            if (_player.transform.position.y <= _currentSurfaceY + killOffset)
            {
                _hasSubmergedPlayer = true;
                Debug.Log("[InkFlood] Player swallowed by the Ink Flood void!");
                _playerHealth.TakeDamage(_playerHealth.MaxHealth * 10f, _player.transform.position, Vector3.up);
            }
        }

        public void ResetFlood()
        {
            _currentBaseY = baseHeight;
            _currentSurfaceY = _currentBaseY;
            _hasSubmergedPlayer = false;
        }
    }
}

