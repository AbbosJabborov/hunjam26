using System;
using UnityEngine;
using Shift.Core;
using Shift.Combat;
using Shift.Player;
using Shift.Enemies;

namespace Shift.Core
{
    /// <summary>
    /// Lightweight audio system with built-in procedural sound synthesis
    /// and override slots for custom audio clips.
    /// Provides instant audio feedback for jump, dimension switch, shooting, and damage.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Custom Clips (Optional - procedural audio used if unassigned)")]
        [SerializeField] private AudioClip switchDimensionClip;
        [SerializeField] private AudioClip gunshotClip;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip enemyDeathClip;
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip grappleClip;
        [SerializeField] private AudioClip levelCompleteClip;

        private AudioSource _audioSource;
        private AudioClip _procSwitch;
        private AudioClip _procGunshot;
        private AudioClip _procHit;
        private AudioClip _procEnemyDeath;
        private AudioClip _procJump;
        private AudioClip _procGrapple;
        private AudioClip _procComplete;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D sound

            GenerateProceduralClips();
        }

        private void Start()
        {
            // Auto-wire to core gameplay events
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += _ => PlaySwitchDimension();
            }

            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                var combat = player.GetComponent<PlayerCombat>();
                if (combat != null)
                {
                    combat.OnWeaponFired += PlayGunshot;
                    combat.OnHitTarget += PlayHit;
                }

                if (player.InputReader != null)
                {
                    player.InputReader.OnJumpPressed += PlayJump;
                }
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelCompleted += _ => PlayLevelComplete();
            }
        }

        private void GenerateProceduralClips()
        {
            _procJump = CreateTone(0.12f, 220f, 440f, 0.4f);
            _procGrapple = CreateTone(0.16f, 350f, 850f, 0.45f);
            _procSwitch = CreateNoiseSweep(0.3f, 600f, 150f, 0.5f);
            _procGunshot = CreateGunshot(0.15f, 0.6f);
            _procHit = CreateTone(0.08f, 600f, 900f, 0.35f);
            _procEnemyDeath = CreateNoiseSweep(0.35f, 300f, 60f, 0.5f);
            _procComplete = CreateArpeggio(0.5f, 0.5f);
        }

        public void PlayJump()
        {
            PlaySound(jumpClip != null ? jumpClip : _procJump);
        }

        public void PlayGrapple()
        {
            PlaySound(grappleClip != null ? grappleClip : _procGrapple);
        }

        public void PlaySwitchDimension()
        {
            PlaySound(switchDimensionClip != null ? switchDimensionClip : _procSwitch);
        }

        public void PlayGunshot()
        {
            PlaySound(gunshotClip != null ? gunshotClip : _procGunshot);
        }

        public void PlayHit()
        {
            PlaySound(hitClip != null ? hitClip : _procHit);
        }

        public void PlayEnemyDeath()
        {
            PlaySound(enemyDeathClip != null ? enemyDeathClip : _procEnemyDeath);
        }

        public void PlayLevelComplete()
        {
            PlaySound(levelCompleteClip != null ? levelCompleteClip : _procComplete);
        }

        private void PlaySound(AudioClip clip)
        {
            if (clip != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(clip);
            }
        }

        #region Procedural Audio Generators
        private AudioClip CreateTone(float duration, float startFreq, float endFreq, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.FloorToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                float envelope = 1f - t;
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("ProcTone", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateGunshot(float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.FloorToInt(sampleRate * duration);
            float[] data = new float[samples];
            var rng = new System.Random();

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float envelope = Mathf.Exp(-t * 15f); // Fast punch decay
                float noise = ((float)rng.NextDouble() * 2f - 1f);
                float thump = Mathf.Sin(2f * Mathf.PI * 90f * ((float)i / sampleRate));
                data[i] = (noise * 0.7f + thump * 0.3f) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("ProcGunshot", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateNoiseSweep(float duration, float startFreq, float endFreq, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.FloorToInt(sampleRate * duration);
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                float freq = Mathf.Lerp(startFreq, endFreq, t);
                float envelope = Mathf.Sin(t * Mathf.PI); // Smooth in and out
                data[i] = Mathf.Sin(2f * Mathf.PI * freq * ((float)i / sampleRate)) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("ProcSweep", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip CreateArpeggio(float duration, float volume)
        {
            int sampleRate = 44100;
            int samples = Mathf.FloorToInt(sampleRate * duration);
            float[] data = new float[samples];
            float[] freqs = new float[] { 261.63f, 329.63f, 392.00f, 523.25f }; // C E G C

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples;
                int noteIndex = Mathf.Clamp(Mathf.FloorToInt(t * freqs.Length), 0, freqs.Length - 1);
                float envelope = 1f - (t * 0.8f);
                data[i] = Mathf.Sin(2f * Mathf.PI * freqs[noteIndex] * ((float)i / sampleRate)) * envelope * volume;
            }

            AudioClip clip = AudioClip.Create("ProcArp", samples, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
        #endregion
    }
}

