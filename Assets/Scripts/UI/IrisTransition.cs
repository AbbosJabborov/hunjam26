using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

namespace Shift.UI
{
    /// <summary>
    /// Universal Iris Wipe Transition Manager:
    /// - Shrinks screen down to a transparent circle hole into an object (e.g. book or player).
    /// - Carries across scenes via DontDestroyOnLoad.
    /// - Expands circle hole back open outward from a target object (e.g. player in TutorialScene).
    /// - Dynamically sizes radius to clear all 4 screen corners on any aspect ratio.
    /// - Disables overlay graphic upon full expansion for zero GPU overhead and clean visuals.
    /// </summary>
    public class IrisTransition : MonoBehaviour
    {
        public static IrisTransition Instance { get; private set; }

        [Header("Transition Settings")]
        [SerializeField] private float defaultCloseDuration = 0.75f;
        [SerializeField] private float defaultOpenDuration = 0.85f;
        [SerializeField] private Color inkOverlayColor = new Color(0.015f, 0.015f, 0.02f, 1f);

        private Canvas _canvas;
        private Image _overlayImage;
        private Material _irisMaterial;
        private Coroutine _currentTransition;
        private float _currentRadius = 2.85f;

        public bool IsTransitioning { get; private set; }
        public float CurrentRadius => _currentRadius;

        public float GetMaxOpenRadius()
        {
            float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
            // Distance from corner (0,0) to opposite corner (1,1) in aspect-corrected UV space + margin for wobble
            return Mathf.Sqrt(aspect * aspect + 1f) + 0.45f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[IrisTransition_Manager]");
                Instance = go.AddComponent<IrisTransition>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            SetupUI();
        }

        private void SetupUI()
        {
            if (_canvas != null) return;

            // 1. Create Top-Level Overlay Canvas
            _canvas = gameObject.GetComponent<Canvas>();
            if (_canvas == null) _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 9999; // Top-most rendering layer

            var scaler = gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            var raycaster = gameObject.GetComponent<GraphicRaycaster>();
            if (raycaster == null) raycaster = gameObject.AddComponent<GraphicRaycaster>();

            // 2. Fullscreen Overlay Image
            var imgGo = new GameObject("IrisOverlayImage");
            imgGo.transform.SetParent(transform, false);
            var rt = imgGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            _overlayImage = imgGo.AddComponent<Image>();
            _overlayImage.raycastTarget = false;

            // 3. Iris Material
            var shader = Shader.Find("UI/IrisWipe");
            if (shader == null)
            {
                Debug.LogError("[IrisTransition] Shader UI/IrisWipe not found!");
                return;
            }

            float maxRad = GetMaxOpenRadius();
            _irisMaterial = new Material(shader);
            _irisMaterial.SetColor("_Color", inkOverlayColor);
            _irisMaterial.SetFloat("_Radius", maxRad);
            _irisMaterial.SetVector("_Center", new Vector4(0.5f, 0.5f, 0, 0));
            UpdateAspectRatio();

            _overlayImage.material = _irisMaterial;
            _currentRadius = maxRad;
            // Initially fully open and dormant
            _overlayImage.enabled = false;
        }

        private void UpdateAspectRatio()
        {
            if (_irisMaterial != null)
            {
                float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height);
                _irisMaterial.SetFloat("_AspectRatio", aspect);
            }
        }

        /// <summary>
        /// Smoothly shrinks the iris circle hole down to 0 into target screen UV (0,0 to 1,1).
        /// </summary>
        public void CloseTo(Vector2 screenUv, float duration = -1f, Action onComplete = null)
        {
            if (duration <= 0f) duration = defaultCloseDuration;
            if (_currentTransition != null) StopCoroutine(_currentTransition);
            _currentTransition = StartCoroutine(AnimateIrisRoutine(screenUv, _currentRadius, 0.0f, duration, onComplete));
        }

        /// <summary>
        /// Smoothly expands the iris circle hole outward from target screen UV until all corners are clear.
        /// </summary>
        public void OpenFrom(Vector2 screenUv, float duration = -1f, Action onComplete = null)
        {
            if (duration <= 0f) duration = defaultOpenDuration;
            if (_currentTransition != null) StopCoroutine(_currentTransition);
            float maxRad = GetMaxOpenRadius();
            _currentTransition = StartCoroutine(AnimateIrisRoutine(screenUv, _currentRadius, maxRad, duration, onComplete));
        }

        private IEnumerator AnimateIrisRoutine(Vector2 centerUv, float startRad, float targetRad, float duration, Action onComplete)
        {
            IsTransitioning = true;
            UpdateAspectRatio();

            if (_overlayImage != null)
            {
                _overlayImage.enabled = true;
                _overlayImage.raycastTarget = targetRad < 1.0f;
            }

            if (_irisMaterial != null)
            {
                _irisMaterial.SetVector("_Center", new Vector4(centerUv.x, centerUv.y, 0, 0));
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smoothT = -(Mathf.Cos(Mathf.PI * t) - 1f) * 0.5f;
                _currentRadius = Mathf.Lerp(startRad, targetRad, smoothT);

                if (_irisMaterial != null)
                {
                    _irisMaterial.SetFloat("_Radius", _currentRadius);
                }
                yield return null;
            }

            _currentRadius = targetRad;
            if (_irisMaterial != null)
            {
                _irisMaterial.SetFloat("_Radius", _currentRadius);
            }

            // If fully open, disable overlay image completely so no edge pixels linger
            float maxRad = GetMaxOpenRadius();
            if (_currentRadius >= maxRad - 0.1f)
            {
                if (_overlayImage != null)
                {
                    _overlayImage.enabled = false;
                    _overlayImage.raycastTarget = false;
                }
            }
            else
            {
                if (_overlayImage != null)
                {
                    _overlayImage.raycastTarget = _currentRadius < 1.0f;
                }
            }

            IsTransitioning = false;
            _currentTransition = null;
            onComplete?.Invoke();
        }

        /// <summary>
        /// Universal Scene Transition:
        /// 1. Shrinks into closeScreenUv
        /// 2. Loads sceneName asynchronously
        /// 3. Finds target object in new scene and expands outward from it!
        /// </summary>
        public void TransitionToScene(string sceneName, Vector2 closeScreenUv, string targetNameOrTag = "Player", float closeDuration = -1f, float openDuration = -1f)
        {
            if (closeDuration <= 0f) closeDuration = defaultCloseDuration;
            if (openDuration <= 0f) openDuration = defaultOpenDuration;

            if (_currentTransition != null) StopCoroutine(_currentTransition);
            _currentTransition = StartCoroutine(SceneTransitionRoutine(sceneName, closeScreenUv, targetNameOrTag, closeDuration, openDuration));
        }

        private IEnumerator SceneTransitionRoutine(string sceneName, Vector2 closeScreenUv, string targetNameOrTag, float closeDur, float openDur)
        {
            IsTransitioning = true;

            // Ensure overlay image is active
            if (_overlayImage != null) _overlayImage.enabled = true;

            float maxRad = GetMaxOpenRadius();
            float currentStartRad = (_currentRadius > 0.1f) ? _currentRadius : maxRad;

            // 1. Shrink iris into close target
            yield return StartCoroutine(AnimateIrisRoutine(closeScreenUv, currentStartRad, 0.0f, closeDur, null));

            // 2. Load scene asynchronously while screen is 100% black
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);
            while (!asyncLoad.isDone)
            {
                yield return null;
            }

            // 3. Wait 1 frame for camera & player initialization in new scene
            yield return null;

            // 4. Resolve target in new scene
            Vector2 openUv = new Vector2(0.5f, 0.5f);
            Transform targetTransform = null;

            var tagObj = GameObject.FindWithTag(targetNameOrTag);
            if (tagObj != null) targetTransform = tagObj.transform;

            if (targetTransform == null)
            {
                var namedObj = GameObject.Find(targetNameOrTag);
                if (namedObj != null) targetTransform = namedObj.transform;
            }

            Camera targetCam = Camera.main;
            if (targetCam == null)
            {
                var cm = FindAnyObjectByType<Shift.Cameras.CameraManager>();
                if (cm != null) targetCam = cm.ActiveCamera;
            }

            if (targetCam != null && targetTransform != null)
            {
                Vector3 vp = targetCam.WorldToViewportPoint(targetTransform.position);
                openUv = new Vector2(Mathf.Clamp01(vp.x), Mathf.Clamp01(vp.y));
            }

            // 5. Expand iris out from target in new scene until fully off-screen!
            maxRad = GetMaxOpenRadius();
            yield return StartCoroutine(AnimateIrisRoutine(openUv, 0.0f, maxRad, openDur, null));

            IsTransitioning = false;
            _currentTransition = null;
        }

        public void SetInstantRadius(float radius, Vector2 centerUv)
        {
            _currentRadius = radius;
            if (_irisMaterial != null)
            {
                UpdateAspectRatio();
                _irisMaterial.SetFloat("_Radius", radius);
                _irisMaterial.SetVector("_Center", new Vector4(centerUv.x, centerUv.y, 0, 0));
            }
            if (_overlayImage != null)
            {
                float maxRad = GetMaxOpenRadius();
                _overlayImage.enabled = radius < maxRad - 0.05f;
                _overlayImage.raycastTarget = radius < 1.0f;
            }
        }
    }
}
