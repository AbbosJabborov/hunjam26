using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace Shift.UI
{
    /// <summary>
    /// Two-frame sketchy animated crosshair:
    /// - Alternates between two spritesheet frames with hand-drawn ink boil timing.
    /// - Operates in unscaled time (works in menus, pause, transitions).
    /// - Optional mouse cursor following for 2D platformer mode and menus.
    /// - Raycast-transparent to ensure clicks and drags pass cleanly through.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class AnimatedCrosshair : MonoBehaviour
    {
        [Header("Spritesheet Frames")]
        [Tooltip("The two sketchy hand-drawn crosshair frames to cycle between")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("Animation speed in frames per second (4-8 gives authentic boiling ink effect)")]
        [SerializeField] private float frameRate = 6f;

        [Header("Cursor Following")]
        [Tooltip("When enabled, this crosshair moves with the mouse cursor")]
        [SerializeField] private bool followMouse = false;

        [Tooltip("Whether to hide the OS hardware cursor while this crosshair is active")]
        [SerializeField] private bool hideHardwareCursor = true;

        [Tooltip("Whether to hide if the cursor is locked (e.g. in FPS perspective mode)")]
        [SerializeField] private bool autoHideWhenCursorLocked = true;

        private Image _image;
        private RectTransform _rectTransform;
        private Canvas _canvas;
        private Camera _canvasCamera;

        public Sprite[] Frames
        {
            get => frames;
            set => frames = value;
        }

        public bool FollowMouse
        {
            get => followMouse;
            set => followMouse = value;
        }

        public bool HideHardwareCursor
        {
            get => hideHardwareCursor;
            set => hideHardwareCursor = value;
        }

        public bool AutoHideWhenCursorLocked
        {
            get => autoHideWhenCursorLocked;
            set => autoHideWhenCursorLocked = value;
        }

        private void Awake()
        {
            _image = GetComponent<Image>();
            _rectTransform = GetComponent<RectTransform>();

            if (_image != null)
            {
                _image.raycastTarget = false;
            }

            CacheCanvas();
            EnsureFramesLoaded();
        }

        private void OnEnable()
        {
            CacheCanvas();
            if (followMouse && hideHardwareCursor)
            {
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (followMouse && hideHardwareCursor)
            {
                Cursor.visible = true;
            }
        }

        private void Start()
        {
            CacheCanvas();
            EnsureFramesLoaded();
        }

        private void Update()
        {
            AnimateFrames();
        }

        private void LateUpdate()
        {
            if (followMouse)
            {
                UpdatePosition();
            }
        }

        private void AnimateFrames()
        {
            if (frames == null || frames.Length == 0 || _image == null) return;

            // Cycle based on unscaled time
            int index = Mathf.FloorToInt(Time.unscaledTime * frameRate) % frames.Length;
            if (index >= 0 && index < frames.Length && frames[index] != null)
            {
                if (_image.sprite != frames[index])
                {
                    _image.sprite = frames[index];
                }
            }
        }

        private void UpdatePosition()
        {
            // If cursor is locked (FPS perspective mode), hide the mouse-following crosshair
            if (autoHideWhenCursorLocked && Cursor.lockState == CursorLockMode.Locked)
            {
                if (_image.enabled) _image.enabled = false;
                return;
            }

            if (!_image.enabled) _image.enabled = true;

            if (hideHardwareCursor && Cursor.visible)
            {
                Cursor.visible = false;
            }

            Vector2 mousePos = Vector2.zero;
            if (Mouse.current != null)
            {
                mousePos = Mouse.current.position.ReadValue();
            }
            else
            {
                mousePos = Input.mousePosition;
            }

            if (_canvas == null) CacheCanvas();

            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                _rectTransform.position = new Vector3(mousePos.x, mousePos.y, 0f);
            }
            else if (_canvas != null)
            {
                RectTransform parentRect = _rectTransform.parent as RectTransform;
                if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect, mousePos, _canvasCamera, out Vector2 localPoint))
                {
                    _rectTransform.anchoredPosition = localPoint;
                }
                else
                {
                    _rectTransform.position = new Vector3(mousePos.x, mousePos.y, 0f);
                }
            }
            else
            {
                _rectTransform.position = new Vector3(mousePos.x, mousePos.y, 0f);
            }
        }

        private void CacheCanvas()
        {
            _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
            {
                _canvasCamera = (_canvas.renderMode == RenderMode.ScreenSpaceCamera) ? _canvas.worldCamera : null;
            }
        }

        public void EnsureFramesLoaded()
        {
            if (frames != null && frames.Length >= 2 && frames[0] != null && frames[1] != null) return;

#if UNITY_EDITOR
            var list = new List<Sprite>();
            string[] searchPaths = new string[]
            {
                "Assets/Sprites/crosshair_spritesheet.png",
                "Assets/Sprites/crosshair_spirtesheet.png"
            };

            foreach (var path in searchPaths)
            {
                var assets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets != null && assets.Length > 0)
                {
                    foreach (var a in assets)
                    {
                        if (a is Sprite s) list.Add(s);
                    }
                    if (list.Count >= 2) break;
                }
            }

            if (list.Count >= 2)
            {
                frames = list.ToArray();
                if (_image != null && _image.sprite == null)
                {
                    _image.sprite = frames[0];
                }
            }
#endif
        }
    }
}
