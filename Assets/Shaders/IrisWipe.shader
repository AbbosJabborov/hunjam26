Shader "UI/IrisWipe"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Overlay Ink Color", Color) = (0.015, 0.015, 0.02, 1.0)
        _Center ("Hole Center (UV)", Vector) = (0.5, 0.5, 0, 0)
        _Radius ("Hole Radius", Range(0.0, 3.5)) = 3.0
        _EdgeSoftness ("Edge Softness", Range(0.001, 0.1)) = 0.02
        _AspectRatio ("Screen Aspect Ratio (W/H)", Float) = 1.7777
        _InkRipples ("Edge Organic Ink Distortion", Range(0, 1)) = 0.25
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+1000"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float2 texcoord : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float2 uv       : TEXCOORD0;
                fixed4 color    : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _Center;
            float _Radius;
            float _EdgeSoftness;
            float _AspectRatio;
            float _InkRipples;

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = UnityObjectToClipPos(IN.vertex);
                OUT.uv = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 diff = IN.uv - _Center.xy;
                diff.x *= _AspectRatio;

                // Subtle organic hand-drawn wobble around the iris aperture
                float angle = atan2(diff.y, diff.x);
                float wobble = sin(angle * 7.0 + _Radius * 4.0) * 0.015 + sin(angle * 15.0) * 0.007;
                float dist = length(diff) + wobble * _InkRipples;

                // Transparent inside the hole, solid ink black outside
                float alpha = smoothstep(_Radius - _EdgeSoftness, _Radius + _EdgeSoftness, dist);

                fixed4 col = IN.color;
                col.a *= saturate(alpha);
                return col;
            }
            ENDCG
        }
    }
}
