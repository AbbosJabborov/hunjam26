using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using Shift.Player;

namespace Shift.Core
{
    /// <summary>
    /// Manages level lifecycle: run start, finish line completion,
    /// speedrun records, and instantaneous restart via 'R' key.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        private const string BestTimePrefKey = "SHIFT_BEST_TIME";

        public bool IsCompleted { get; private set; }
        public float FinalTime { get; private set; }
        public float BestTime { get; private set; }
        public bool IsNewBestTime { get; private set; }

        public event Action<float> OnLevelCompleted;
        public event Action OnLevelRestarted;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BestTime = PlayerPrefs.GetFloat(BestTimePrefKey, 0f);
        }

        private void Start()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null && player.InputReader != null)
            {
                player.InputReader.OnRestartPressed += HandleRestartInput;
            }

            var playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied += HandlePlayerDied;
            }
        }

        private void OnDestroy()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null && player.InputReader != null)
            {
                player.InputReader.OnRestartPressed -= HandleRestartInput;
            }

            var playerHealth = UnityEngine.Object.FindAnyObjectByType<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.OnPlayerDied -= HandlePlayerDied;
            }

            if (Instance == this) Instance = null;
        }

        private void HandleRestartInput()
        {
            RestartLevel();
        }

        private void HandlePlayerDied()
        {
            // Brief delay then restart
            Invoke(nameof(RestartLevel), 1.0f);
        }

        public void CompleteLevel()
        {
            if (IsCompleted) return;

            IsCompleted = true;
            if (GameTimer.Instance != null)
            {
                GameTimer.Instance.StopTimer();
                FinalTime = GameTimer.Instance.ElapsedTime;
            }

            if (FinalTime > 0.01f && (BestTime <= 0f || FinalTime < BestTime))
            {
                BestTime = FinalTime;
                IsNewBestTime = true;
                PlayerPrefs.SetFloat(BestTimePrefKey, BestTime);
                PlayerPrefs.Save();
            }

            OnLevelCompleted?.Invoke(FinalTime);
            Debug.Log($"[LevelManager] Level Completed! Time: {GameTimer.Instance?.GetFormattedTime()} (Best: {FormatTime(BestTime)})");
        }

        public static string FormatTime(float timeInSeconds)
        {
            if (timeInSeconds <= 0f) return "--:--.--";
            int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
            int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
            int fraction = Mathf.FloorToInt((timeInSeconds * 100f) % 100f);
            return string.Format("{0:00}:{1:00}.{2:00}", minutes, seconds, fraction);
        }

        public void RestartLevel()
        {
            Time.timeScale = 1.0f;
            Time.fixedDeltaTime = 0.02f;
            OnLevelRestarted?.Invoke();
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
