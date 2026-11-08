using System.Collections;
using UnityEngine;
using Shift.Player;
using Shift.VFX;

namespace Shift.Environment
{
    /// <summary>
    /// Handles player interaction with the ink river:
    /// - Illustrated water skimming wakes when swinging low over the wave crests.
    /// - Foam and droplet kick-up when brushing the surface at high speeds.
    /// - Near-water landing mist.
    /// </summary>
    public class InkWaterInteraction : MonoBehaviour
    {
        [Header("Skim Wake Settings")]
        [Tooltip("Maximum clearance above wave crests to count as skimming the water surface.")]
        [SerializeField] private float maxSkimClearance = 1.15f;
        [SerializeField] private float minSkimSpeed = 3.5f;
        [SerializeField] private float wakeInterval = 0.08f;

        private Rigidbody _rb;
        private PlayerGrapple _grapple;
        private PlayerMovement _movement;
        private InkLakeHazard _cachedLake;
        private Renderer _lakeRenderer;
        private Material _lakeMaterial;

        private float _lastWakeTime;
        private Sprite _wakeSprite;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _grapple = GetComponent<PlayerGrapple>();
            _movement = GetComponent<PlayerMovement>();
        }

        private void Start()
        {
            FindLake();

            if (_movement != null)
            {
                _movement.OnLanded += HandlePlayerLanded;
            }

            _wakeSprite = Resources.Load<Sprite>("ink_wake_streak");
#if UNITY_EDITOR
            if (_wakeSprite == null)
            {
                _wakeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/InkWater/ink_wake_streak.png");
            }
#endif
        }

        private void OnDestroy()
        {
            if (_movement != null)
            {
                _movement.OnLanded -= HandlePlayerLanded;
            }
        }

        private void FindLake()
        {
            _cachedLake = Object.FindAnyObjectByType<InkLakeHazard>();
            if (_cachedLake != null)
            {
                _lakeRenderer = _cachedLake.GetComponent<Renderer>();
                if (_lakeRenderer != null)
                {
                    _lakeMaterial = _lakeRenderer.sharedMaterial;
                }
            }
        }

        private void Update()
        {
            if (_rb == null) return;
            if (_cachedLake == null || _lakeRenderer == null)
            {
                FindLake();
                if (_cachedLake == null || _lakeRenderer == null) return;
            }

            Bounds b = _lakeRenderer.bounds;
            Vector3 pos = transform.position;

            // Check if player is horizontally within lake bounds
            if (pos.x < b.min.x || pos.x > b.max.x) return;

            // Read wave parameters
            float waveSpeed = _lakeMaterial != null && _lakeMaterial.HasProperty("_WaveSpeed") ? _lakeMaterial.GetFloat("_WaveSpeed") : 1.3f;
            float waveHeight = _lakeMaterial != null && _lakeMaterial.HasProperty("_WaveHeight") ? _lakeMaterial.GetFloat("_WaveHeight") : 0.28f;
            float waveSteepness = _lakeMaterial != null && _lakeMaterial.HasProperty("_WaveSteepness") ? _lakeMaterial.GetFloat("_WaveSteepness") : 2.8f;
            float waterlineOffset = _lakeMaterial != null && _lakeMaterial.HasProperty("_WaterlineOffset") ? _lakeMaterial.GetFloat("_WaterlineOffset") : 0.32f;

            // Sample water surface height at player's current X position
            float surfaceY = InkWaterMath.GetSurfaceY(pos, b.max.y, waterlineOffset, Time.time, waveSpeed, waveHeight, waveSteepness);
            float feetY = pos.y - 0.95f;
            float clearance = feetY - surfaceY;

            Vector3 vel = _rb.linearVelocity;
            float speed = vel.magnitude;
            bool isSwinging = _grapple != null && _grapple.IsSwinging;

            // Skimming condition: low above water with high velocity or pendulum swing
            if (clearance >= -0.1f && clearance <= maxSkimClearance && (speed >= minSkimSpeed || isSwinging))
            {
                if (Time.time - _lastWakeTime >= wakeInterval)
                {
                    _lastWakeTime = Time.time;
                    Vector3 wakePos = new Vector3(pos.x, surfaceY + 0.05f, b.min.z - 0.05f);

                    // 1. Kick up calligraphic cream foam droplets
                    if (InkSplatterManager.Instance != null)
                    {
                        InkSplatterManager.Instance.EmitWaterWake(wakePos, vel);
                    }

                    // 2. Spawn a momentary skimming wake streak sprite
                    if (_wakeSprite != null)
                    {
                        StartCoroutine(SpawnWakeStreakRoutine(wakePos, vel));
                    }
                }
            }
        }

        private IEnumerator SpawnWakeStreakRoutine(Vector3 pos, Vector3 vel)
        {
            var go = new GameObject("SkimWake_Streak");
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _wakeSprite;
            sr.color = new Color(0.96f, 0.94f, 0.88f, 0.75f);
            sr.sortingOrder = 3;

            float facing = Mathf.Sign(vel.x);
            if (facing == 0) facing = 1f;

            go.transform.localScale = new Vector3(facing * 0.45f, 0.5f, 1f);

            float duration = 0.35f;
            float elapsed = 0f;
            Vector3 startScale = go.transform.localScale;
            Vector3 targetScale = new Vector3(facing * 1.3f, 0.35f, 1f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                if (go == null) yield break;

                go.transform.localScale = Vector3.Lerp(startScale, targetScale, t);
                Color c = sr.color;
                c.a = Mathf.Lerp(0.75f, 0f, t);
                sr.color = c;

                yield return null;
            }

            if (go != null) Destroy(go);
        }

        private void HandlePlayerLanded(Vector3 landPoint)
        {
            if (_lakeRenderer == null) return;
            Bounds b = _lakeRenderer.bounds;

            // If landing on a bridge/ledge close above the water
            if (landPoint.x >= b.min.x - 2f && landPoint.x <= b.max.x + 2f)
            {
                if (landPoint.y <= b.max.y + 4.5f)
                {
                    // Kick up gentle mist and ink droplets
                    if (InkSplatterManager.Instance != null)
                    {
                        InkSplatterManager.Instance.EmitWaterWake(landPoint + Vector3.up * 0.1f, Vector3.up * 2f);
                    }
                }
            }
        }
    }
}

