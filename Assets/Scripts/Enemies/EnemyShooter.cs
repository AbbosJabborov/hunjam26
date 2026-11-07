using UnityEngine;
using Shift.Combat;
using Shift.Player;

namespace Shift.Enemies
{
    /// <summary>
    /// Basic shooter enemy. Remains stationed or patrols, detects the player,
    /// aims, and shoots slow-motion dodgeable projectiles.
    /// Exists across both dimensions.
    /// </summary>
    [RequireComponent(typeof(EnemyHealth))]
    public class EnemyShooter : MonoBehaviour
    {
        [Header("Detection & Targeting")]
        [SerializeField] private float detectionRange = 25f;
        [SerializeField] private LayerMask lineOfSightMask = ~0;

        [Header("Attack Settings")]
        [SerializeField] private float fireInterval = 2.0f;
        [SerializeField] private float projectileSpeed = 12f;
        [SerializeField] private float projectileDamage = 25f;
        [SerializeField] private Transform firePoint;
        [SerializeField] private GameObject projectilePrefab;

        [Header("Aiming")]
        [SerializeField] private float rotationSpeed = 5f;

        private Transform _playerTarget;
        private float _lastFireTime;
        private EnemyHealth _health;
        private EnemyVisuals _visuals;

        private void Awake()
        {
            _health = GetComponent<EnemyHealth>();
            _visuals = GetComponent<EnemyVisuals>();
            _lastFireTime = Time.time + Random.Range(0.2f, 1.0f); // Stagger initial shots
        }

        private void Start()
        {
            FindPlayer();

            if (firePoint == null)
            {
                var fp = new GameObject("FirePoint");
                fp.transform.SetParent(transform);
                fp.transform.localPosition = new Vector3(0f, 1.2f, 0.5f);
                firePoint = fp.transform;
            }
        }

        private void FindPlayer()
        {
            var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (player != null)
            {
                _playerTarget = player.transform;
            }
        }

        private void Update()
        {
            if (_health != null && !_health.IsAlive) return;

            if (_playerTarget == null)
            {
                FindPlayer();
                if (_playerTarget == null) return;
            }

            float dist = Vector3.Distance(transform.position, _playerTarget.position);
            if (dist <= detectionRange)
            {
                AimAtPlayer();

                if (Time.time - _lastFireTime >= fireInterval)
                {
                    _lastFireTime = Time.time;
                    Shoot();
                }
            }
        }

        private void AimAtPlayer()
        {
            Vector3 targetPos = _playerTarget.position + Vector3.up * 1.0f; // Aim at chest height
            Vector3 dir = (targetPos - transform.position).normalized;

            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private void Shoot()
        {
            if (_visuals != null)
            {
                _visuals.PlayAttack();
            }

            Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position + Vector3.up;
            Vector3 targetPos = _playerTarget.position + Vector3.up * 1.0f;
            Vector3 shootDir = (targetPos - spawnPos).normalized;

            GameObject projObj;
            if (projectilePrefab != null)
            {
                projObj = Instantiate(projectilePrefab, spawnPos, Quaternion.LookRotation(shootDir));
            }
            else
            {
                // Fallback procedural projectile
                projObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projObj.name = "EnemyBullet";
                projObj.transform.position = spawnPos;
                projObj.transform.rotation = Quaternion.LookRotation(shootDir);
                projObj.transform.localScale = Vector3.one * 0.4f;

                var r = projObj.GetComponent<Renderer>();
                if (r != null)
                {
                    var mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                    mat.color = new Color(1f, 0.2f, 0.1f); // Glowing red
                    r.material = mat;
                }
            }

            var proj = projObj.GetComponent<EnemyProjectile>();
            if (proj == null)
            {
                proj = projObj.AddComponent<EnemyProjectile>();
            }

            proj.Initialize(gameObject, projectileDamage, projectileSpeed);
        }

        public void SetFirePoint(Transform fp)
        {
            firePoint = fp;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionRange);
        }
    }
}
