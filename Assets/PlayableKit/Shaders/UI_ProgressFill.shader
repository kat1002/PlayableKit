Shader "PlayableKit/UI_ProgressFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0

        _UVRect ("Sprite UV Rect (x,y,w,h)", Vector) = (0,0,1,1)
        _FillSize ("Fill Size px (set by UIProgressFillFX)", Vector) = (100,40,0,0)
        _Progress ("Progress (set by UIProgressFillFX)", Range(0,1)) = 1
        _StripeColor ("Stripe Color", Color) = (1,1,1,0.22)
        _StripePeriod ("Stripe Period (px)", Float) = 34
        _StripeDuty ("Stripe Duty", Range(0,1)) = 0.45
        _StripeSlant ("Stripe Slant", Float) = 1
        _StripeSpeed ("Stripe Speed (px/s)", Float) = 60
        _GlossColor ("Gloss Color", Color) = (1,1,1,0.35)
        _EdgeColor ("Leading Edge Color", Color) = (1,1,0.85,0.9)
        _EdgeWidth ("Leading Edge Width (px)", Float) = 16
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
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
            Name "Default"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;

            float4 _UVRect;
            float4 _FillSize;
            float _Progress;
            fixed4 _StripeColor;
            float _StripePeriod;
            float _StripeDuty;
            float _StripeSlant;
            float _StripeSpeed;
            fixed4 _GlossColor;
            fixed4 _EdgeColor;
            float _EdgeWidth;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);

                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);

                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 p = (IN.texcoord - _UVRect.xy) / max(_UVRect.zw, 1e-5);
                float2 px = p * _FillSize.xy;

                float f = frac((px.x + px.y * _StripeSlant - _Time.y * _StripeSpeed) / max(_StripePeriod, 1.0));
                float stripe = smoothstep(0.0, 0.06, f) * (1.0 - smoothstep(_StripeDuty, _StripeDuty + 0.06, f));
                float gloss = smoothstep(0.5, 0.8, p.y) * (1.0 - smoothstep(0.88, 1.0, p.y));
                // reveal mask: the sprite stays full size, pixels right of the progress cut are hidden (1px soft edge)
                float cut = _Progress * _FillSize.x;
                float reveal = saturate(cut - px.x + 0.5);
                float edge = smoothstep(cut - max(_EdgeWidth, 1.0), cut, px.x);

                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                color.rgb = lerp(color.rgb, _StripeColor.rgb, stripe * _StripeColor.a);
                color.rgb += (gloss * _GlossColor.rgb * _GlossColor.a + edge * _EdgeColor.rgb * _EdgeColor.a) * color.a;
                color.a *= reveal;

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
        ENDCG
        }
    }
}
