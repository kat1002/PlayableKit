Shader "PlayableKit/PA_FakeShadow"
{
    // Unlit fake shadow: a soft ellipse / rounded box generated from the UVs, alpha-blended onto the
    // ground. No textures, no lighting, no shadow variants - deliberately tiny so it survives Luna's
    // shader stripping and costs one cheap fragment per pixel of the decal.
    Properties
    {
        _Color ("Shadow Color", Color) = (0,0,0,1)
        _Strength ("Strength", Range(0,1)) = 0.45
        _Softness ("Edge Softness", Range(0.05,1)) = 0.6
        _Boxiness ("Boxiness (0 = ellipse, 1 = box)", Range(0,1)) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-100" "IgnoreProjector"="True" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            Offset -1, -1

            CGPROGRAM
            #pragma target 2.0
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            fixed4 _Color;
            half _Strength;
            half _Softness;
            half _Boxiness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed alpha : TEXCOORD1;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                // Vertex alpha lets the baked building mesh vary strength per shadow.
                o.alpha = v.color.a;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half2 p = (i.uv - 0.5h) * 2.0h;
                half radial = length(p);
                half boxy = max(abs(p.x), abs(p.y));
                half d = lerp(radial, boxy, _Boxiness);
                half edge = 1.0h - smoothstep(1.0h - _Softness, 1.0h, d);
                fixed4 col = _Color;
                col.a = saturate(edge * _Strength * i.alpha * _Color.a);
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
