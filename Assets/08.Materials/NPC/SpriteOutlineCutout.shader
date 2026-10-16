Shader "ShotgunAndRun/SpriteOutlineCutout"
{
    // 상호작용 외곽선용 단색 스프라이트. 알파가 _Cutoff 이상인 픽셀만 렌더러 색으로 칠한다.
    // 발밑 반투명 그림자(알파 100/255)는 잘라 내서 외곽선이 그림자 타원까지 번지지 않게 한다.
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        [MaterialToggle] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment CutoutSolidColorFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile_local _ PIXELSNAP_ON
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA

            #include "UnitySprites.cginc"

            fixed _Cutoff;

            fixed4 CutoutSolidColorFrag(v2f input) : SV_Target
            {
                fixed4 sampledSprite = SampleSpriteTexture(input.texcoord);
                clip(sampledSprite.a - _Cutoff);
                fixed4 color = input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
