Shader "Custom/InkLakeWater"
{
    Properties
    {
        _BaseColor ("Void Ink Base (Deep Charcoal)", Color) = (0.015, 0.015, 0.02, 1.0)
        _ShallowColor ("Ink Wash (Mid-Tone)", Color) = (0.09, 0.08, 0.075, 1.0)
        _FoamColor ("Crest Ink Foam (Parchment Cream)", Color) = (0.95, 0.92, 0.84, 1.0)
        _CrestLineColor ("Wave Rim Outline", Color) = (1.0, 0.99, 0.96, 1.0)
        _AccentColor ("Subtle Sepia Accent", Color) = (0.18, 0.14, 0.12, 1.0)

        _WaveSpeed ("Overall Wave Speed", Float) = 1.3
        _WaveHeight ("Total Wave Amplitude", Range(0.0, 1.5)) = 0.28
        _WaveFreq1 ("Harmonic 1 Frequency (Broad)", Float) = 0.45
        _WaveFreq2 ("Harmonic 2 Frequency (Cross)", Float) = 1.10
        _WaveFreq3 ("Harmonic 3 Frequency (Choppy)", Float) = 2.40
        _WaveSteepness ("Crest Sharpness", Range(1.0, 5.0)) = 2.8

        _FoamCutoff ("Foam Elevation Threshold", Range(0.0, 1.0)) = 0.58
        _FoamBands ("Posterized Foam Bands", Range(1, 6)) = 3
        _FoamWidth ("Crest Foam Thickness", Range(0.01, 0.4)) = 0.12
        _SwirlScale ("Ink Swirl Scale", Float) = 1.3
        _SwirlSpeed ("Ink Swirl Flow Speed", Float) = 0.45
        _SwirlStrength ("Swirl Distortion Strength", Range(0.0, 1.0)) = 0.45

        [Toggle] _EnableWaveClip ("Clip Top Silhouette (Flat Cube Waves)", Float) = 1.0
        _WaterlineOffset ("Waterline Drop from Top (Flat Cubes)", Range(0.0, 1.0)) = 0.32
        _ClipRimThickness ("Crest Rim Thickness", Range(0.01, 0.25)) = 0.055
        _SideDepthFade ("Side Face Ink Darkening Depth", Float) = 2.5
        _SideBandCount ("Side Wave Contours", Range(0, 5)) = 3

        // Hand-drawn side water details
        _SideInkLineColor ("Ink Contour Color", Color) = (0.22, 0.18, 0.16, 1)
        _SideInkNoiseScale ("Ink Noise Scale", Float) = 2.5
        _SideInkNoiseStrength ("Ink Noise Strength", Range(0, 1)) = 0.45
        _SideStrokeSpeed ("Ink Stroke Animation Speed", Float) = 0.18
        _SideContourFrequency ("Contour Frequency", Float) = 3.2
        _SideContourStrength ("Contour Strength", Range(0, 1)) = 0.3
        _SideHighlightVariation ("Highlight Variation", Range(0, 1)) = 0.45

        [Toggle] _EnableVertexDisplacement ("Enable Vertex Displacement", Float) = 1.0
        [Toggle] _DisplaceTopOnly ("Displace Top Vertices Only", Float) = 1.0

        _Smoothness ("Ink Surface Glossiness", Range(0.0, 1.0)) = 0.92
        _SpecularThreshold ("Toon Specular Threshold", Range(0.7, 1.0)) = 0.96
        _SpecularColor ("Ink Glint Color", Color) = (1.0, 0.98, 0.94, 1.0)
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry" 
        }
        LOD 200
        Cull Off
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _ShallowColor;
                float4 _FoamColor;
                float4 _CrestLineColor;
                float4 _AccentColor;
                float4 _SpecularColor;

                float _WaveSpeed;
                float _WaveHeight;
                float _WaveFreq1;
                float _WaveFreq2;
                float _WaveFreq3;
                float _WaveSteepness;

                float _FoamCutoff;
                float _FoamBands;
                float _FoamWidth;
                float _SwirlScale;
                float _SwirlSpeed;
                float _SwirlStrength;

                float _EnableWaveClip;
                float _WaterlineOffset;
                float _ClipRimThickness;
                float _SideDepthFade;
                float _SideBandCount;

                float4 _SideInkLineColor;
                float _SideInkNoiseScale;
                float _SideInkNoiseStrength;
                float _SideStrokeSpeed;
                float _SideContourFrequency;
                float _SideContourStrength;
                float _SideHighlightVariation;

                float _EnableVertexDisplacement;
                float _DisplaceTopOnly;

                float _Smoothness;
                float _SpecularThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float4 tangentOS    : TANGENT;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 positionOS   : TEXCOORD1;
                float3 normalWS     : TEXCOORD2;
                float3 normalOS     : TEXCOORD3;
                float2 uv           : TEXCOORD4;
                float  worldTopY    : TEXCOORD5;
                float  waveOffsetWS : TEXCOORD6;
            };

            // Fast procedural hash and 2D value noise
            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float ValueNoise2D(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float a = Hash21(i);
                float b = Hash21(i + float2(1.0, 0.0));
                float c = Hash21(i + float2(0.0, 1.0));
                float d = Hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float FractalNoise2D(float2 p)
            {
                float val = 0.0;
                float amp = 0.5;
                for (int i = 0; i < 3; i++)
                {
                    val += ValueNoise2D(p) * amp;
                    p *= 2.15;
                    amp *= 0.5;
                }
                return val;
            }

            // Multi-harmonic stylized wave function
            // Returns normalized crest factor [0..1] and world displacement in outDisplacement
            float CalculateStylizedWave(float2 worldXZ, float time, out float outDisplacement)
            {
                float t = time * _WaveSpeed;

                // Wave 1: Primary rolling swell (moves along +X with slight Z diagonal)
                float2 dir1 = normalize(float2(1.0, 0.18));
                float phase1 = dot(dir1, worldXZ) * _WaveFreq1 + t * 1.05;
                float sin1 = sin(phase1);
                // Sharpen peaks (woodblock wave profile)
                float crest1 = pow(abs((sin1 + 1.0) * 0.5), _WaveSteepness);

                // Wave 2: Secondary opposing cross swell (creates organic chop and interference)
                float2 dir2 = normalize(float2(-0.72, 0.69));
                float phase2 = dot(dir2, worldXZ) * _WaveFreq2 - t * 0.88 + 1.618;
                float sin2 = sin(phase2);
                float crest2 = pow(abs((sin2 + 1.0) * 0.5), _WaveSteepness * 0.85);

                // Wave 3: Fast choppy ripple for calligraphic surface texture
                float2 dir3 = normalize(float2(0.35, -0.93));
                float phase3 = dot(dir3, worldXZ) * _WaveFreq3 + t * 1.60 + 3.1415;
                float sin3 = sin(phase3);
                float crest3 = (sin3 + 1.0) * 0.5;

                // Wave 4: Macro breathing tidal surge
                float surge = sin(worldXZ.x * 0.10 + t * 0.32) * 0.22;

                // Weighted combination
                float combinedNorm = crest1 * 0.52 + crest2 * 0.30 + crest3 * 0.18;
                // Organic, hand-drawn irregularity
                float edgeRipple = sin(worldXZ.x * 7.5 + worldXZ.y * 2.1 + t * 2.2) * 0.025;
                edgeRipple += sin(worldXZ.x * 13.0 - t * 3.1) * 0.009;
                combinedNorm = saturate( combinedNorm + surge * 0.15 + edgeRipple );

                // World displacement
                outDisplacement = (combinedNorm * 2.0 - 1.0) * _WaveHeight;
                return combinedNorm;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;

                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;
                output.uv = input.uv;

                // Calculate base world position
                float3 worldPos = TransformObjectToWorld(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                // Calculate nominal world top height of this object (center of top face)
                float3 localTopPos = float3(0.0, 0.5, 0.0);
                float3 worldTop = TransformObjectToWorld(localTopPos);
                output.worldTopY = worldTop.y;

                // Wave calculation at this vertex world XZ
                float waveDisp = 0.0;
                float crestFactor = CalculateStylizedWave(worldPos.xz, _Time.y, waveDisp);
                output.waveOffsetWS = waveDisp;

                // Vertex displacement for subdivided meshes
                if (_EnableVertexDisplacement > 0.5)
                {
                    float factor = 1.0;
                    if (_DisplaceTopOnly > 0.5)
                    {
                        // Displace vertices that belong to the top surface or upper half of the box
                        factor = saturate((input.positionOS.y + 0.1) * 3.0);
                    }
                    worldPos.y -= _WaterlineOffset * factor;
                    worldPos.y += waveDisp * factor;
                }

                output.positionWS = worldPos;
                output.positionCS = TransformWorldToHClip(worldPos);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDirWS = normalize(GetCameraPositionWS() - input.positionWS);

                // Detect if viewing top surface (XZ water plane) or vertical front/side faces
                bool isTopFace = input.normalOS.y > 0.35 || normalWS.y > 0.65;

                // --- 2D ORTHO PLATFORMER VIEW CULLING ---
                // When viewed from an orthographic camera (Platformer 2D view):
                // In pure 2D side-scrolling, only the front cross-section wave silhouette should exist.
                // Discard top face (XZ plane) and any non-camera-facing surfaces to eliminate hovering quads.
                if (unity_OrthoParams.w > 0.5)
                {
                    if (isTopFace || normalWS.z > -0.1)
                    {
                        discard;
                    }
                }

                // Re-evaluate wave math at pixel precision for crisp procedural contours
                float waveDisp = 0.0;
                float waveCrest = CalculateStylizedWave(input.positionWS.xz, _Time.y, waveDisp);

                // --- 1. SIDE / FRONT FACE (Ortho 2D Platformer View) ---
                if (!isTopFace)
                {
                    // Waterline level dropped slightly so both wave peaks and troughs fit within the cube geometry
                    float baseWaterline = input.worldTopY - _WaterlineOffset;
                    float waterSurfaceY = baseWaterline + waveDisp;
                    float deltaFromSurface = input.positionWS.y - waterSurfaceY;

                    // Physical silhouette clipping: Discard pixels above the undulating wave surface
                    if (_EnableWaveClip > 0.5)
                    {
                        if (deltaFromSurface > 0.0)
                        {
                            discard;
                        }
                    }

                    // Distance below the animated crest line (0 = at crest line, >0 = deep in water)
                    float depthBelowCrest = -deltaFromSurface;

                    // -------------------------------------------------
                    // HAND-DRAWN PAPER WATER RENDERING
                    // -------------------------------------------------
                    float2 inkCoord = float2(
                        input.positionWS.x * _SideInkNoiseScale,
                        depthBelowCrest * _SideInkNoiseScale * 0.65
                    );

                    // Slowly drifting procedural ink texture
                    float strokeNoise = ValueNoise2D(
                        inkCoord + float2(_Time.y * _SideStrokeSpeed, 0.0)
                    );

                    // Secondary noise for irregularity
                    float secondaryNoise = ValueNoise2D(
                        inkCoord * 1.8 - float2(0.0, _Time.y * 0.12)
                    );

                    // Combine noise layers
                    float inkVariation = lerp( strokeNoise, secondaryNoise, 0.35 );

                    // 1. Hand-drawn crest highlight
                    float rimThickness = _ClipRimThickness * lerp(0.65, 1.35, inkVariation);
                    float rim = 1.0 - smoothstep( 0.0, rimThickness, depthBelowCrest );

                    // Vary highlight opacity
                    rim *= lerp( 1.0, smoothstep(0.15, 0.75, inkVariation), _SideHighlightVariation );

                    // 2. Organic contour strokes
                    float contourPhase = depthBelowCrest * _SideContourFrequency
                                       + (inkVariation - 0.5) * _SideInkNoiseStrength * 2.0
                                       - _Time.y * 0.35;
                    float contourWave = abs(sin(contourPhase));

                    // Thin, irregular contour lines
                    float contourLine = 1.0 - smoothstep( 0.035, 0.13, contourWave );

                    // Break up the strokes so they resemble ink marks
                    float strokeMask = smoothstep( 0.28, 0.68, inkVariation );

                    // Fade contours as they get deeper
                    float contourDepthFade = saturate( 1.0 - depthBelowCrest / max(0.1, _SideDepthFade) );
                    float contour = contourLine * strokeMask * contourDepthFade * _SideContourStrength;

                    // 3. Deep ink gradient
                    float depthGradient = saturate( depthBelowCrest / max(0.1, _SideDepthFade) );
                    float3 sideInkColor = lerp( _ShallowColor.rgb, _BaseColor.rgb, depthGradient );

                    // 4. Subtle ink wash variation
                    sideInkColor = lerp( sideInkColor, _AccentColor.rgb, (inkVariation - 0.5) * _SideInkNoiseStrength * 0.35 );

                    // 5. Hand-drawn contour strokes
                    sideInkColor = lerp( sideInkColor, _SideInkLineColor.rgb, saturate(contour) );

                    // 6. Bright parchment-colored crest
                    sideInkColor = lerp( sideInkColor, _CrestLineColor.rgb, saturate(rim) );

                    // 7. Subtle edge illumination
                    float fresnel = pow( saturate(1.0 - dot(normalWS, viewDirWS)), 3.0 ) * 0.12;
                    sideInkColor += _FoamColor.rgb * fresnel;

                    return half4(sideInkColor, 1.0);
                }

                // --- 2. TOP WATER SURFACE (3D FPS View & Top Perspective) ---
                // Calculate swirling ink currents using multi-frequency procedural noise
                float2 swirlCoord1 = input.positionWS.xz * (_SwirlScale * 0.16) + float2(_Time.y * _SwirlSpeed * 0.22, _Time.y * _SwirlSpeed * 0.11);
                float2 swirlCoord2 = input.positionWS.xz * (_SwirlScale * 0.32) - float2(_Time.y * _SwirlSpeed * 0.14, _Time.y * _SwirlSpeed * 0.26);

                float noise1 = FractalNoise2D(swirlCoord1);
                float noise2 = ValueNoise2D(swirlCoord2);
                float combinedSwirl = lerp(noise1, noise2, 0.45);

                // Modulate wave crest elevation with swirling ink turbulence
                float modulatedCrest = saturate(waveCrest + (combinedSwirl - 0.5) * _SwirlStrength);

                // Posterized woodblock foam rings / fingers (non-photorealistic stepped bands)
                float foamLevel = (modulatedCrest - _FoamCutoff) / max(0.001, (1.0 - _FoamCutoff));
                foamLevel = saturate(foamLevel);

                // Posterize into sharp graphic bands (like traditional woodblock prints)
                float bands = max(1.0, _FoamBands);
                float posterizedFoam = floor(foamLevel * bands) / bands;

                // Base ink wash gradient (shallow vs deep ink void)
                float3 surfaceColor = lerp(_BaseColor.rgb, _ShallowColor.rgb, modulatedCrest * 0.7);

                // Accentuate ink swirl currents
                surfaceColor = lerp(surfaceColor, _AccentColor.rgb, combinedSwirl * 0.38);

                // Apply crisp stylized foam crests
                surfaceColor = lerp(surfaceColor, _FoamColor.rgb, posterizedFoam);

                // Very sharp wave crest rim line
                float crestPeak = smoothstep(1.0 - _FoamWidth, 1.0, modulatedCrest);
                surfaceColor = lerp(surfaceColor, _CrestLineColor.rgb, crestPeak);

                // Stylized Wet Ink Specular Glint (Toon stepped reflection)
                Light mainLight = GetMainLight();
                float3 lightDir = normalize(mainLight.direction);
                float3 halfDir = normalize(lightDir + viewDirWS);
                float NdotH = saturate(dot(normalWS, halfDir));
                float toonSpecular = step(_SpecularThreshold, NdotH) * _Smoothness;
                surfaceColor += _SpecularColor.rgb * toonSpecular * mainLight.color;

                // Subtle ink Fresnel edge highlight
                float fresnel = pow(saturate(1.0 - dot(normalWS, viewDirWS)), 4.0) * 0.35;
                surfaceColor = lerp(surfaceColor, _FoamColor.rgb, fresnel * modulatedCrest);

                return half4(surfaceColor, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
