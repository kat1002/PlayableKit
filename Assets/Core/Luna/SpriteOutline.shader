// Luna/Playworks-safe sprite outline.
//
// Replaces "Shader Graphs/Sprite_Outline" (a Shader Graph, which Luna cannot translate and
// renders as a flat green fill). Hand-written CG like the other shaders in this folder
// (ChromaKeyGreenVideo.shader, PlayworksUnlitTexture.shader) so it survives the Luna export.
//
// Base path is identical to Unity's Sprites/Default (tex2D(_MainTex) * vertexColor, straight
// alpha). When _Outline > 0 it adds a thin colored rim by dilating the sprite alpha with an
// 8-tap sample in UV space (explicit width, no _MainTex_TexelSize - unreliable under Luna).
Shader "SnakePlayableAds/Sprite Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlineColor ("Outline Color", Color) = (0.313, 0.991, 0.354, 1)
        _OutlineWidthUV ("Outline Width (UV)", Range(0, 0.06)) = 0.02
        _Outline ("Outline On", Float) = 1
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

        Lighting Off
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                fixed4 color  : COLOR;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _OutlineColor;
            float _OutlineWidthUV;
            float _Outline;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 base = tex2D(_MainTex, i.uv) * i.color;

                if (_Outline > 0.5)
                {
                    float w = _OutlineWidthUV;
                    float a = 0.0;
                    a = max(a, tex2D(_MainTex, i.uv + float2( w, 0.0)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2(-w, 0.0)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2( 0.0,  w)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2( 0.0, -w)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2( w,  w)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2(-w,  w)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2( w, -w)).a);
                    a = max(a, tex2D(_MainTex, i.uv + float2(-w, -w)).a);

                    float rimA = _OutlineColor.a * saturate(a) * (1.0 - base.a);

                    fixed4 outc;
                    outc.rgb = lerp(_OutlineColor.rgb, base.rgb, base.a);
                    outc.a = max(base.a, rimA);
                    return outc;
                }

                return base;
            }
            ENDCG
        }
    }

    Fallback Off
}
