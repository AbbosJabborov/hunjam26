Shader "Custom/SketchyPlatform"
{
    Properties
    {
        [Header(Spritesheet and Animation)]
        _MainTex ("Spritesheet Texture", 2D) = "white" {}
        _BaseColor ("Base Color Tint", Color) = (1, 1, 1, 1)
        _AnimationFps ("Boiling Animation FPS", Float) = 7.0
        [Toggle] _DesyncByPosition ("Desynchronize Phase by Position", Float) = 1.0

        [Header(Frame Rectangles in Spritesheet)]
        _Frame0Rect ("Frame 0 UV Rect (X, Y, W, H)", Vector) = (0.0095, 0.0170, 0.4820, 0.9660)
        _Frame1Rect ("Frame 1 UV Rect (X, Y, W, H)", Vector) = (0.5070, 0.0140, 0.4855, 0.9750)

        [Header(9 Slice Border Settings)]
        _BorderWorldWidth ("Border Physical Width (Meters)", Float) = 0.65
        _BorderNorm ("Border Sprite Fraction", Range(0.1, 0.45)) = 0.311
        [Toggle] _TileCenter ("Tile Center Texture", Float) = 0.0
        _CenterTileScale ("Center Tile Scale (Meters)", Float) = 1.5

        [Header(Scale and Dimensions)]
        _ManualScale ("Manual Scale Override (Leave 0,0,0 for Auto)", Vector) = (0, 0, 0, 0)

        [Header(Lighting)]
        _LightContribution ("Lighting Influence (0 = Unlit 2D, 1 = 3D Lit)", Range(0.0, 1.0)) = 0.35
        _ShadowStrength ("Shadow Reception Strength", Range(0.0, 1.0)) = 0.45
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
        Cull Back
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

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 positionWS   : TEXCOORD0;
                float3 normalWS     : TEXCOORD1;
                float3 positionOS   : TEXCOORD2;
                float3 normalOS     : TEXCOORD3;
                float3 objectScale  : TEXCOORD4;
                float3 objectCenterWS : TEXCOORD5;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Frame0Rect;
                float4 _Frame1Rect;
                float4 _ManualScale;
                float _AnimationFps;
                float _DesyncByPosition;
                float _BorderWorldWidth;
                float _BorderNorm;
                float _TileCenter;
                float _CenterTileScale;
                float _LightContribution;
                float _ShadowStrength;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;

                VertexPositionInputs vertexInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = vertexInputs.positionCS;
                output.positionWS = vertexInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.positionOS = input.positionOS.xyz;
                output.normalOS = input.normalOS;

                // Extract lossy world scale from unity_ObjectToWorld matrix
                float3 autoScale = float3(
                    length(float3(unity_ObjectToWorld._m00, unity_ObjectToWorld._m10, unity_ObjectToWorld._m20)),
                    length(float3(unity_ObjectToWorld._m01, unity_ObjectToWorld._m11, unity_ObjectToWorld._m21)),
                    length(float3(unity_ObjectToWorld._m02, unity_ObjectToWorld._m12, unity_ObjectToWorld._m22))
                );

                output.objectScale = (_ManualScale.x > 0.001 && _ManualScale.y > 0.001) ? _ManualScale.xyz : autoScale;
                output.objectCenterWS = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);

                return output;
            }

            // 1D Non-linear 9-slice mapping function
            float Remap9Slice1D(float coord, float totalDim, float borderMeters, float borderNorm, float tileCenter, float tileScale)
            {
                float b = min(borderMeters, totalDim * 0.499);
                if (coord < b)
                {
                    return (coord / max(0.0001, b)) * borderNorm;
                }
                else if (coord > totalDim - b)
                {
                    float rightAlpha = (coord - (totalDim - b)) / max(0.0001, b);
                    return (1.0 - borderNorm) + rightAlpha * borderNorm;
                }
                else
                {
                    float interiorCoord = coord - b;
                    float interiorWidth = max(0.0001, totalDim - 2.0 * b);
                    float interiorNorm = max(0.0001, 1.0 - 2.0 * borderNorm);

                    if (tileCenter > 0.5)
                    {
                        float repeatU = frac(interiorCoord / max(0.01, tileScale));
                        return borderNorm + repeatU * interiorNorm;
                    }
                    else
                    {
                        float stretchU = interiorCoord / interiorWidth;
                        return borderNorm + stretchU * interiorNorm;
                    }
                }
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 absN = abs(input.normalOS);
                float2 faceUV = float2(0.0, 0.0);
                float2 faceDim = float2(1.0, 1.0);

                // Determine which face of the cube we are rendering from object-space normal
                if (absN.y >= absN.x && absN.y >= absN.z)
                {
                    // Top face (+Y) or Bottom face (-Y)
                    faceDim = float2(input.objectScale.x, input.objectScale.z);
                    if (input.normalOS.y > 0.0)
                    {
                        faceUV = float2(input.positionOS.x + 0.5, input.positionOS.z + 0.5);
                    }
                    else
                    {
                        faceUV = float2(input.positionOS.x + 0.5, 0.5 - input.positionOS.z);
                    }
                }
                else if (absN.x >= absN.y && absN.x >= absN.z)
                {
                    // Right face (+X) or Left face (-X)
                    faceDim = float2(input.objectScale.z, input.objectScale.y);
                    if (input.normalOS.x > 0.0)
                    {
                        faceUV = float2(0.5 - input.positionOS.z, input.positionOS.y + 0.5);
                    }
                    else
                    {
                        faceUV = float2(input.positionOS.z + 0.5, input.positionOS.y + 0.5);
                    }
                }
                else
                {
                    // Front face (-Z) or Back face (+Z)
                    faceDim = float2(input.objectScale.x, input.objectScale.y);
                    if (input.normalOS.z < 0.0)
                    {
                        faceUV = float2(input.positionOS.x + 0.5, input.positionOS.y + 0.5);
                    }
                    else
                    {
                        faceUV = float2(0.5 - input.positionOS.x, input.positionOS.y + 0.5);
                    }
                }

                // Physical metric coordinates across the face
                float2 physicalCoord = saturate(faceUV) * faceDim;

                // Remap physical coordinates using continuous 9-slice formula
                float uNorm = Remap9Slice1D(physicalCoord.x, faceDim.x, _BorderWorldWidth, _BorderNorm, _TileCenter, _CenterTileScale);
                float vNorm = Remap9Slice1D(physicalCoord.y, faceDim.y, _BorderWorldWidth, _BorderNorm, _TileCenter, _CenterTileScale);

                // Compute boiling animation frame
                float phase = 0.0;
                if (_DesyncByPosition > 0.5)
                {
                    phase = frac(sin(dot(input.objectCenterWS.xyz, float3(12.9898, 78.233, 45.164))) * 43758.5453);
                }

                float animTime = _Time.y + phase * 2.0;
                int frame = (int)floor(fmod(animTime * _AnimationFps, 2.0));

                float4 activeRect = (frame == 0) ? _Frame0Rect : _Frame1Rect;
                float2 sampleUV = activeRect.xy + float2(uNorm * activeRect.z, vNorm * activeRect.w);

                half4 texCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, sampleUV);
                half4 col = texCol * _BaseColor;

                // Stylized Lighting & Shadows
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                    Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                    half shadow = mainLight.shadowAttenuation;
                #else
                    Light mainLight = GetMainLight();
                    half shadow = 1.0;
                #endif

                half NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                half lightFactor = lerp(1.0, NdotL * shadow, _LightContribution * _ShadowStrength);
                col.rgb *= lightFactor;

                return col;
            }
            ENDHLSL
        }

        // Shadow Caster Pass (allows platforms to cast clean shadows)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _MainLightPosition.xyz));
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}

