using System.Collections;
using UnityEngine;
using Shift.Player;
using Shift.Combat;
using Shift.VFX;

namespace Shift.Environment
{
    /// <summary>
    /// Lethal Ink Lake hazard (Lava-like behavior).
    /// Instantly destroys/kills the player upon touching or entering the volume.
    /// Can be attached to a standard Cube, a subdivided mesh with InkMeshGenerator,
    /// or any water/hazard collider. Works in both 2D Orthographic and 3D FPS dimensions.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class InkLakeHazard : MonoBehaviour
    {
        [Header("Damage Settings")]
        [Tooltip("Lethal damage amount applied to instantly kill the player upon contact.")]
        [SerializeField] private float lethalDamage = 10000f;

        [Header("Hazard Detection")]
        [Tooltip("Whether to treat the collider as a trigger volume automatically on Awake.")]
        [SerializeField] private bool autoSetTrigger = true;

        [Tooltip("Also detect when player falls below the top surface plane.")]
        [SerializeField] private bool enableSurfaceSubmersionCheck = true;

        [Tooltip("Vertical tolerance above surface bounds before triggering death.")]
        [SerializeField] private float submersionTolerance = 0.15f;

        [Header("VFX & Feedback")]
        [Tooltip("Spawn ink splash particle burst on kill.")]
        [SerializeField] private bool spawnInkSplatter = true;

        [SerializeField] private int splatterParticleCount = 20;
        [SerializeField] private float splatterSpeed = 5.5f;

        private Collider _collider;
        private bool _hasKilledThisSession;
        private PlayerController _cachedPlayer;

        public Collider HazardCollider => _collider != null ? _collider : (_collider = GetComponent<Collider>());

        private void Awake()
        {
            _collider = GetComponent<Collider>();
            if (_collider != null && autoSetTrigger)
            {
                _collider.isTrigger = true;
            }
        }

        private void Start()
        {
            FindPlayer();
        }

        private void FindPlayer()
        {
            _cachedPlayer = Object.FindAnyObjectByType<PlayerController>();
        }

        private void OnTriggerEnter(Collider other)
        {
            ProcessContact(other.gameObject, other.ClosestPoint(transform.position));
        }

        private void OnTriggerStay(Collider other)
        {
            ProcessContact(other.gameObject, other.ClosestPoint(transform.position));
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector3 hitPoint = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            ProcessContact(collision.gameObject, hitPoint);
        }

        private void Update()
        {
            if (!enableSurfaceSubmersionCheck || _hasKilledThisSession) return;

            if (_cachedPlayer == null)
            {
                FindPlayer();
                if (_cachedPlayer == null) return;
            }

            // Check if player is horizontally within bounds and at or below surface Y
            Bounds b = _collider != null ? _collider.bounds : new Bounds(transform.position, transform.localScale);
            Vector3 playerPos = _cachedPlayer.transform.position;

            bool withinHorizontalBounds = (playerPos.x >= b.min.x && playerPos.x <= b.max.x) &&
                                          (playerPos.z >= b.min.z && playerPos.z <= b.max.z);

            if (withinHorizontalBounds && playerPos.y <= b.max.y + submersionTolerance && playerPos.y >= b.min.y - 1f)
            {
                ProcessContact(_cachedPlayer.gameObject, playerPos);
            }
        }

        private void ProcessContact(GameObject targetGo, Vector3 hitPoint)
        {
            if (_hasKilledThisSession) return;

            // Check for PlayerHealth or IDamageable
            var health = targetGo.GetComponentInParent<PlayerHealth>();
            if (health == null && targetGo.CompareTag("Player"))
            {
                health = Object.FindAnyObjectByType<PlayerHealth>();
            }

            if (health != null && health.IsAlive)
            {
                _hasKilledThisSession = true;
                Debug.Log($"[InkLakeHazard] Player submerged in deadly ink lake at {hitPoint}!");

                // Spawn illustrated ink splash and droplet VFX
                if (spawnInkSplatter)
                {
                    InkSplashEffect.Spawn(hitPoint);
                }

                // Apply lethal damage
                health.TakeDamage(lethalDamage, hitPoint, Vector3.up);

                // Subscribe to restart/death to reset flag
                health.OnPlayerDied += ResetDeathStateOnRestart;
            }
        }

        private void ResetDeathStateOnRestart()
        {
            StartCoroutine(ResetCooldownRoutine());
        }

        private IEnumerator ResetCooldownRoutine()
        {
            yield return new WaitForSeconds(1.5f);
            _hasKilledThisSession = false;
        }

        private void OnDestroy()
        {
            if (_cachedPlayer != null)
            {
                var health = _cachedPlayer.GetComponent<PlayerHealth>();
                if (health != null)
                {
                    health.OnPlayerDied -= ResetDeathStateOnRestart;
                }
            }
        }

        [ContextMenu("Setup Box Hazard Collider")]
        public void SetupBoxCollider()
        {
            var col = GetComponent<BoxCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<BoxCollider>();
            }
            col.isTrigger = true;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.1f, 0.4f, 0.9f, 0.25f);
            Collider c = GetComponent<Collider>();
            if (c != null)
            {
                Gizmos.DrawCube(c.bounds.center, c.bounds.size);
                Gizmos.color = new Color(0.2f, 0.8f, 1.0f, 0.8f);
                Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);

                // Top surface line
                Vector3 surfaceCenter = new Vector3(c.bounds.center.x, c.bounds.max.y, c.bounds.center.z);
                Vector3 surfaceSize = new Vector3(c.bounds.size.x, 0.05f, c.bounds.size.z);
                Gizmos.color = new Color(1.0f, 0.2f, 0.2f, 0.9f);
                Gizmos.DrawWireCube(surfaceCenter, surfaceSize);
            }
        }
#endif
    }
}

