using System.Collections;
using UnityEngine;

namespace Shift.VFX
{
    /// <summary>
    /// Spawns and animates a stylized, Japanese calligraphy water splash effect
    /// with an upward expanding fountain plume, radial droplets, and expanding surface ripples.
    /// </summary>
    public class InkSplashEffect : MonoBehaviour
    {
        [Header("Plume Animation")]
        [SerializeField] private SpriteRenderer plumeRenderer;
        [SerializeField] private Vector3 maxPlumeScale = new Vector3(1.4f, 1.8f, 1f);
        [SerializeField] private float duration = 0.65f;

        [Header("Surface Ripple")]
        [SerializeField] private SpriteRenderer rippleRenderer;
        [SerializeField] private Vector3 maxRippleScale = new Vector3(3.5f, 0.6f, 1f);

        public static InkSplashEffect Spawn(Vector3 position, float scale = 1f)
        {
            var go = new GameObject("InkSplash_Instance");
            go.transform.position = position;

            var splash = go.AddComponent<InkSplashEffect>();
            splash.BuildVisuals();
            splash.transform.localScale = Vector3.one * scale;

            // Trigger droplet burst via InkSplatterManager
            if (InkSplatterManager.Instance != null)
            {
                InkSplatterManager.Instance.EmitBurst(position + Vector3.up * 0.2f, 16, 6.5f, Vector3.up);
            }

            return splash;
        }

        private void Start()
        {
            StartCoroutine(AnimateSplashRoutine());
        }

        public void BuildVisuals()
        {
            // 1. Plume Sprite
            var plumeGo = new GameObject("SplashPlume");
            plumeGo.transform.SetParent(transform, false);
            plumeRenderer = plumeGo.AddComponent<SpriteRenderer>();

            var plumeSprite = Resources.Load<Sprite>("ink_splash_fountain");
#if UNITY_EDITOR
            if (plumeSprite == null)
            {
                plumeSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/InkWater/ink_splash_fountain.png");
            }
#endif
            plumeRenderer.sprite = plumeSprite;
            plumeRenderer.color = new Color(0.04f, 0.035f, 0.035f, 1f);
            plumeRenderer.sortingOrder = 5;

            // Position pivot at the bottom
            plumeGo.transform.localPosition = new Vector3(0f, 0.8f, -0.05f);
            plumeGo.transform.localScale = new Vector3(0.3f, 0.1f, 1f);

            // 2. Ripple Sprite
            var rippleGo = new GameObject("SplashRipple");
            rippleGo.transform.SetParent(transform, false);
            rippleRenderer = rippleGo.AddComponent<SpriteRenderer>();

            var rippleSprite = Resources.Load<Sprite>("ink_wake_streak");
#if UNITY_EDITOR
            if (rippleSprite == null)
            {
                rippleSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/InkWater/ink_wake_streak.png");
            }
#endif
            rippleRenderer.sprite = rippleSprite;
            rippleRenderer.color = new Color(0.96f, 0.94f, 0.88f, 0.85f);
            rippleRenderer.sortingOrder = 4;
            rippleGo.transform.localPosition = new Vector3(0f, 0.05f, -0.02f);
            rippleGo.transform.localScale = Vector3.zero;
        }

        private IEnumerator AnimateSplashRoutine()
        {
            float elapsed = 0f;

            Vector3 startPlumeScale = new Vector3(0.4f, 0.15f, 1f);
            Color plumeColor = plumeRenderer != null ? plumeRenderer.color : Color.black;
            Color rippleColor = rippleRenderer != null ? rippleRenderer.color : Color.white;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Quick explosive rise (0 to 0.25 duration), gentle settle (0.25 to 1.0)
                float riseT = Mathf.Clamp01(t / 0.28f);
                float easeRise = 1f - Mathf.Pow(1f - riseT, 3f);

                if (plumeRenderer != null)
                {
                    // Plume expands upward then dissipates
                    Vector3 currentScale = Vector3.Lerp(startPlumeScale, maxPlumeScale, easeRise);
                    if (t > 0.35f)
                    {
                        float fadeT = (t - 0.35f) / 0.65f;
                        currentScale.y *= (1f - fadeT * 0.35f);
                        Color c = plumeColor;
                        c.a = Mathf.Lerp(1f, 0f, fadeT);
                        plumeRenderer.color = c;
                    }
                    plumeRenderer.transform.localScale = currentScale;
                }

                if (rippleRenderer != null)
                {
                    // Ripple expands horizontally along water surface
                    float rippleT = 1f - Mathf.Pow(1f - t, 2f);
                    rippleRenderer.transform.localScale = Vector3.Lerp(Vector3.zero, maxRippleScale, rippleT);

                    Color rc = rippleColor;
                    rc.a = Mathf.Lerp(0.85f, 0f, t);
                    rippleRenderer.color = rc;
                }

                yield return null;
            }

            Destroy(gameObject);
        }
    }
}

