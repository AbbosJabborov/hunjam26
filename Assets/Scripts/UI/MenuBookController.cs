using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Shift.UI
{
    /// <summary>
    /// Controls the MenuScene book:
    /// 1. Hovering idle state: gentle bobbing floating book on screen.
    /// 2. Interactive trigger: Left click or Any Key starts book opening animation.
    /// 3. Plays opening spritesheet animation (5 frames).
    /// 4. Fades away opening panel to reveal the Interactive Two-Page Comic Book Spread!
    /// </summary>
    public class MenuBookController : MonoBehaviour
    {
        [Header("Sprites & Animation")]
        [SerializeField] private Sprite closedBookCover;
        [SerializeField] private Sprite[] openingFrames;
        [SerializeField] private float frameDuration = 0.14f;

        [Header("Hovering Float Settings")]
        [SerializeField] private RectTransform bookTransform;
        [SerializeField] private RectTransform shadowTransform;
        [SerializeField] private float hoverAmplitude = 18f;
        [SerializeField] private float hoverFrequency = 2.2f;
        [SerializeField] private float tiltAmplitude = 1.6f;

        [Header("Panels & Targets")]
        [SerializeField] private Image bookImage;
        [SerializeField] private CanvasGroup openingPanelCanvasGroup;
        [SerializeField] private GameObject comicBookPanel;
        [SerializeField] private InteractiveComicBook comicBook;

        private Vector2 _initialBookPos;
        private Vector2 _initialShadowScale;
        private bool _isOpening = false;
        private bool _hasStarted = false;

        private void Awake()
        {
            if (bookTransform != null) _initialBookPos = bookTransform.anchoredPosition;
            if (shadowTransform != null) _initialShadowScale = shadowTransform.localScale;

            LoadDefaultSprites();
        }

        private void Start()
        {
            if (bookImage != null && closedBookCover != null)
            {
                bookImage.sprite = closedBookCover;
            }

            if (comicBookPanel != null)
            {
                comicBookPanel.SetActive(false);
            }

            if (openingPanelCanvasGroup != null)
            {
                openingPanelCanvasGroup.alpha = 1f;
                openingPanelCanvasGroup.interactable = true;
                openingPanelCanvasGroup.blocksRaycasts = true;
            }
        }

        public void LoadDefaultSprites()
        {
#if UNITY_EDITOR
            if (closedBookCover == null)
            {
                closedBookCover = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Menu/menu.png");
            }

            if (openingFrames == null || openingFrames.Length == 0)
            {
                var guids = UnityEditor.AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/Menu/opening_anim_spritesheet.png");
                var list = new System.Collections.Generic.List<Sprite>();
                foreach (var obj in guids)
                {
                    if (obj is Sprite s) list.Add(s);
                }
                // Sort by name so frames 0 through 4 are in exact sequence
                list.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));
                openingFrames = list.ToArray();
            }
#endif
        }

        private void Update()
        {
            if (_hasStarted) return;

            // 1. Idle Hovering Float
            if (!_isOpening && bookTransform != null)
            {
                float time = Time.unscaledTime;
                float offsetY = Mathf.Sin(time * hoverFrequency) * hoverAmplitude;
                float tiltZ = Mathf.Sin(time * hoverFrequency * 0.7f) * tiltAmplitude;

                bookTransform.anchoredPosition = _initialBookPos + new Vector2(0f, offsetY);
                bookTransform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);

                // Shadow scales inversely with hover elevation
                if (shadowTransform != null)
                {
                    float shadowFactor = 1f - (offsetY / hoverAmplitude) * 0.12f;
                    shadowTransform.localScale = new Vector3(_initialShadowScale.x * shadowFactor, _initialShadowScale.y * shadowFactor, 1f);
                }
            }

            // 2. Click or Any Key Trigger
            if (!_isOpening)
            {
                if (Input.GetMouseButtonDown(0) || Input.anyKeyDown)
                {
                    StartBookOpening();
                }
            }
        }

        public void StartBookOpening()
        {
            if (_isOpening || _hasStarted) return;
            _isOpening = true;
            _hasStarted = true;

            StartCoroutine(OpenBookSequenceRoutine());
        }

        private IEnumerator OpenBookSequenceRoutine()
        {
            // Settle hover smoothly to center
            if (bookTransform != null)
            {
                bookTransform.anchoredPosition = _initialBookPos;
                bookTransform.localRotation = Quaternion.identity;
            }

            // Play 5 opening animation frames
            if (openingFrames != null && openingFrames.Length > 0 && bookImage != null)
            {
                for (int i = 0; i < openingFrames.Length; i++)
                {
                    bookImage.sprite = openingFrames[i];
                    yield return new WaitForSecondsRealtime(frameDuration);
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(0.5f);
            }

            // Activate the Interactive Comic Book spread behind/ready
            if (comicBookPanel != null)
            {
                comicBookPanel.SetActive(true);
                if (comicBook != null)
                {
                    comicBook.SetSpreadImmediate(1);
                }
            }

            // Fade away the opening book panel
            float fadeDuration = 0.28f;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Clamp01(1f - (elapsed / fadeDuration));
                if (openingPanelCanvasGroup != null)
                {
                    openingPanelCanvasGroup.alpha = alpha;
                }
                yield return null;
            }

            if (openingPanelCanvasGroup != null)
            {
                openingPanelCanvasGroup.alpha = 0f;
                openingPanelCanvasGroup.interactable = false;
                openingPanelCanvasGroup.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }
    }
}

