Shader "Hidden/Marubatsu/ArcadePortrait"
{
    Properties
    {
        _MainTex ("Portrait", 2D) = "white" {}
        _Crop ("Crop", Vector) = (0, 0, 1, 1)
        _Key ("Backdrop color", Vector) = (0.12, 0.15, 0.2, 1)
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
            float4 _Crop, _Key;

            fixed4 frag(v2f_img input) : SV_Target
            {
                float3 rgb = tex2D(_MainTex, _Crop.xy + input.uv * _Crop.zw).rgb;
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = LinearToGammaSpace(rgb);
                #endif
                float coverage = smoothstep(0.018, 0.085, distance(rgb, _Key.rgb));
                #ifndef UNITY_COLORSPACE_GAMMA
                    rgb = GammaToLinearSpace(rgb);
                #endif
                return fixed4(rgb, coverage);
            }
            ENDCG
        }
    }
}
