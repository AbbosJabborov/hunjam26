using System;
using UnityEngine;

namespace Shift.Core
{
    /// <summary>
    /// Tracks speedrun completion time using unscaled delta time.
    /// Crucial for SHIFT: slow-motion gameplay in FPS dimension does NOT slow down
    /// the real elapsed time counter.
    /// </summary>
    public class GameTimer : MonoBehaviour
    {
        public static GameTimer Instance { get; private set; }

        [SerializeField] private bool autoStart = true;

        private float _elapsedTime;
        private bool _isRunning;

        public float ElapsedTime => _elapsedTime;
        public bool IsRunning => _isRunning;

        public event Action<float> OnTimerTick;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (autoStart)
            {
                StartTimer();
            }
        }

        private void Update()
        {
            if (_isRunning)
            {
                // Unscaled delta time preserves true real-world elapsed time
                _elapsedTime += Time.unscaledDeltaTime;
                OnTimerTick?.Invoke(_elapsedTime);
            }
        }

        public void StartTimer()
        {
            _elapsedTime = 0f;
            _isRunning = true;
        }

        public void StopTimer()
        {
            _isRunning = false;
        }

        public void ResetTimer()
        {
            _elapsedTime = 0f;
            _isRunning = false;
        }

        public string GetFormattedTime()
        {
            int minutes = Mathf.FloorToInt(_elapsedTime / 60f);
            int seconds = Mathf.FloorToInt(_elapsedTime % 60f);
            int fraction = Mathf.FloorToInt((_elapsedTime * 100f) % 100f);
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fraction);
        }
    }
}

