using System.Collections.Generic;
using UnityEngine;
using Shift.Player;
using Shift.Combat;

namespace Shift.VFX
{
    /// <summary>
    /// Spawns pitch-black hand-drawn ink droplet splatters for player movement
    /// (footsteps, landing, wall-jumps, sliding) and combat impacts.
    /// In slow-motion (FPS mode), ink particles suspend and linger like jagged pen marks on parchment.
    /// </summary>
    public class InkSplatterManager : MonoBehaviour
    {
        public static InkSplatterManager Instance { get; private set; }

        [Header("Ink Particle Settings")]
        [SerializeField] private Material inkParticleMaterial;
        [SerializeField] private Color inkColor = new Color(0.06f, 0.05f, 0.05f, 0.95f);
        [SerializeField] private float baseParticleSize = 0.35f;

        private ParticleSystem _particleSys;
        private ParticleSystem.EmitParams _emitParams;
        private PlayerMovement _playerMovement;
        private PlayerCombat _playerCombat;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            SetupParticleSystem();
        }

        private void Start()
        {
            FindPlayerReferences();
        }

        private void OnDestroy()
        {
            UnbindPlayer();
            if (Instance == this) Instance = null;
        }

        private void SetupParticleSystem()
        {
            _particleSys = GetComponent<ParticleSystem>();
            if (_particleSys == null)
            {
                _particleSys = gameObject.AddComponent<ParticleSystem>();
            }

            var main = _particleSys.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = 0.8f;
            main.startSpeed = 3.5f;
            main.startSize = baseParticleSize;
            main.startColor = inkColor;
            main.gravityModifier = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = false; // Automatically slows down with Time.timeScale in slow-motion!

            var emission = _particleSys.emission;
            emission.enabled = false; // Explicit emission only

            var shape = _particleSys.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f;

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null && inkParticleMaterial != null)
            {
                renderer.material = inkParticleMaterial;
            }

            _emitParams = new ParticleSystem.EmitParams();
        }

        private void FindPlayerReferences()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                _playerMovement = player.GetComponent<PlayerMovement>();
                if (_playerMovement != null)
                {
                    _playerMovement.OnLanded += SpawnLandingSplatter;
                    _playerMovement.OnWallJumped += SpawnWallJumpSplatter;
                    _playerMovement.OnJumped += SpawnJumpSplatter;
                    _playerMovement.OnSlideStarted += SpawnSlideSplatter;
                }

                _playerCombat = player.GetComponent<PlayerCombat>();
                if (_playerCombat != null)
                {
                    _playerCombat.OnHitTarget += SpawnCombatHitSplatter;
                }
            }
        }

        private void UnbindPlayer()
        {
            if (_playerMovement != null)
            {
                _playerMovement.OnLanded -= SpawnLandingSplatter;
                _playerMovement.OnWallJumped -= SpawnWallJumpSplatter;
                _playerMovement.OnJumped -= SpawnJumpSplatter;
                _playerMovement.OnSlideStarted -= SpawnSlideSplatter;
            }

            if (_playerCombat != null)
            {
                _playerCombat.OnHitTarget -= SpawnCombatHitSplatter;
            }
        }

        public void SpawnLandingSplatter(Vector3 position)
        {
            EmitBurst(position + Vector3.up * 0.1f, 8, 3.5f, Vector3.up);
        }

        public void SpawnJumpSplatter(Vector3 position)
        {
            EmitBurst(position + Vector3.up * 0.1f, 4, 2.0f, -Vector3.up);
        }

        public void SpawnWallJumpSplatter(Vector3 position, Vector3 wallNormal)
        {
            Vector3 burstDir = (wallNormal + Vector3.up * 0.5f).normalized;
            EmitBurst(position + Vector3.up * 0.8f, 7, 4.0f, burstDir);
        }

        public void SpawnSlideSplatter(Vector3 position)
        {
            EmitBurst(position + Vector3.up * 0.15f, 6, 2.5f, Vector3.up);
        }

        public void SpawnCombatHitSplatter()
        {
            if (Camera.main != null)
            {
                // In front of camera
                Vector3 spawnPos = Camera.main.transform.position + Camera.main.transform.forward * 4.5f;
                EmitBurst(spawnPos, 12, 5.0f, -Camera.main.transform.forward);
            }
        }

        public void EmitBurst(Vector3 position, int count, float speed, Vector3 generalDirection)
        {
            if (_particleSys == null) return;

            for (int i = 0; i < count; i++)
            {
                Vector3 randDir = (generalDirection + Random.insideUnitSphere * 0.8f).normalized;
                _emitParams.position = position + Random.insideUnitSphere * 0.12f;
                _emitParams.velocity = randDir * (speed * Random.Range(0.6f, 1.4f));
                _emitParams.startSize = baseParticleSize * Random.Range(0.6f, 1.3f);
                _emitParams.startLifetime = Random.Range(0.4f, 0.9f);
                _emitParams.startColor = inkColor;

                _particleSys.Emit(_emitParams, 1);
            }
        }
    }
}

