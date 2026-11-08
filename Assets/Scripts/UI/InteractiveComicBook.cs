using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Shift.UI
{
    public enum PageFlipRotationStyle
    {
        [Tooltip("3D Y-axis rotation around the center spine crease")]
        Rotate3D = 0,
        [Tooltip("2D flat cosine-width fold (strictly in-plane, zero perspective distortion)")]
        FlatScale2D = 1
    }

    /// <summary>
    /// Interactive Two-Page Comic Book Reader:
    /// - Realistic tactile book spread with Spine in center.
    /// - Spread 1: Left = Blank Parchment | Right = Comic Page 1.
    /// - Spread 2: Left = Comic Page 2     | Right = Comic Page 3.
    /// - Interactive drag & swipe cursor page flip with full flip-back support.
    /// - Zero Text (Game Jam twist): purely visual/tactile storytelling.
    /// - When on Spread 2, any key press triggers Iris Wipe transition into TutorialScene.
    /// </summary>
    public class InteractiveComicBook : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Comic Sprites")]
        [SerializeField] private Sprite comicPage1;
        [SerializeField] private Sprite comicPage2;
        [SerializeField] private Sprite comicPage3;
        [SerializeField] private Sprite blankParchmentSprite;

        [Header("UI Page Elements")]
        [SerializeField] private RectTransform bookContainer;
        [SerializeField] private Image leftPageImage;
        [SerializeField] private Image rightPageImage;
        [SerializeField] private RectTransform turningPageFlap;
        [SerializeField] private Image turningPageFront; // Shows page 1 turning
        [SerializeField] private Image turningPageBack;  // Shows page 2 on back of flap

        [Header("Spine & Shadow")]
        [SerializeField] private Image centerSpineShadow;
        [SerializeField] private Image pageTurnShadow;

        [Header("Tactile Flip Settings")]
        [SerializeField] private float flipDuration = 0.45f;
        [SerializeField] private float dragSensitivity = 0.85f;
        [SerializeField] private float flipThresholdFraction = 0.35f;

        [Header("Rotation Style Settings")]
        [Tooltip("Choose between full 3D Y-axis rotation or 2D flat cosine scaling")]
        [SerializeField] private PageFlipRotationStyle rotationStyle = PageFlipRotationStyle.Rotate3D;

        [Tooltip("Invert/flip the rotation direction (swaps between turning towards camera vs into screen, or clockwise vs counter-clockwise)")]
        [SerializeField] private bool flipRotationDirection = false;

        [Header("Visual Prompt (Zero Text)")]
        [SerializeField] private RectTransform dogEarPrompt; // Animated page corner curl on Spread 1
        [SerializeField] private RectTransform continuePrompt; // Ink eye pulse icon on Spread 2

        private int _currentSpread = 1; // 1 = (Blank | Page 1), 2 = (Page 2 | Page 3)
        private bool _isDragging = false;
        private bool _isAnimating = false;
        private Vector2 _dragStartPos;
        private float _currentFlapAngle = 0f; // 0 = flat on right, 180 = flat on left
        private Canvas _rootCanvas;
        private Coroutine _flipRoutine;
        private bool _inputLocked = false;

        public int CurrentSpread => _currentSpread;

        public PageFlipRotationStyle RotationStyle
        {
            get => rotationStyle;
            set => rotationStyle = value;
        }

        public bool FlipRotationDirection
        {
            get => flipRotationDirection;
            set => flipRotationDirection = value;
        }

        private void Awake()
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            LoadDefaultComicSprites();
            SetupPages();
        }

        private void Start()
        {
            SetSpreadImmediate(1);
        }

        private void Update()
        {
            if (_inputLocked || _isAnimating || _isDragging) return;

            // When player has reached Spread 2 (read entire comic), any keyboard or gamepad button triggers transition
            if (_currentSpread == 2)
            {
                if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1))
                {
                    TriggerTransitionToTutorial();
                }
            }

            // Animate dog-ear breathing curl on Spread 1
            if (dogEarPrompt != null && dogEarPrompt.gameObject.activeSelf)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(Time.unscaledTime * 4.5f);
                dogEarPrompt.localScale = new Vector3(pulse, pulse, 1f);
            }

            // Animate continue prompt pulse on Spread 2
            if (continuePrompt != null && continuePrompt.gameObject.activeSelf)
            {
                float pulse = 1f + 0.12f * Mathf.Sin(Time.unscaledTime * 3.5f);
                continuePrompt.localScale = new Vector3(pulse, pulse, 1f);
            }
        }

        public void LoadDefaultComicSprites()
        {
#if UNITY_EDITOR
            if (comicPage1 == null)
                comicPage1 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/comic/comic_page_01.png");
            if (comicPage2 == null)
                comicPage2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/comic/comic_page_02.png");
            if (comicPage3 == null)
                comicPage3 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/comic/comic_page_03.png");
            if (blankParchmentSprite == null)
                blankParchmentSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/comic/blank_parchment_page.png");
#endif
        }

        private void SetupPages()
        {
            if (turningPageFlap != null)
            {
                turningPageFlap.sizeDelta = new Vector2(650f, 920f);
                turningPageFlap.pivot = new Vector2(0f, 0.5f);
                turningPageFlap.anchoredPosition = Vector2.zero;
                turningPageFlap.localScale = Vector3.one;
                turningPageFlap.gameObject.SetActive(false);
            }

            if (turningPageFront != null)
            {
                RectTransform rt = turningPageFront.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.localScale = Vector3.one;
            }

            if (turningPageBack != null)
            {
                RectTransform rt = turningPageBack.rectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.localScale = new Vector3(-1f, 1f, 1f);
            }
        }

        public void SetSpreadImmediate(int spread)
        {
            _currentSpread = spread;
            _isAnimating = false;
            _isDragging = false;
            _currentFlapAngle = (spread == 1) ? 0f : 180f;

            if (turningPageFlap != null)
            {
                turningPageFlap.gameObject.SetActive(false);
            }

            if (spread == 1)
            {
                if (leftPageImage != null)
                {
                    leftPageImage.sprite = blankParchmentSprite;
                    leftPageImage.color = new Color(0.92f, 0.90f, 0.85f, 1f);
                }
                if (rightPageImage != null)
                {
                    rightPageImage.sprite = comicPage1;
                    rightPageImage.color = Color.white;
                }
                if (dogEarPrompt != null) dogEarPrompt.gameObject.SetActive(true);
                if (continuePrompt != null) continuePrompt.gameObject.SetActive(false);
            }
            else
            {
                if (leftPageImage != null)
                {
                    leftPageImage.sprite = comicPage2;
                    leftPageImage.color = Color.white;
                }
                if (rightPageImage != null)
                {
                    rightPageImage.sprite = comicPage3;
                    rightPageImage.color = Color.white;
                }
                if (dogEarPrompt != null) dogEarPrompt.gameObject.SetActive(false);
                if (continuePrompt != null) continuePrompt.gameObject.SetActive(true);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_inputLocked || _isAnimating) return;

            _dragStartPos = eventData.position;
            _isDragging = true;

            // Prepare flap for drag interaction
            if (turningPageFlap != null)
            {
                turningPageFlap.gameObject.SetActive(true);

                if (_currentSpread == 1)
                {
                    // Flipping from Spread 1 to Spread 2
                    // Front of flap = Page 1 turning away
                    // Back of flap = Page 2 turning into view
                    // Right page underneath = Page 3
                    if (turningPageFront != null) turningPageFront.sprite = comicPage1;
                    if (turningPageBack != null) turningPageBack.sprite = comicPage2;
                    if (rightPageImage != null) rightPageImage.sprite = comicPage3;
                    if (leftPageImage != null) leftPageImage.sprite = blankParchmentSprite;
                    SetFlapAngle(0f);
                }
                else
                {
                    // Flipping back from Spread 2 to Spread 1
                    // Flap starts at 180 (left side)
                    // Back of flap = Page 2 turning away
                    // Front of flap = Page 1 turning back into view
                    // Right page underneath = Page 3
                    if (turningPageFront != null) turningPageFront.sprite = comicPage1;
                    if (turningPageBack != null) turningPageBack.sprite = comicPage2;
                    if (rightPageImage != null) rightPageImage.sprite = comicPage3;
                    if (leftPageImage != null) leftPageImage.sprite = blankParchmentSprite;
                    SetFlapAngle(180f);
                }
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _inputLocked || _isAnimating) return;

            float deltaX = eventData.position.x - _dragStartPos.x;
            float bookWidth = bookContainer != null ? bookContainer.rect.width : Screen.width;
            float halfWidth = Mathf.Max(100f, bookWidth * 0.5f);

            if (_currentSpread == 1)
            {
                // Dragging left (negative deltaX) turns page 1 towards left (0 -> 180 deg)
                float fraction = Mathf.Clamp01(-deltaX / halfWidth);
                SetFlapAngle(fraction * 180f);
            }
            else
            {
                // Dragging right (positive deltaX) turns page back towards right (180 -> 0 deg)
                float fraction = Mathf.Clamp01(deltaX / halfWidth);
                SetFlapAngle(180f - fraction * 180f);
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_isDragging || _inputLocked || _isAnimating) return;
            _isDragging = false;

            float deltaX = eventData.position.x - _dragStartPos.x;
            float bookWidth = bookContainer != null ? bookContainer.rect.width : Screen.width;
            float halfWidth = Mathf.Max(100f, bookWidth * 0.5f);

            if (_currentSpread == 1)
            {
                // If dragged past threshold or simple click on right page
                bool shouldFlip = (Mathf.Abs(deltaX) < 15f && eventData.position.x > Screen.width * 0.45f) ||
                                  (_currentFlapAngle >= 180f * flipThresholdFraction);

                if (shouldFlip)
                {
                    AnimateFlapTo(180f, () => SetSpreadImmediate(2));
                }
                else
                {
                    AnimateFlapTo(0f, () => SetSpreadImmediate(1));
                }
            }
            else
            {
                // On Spread 2:
                // If dragged right past threshold or clicked on left page -> flip back
                bool shouldFlipBack = (_currentFlapAngle <= 180f * (1f - flipThresholdFraction));

                if (shouldFlipBack)
                {
                    AnimateFlapTo(0f, () => SetSpreadImmediate(1));
                }
                else
                {
                    AnimateFlapTo(180f, () => SetSpreadImmediate(2));

                    // If clicked anywhere on Spread 2 without dragging, advance to tutorial!
                    if (Mathf.Abs(deltaX) < 15f)
                    {
                        TriggerTransitionToTutorial();
                    }
                }
            }
        }

        public void SetFlapAngle(float angleDegrees)
        {
            _currentFlapAngle = Mathf.Clamp(angleDegrees, 0f, 180f);

            if (turningPageFlap == null) return;

            bool isPastHalf = _currentFlapAngle >= 90f;
            if (turningPageFront != null) turningPageFront.gameObject.SetActive(!isPastHalf);
            if (turningPageBack != null) turningPageBack.gameObject.SetActive(isPastHalf);

            float rad = _currentFlapAngle * Mathf.Deg2Rad;

            if (rotationStyle == PageFlipRotationStyle.Rotate3D)
            {
                // Sign determines clockwise vs counter-clockwise 3D sweep
                float sign = flipRotationDirection ? 1f : -1f;
                turningPageFlap.pivot = new Vector2(0f, 0.5f);
                turningPageFlap.anchoredPosition = Vector2.zero;
                turningPageFlap.localRotation = Quaternion.Euler(0f, sign * _currentFlapAngle, 0f);
                turningPageFlap.localScale = Vector3.one;

                if (turningPageBack != null)
                {
                    turningPageBack.rectTransform.localScale = new Vector3(-1f, 1f, 1f);
                }
            }
            else // FlatScale2D
            {
                turningPageFlap.localRotation = Quaternion.identity;
                float cosAngle = Mathf.Cos(rad);
                float scaleX = Mathf.Abs(cosAngle);

                if (!isPastHalf)
                {
                    // Front of page turning from right towards spine
                    turningPageFlap.pivot = new Vector2(0f, 0.5f);
                    turningPageFlap.anchoredPosition = Vector2.zero;
                    turningPageFlap.localScale = new Vector3(scaleX, 1f, 1f);
                }
                else
                {
                    // Back of page turning from spine towards left
                    turningPageFlap.pivot = new Vector2(1f, 0.5f);
                    turningPageFlap.anchoredPosition = Vector2.zero;
                    turningPageFlap.localScale = new Vector3(scaleX, 1f, 1f);
                    if (turningPageBack != null)
                    {
                        turningPageBack.rectTransform.localScale = Vector3.one;
                    }
                }
            }

            // Dynamic shadow under turning flap
            if (pageTurnShadow != null)
            {
                float shadowAlpha = Mathf.Sin(rad) * 0.45f;
                pageTurnShadow.color = new Color(0f, 0f, 0f, shadowAlpha);
                pageTurnShadow.gameObject.SetActive(shadowAlpha > 0.02f);
            }
        }

        private void AnimateFlapTo(float targetAngle, Action onDone)
        {
            if (_flipRoutine != null) StopCoroutine(_flipRoutine);
            _flipRoutine = StartCoroutine(FlapAnimationRoutine(targetAngle, onDone));
        }

        private IEnumerator FlapAnimationRoutine(float targetAngle, Action onDone)
        {
            _isAnimating = true;
            float startAngle = _currentFlapAngle;
            float delta = Mathf.Abs(targetAngle - startAngle);
            float duration = Mathf.Max(0.12f, flipDuration * (delta / 180f));

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                SetFlapAngle(Mathf.Lerp(startAngle, targetAngle, smoothT));
                yield return null;
            }

            SetFlapAngle(targetAngle);
            _isAnimating = false;
            _flipRoutine = null;
            onDone?.Invoke();
        }

        private void TriggerTransitionToTutorial()
        {
            if (_inputLocked) return;
            _inputLocked = true;

            // Determine screen UV center of the book
            Vector2 bookScreenUv = new Vector2(0.5f, 0.5f);
            if (bookContainer != null)
            {
                Vector3 screenPoint = RectTransformUtility.WorldToScreenPoint(null, bookContainer.position);
                bookScreenUv = new Vector2(
                    Mathf.Clamp01(screenPoint.x / Mathf.Max(1f, Screen.width)),
                    Mathf.Clamp01(screenPoint.y / Mathf.Max(1f, Screen.height))
                );
            }

            // Trigger universal Iris Transition to TutorialScene!
            if (IrisTransition.Instance != null)
            {
                IrisTransition.Instance.TransitionToScene("TutorialScene", bookScreenUv, "Player", 0.75f, 0.85f);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("TutorialScene");
            }
        }
    }
}
