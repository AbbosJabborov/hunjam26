using System;
using UnityEngine;
using Shift.Cameras;
using Shift.Core;

namespace Shift.Environment
{
    /// <summary>
    /// Multi-plane parallax background controller for the "Ink Flood" sketchbook aesthetic.
    /// Manages 3 distinct visual depth planes:
    /// - Far Background: Mountain horizon and aged paper canvas (always covers the camera frustum).
    /// - Mid Clouds: Floating ink clouds in the sky with wind drift, anchored in the sky.
    /// - Foreground: Architectural silhouettes and rooftops anchored firmly to the ground so they never jump.
    /// Driven directly by CameraManager for 0-latency jitter-free scrolling.
    /// </summary>
    [ExecuteAlways]
    [DefaultExecutionOrder(100)]
    public class ParallaxBackground : MonoBehaviour
    {
        public static ParallaxBackground Instance { get; private set; }

        [System.Serializable]
        public class ParallaxLayer
        {
            public string name;
            public Renderer layerRenderer;
            [Tooltip("Horizontal parallax factor (lower = further away, moves slower)")]
            public float parallaxX = 0.005f;
            [Tooltip("Automatic wind drift speed in UV units per second")]
            public float autoScrollSpeedX = 0f;
            [Tooltip("Texture tiling across the plane")]
            public Vector2 tiling = new Vector2(2f, 1f);
            [Tooltip("Base UV offset")]
            public Vector2 baseOffset = Vector2.zero;

            [Header("Geometry & Positioning")]
            [Tooltip("Width of this quad plane (large enough to never expose edges)")]
            public float planeWidth = 160f;
            [Tooltip("Height of this quad plane")]
            public float planeHeight = 70f;
            [Tooltip("If true, plane follows camera Y so it always covers the viewport (ideal for base parchment canvas). If false, stays at fixed world Y (ideal for ground houses/sky clouds).")]
            public bool followCameraY = false;
            [Tooltip("Vertical follow multiplier when followCameraY is true")]
            public float verticalRatio = 1.0f;
            [Tooltip("World Y anchor (when followCameraY is false) or vertical offset relative to camera (when followCameraY is true)")]
            public float yAnchor = 0f;
            [Tooltip("Z depth in world space")]
            public float zDepth = 25f;

            [HideInInspector] public MaterialPropertyBlock propBlock;
        }

        [Header("Target & Camera (Fallback if CameraManager not used)")]
        [SerializeField] private Camera targetCamera;

        [Header("Parallax Depth Layers")]
        [SerializeField] private ParallaxLayer backgroundLayer = new ParallaxLayer
        {
            name = "Far Background",
            parallaxX = 0.003f,
            autoScrollSpeedX = 0f,
            tiling = new Vector2(2.0f, 1.0f),
            baseOffset = Vector2.zero,
            planeWidth = 160f,
            planeHeight = 85f,
            followCameraY = true, // Follows camera Y to guarantee aged parchment paper covers 100% of the screen
            verticalRatio = 1.0f,
            yAnchor = 0f,
            zDepth = 30f
        };

        [SerializeField] private ParallaxLayer cloudsLayer = new ParallaxLayer
        {
            name = "Mid Clouds",
            parallaxX = 0.008f,
            autoScrollSpeedX = 0.008f, // Gentle continuous atmospheric wind drift
            tiling = new Vector2(2.0f, 1.0f),
            baseOffset = new Vector2(0f, 0.05f),
            planeWidth = 160f,
            planeHeight = 45f,
            followCameraY = false, // Anchored in the sky; does not bounce when player jumps
            yAnchor = 17f,
            zDepth = 25f
        };

        [SerializeField] private ParallaxLayer foregroundLayer = new ParallaxLayer
        {
            name = "Near Foreground",
            parallaxX = 0.018f,
            autoScrollSpeedX = 0f,
            tiling = new Vector2(2.5f, 1.0f),
            baseOffset = new Vector2(0f, -0.05f),
            planeWidth = 160f,
            planeHeight = 55f,
            followCameraY = false, // Anchored firmly to ground floor; houses NEVER jump with player!
            yAnchor = 10f,
            zDepth = 20f
        };

        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");
        private int _lastUpdatedFrame = -1;

        private void Awake()
        {
            Instance = this;
            InitPropertyBlocks();
        }

        private void OnEnable()
        {
            Instance = this;
            InitPropertyBlocks();
        }

        private void Start()
        {
            Instance = this;
            InitPropertyBlocks();
            FindActiveCamera();
        }

        private void InitPropertyBlocks()
        {
            if (backgroundLayer.propBlock == null) backgroundLayer.propBlock = new MaterialPropertyBlock();
            if (cloudsLayer.propBlock == null) cloudsLayer.propBlock = new MaterialPropertyBlock();
            if (foregroundLayer.propBlock == null) foregroundLayer.propBlock = new MaterialPropertyBlock();
        }

        private void FindActiveCamera()
        {
            if (CameraManager.Instance != null && CameraManager.Instance.ActiveCamera != null)
            {
                targetCamera = CameraManager.Instance.ActiveCamera;
            }
            else if (Camera.main != null)
            {
                targetCamera = Camera.main;
            }
        }

        /// <summary>
        /// Synchronously called by CameraManager immediately after camera updates,
        /// ensuring 0 frames of lag and perfectly smooth, jitter-free parallax.
        /// </summary>
        public void UpdateParallax(Vector3 camPos, float time)
        {
            if (Application.isPlaying && _lastUpdatedFrame == Time.frameCount) return;
            _lastUpdatedFrame = Application.isPlaying ? Time.frameCount : -1;

            InitPropertyBlocks();

            UpdateLayer(backgroundLayer, camPos, time);
            UpdateLayer(cloudsLayer, camPos, time);
            UpdateLayer(foregroundLayer, camPos, time);
        }

        public void ManualUpdate()
        {
            if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
            {
                FindActiveCamera();
                if (targetCamera == null) return;
            }

            Vector3 camPos = targetCamera.transform.position;
            float currentTime = Application.isPlaying ? Time.time : 0f;
            UpdateParallax(camPos, currentTime);
        }

        private void LateUpdate()
        {
            // Fallback for editor mode or if not driven directly by CameraManager
            if (!Application.isPlaying || _lastUpdatedFrame != Time.frameCount)
            {
                ManualUpdate();
            }
        }

        private void UpdateLayer(ParallaxLayer layer, Vector3 camPos, float time)
        {
            if (layer.layerRenderer == null) return;
            if (layer.propBlock == null) layer.propBlock = new MaterialPropertyBlock();

            // 1. Position and scale quad plane
            Transform layerTransform = layer.layerRenderer.transform;
            float targetY = layer.followCameraY
                ? (camPos.y * layer.verticalRatio + layer.yAnchor)
                : layer.yAnchor;

            layerTransform.position = new Vector3(camPos.x, targetY, layer.zDepth);
            layerTransform.localScale = new Vector3(layer.planeWidth, layer.planeHeight, 1f);

            // 2. Continuous, positive mathematical fract to eliminate negative modulo artifacts and jitter
            float rawX = camPos.x * layer.parallaxX + time * layer.autoScrollSpeedX + layer.baseOffset.x;
            float uvX = rawX - Mathf.Floor(rawX);
            float uvY = layer.baseOffset.y;

            Vector4 stVector = new Vector4(layer.tiling.x, layer.tiling.y, uvX, uvY);

            // 3. Apply via MaterialPropertyBlock (zero GC allocation, perfectly smooth)
            layer.layerRenderer.GetPropertyBlock(layer.propBlock);
            layer.propBlock.SetVector(BaseMapST, stVector);
            layer.propBlock.SetVector(MainTexST, stVector);
            layer.layerRenderer.SetPropertyBlock(layer.propBlock);
        }

        public void SetupLayers(Renderer bg, Renderer clouds, Renderer fg)
        {
            backgroundLayer.layerRenderer = bg;
            cloudsLayer.layerRenderer = clouds;
            foregroundLayer.layerRenderer = fg;
            InitPropertyBlocks();
        }
    }
}
