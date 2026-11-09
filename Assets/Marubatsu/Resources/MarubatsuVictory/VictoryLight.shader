Shader "Marubatsu/VictoryLight"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Profile ("Strip / Disc / Flake / Star", Float) = 0
        [Enum(UnityEngine.Rendering.BlendMode)] _Destination ("Destination blend", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One [_Destination]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Tint;
            float _Profile;

            struct Vertex
            {
                float4 position : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };
            struct Fragment
            {
                float4 position : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Fragment vert(Vertex vertex)
            {
                Fragment output;
                output.position = UnityObjectToClipPos(vertex.position);
                output.color = vertex.color * _Tint;
                output.uv = vertex.uv;
                return output;
            }

            float4 frag(Fragment input) : SV_Target
            {
                float2 centered = input.uv * 2 - 1;
                float distance = _Profile < 0.5 ? abs(centered.y) : length(centered);
                float edge = saturate(1 - distance);
                float coverage = _Profile > 1.5 ? 1 : edge * edge;
                if (_Profile > 2.5)
                {
                    float2 points = abs(centered);
                    coverage = saturate((1 - max(points.x, points.y) - min(points.x, points.y) * 3.4) * 6);
                }
                float alpha = input.color.a * coverage;
                return float4(input.color.rgb * alpha, alpha);
            }
            ENDCG
        }
    }
}
