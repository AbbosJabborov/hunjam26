using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Shift.Environment
{
    /// <summary>
    /// Applies 9-sliced, 2-frame animated hand-drawn sketchy blocks across all sides of a platform.
    /// Preserves crisp 9-slice borders regardless of block scale, animates boiling line vibration,
    /// and seamlessly supports both 2D Orthographic Platformer view and 3D FPS view.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class SketchyPlatformBlock : MonoBehaviour
    {
        [Header("Sprite Frames")]
        [SerializeField] private Sprite frame0;
        [SerializeField] private Sprite frame1;

        [Header("Boiling Line Animation")]
        [Tooltip("Vibration speed in frames per second. 6-8 fps provides a classic hand-drawn vibe.")]
        [Range(1f, 24f)]
        [SerializeField] private float animationFps = 7f;

        [Tooltip("Desynchronizes the animation phase per block based on world position so they don't twitch simultaneously.")]
        [SerializeField] private bool randomizePhase = true;

        [Header("Faces Configuration")]
        [Tooltip("When true, generates 9-sliced sprites on all 6 faces of the 3D block (Front, Top, Left, Right, Bottom, Back).")]
        [SerializeField] private bool generateAllFaces = true;

        [Header("Visuals & Materials")]
        [SerializeField] private Color blockColor = Color.white;
        [SerializeField] private int sortingOrder = 1;
        [SerializeField] private bool hideOriginalMeshRenderer = true;

        private Transform _container;
        private SpriteRenderer[] _faceRenderers;
        private Vector3 _lastLossyScale;
        private int _currentFrame = -1;
        private float _phaseOffset;
        private MeshRenderer _parentMeshRenderer;

        private void OnEnable()
        {
            LoadDefaultSprites();
            CalculatePhase();
            RebuildFaces();
        }

        private void OnDisable()
        {
            RestoreMeshRenderer();
        }

        private void Start()
        {
            LoadDefaultSprites();
            CalculatePhase();
            RebuildFaces();
        }

        private void CalculatePhase()
        {
            if (randomizePhase)
            {
                Vector3 p = transform.position;
                _phaseOffset = Mathf.Abs((p.x * 0.37f + p.y * 0.71f + p.z * 0.19f) % 1f);
            }
            else
            {
                _phaseOffset = 0f;
            }
        }

        public void LoadDefaultSprites()
        {
            if (frame0 != null && frame1 != null) return;

#if UNITY_EDITOR
            var sprites = AssetDatabase.LoadAllAssetsAtPath("Assets/Sprites/blocks.png");
            foreach (var obj in sprites)
            {
                if (obj is Sprite s)
                {
                    if (s.name == "blocks_0" && frame0 == null) frame0 = s;
                    if (s.name == "blocks_1" && frame1 == null) frame1 = s;
                }
            }
#endif
        }

        [ContextMenu("Rebuild Faces")]
        public void RebuildFaces()
        {
            LoadDefaultSprites();

            // Find or create container
            var existing = transform.Find("Sketchy_Visuals");
            if (existing != null)
            {
                if (Application.isPlaying) Destroy(existing.gameObject);
                else DestroyImmediate(existing.gameObject);
            }

            var containerGo = new GameObject("Sketchy_Visuals");
            containerGo.transform.SetParent(transform, false);
            containerGo.transform.localPosition = Vector3.zero;
            containerGo.transform.localRotation = Quaternion.identity;
            containerGo.transform.localScale = Vector3.one;
            _container = containerGo.transform;

            Vector3 s = transform.lossyScale;
            _lastLossyScale = s;

            float sx = Mathf.Max(0.001f, s.x);
            float sy = Mathf.Max(0.001f, s.y);
            float sz = Mathf.Max(0.001f, s.z);

            Vector3 invScale = new Vector3(1f / sx, 1f / sy, 1f / sz);

            var renderersList = new System.Collections.Generic.List<SpriteRenderer>();

            Sprite defaultSprite = frame0 != null ? frame0 : frame1;

            // Helper to build an individual face
            void BuildFace(string faceName, Vector3 localPos, Quaternion localRot, Vector2 faceSize)
            {
                var faceGo = new GameObject(faceName);
                faceGo.transform.SetParent(_container, false);
                faceGo.transform.localPosition = localPos;
                faceGo.transform.localRotation = localRot;
                faceGo.transform.localScale = invScale;

                var sr = faceGo.AddComponent<SpriteRenderer>();
                sr.sprite = defaultSprite;
                sr.drawMode = SpriteDrawMode.Sliced;
                sr.size = faceSize;
                sr.color = blockColor;
                sr.sortingOrder = sortingOrder;

                renderersList.Add(sr);
            }

            // 1. Front Face (XY plane, facing -Z toward Platformer Camera)
            BuildFace("Face_Front", new Vector3(0f, 0f, -0.501f), Quaternion.identity, new Vector2(sx, sy));

            if (generateAllFaces)
            {
                // 2. Back Face (XY plane, facing +Z)
                BuildFace("Face_Back", new Vector3(0f, 0f, 0.501f), Quaternion.Euler(0f, 180f, 0f), new Vector2(sx, sy));

                // 3. Top Face (XZ plane, facing +Y, floor surface)
                BuildFace("Face_Top", new Vector3(0f, 0.501f, 0f), Quaternion.Euler(90f, 0f, 0f), new Vector2(sx, sz));

                // 4. Bottom Face (XZ plane, facing -Y, underside)
                BuildFace("Face_Bottom", new Vector3(0f, -0.501f, 0f), Quaternion.Euler(-90f, 0f, 0f), new Vector2(sx, sz));

                // 5. Left Face (YZ plane, facing -X)
                BuildFace("Face_Left", new Vector3(-0.501f, 0f, 0f), Quaternion.Euler(0f, 90f, 0f), new Vector2(sz, sy));

                // 6. Right Face (YZ plane, facing +X)
                BuildFace("Face_Right", new Vector3(0.501f, 0f, 0f), Quaternion.Euler(0f, -90f, 0f), new Vector2(sz, sy));
            }

            _faceRenderers = renderersList.ToArray();
            _currentFrame = -1;

            ApplyMeshRendererState();
        }

        private void ApplyMeshRendererState()
        {
            if (_parentMeshRenderer == null)
            {
                _parentMeshRenderer = GetComponent<MeshRenderer>();
            }

            if (_parentMeshRenderer != null && hideOriginalMeshRenderer)
            {
                // Set to shadow caster only so 3D shadows are cast but visual surface is 100% hand-drawn sketchy
                _parentMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                _parentMeshRenderer.receiveShadows = false;
            }
        }

        private void RestoreMeshRenderer()
        {
            if (_parentMeshRenderer != null)
            {
                _parentMeshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                _parentMeshRenderer.receiveShadows = true;
            }

            if (_container != null)
            {
                if (Application.isPlaying) Destroy(_container.gameObject);
                else DestroyImmediate(_container.gameObject);
            }
        }

        private void Update()
        {
            // Auto rebuild if scale was altered in inspector/editor
            if (transform.lossyScale != _lastLossyScale)
            {
                RebuildFaces();
            }

            if (frame0 == null || frame1 == null || _faceRenderers == null || _faceRenderers.Length == 0)
            {
                return;
            }

#if UNITY_EDITOR
            float time = Application.isPlaying ? Time.time : (float)EditorApplication.timeSinceStartup;
#else
            float time = Time.time;
#endif

            int frame = (int)((time + _phaseOffset) * animationFps) % 2;
            if (frame != _currentFrame)
            {
                _currentFrame = frame;
                Sprite targetSprite = (frame == 0) ? frame0 : frame1;
                for (int i = 0; i < _faceRenderers.Length; i++)
                {
                    if (_faceRenderers[i] != null)
                    {
                        _faceRenderers[i].sprite = targetSprite;
                    }
                }
            }
        }
    }
}

