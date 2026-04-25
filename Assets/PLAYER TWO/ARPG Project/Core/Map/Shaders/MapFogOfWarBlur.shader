Shader "PLAYER TWO/ARPG Project/Map Fog Of War Blur"
{
    Properties
    {
        _MainTex ("Fog Texture", 2D) = "white" {}
        _HiddenColor ("Hidden Color", Color) = (0, 0, 0, 1)
        _DiscoveredColor ("Discovered Color", Color) = (0, 0, 0, 0)
        _BlurSize ("Blur Size", Float) = 0.008
        _EdgeBias ("Edge Bias", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM

            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _HiddenColor;
            float4 _DiscoveredColor;
            float _BlurSize;
            float _EdgeBias;

            fixed4 frag(v2f_img i) : SV_Target
            {
                float sum = 0.0;

                [unroll] for (int y = -2; y <= 2; y++)
                [unroll] for (int x = -2; x <= 2; x++)
                    sum += tex2D(_MainTex, i.uv + float2(x, y) * _BlurSize).r;

                float t = saturate((sum / 25.0 - _EdgeBias) / max(1.0 - _EdgeBias, 0.001));
                return lerp(_HiddenColor, _DiscoveredColor, t);
            }

            ENDCG
        }
    }
}
