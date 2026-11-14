// Glow outline around a UI sprite's silhouette, drawn by one Image.
// The Image rect must be the sprite's native size grown by _Pad texels on every side
// (RectTransform stretch with offsets -_Pad); the shader remaps UVs so the sprite stays
// at its original place and the padding holds the glow. Inside the silhouette it is
// transparent, so only the outline + soft glow outside the shape is visible.
// Requires an unpacked sprite that covers its whole texture (UV 0..1).
Shader "PlayableKit/PA_UI_GlowOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OutlineColor ("Outline Color", Color) = (1, 0.886, 0.431, 1)
        _GlowColor ("Glow Color", Color) = (1, 0.816, 0.25, 0.55)
        _Pad ("Padding (texels)", Float) = 18
        _OutlineWidth ("Outline Width (texels)", Float) = 6
        _GlowWidth ("Glow Width (texels)", Float) = 16

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="False"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize; // (1/w, 1/h, w, h)
            fixed4 _Color;
            fixed4 _OutlineColor;
            fixed4 _GlowColor;
            float _Pad;
            float _OutlineWidth;
            float _GlowWidth;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                // Quad UV 0..1 spans the padded rect; convert to sprite texel space.
                float2 size = _MainTex_TexelSize.zw;
                OUT.texcoord = (v.texcoord * (size + 2.0 * _Pad) - _Pad) * _MainTex_TexelSize.xy;
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Alpha of the silhouette, 0 outside the texture bounds.
            float A(float2 uv)
            {
                float2 inside = step(0.0, uv) * step(uv, 1.0);
                return tex2D(_MainTex, saturate(uv)).a * inside.x * inside.y;
            }

            // Max silhouette alpha on a 12-tap ring of radius r (texels).
            float Ring(float2 uv, float r)
            {
                float2 t = _MainTex_TexelSize.xy * r;
                float m = 0.0;
                m = max(m, A(uv + t * float2( 1.000,  0.000)));
                m = max(m, A(uv + t * float2( 0.866,  0.500)));
                m = max(m, A(uv + t * float2( 0.500,  0.866)));
                m = max(m, A(uv + t * float2( 0.000,  1.000)));
                m = max(m, A(uv + t * float2(-0.500,  0.866)));
                m = max(m, A(uv + t * float2(-0.866,  0.500)));
                m = max(m, A(uv + t * float2(-1.000,  0.000)));
                m = max(m, A(uv + t * float2(-0.866, -0.500)));
                m = max(m, A(uv + t * float2(-0.500, -0.866)));
                m = max(m, A(uv + t * float2( 0.000, -1.000)));
                m = max(m, A(uv + t * float2( 0.500, -0.866)));
                m = max(m, A(uv + t * float2( 0.866, -0.500)));
                return m;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                float center = A(uv);

                // Solid outline: anything within _OutlineWidth of the shape.
                float outline = max(Ring(uv, _OutlineWidth * 0.5), Ring(uv, _OutlineWidth));

                // Soft glow: fades out across _GlowWidth beyond the outline.
                float g1 = Ring(uv, _OutlineWidth + _GlowWidth * 0.33);
                float g2 = Ring(uv, _OutlineWidth + _GlowWidth * 0.66);
                float g3 = Ring(uv, _OutlineWidth + _GlowWidth);
                float glow = (g1 + g2 + g3) / 3.0;

                fixed4 col = _GlowColor;
                col.a *= glow;
                col = lerp(col, _OutlineColor, outline);
                col.a *= IN.color.a * (1.0 - center); // transparent inside the hole
                col.rgb *= IN.color.rgb;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
