using System.Collections;
using UnityEngine;
using Shift.Core;
using Shift.Cameras;
using Shift.Player;

namespace Shift.Combat
{
    /// <summary>
    /// Handles dual-dimension combat shooting:
    /// - FPS Mode: launches perspective 3D bullet (bullet_1) towards crosshair, with camera recoil.
    /// - Platformer Mode: launches orthographic 2D bullet (bullet_0) horizontally, triggers 2D shooting animation.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon Stats")]
        [SerializeField] private float damage = 100f; // High damage for decisive hits
        [SerializeField] private float fireRate = 4f; // Shots per second
        [SerializeField] private float maxRange = 150f;
        [SerializeField] private LayerMask hitLayers = ~0;

        [Header("Projectile & Visuals")]
        [SerializeField] private GameObject bulletPrefab;
        [SerializeField] private LineRenderer tracerLinePrefab;
        [SerializeField] private Transform firePoint;
        [SerializeField] private float tracerDuration = 0.05f;

        public event System.Action OnHitTarget;
        public event System.Action OnWeaponFired;

        private PlayerInputReader _input;
        private PlayerVisuals _visuals;
        private Collider[] _playerColliders;
        private float _lastFireTime = -999f;

        public void SetBulletPrefab(GameObject prefab)
        {
            bulletPrefab = prefab;
        }

        private void Awake()
        {
            _input = GetComponent<PlayerInputReader>();
            if (_input == null)
            {
                var controller = GetComponent<PlayerController>();
                if (controller != null) _input = controller.InputReader;
            }

            _visuals = GetComponentInChildren<PlayerVisuals>();
            _playerColliders = GetComponentsInChildren<Collider>(true);
        }

        private void Update()
        {
            // Shooting is strictly an FPV/FPS ability; disabled in 2D platformer mode
            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;
            if (!isFps) return;

            if (_input != null && (_input.ShootTriggered || _input.ShootHeld))
            {
                TryShoot();
            }
        }

        public bool TryShoot()
        {
            // Shooting is strictly allowed only in 3D perspective (FPV) mode
            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;
            if (!isFps)
            {
                return false;
            }

            // Use unscaledTime so fire rate remains responsive in slow motion
            if (Time.unscaledTime - _lastFireTime < (1f / fireRate))
            {
                return false;
            }

            _lastFireTime = Time.unscaledTime;
            OnWeaponFired?.Invoke();

            PerformShotFPS();

            return true;
        }

        private void PerformShotFPS()
        {
            Camera fpsCam = CameraManager.Instance != null ? CameraManager.Instance.FPSCamera : Camera.main;
            if (fpsCam == null) return;

            CameraManager.Instance?.ApplyRecoil(1.2f);

            Ray ray = new Ray(fpsCam.transform.position, fpsCam.transform.forward);
            Vector3 spawnPos = firePoint != null ? firePoint.position : ray.origin + ray.direction * 0.4f;

            if (bulletPrefab != null)
            {
                var bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(ray.direction));
                var bullet = bulletObj.GetComponent<BulletProjectile>();
                if (bullet != null)
                {
                    bullet.Initialize(ray.direction, damage, isPlayer: true);
                }
            }
            else
            {
                // Fallback instant raycast if no bullet prefab assigned
                PerformRaycastShot(ray, spawnPos);
            }
        }

        private void PerformShotPlatformer()
        {
            Vector3 facing = _visuals != null ? _visuals.FacingDirection : Vector3.right;
            Vector3 spawnPos = transform.position + Vector3.up * 1.0f + facing * 0.6f;

            _visuals?.TriggerShootAnimation();

            if (bulletPrefab != null)
            {
                var bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(facing));
                var bullet = bulletObj.GetComponent<BulletProjectile>();
                if (bullet != null)
                {
                    bullet.Initialize(facing, damage, isPlayer: true);
                }
            }
            else
            {
                Ray ray = new Ray(spawnPos, facing);
                PerformRaycastShot(ray, spawnPos);
            }
        }

        private void PerformRaycastShot(Ray ray, Vector3 origin)
        {
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
                if (damageable == null) damageable = validHit.collider.GetComponent<IDamageable>();

                if (damageable != null)
                {
                    damageable.TakeDamage(damage, validHit.point, validHit.normal);
                    OnHitTarget?.Invoke();
                }
            }

            StartCoroutine(ShowTracer(origin, endPoint));
        }

        private IEnumerator ShowTracer(Vector3 start, Vector3 end)
        {
            GameObject lineObj = new GameObject("BulletTracer");
            LineRenderer line = lineObj.AddComponent<LineRenderer>();
            line.startWidth = 0.04f;
            line.endWidth = 0.04f;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default"));
            line.material.color = new Color(1f, 0.85f, 0.2f, 1f);

            yield return new WaitForSecondsRealtime(tracerDuration);

            Destroy(lineObj);
        }
    }
}
