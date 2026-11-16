Shader "Marubatsu/LandingFlame"
{
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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
                output.color = vertex.color;
                output.uv = vertex.uv;
                return output;
            }

            float Hash(float2 coordinate)
            {
                float3 p = frac(coordinate.xyx * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float2 coordinate)
            {
                float2 cell = floor(coordinate);
                float2 blend = frac(coordinate);
                blend *= blend * (3 - 2 * blend);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), blend.x),
                            lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), blend.x), blend.y);
            }

            float4 frag(Fragment input) : SV_Target
            {
                float2 uv = input.uv;
                float smoke = step(input.color.r, .45);
                float time = _Time.y * lerp(7.5, 2.2, smoke);
                float2 flow = uv * float2(3.7, 5.2) + float2(input.color.g * 9, -time);
                float broad = Noise(flow);
                float fine = Noise(flow * 2.17 + 17);
                float turbulence = broad * .72 + fine * .28;
                float drift = (Noise(float2(time * .55, uv.y * 3 + input.color.g * 5)) - .5) * .34;
                float2 shape = uv * 2 - 1;
                shape.x += drift;
                float distance = dot(shape, shape);
                float density = 1 - distance + (turbulence - .6) * .88;
                float edgeFade = smoothstep(0, .12, uv.y) * smoothstep(0, .12, 1 - uv.y) *
                                 smoothstep(0, .08, uv.x) * smoothstep(0, .08, 1 - uv.x);
                float coverage = smoothstep(0, .67, density) * edgeFade * input.color.a;
                float core = smoothstep(.35, .93, density) * (.72 + input.color.g * .28);
                float3 fire = lerp(float3(.64, .045, .008), float3(1, .43, .05), saturate(density * 1.7));
                fire = lerp(fire, float3(1, .94, .67), core);
                float3 color = lerp(fire, input.color.rgb * (.58 + .42 * turbulence), smoke);
                return float4(color * coverage, coverage);
            }
            ENDCG
        }
    }
}
