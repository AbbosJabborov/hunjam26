using UnityEngine;
using Shift.Core;
using Shift.Cameras;
using Shift.Player;

namespace Shift.Combat
{
    /// <summary>
    /// Dual-dimension hand-drawn ink bullet projectile:
    /// - 2D Platformer (Orthographic): displays bullet_0, facing left/right along travel direction.
    /// - 3D FPS (Perspective): displays bullet_1, camera-billboarded flying into the scene.
    /// Supports continuous collision detection, slow-motion flight, and ink impact burst.
    /// </summary>
    public class BulletProjectile : MonoBehaviour
    {
        [Header("Sprites")]
        [SerializeField] private Sprite orthoSprite;       // bullet_0 (side view, default facing left)
        [SerializeField] private Sprite perspectiveSprite; // bullet_1 (front/perspective head-on view)
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Header("Projectile Settings")]
        [SerializeField] private float speed = 55f;
        [SerializeField] private float damage = 100f;
        [SerializeField] private float maxLifetime = 4.0f;
        [SerializeField] private float visualScale = 0.5f;
        [SerializeField] private LayerMask hitLayers = ~0;

        private Vector3 _direction = Vector3.forward;
        private bool _isPlayerBullet = true;
        private float _spawnTime;

        public void Initialize(Vector3 direction, float bulletDamage, bool isPlayer = true)
        {
            _direction = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            damage = bulletDamage;
            _isPlayerBullet = isPlayer;
            _spawnTime = Time.time;

            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            transform.localScale = Vector3.one * visualScale;

            UpdateVisualDimension(DimensionManager.Instance != null ? DimensionManager.Instance.CurrentDimension : DimensionType.FPS);
        }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            transform.localScale = Vector3.one * visualScale;
        }

        private void Start()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged += HandleDimensionChanged;
                UpdateVisualDimension(DimensionManager.Instance.CurrentDimension);
            }
            else
            {
                UpdateVisualDimension(DimensionType.FPS);
            }

            Destroy(gameObject, maxLifetime);
        }

        private void OnDestroy()
        {
            if (DimensionManager.Instance != null)
            {
                DimensionManager.Instance.OnDimensionChanged -= HandleDimensionChanged;
            }
        }

        private void HandleDimensionChanged(DimensionType dimension)
        {
            UpdateVisualDimension(dimension);
        }

        public void UpdateVisualDimension(DimensionType dimension)
        {
            if (spriteRenderer == null) return;

            bool isFps = (dimension == DimensionType.FPS);

            if (isFps)
            {
                if (perspectiveSprite != null) spriteRenderer.sprite = perspectiveSprite;
                spriteRenderer.flipX = false;
            }
            else
            {
                if (orthoSprite != null) spriteRenderer.sprite = orthoSprite;
                UpdateOrthoOrientation();
            }
        }

        private void Update()
        {
            // Moves using scaled time (slows in FPS mode for SUPERHOT deliberate bullet dodge feel)
            float stepDistance = speed * Time.deltaTime;
            Vector3 step = _direction * stepDistance;

            // Continuous collision check along step vector
            if (Physics.SphereCast(transform.position, 0.2f, _direction, out RaycastHit hit, stepDistance, hitLayers, QueryTriggerInteraction.Ignore))
            {
                // Ignore player if player bullet
                if (_isPlayerBullet && (hit.collider.CompareTag("Player") || hit.collider.GetComponentInParent<PlayerController>() != null))
                {
                    transform.position += step;
                    return;
                }

                // Ignore owner enemy if enemy bullet
                if (!_isPlayerBullet && hit.collider.GetComponentInParent<Enemies.EnemyHealth>() != null)
                {
                    transform.position += step;
                    return;
                }

                OnHit(hit);
                return;
            }

            transform.position += step;
        }

        private void LateUpdate()
        {
            if (spriteRenderer == null) return;

            bool isFps = DimensionManager.Instance != null && DimensionManager.Instance.IsFPS;

            if (isFps)
            {
                // FPS Mode: Billboard bullet to face the active camera
                Camera cam = CameraManager.Instance != null ? CameraManager.Instance.ActiveCamera : Camera.main;
                if (cam != null)
                {
                    transform.rotation = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);
                }
            }
            else
            {
                UpdateOrthoOrientation();
            }
        }

        private void UpdateOrthoOrientation()
        {
            if (spriteRenderer == null) return;

            // bullet_0 is drawn pointing left (-X)
            // If moving right (+X), flipX = true. If moving left (-X), flipX = false.
            bool movingRight = _direction.x >= 0f;
            spriteRenderer.flipX = movingRight;

            // Apply Z tilt if projectile travels diagonally
            float angle = Mathf.Atan2(_direction.y, Mathf.Abs(_direction.x)) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, movingRight ? angle : -angle);
        }

        private void OnHit(RaycastHit hit)
        {
            // Apply damage to IDamageable
            IDamageable damageable = hit.collider.GetComponentInParent<IDamageable>();
            if (damageable == null) damageable = hit.collider.GetComponent<IDamageable>();

            if (damageable != null)
            {
                damageable.TakeDamage(damage, hit.point, hit.normal);
            }

            // Spawn ink splatter
            if (VFX.InkSplatterManager.Instance != null)
            {
                VFX.InkSplatterManager.Instance.SpawnCombatHitSplatter();
                VFX.InkSplatterManager.Instance.EmitBurst(hit.point, 14, 4.5f, hit.normal);
            }

            Destroy(gameObject);
        }

        public void SetSprites(Sprite ortho, Sprite persp, SpriteRenderer sr)
        {
            orthoSprite = ortho;
            perspectiveSprite = persp;
            spriteRenderer = sr;
        }
    }
}

