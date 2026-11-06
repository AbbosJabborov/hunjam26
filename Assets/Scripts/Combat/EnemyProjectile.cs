using UnityEngine;
using Shift.Player;

namespace Shift.Combat
{
    /// <summary>
    /// Projectile fired by enemies. Moves using scaled Time.deltaTime,
    /// so in FPS dimension slow-motion (timeScale 0.25) it slows down noticeably,
    /// enabling SUPERHOT-style bullet dodging.
    /// </summary>
    [RequireComponent(typeof(SphereCollider))]
    public class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 14f;
        [SerializeField] private float damage = 20f;
        [SerializeField] private float maxLifetime = 6f;

        private GameObject _owner;
        private float _spawnTime;

        public void Initialize(GameObject owner, float projectileDamage, float projectileSpeed)
        {
            _owner = owner;
            damage = projectileDamage;
            speed = projectileSpeed;
            _spawnTime = Time.time;
        }

        private void Start()
        {
            var col = GetComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.25f;

            Destroy(gameObject, maxLifetime);
        }

        private void Update()
        {
            // Translates using scaled time (slows in FPS mode)
            transform.position += transform.forward * (speed * Time.deltaTime);
        }

        private void OnTriggerEnter(Collider other)
        {
            // Ignore owner or other projectiles
            if (other.gameObject == _owner || other.transform.IsChildOf(_owner != null ? _owner.transform : transform))
            {
                return;
            }

            if (other.GetComponent<EnemyProjectile>() != null) return;

            // Damage player if hit
            if (other.TryGetComponent<PlayerHealth>(out var playerHealth))
            {
                playerHealth.TakeDamage(damage, transform.position, -transform.forward);
                Destroy(gameObject);
                return;
            }

            if (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<PlayerHealth>(out var rbHealth))
            {
                rbHealth.TakeDamage(damage, transform.position, -transform.forward);
                Destroy(gameObject);
                return;
            }

            // Hit solid geometry
            if (!other.isTrigger)
            {
                Destroy(gameObject);
            }
        }
    }
}

