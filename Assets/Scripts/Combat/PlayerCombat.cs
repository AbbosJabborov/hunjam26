using System.Collections;
using UnityEngine;
using Shift.Core;
using Shift.Cameras;
using Shift.Player;

namespace Shift.Combat
{
    /// <summary>
    /// Handles FPS raycast shooting, weapon cooldown, bullet tracers, and damage application.
    /// Only active when the player is in FPS dimension.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon Stats")]
        [SerializeField] private float damage = 100f; // One-shot kill as requested
        [SerializeField] private float fireRate = 4f; // Shots per second
        [SerializeField] private float maxRange = 150f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Visual Feedback")]
        [SerializeField] private LineRenderer tracerLinePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float tracerDuration = 0.05f;

        public event System.Action OnHitTarget;
        public event System.Action OnWeaponFired;

        private PlayerInputReader _input;
        private Collider[] _playerColliders;
        private float _lastFireTime = -999f;

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            if (_input == null)
            {
                var controller = GetComponent<PlayerController>();
                if (controller != null) _input = controller.InputReader;
            }

            _playerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            // Combat only permitted in FPS dimension
            if (DimensionManager.Instance == null || !DimensionManager.Instance.IsFPS) return;

            if (_input != null && (_input.ShootTriggered || _input.ShootHeld))
            {
                TryShoot();
            }
        }

        public bool TryShoot()
        {
            // Use unscaledTime so fire rate remains responsive in slow motion
            if (Time.unscaledTime - _lastFireTime < (1f / fireRate))
            {
                return false;
            }

            _lastFireTime = Time.unscaledTime;
            OnWeaponFired?.Invoke();
            PerformShot();
            return true;
        }

        private void PerformShot()
        {
            Camera fpsCam = CameraManager.Instance != null ? CameraManager.Instance.FPSCamera : Camera.main;
            if (fpsCam == null) return;

            CameraManager.Instance?.ApplyRecoil(1.2f);

            Ray ray = new Ray(fpsCam.transform.position, fpsCam.transform.forward);
            Vector3 origin = firePoint != null ? firePoint.position : ray.origin;
            Vector3 endPoint = ray.origin + ray.direction * maxRange;

            if (_playerColliders == null || _playerColliders.Length == 0)
            {
                _playerColliders = GetComponentsInChildren<Collider>(true);
            }

            RaycastHit[] hits = Physics.RaycastAll(ray, maxRange, hitLayers, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit validHit = default;
            bool hitFound = false;

            foreach (var h in hits)
            {
                // Skip player colliders
                bool isPlayer = false;
                if (h.collider.transform.IsChildOf(transform) || h.collider.gameObject == gameObject)
                {
                    isPlayer = true;
                }
                else
                {
                    for (int i = 0; i < _playerColliders.Length; i++)
                    {
                        if (_playerColliders[i] == h.collider)
                        {
                            isPlayer = true;
                            break;
                        }
                    }
                }

                if (!isPlayer)
                {
                    validHit = h;
                    hitFound = true;
                    break;
                }
            }

            if (hitFound)
            {
                endPoint = validHit.point;

                IDamageable damageable = validHit.collider.GetComponentInParent<IDamageable>();
                if (damageable == null)
                {
                    damageable = validHit.collider.GetComponent<IDamageable>();
                }

                if (damageable != null)
                {
                    damageable.TakeDamage(damage, validHit.point, validHit.normal);
                    OnHitTarget?.Invoke();
                    Debug.Log($"[PlayerCombat] Hit {validHit.collider.name} for {damage} damage! Target destroyed/hit.");
                }
            }

            StartCoroutine(ShowTracer(origin, endPoint));
        }

        private IEnumerator ShowTracer(Vector3 start, Vector3 end)
        {
            // Create a temporary tracer line if no prefab assigned
            GameObject lineObj = new GameObject("BulletTracer");
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            line.material.color = new Color(1f, 0.85f, 0.2f, 1f); // Bright golden tracer

            yield return new WaitForSecondsRealtime(tracerDuration);

            Destroy(lineObj);
        }
    }
}

