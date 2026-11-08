using UnityEngine;

namespace Shift.Environment
{
    /// <summary>
    /// Spawns and manages secondary and tertiary parallax ink water layers behind the main river.
    /// Runs with staggered wave speeds, heights, and tints using MaterialPropertyBlocks,
    /// creating authentic Japanese woodblock depth without complicating the core shader.
    /// </summary>
    [ExecuteAlways]
    public class InkRiverLayerParallax : MonoBehaviour
    {
        [Header("Layer Configuration")]
        [SerializeField] private bool enableParallaxLayers = true;
        [SerializeField] private Material baseWaterMaterial;

        [Header("Midground Layer")]
        [SerializeField] private float midgroundZOffset = 0.85f;
        [SerializeField] private float midgroundWaveSpeed = 0.92f;
        [SerializeField] private float midgroundWaveHeight = 0.24f;
        [SerializeField] private Color midgroundShallowColor = new Color(0.06f, 0.05f, 0.05f, 1f);
        [SerializeField] private Color midgroundBaseColor = new Color(0.01f, 0.01f, 0.012f, 1f);
        [SerializeField] private Color midgroundCrestColor = new Color(0.75f, 0.72f, 0.65f, 0.85f);

        [Header("Background Layer")]
        [SerializeField] private float backgroundZOffset = 1.75f;
        [SerializeField] private float backgroundWaveSpeed = 0.58f;
        [SerializeField] private float backgroundWaveHeight = 0.18f;
        [SerializeField] private Color backgroundShallowColor = new Color(0.035f, 0.03f, 0.03f, 1f);
        [SerializeField] private Color backgroundBaseColor = new Color(0.005f, 0.005f, 0.008f, 1f);
        [SerializeField] private Color backgroundCrestColor = new Color(0.55f, 0.52f, 0.48f, 0.65f);

        private GameObject _midgroundGo;
        private GameObject _backgroundGo;
        private MeshRenderer _midRenderer;
        private MeshRenderer _backRenderer;
        private MaterialPropertyBlock _midMpb;
        private MaterialPropertyBlock _backMpb;

        private void OnEnable()
        {
            SetupLayers();
        }

        private void Start()
        {
            SetupLayers();
        }

        private void OnDisable()
        {
            CleanupLayers();
        }

        [ContextMenu("Rebuild Layers")]
        public void SetupLayers()
        {
            if (!enableParallaxLayers)
            {
                CleanupLayers();
                return;
            }

            var parentMf = GetComponent<MeshFilter>();
            var parentMr = GetComponent<MeshRenderer>();
            if (parentMf == null || parentMr == null) return;

            if (baseWaterMaterial == null)
            {
                baseWaterMaterial = parentMr.sharedMaterial;
            }

            // 1. Setup Midground
            _midgroundGo = GetOrCreateChild("Water_Layer_Midground");
            var midMf = GetOrAddComponent<MeshFilter>(_midgroundGo);
            midMf.sharedMesh = parentMf.sharedMesh;
            _midRenderer = GetOrAddComponent<MeshRenderer>(_midgroundGo);
            _midRenderer.sharedMaterial = baseWaterMaterial;
            _midgroundGo.transform.localPosition = new Vector3(0f, -0.05f, midgroundZOffset);
            _midgroundGo.transform.localRotation = Quaternion.identity;
            _midgroundGo.transform.localScale = Vector3.one;

            // Remove any collider on background layers
            var col = _midgroundGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);

            // 2. Setup Background
            _backgroundGo = GetOrCreateChild("Water_Layer_Background");
            var backMf = GetOrAddComponent<MeshFilter>(_backgroundGo);
            backMf.sharedMesh = parentMf.sharedMesh;
            _backRenderer = GetOrAddComponent<MeshRenderer>(_backgroundGo);
            _backRenderer.sharedMaterial = baseWaterMaterial;
            _backgroundGo.transform.localPosition = new Vector3(0f, -0.10f, backgroundZOffset);
            _backgroundGo.transform.localRotation = Quaternion.identity;
            _backgroundGo.transform.localScale = Vector3.one;

            var backCol = _backgroundGo.GetComponent<Collider>();
            if (backCol != null) DestroyImmediate(backCol);

            ApplyProperties();
        }

        private void Update()
        {
            // Apply properties if altered in inspector or during play
            if (_midMpb == null || _backMpb == null)
            {
                ApplyProperties();
            }
        }

        public void ApplyProperties()
        {
            if (_midMpb == null) _midMpb = new MaterialPropertyBlock();
            if (_backMpb == null) _backMpb = new MaterialPropertyBlock();

            // Midground Material Properties
            if (_midRenderer != null)
            {
                _midRenderer.GetPropertyBlock(_midMpb);
                _midMpb.SetFloat("_WaveSpeed", midgroundWaveSpeed);
                _midMpb.SetFloat("_WaveHeight", midgroundWaveHeight);
                _midMpb.SetColor("_ShallowColor", midgroundShallowColor);
                _midMpb.SetColor("_BaseColor", midgroundBaseColor);
                _midMpb.SetColor("_CrestLineColor", midgroundCrestColor);
                _midMpb.SetFloat("_WaveFreq1", 0.52f);
                _midMpb.SetFloat("_WaveFreq2", 1.25f);
                _midRenderer.SetPropertyBlock(_midMpb);
            }

            // Background Material Properties
            if (_backRenderer != null)
            {
                _backRenderer.GetPropertyBlock(_backMpb);
                _backMpb.SetFloat("_WaveSpeed", backgroundWaveSpeed);
                _backMpb.SetFloat("_WaveHeight", backgroundWaveHeight);
                _backMpb.SetColor("_ShallowColor", backgroundShallowColor);
                _backMpb.SetColor("_BaseColor", backgroundBaseColor);
                _backMpb.SetColor("_CrestLineColor", backgroundCrestColor);
                _backMpb.SetFloat("_WaveFreq1", 0.38f);
                _backMpb.SetFloat("_WaveFreq2", 0.85f);
                _backRenderer.SetPropertyBlock(_backMpb);
            }
        }

        private GameObject GetOrCreateChild(string childName)
        {
            var child = transform.Find(childName);
            if (child != null) return child.gameObject;

            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            return go;
        }

        private T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            var comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private void CleanupLayers()
        {
            var mid = transform.Find("Water_Layer_Midground");
            if (mid != null)
            {
                if (Application.isPlaying) Destroy(mid.gameObject);
                else DestroyImmediate(mid.gameObject);
            }

            var back = transform.Find("Water_Layer_Background");
            if (back != null)
            {
                if (Application.isPlaying) Destroy(back.gameObject);
                else DestroyImmediate(back.gameObject);
            }
        }
    }
}

