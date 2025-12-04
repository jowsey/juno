Shader "Custom/URP/Circle"
{
    Properties
    {
        [MainColor] _Color ("Color", Color) = (1,1,1,1)
        [MainTexture] _MainTex ("Texture", 2D) = "white" {}
        _EdgeSoftness ("Edge Softness", Range(0, 0.1)) = 0.0001

        [Header(Radial Fade)]
        [Toggle(_USE_RADIAL_FADE)] _UseFade ("Use Radial Fade", Float) = 0
        _FadeExponent ("Fade Exponent", Range(0.1, 10)) = 2.0
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags
            {
                "LightMode" = "Universal2D"
            }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _USE_RADIAL_FADE
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _EdgeSoftness;
                half _FadeExponent;
                half _InnerRadius;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;

                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color * _Color;

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half dist = distance(input.uv, half2(0.5h, 0.5h)) * 2.0h;
                half circle = 1.0h - smoothstep(1.0h - _EdgeSoftness, 1.0h, dist);

                half alpha = circle;
                #if defined(_USE_RADIAL_FADE)
                half fadeStart = saturate((dist - _InnerRadius) / (1.0h - _InnerRadius));
                alpha = circle * pow(1.0h - fadeStart, _FadeExponent);
                #endif

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 color = texColor * input.color;

                return half4(color.rgb, alpha * color.a);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/2D/Sprite-Unlit-Default"
}