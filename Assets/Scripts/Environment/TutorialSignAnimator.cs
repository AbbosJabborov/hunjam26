using UnityEngine;

namespace Shift.Environment
{
    /// <summary>
    /// Handles the visual presentation and atmospheric hanging sway of tutorial banner signs.
    /// Animates between two spritesheet frames and provides subtle pendulum physics.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class TutorialSignAnimator : MonoBehaviour
    {
        [Header("Animation Frames")]
        [Tooltip("The two keyframe sprites (idle/prompt and action)")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Frame duration in seconds (0.4s to 0.5s gives ideal reading time)")]
        [SerializeField] private float frameDuration = 0.45f;

        [Header("Atmospheric Hanging Sway")]
        [SerializeField] private bool enableSway = true;
        [SerializeField] private float swayAngle = 1.25f;
        [SerializeField] private float swaySpeed = 1.4f;

        private SpriteRenderer _spriteRenderer;
        private Animator _animator;
        private float _timer = 0f;
        private int _frameIndex = 0;
        private float _phaseOffset;
        private Quaternion _initialRotation;

        public Sprite[] Frames
        {
            get => frames;
            set => frames = value;
        }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _animator = GetComponent<Animator>();
            _initialRotation = transform.localRotation;
            _phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Start()
        {
            if (frames != null && frames.Length > 0 && _spriteRenderer != null)
            {
                _spriteRenderer.sprite = frames[0];
            }
        }

        private void Update()
        {
            // If an Animator component with a valid controller is present, it drives sprite changes.
            // Otherwise, this script drives the 2-frame sprite cycle directly.
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                AnimateSpriteFrames();
            }

            if (enableSway)
            {
                ApplyAtmosphericSway();
            }
        }

        private void AnimateSpriteFrames()
        {
            if (frames == null || frames.Length == 0 || _spriteRenderer == null) return;

            _timer += Time.deltaTime;
            if (_timer >= frameDuration)
            {
                _timer -= frameDuration;
                _frameIndex = (_frameIndex + 1) % frames.Length;
                _spriteRenderer.sprite = frames[_frameIndex];
            }
        }

        private void ApplyAtmosphericSway()
        {
            float sway = Mathf.Sin(Time.time * swaySpeed + _phaseOffset) * swayAngle;
            transform.localRotation = _initialRotation * Quaternion.Euler(0f, 0f, sway);
        }
    }
}

