using System.Collections.Generic;
using UnityEngine;
using Shift.Core;

namespace Shift.Environment
{
    /// <summary>
    /// Spawns and animates stylized Japanese calligraphy foam sprites along the river surface.
    /// Each foam sprite rides the mathematical wave crest, drifting with the current,
    /// tilting with the wave tangent, and pulsing in scale and opacity.
    /// </summary>
    [ExecuteAlways]
    public class InkRiverFoamController : MonoBehaviour
    {
        [Header("Foam Sprites")]
        [SerializeField] private Sprite[] foamSprites;

        [Header("Spawn Settings")]
        [SerializeField] private int foamCount = 12;
        [SerializeField] private Vector2 baseScaleRange = new Vector2(0.6f, 0.95f);
        [SerializeField] private float driftSpeed = 0.65f;
        [SerializeField] private float crestAlphaThreshold = 0.0f;
        [SerializeField] private Color foamColor = new Color(0.96f, 0.94f, 0.88f, 0.88f);

        [Header("Wave Parameters (Auto-read from Material if null)")]
        [SerializeField] private Material waterMaterial;
        [SerializeField] private float waveSpeed = 1.3f;
        [SerializeField] private float waveHeight = 0.28f;
        [SerializeField] private float waveSteepness = 2.8f;
        [SerializeField] private float waterlineOffset = 0.32f;

        private class FoamInstance
        {
            public GameObject gameObject;
            public SpriteRenderer renderer;
            public float xPos;
            public float speed;
            public float baseScale;
            public float scaleOscFreq;
            public float phaseOffset;
            public int spriteIndex;
        }

        private readonly List<FoamInstance> _instances = new List<FoamInstance>();
        private Transform _container;
        private Renderer _parentRenderer;

        private void OnEnable()
        {
            Initialize();
        }

        private void OnDisable()
        {
            Cleanup();
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            Cleanup();

            _parentRenderer = GetComponent<Renderer>();
            if (waterMaterial == null && _parentRenderer != null)
            {
                waterMaterial = _parentRenderer.sharedMaterial;
            }

            ReadMaterialParameters();

            // Load default sprites if not assigned
            if (foamSprites == null || foamSprites.Length == 0)
            {
                var s1 = Resources.Load<Sprite>("foam_crest_curl");
                if (s1 == null)
                {
#if UNITY_EDITOR
                    s1 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/InkWater/foam_crest_curl.png");
                    var s2 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/InkWater/foam_crest_claw.png");
                    var s3 = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/InkWater/foam_crest_wisps.png");
                    var list = new List<Sprite>();
                    if (s1 != null) list.Add(s1);
                    if (s2 != null) list.Add(s2);
                    if (s3 != null) list.Add(s3);
                    foamSprites = list.ToArray();
#endif
                }
            }

            if (foamSprites == null || foamSprites.Length == 0) return;

            // Create container
            var existing = transform.Find("Foam_Crest_Container");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            var containerGo = new GameObject("Foam_Crest_Container");
            containerGo.transform.SetParent(transform, false);
            _container = containerGo.transform;

            Bounds b = GetWorldBounds();
            float minX = b.min.x + 1f;
            float maxX = b.max.x - 1f;
            float rangeX = Mathf.Max(1f, maxX - minX);

            for (int i = 0; i < foamCount; i++)
            {
                var go = new GameObject($"Foam_{i:D2}");
                go.transform.SetParent(_container, false);

                var sr = go.AddComponent<SpriteRenderer>();
                int sIndex = i % foamSprites.Length;
                sr.sprite = foamSprites[sIndex];
                sr.color = foamColor;
                sr.sortingOrder = 2; // In front of water body

                var inst = new FoamInstance
                {
                    gameObject = go,
                    renderer = sr,
                    xPos = minX + (i / (float)foamCount) * rangeX + Random.Range(-0.8f, 0.8f),
                    speed = driftSpeed * Random.Range(0.85f, 1.25f),
                    baseScale = Random.Range(baseScaleRange.x, baseScaleRange.y),
                    scaleOscFreq = Random.Range(1.8f, 3.2f),
                    phaseOffset = Random.Range(0f, 6.28f),
                    spriteIndex = sIndex
                };

                _instances.Add(inst);
            }

            UpdatePositions(0f);
        }

        private void ReadMaterialParameters()
        {
            if (waterMaterial != null)
            {
                if (waterMaterial.HasProperty("_WaveSpeed")) waveSpeed = waterMaterial.GetFloat("_WaveSpeed");
                if (waterMaterial.HasProperty("_WaveHeight")) waveHeight = waterMaterial.GetFloat("_WaveHeight");
                if (waterMaterial.HasProperty("_WaveSteepness")) waveSteepness = waterMaterial.GetFloat("_WaveSteepness");
                if (waterMaterial.HasProperty("_WaterlineOffset")) waterlineOffset = waterMaterial.GetFloat("_WaterlineOffset");
            }
        }

        private void Cleanup()
        {
            _instances.Clear();
            var existing = transform.Find("Foam_Crest_Container");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }
        }

        private Bounds GetWorldBounds()
        {
            if (_parentRenderer != null) return _parentRenderer.bounds;
            return new Bounds(transform.position, transform.lossyScale);
        }

        private void Update()
        {
            float dt = Application.isPlaying ? Time.deltaTime : 0.016f;
            float time = Application.isPlaying ? Time.time : (float)UnityEditor.EditorApplication.timeSinceStartup;

            // In 2D Platformer view, foam sprites are active; hide in 3D FPS if needed
            bool isPlatformer = DimensionManager.Instance == null || DimensionManager.Instance.IsPlatformer;
            if (_container != null)
            {
                _container.gameObject.SetActive(isPlatformer);
            }

            if (!isPlatformer) return;

            UpdatePositions(dt, time);
        }

        private void UpdatePositions(float dt, float time = 0f)
        {
            if (_instances.Count == 0) return;

            Bounds b = GetWorldBounds();
            float minX = b.min.x - 1f;
            float maxX = b.max.x + 1f;
            float width = maxX - minX;

            float baseTopY = b.max.y;
            float frontZ = b.min.z - 0.08f; // Sit precisely on front wave face

            for (int i = 0; i < _instances.Count; i++)
            {
                var inst = _instances[i];
                if (inst.gameObject == null) continue;

                // Drift along X with river flow
                inst.xPos += inst.speed * dt;
                if (inst.xPos > maxX)
                {
                    inst.xPos -= width;
                }

                float x = inst.xPos;
                Vector3 samplePos = new Vector3(x, 0f, b.min.z);

                // Sample mathematical wave height at this position
                float y = InkWaterMath.GetSurfaceY(samplePos, baseTopY, waterlineOffset, time, waveSpeed, waveHeight, waveSteepness);

                // Calculate slope/tangent for natural tilting
                float delta = 0.25f;
                float yLeft = InkWaterMath.GetSurfaceY(new Vector3(x - delta, 0f, b.min.z), baseTopY, waterlineOffset, time, waveSpeed, waveHeight, waveSteepness);
                float yRight = InkWaterMath.GetSurfaceY(new Vector3(x + delta, 0f, b.min.z), baseTopY, waterlineOffset, time, waveSpeed, waveHeight, waveSteepness);
                float angle = Mathf.Atan2(yRight - yLeft, delta * 2f) * Mathf.Rad2Deg;

                // Position on wave surface
                inst.gameObject.transform.position = new Vector3(x, y - 0.02f, frontZ);
                inst.gameObject.transform.rotation = Quaternion.Euler(0f, 0f, angle * 0.75f);

                // Pulsing scale and alpha near wave crests
                float crestElevation = y - (baseTopY - waterlineOffset);
                float wavePhase = (crestElevation + waveHeight) / Mathf.Max(0.01f, waveHeight * 2f); // 0 to 1

                float scaleBreath = 1f + Mathf.Sin(time * inst.scaleOscFreq + inst.phaseOffset) * 0.15f;
                inst.gameObject.transform.localScale = Vector3.one * (inst.baseScale * scaleBreath);

                // Foam is more opaque at crests, softly fades as wave falls
                float alpha = Mathf.Clamp01(Mathf.Lerp(0.35f, 1f, wavePhase));
                Color c = foamColor;
                c.a *= alpha;
                inst.renderer.color = c;
            }
        }
    }
}

