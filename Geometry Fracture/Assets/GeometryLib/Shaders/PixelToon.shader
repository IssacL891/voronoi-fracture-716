Shader "Custom/PixelToon"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PosterizeLevels ("Posterize Levels", Float) = 4
        _PixelSize ("Pixel Size (samples)", Float) = 32
        _ColorTint ("Color Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100

        Pass
        {
            Cull Off
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _PosterizeLevels;
            float _PixelSize;
            float4 _ColorTint;

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                // pixelate in UV-space by snapping UVs to a low-resolution grid
                if (_PixelSize > 1.0)
                {
                    uv = floor(uv * _PixelSize) / _PixelSize;
                }

                float4 c = tex2D(_MainTex, uv);

                // posterize
                if (_PosterizeLevels > 1.0)
                {
                    c.rgb = floor(c.rgb * _PosterizeLevels) / _PosterizeLevels;
                }

                // apply tint
                c.rgb *= _ColorTint.rgb;

                return c;
            }
            ENDCG
        }
    }
    FallBack "Sprites/Default"
}
