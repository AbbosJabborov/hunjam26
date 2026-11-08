using UnityEngine;

namespace Shift.Environment
{
    /// <summary>
    /// Exact C# mirror of the HLSL wave calculation in InkLakeWater.shader.
    /// Allows physics, foam sprites, and player interaction effects to sample
    /// the exact undulating wave surface at any world coordinate and time.
    /// </summary>
    public static class InkWaterMath
    {
        public static float SampleWaveDisplacement(Vector2 worldXZ, float time,
            float waveSpeed = 1.3f, float waveHeight = 0.28f, float waveSteepness = 2.8f,
            float waveFreq1 = 0.45f, float waveFreq2 = 1.10f, float waveFreq3 = 2.40f)
        {
            float t = time * waveSpeed;

            // Wave 1: Primary rolling swell
            Vector2 dir1 = new Vector2(1.0f, 0.18f).normalized;
            float phase1 = Vector2.Dot(dir1, worldXZ) * waveFreq1 + t * 1.05f;
            float sin1 = Mathf.Sin(phase1);
            float crest1 = Mathf.Pow(Mathf.Abs((sin1 + 1.0f) * 0.5f), waveSteepness);

            // Wave 2: Secondary opposing cross swell
            Vector2 dir2 = new Vector2(-0.72f, 0.69f).normalized;
            float phase2 = Vector2.Dot(dir2, worldXZ) * waveFreq2 - t * 0.88f + 1.618f;
            float sin2 = Mathf.Sin(phase2);
            float crest2 = Mathf.Pow(Mathf.Abs((sin2 + 1.0f) * 0.5f), waveSteepness * 0.85f);

            // Wave 3: Fast choppy ripple
            Vector2 dir3 = new Vector2(0.35f, -0.93f).normalized;
            float phase3 = Vector2.Dot(dir3, worldXZ) * waveFreq3 + t * 1.60f + Mathf.PI;
            float sin3 = Mathf.Sin(phase3);
            float crest3 = (sin3 + 1.0f) * 0.5f;

            // Wave 4: Macro breathing tidal surge
            float surge = Mathf.Sin(worldXZ.x * 0.10f + t * 0.32f) * 0.22f;

            // Weighted combination
            float combinedNorm = crest1 * 0.52f + crest2 * 0.30f + crest3 * 0.18f;

            // Organic, hand-drawn irregularity
            float edgeRipple = Mathf.Sin(worldXZ.x * 7.5f + worldXZ.y * 2.1f + t * 2.2f) * 0.025f;
            edgeRipple += Mathf.Sin(worldXZ.x * 13.0f - t * 3.1f) * 0.009f;
            combinedNorm = Mathf.Clamp01(combinedNorm + surge * 0.15f + edgeRipple);

            return (combinedNorm * 2.0f - 1.0f) * waveHeight;
        }

        public static float GetSurfaceY(Vector3 worldPos, float baseTopY, float waterlineOffset, float time,
            float waveSpeed = 1.3f, float waveHeight = 0.28f, float waveSteepness = 2.8f)
        {
            float disp = SampleWaveDisplacement(new Vector2(worldPos.x, worldPos.z), time, waveSpeed, waveHeight, waveSteepness);
            return (baseTopY - waterlineOffset) + disp;
        }
    }
}

