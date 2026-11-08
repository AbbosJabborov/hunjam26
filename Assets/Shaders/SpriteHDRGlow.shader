Shader "Custom/SpriteHDRGlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint Color", Color) = (1,1,1,1)
        [HDR] _GlowColor ("HDR Glow Color", Color) = (1.6, 1.6, 1.6, 1)
        _GlowIntensity ("Glow Intensity", Range(1, 4)) = 1.5
        _InkThreshold ("Ink Cutoff (Preserve Black Lines)", Range(0, 0.8)) = 0.25
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Name "SpriteGlow"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
                    UNITY_DEFINE_INSTANCED_PROP(float2, unity_SpriteFlipArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)

                #define unity_SpriteColor  UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
                #define unity_SpriteFlip   UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteFlipArray)
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GlowColor;
                float _GlowIntensity;
                float _InkThreshold;
                float4 _RendererColor;
                float4 _Flip;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 flip = float2(1.0, 1.0);
                #ifdef UNITY_INSTANCING_ENABLED
                    flip = unity_SpriteFlip;
                #else
                    if (unity_SpriteProps.x != 0.0) flip.x = unity_SpriteProps.x;
                    if (unity_SpriteProps.y != 0.0) flip.y = unity_SpriteProps.y;
                #endif

                float3 posOS = input.positionOS.xyz;
                posOS.xy *= flip;

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 finalColor = texColor * input.color;

                // Calculate brightness of the original sprite pixel (0 = black ink, 1 = white paper body)
                float brightness = dot(texColor.rgb, float3(0.299, 0.587, 0.114));

                // If the pixel is white body (brightness > _InkThreshold), apply HDR emission for Bloom!
                // If it's black ink outline (brightness <= _InkThreshold), preserve crisp black ink with 0 emission!
                float glowWeight = smoothstep(_InkThreshold, _InkThreshold + 0.15, brightness);

                // Blend normal color into HDR glow color for bright body regions
                float3 hdrColor = finalColor.rgb * _GlowColor.rgb * _GlowIntensity;
                finalColor.rgb = lerp(finalColor.rgb, hdrColor, glowWeight);

                return finalColor;
            }
            ENDHLSL
        }

        // Fallback pass for SRPDefaultUnlit or 2D rendering passes
        Pass
        {
            Name "SpriteGlowUnlit"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float4 color        : COLOR;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            #ifdef UNITY_INSTANCING_ENABLED
                UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
                    UNITY_DEFINE_INSTANCED_PROP(float4, unity_SpriteRendererColorArray)
                    UNITY_DEFINE_INSTANCED_PROP(float2, unity_SpriteFlipArray)
                UNITY_INSTANCING_BUFFER_END(PerDrawSprite)

                #define unity_SpriteColor  UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteRendererColorArray)
                #define unity_SpriteFlip   UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, unity_SpriteFlipArray)
            #endif

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _GlowColor;
                float _GlowIntensity;
                float _InkThreshold;
                float4 _RendererColor;
                float4 _Flip;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float2 flip = float2(1.0, 1.0);
                #ifdef UNITY_INSTANCING_ENABLED
                    flip = unity_SpriteFlip;
                #else
                    if (unity_SpriteProps.x != 0.0) flip.x = unity_SpriteProps.x;
                    if (unity_SpriteProps.y != 0.0) flip.y = unity_SpriteProps.y;
                #endif

                float3 posOS = input.positionOS.xyz;
                posOS.xy *= flip;

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv = input.uv;
                output.color = input.color * _Color * _RendererColor;

                return output;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float4 finalColor = texColor * input.color;

                float brightness = dot(texColor.rgb, float3(0.299, 0.587, 0.114));
                float glowWeight = smoothstep(_InkThreshold, _InkThreshold + 0.15, brightness);

                float3 hdrColor = finalColor.rgb * _GlowColor.rgb * _GlowIntensity;
                finalColor.rgb = lerp(finalColor.rgb, hdrColor, glowWeight);

                return finalColor;
            }
            ENDHLSL
        }
    }

    Fallback "Sprites/Default"
}

