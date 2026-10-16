// 탄 궤적 · 탄 머리 · 발광 헤일로용 가산 블렌딩 셰이더
// 2D 라이트를 받지 않으므로 어두운 던전에서도 스스로 빛나 보인다. 알파는 밝기로 쓰인다.
// LightMode 태그가 없어 SRPDefaultUnlit으로 그려지므로 2D Renderer와 Universal Renderer 모두에서 동작한다.
// 구조는 URP Sprite-Unlit-Default를 따른다: 2D SRP Batcher에서는 SpriteRenderer 색 · 반전이
// 정점 색이 아니라 unity_SpriteColor · unity_SpriteProps로 들어온다(LineRenderer에서는 흰색 · 반전 없음).
Shader "ShotgunAndRun/Sprite Additive"
{
    Properties
    {
        [MainTexture][NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            // 2D SRP Batcher는 _ST · _TexelSize 속성을 지원하지 않으므로 타일링 · 오프셋은 쓰지 않는다.
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();

                float3 positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
                output.positionCS = TransformObjectToHClip(positionOS);
                output.uv = input.uv;
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
