Shader "Custom/URP/Circle"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _EdgeSoftness ("Edge Softness", Range(0, 0.1)) = 0.0001

        [Header(Radial Fade)]
        _UseFade ("Use Radial Fade", Float) = 0
        _FadeExponent ("Fade Exponent", Range(0.1, 10)) = 2.0
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.0

        _MainTex ("Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent" "RenderType"="Transparent"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _Color;
            float _EdgeSoftness;
            float _UseFade;
            float _FadeExponent;
            float _InnerRadius;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                // distance from center
                float2 center = float2(0.5, 0.5);
                float dist = distance(i.uv, center) * 2.0; // normalise to 0-1

                // circle mask with anti-aliasing
                float circle = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, dist);

                // radial fade
                float alpha = circle;
                if (_UseFade > 0.5)
                {
                    float fadeStart = saturate((dist - _InnerRadius) / (1.0 - _InnerRadius));
                    float fade = pow(1.0 - fadeStart, _FadeExponent);
                    alpha = circle * fade;
                }

                // texture
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                half4 color = texColor * i.color;

                return half4(color.rgb, alpha * color.a);
            }
            ENDHLSL
        }
    }
}
