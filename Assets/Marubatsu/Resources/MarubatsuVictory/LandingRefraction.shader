Shader "Marubatsu/LandingRefraction"
{
    SubShader
    {
        Tags { "Queue"="Transparent+100" "RenderType"="Transparent" }

        GrabPass { "_MarubatsuBeforeLandingRefraction" }

        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MarubatsuBeforeLandingRefraction;

            struct Vertex
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Fragment
            {
                float4 position : SV_POSITION;
                float4 grab : TEXCOORD0;
                float4 center : TEXCOORD1;
                float2 wave : TEXCOORD2;
            };

            Fragment vert(Vertex vertex)
            {
                Fragment output;
                output.position = UnityObjectToClipPos(vertex.position);
                output.grab = ComputeGrabScreenPos(output.position);
                output.center = ComputeGrabScreenPos(UnityObjectToClipPos(float4(0, 0, 0, 1)));
                output.wave = float2(vertex.uv.y, vertex.color.a);
                return output;
            }

            float4 frag(Fragment input) : SV_Target
            {
                float2 uv = input.grab.xy / input.grab.w;
                float2 center = input.center.xy / input.center.w;
                float2 delta = (uv - center) * _ScreenParams.xy;
                float2 direction = delta * rsqrt(max(dot(delta, delta), 1.0));
                float band = input.wave.x * 2 - 1;
                float envelope = saturate(1 - band * band);
                float displacement = sin(band * UNITY_PI) * envelope * input.wave.y;
                float2 sampleUV = uv + direction * displacement * .055 * _ScreenParams.y / _ScreenParams.xy;
                float3 refracted = tex2D(_MarubatsuBeforeLandingRefraction, saturate(sampleUV)).rgb;
                return float4(refracted, envelope);
            }
            ENDCG
        }
    }
}
