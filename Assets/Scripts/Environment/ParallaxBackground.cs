using System;
using UnityEngine;
using Shift.Cameras;
using Shift.Core;

namespace Shift.Environment
{
    /// <summary>
    /// Multi-plane parallax background controller for the "Ink Flood" sketchbook aesthetic.
    /// Manages 3 distinct visual depth planes:
    /// - Far Background: Mountain horizon and aged paper canvas.
    /// - Mid Clouds: Floating ink clouds with parallax and gentle wind drift.
    /// - Foreground: Near architectural silhouettes and structural sketch marks.
    /// Uses MaterialPropertyBlock with repeating UV offsets for seamless infinite scrolling.
    /// </summary>
    [ExecuteAlways]
    public class ParallaxBackground : MonoBehaviour
    {
        [System.Serializable]
        public class ParallaxLayer
        {
            public string name;
            public Renderer layerRenderer;
            [Tooltip("Horizontal parallax factor (lower = further away, moves slower)")]
            public float parallaxX = 0.005f;
            [Tooltip("Vertical parallax factor")]
            public float parallaxY = 0.002f;
            [Tooltip("Automatic wind drift speed in UV units per second")]
            public float autoScrollSpeedX = 0f;
            [Tooltip("Automatic vertical drift speed")]
            public float autoScrollSpeedY = 0f;
            [Tooltip("Texture tiling across the plane")]
            public Vector2 tiling = new Vector2(2f, 1f);
            [Tooltip("Base UV offset")]
            public Vector2 baseOffset = Vector2.zero;
            [Tooltip("Vertical position offset relative to camera")]
            public float yOffset = 0f;
            [Tooltip("Z depth in world space")]
            public float zDepth = 25f;

            [HideInInspector] public MaterialPropertyBlock propBlock;
        }

        [Header("Target & Camera")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private float verticalFollowRatio = 0.3f;
        [SerializeField] private float planeWidth = 140f;
        [SerializeField] private float planeHeight = 70f;

        [Header("Parallax Depth Layers")]
        [SerializeField] private ParallaxLayer backgroundLayer = new ParallaxLayer
        {
            name = "Far Background",
            parallaxX = 0.003f,
            parallaxY = 0f,
            autoScrollSpeedX = 0f,
            tiling = new Vector2(2.0f, 1.0f),
            baseOffset = Vector2.zero,
            yOffset = 4.0f,
            zDepth = 30f
        };

        [SerializeField] private ParallaxLayer cloudsLayer = new ParallaxLayer
        {
            name = "Mid Clouds",
            parallaxX = 0.008f,
            parallaxY = 0f,
            autoScrollSpeedX = 0.008f, // Gentle continuous wind drift
            tiling = new Vector2(2.0f, 1.0f),
            baseOffset = new Vector2(0f, 0.05f),
            yOffset = 7.0f,
            zDepth = 25f
        };

        [SerializeField] private ParallaxLayer foregroundLayer = new ParallaxLayer
        {
            name = "Near Foreground",
            parallaxX = 0.018f,
            parallaxY = 0f,
            autoScrollSpeedX = 0f,
            tiling = new Vector2(2.5f, 1.0f),
            baseOffset = new Vector2(0f, -0.05f),
            yOffset = -2.0f,
            zDepth = 20f
        };

        private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
        private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");

        private void OnEnable()
        {
            InitPropertyBlocks();
        }

        private void Start()
        {
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

        public void ManualUpdate()
        {
            if (targetCamera == null || !targetCamera.gameObject.activeInHierarchy)
            {
                FindActiveCamera();
                if (targetCamera == null) return;
            }

            Vector3 camPos = targetCamera.transform.position;
            float currentTime = Application.isPlaying ? Time.time : 0f;

            UpdateLayer(backgroundLayer, camPos, currentTime);
            UpdateLayer(cloudsLayer, camPos, currentTime);
            UpdateLayer(foregroundLayer, camPos, currentTime);
        }

        private void LateUpdate()
        {
            ManualUpdate();
        }

        private void UpdateLayer(ParallaxLayer layer, Vector3 camPos, float time)
        {
            if (layer.layerRenderer == null) return;

            if (layer.propBlock == null) layer.propBlock = new MaterialPropertyBlock();

            // 1. Center layer plane with camera horizontally, follow vertical motion with damping
            Transform layerTransform = layer.layerRenderer.transform;
            float targetY = camPos.y * verticalFollowRatio + layer.yOffset;
            layerTransform.position = new Vector3(camPos.x, targetY, layer.zDepth);
            layerTransform.localScale = new Vector3(planeWidth, planeHeight, 1f);

            // 2. Calculate UV texture offset for repeating parallax scroll
            float uvX = (camPos.x * layer.parallaxX + time * layer.autoScrollSpeedX + layer.baseOffset.x) % 1.0f;
            float uvY = (camPos.y * layer.parallaxY + time * layer.autoScrollSpeedY + layer.baseOffset.y) % 1.0f;

            Vector4 stVector = new Vector4(layer.tiling.x, layer.tiling.y, uvX, uvY);

            // 3. Apply via MaterialPropertyBlock (zero GC allocation, works in Edit and Play mode)
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
