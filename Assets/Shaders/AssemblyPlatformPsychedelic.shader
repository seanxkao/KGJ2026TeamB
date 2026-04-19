Shader "KGJ/AssemblyPlatformPsychedelic"
{
    Properties
    {
        _Brightness ("Brightness", Range(0, 2)) = 0.88
        _Saturation ("Saturation", Range(0, 1)) = 0.38
        _HueSpeed ("Hue Speed", Float) = 0.22
        _PatternScale ("Pattern Scale", Float) = 0.35
        _ScrollSpeedX ("Scroll Speed X", Float) = 0.11
        _ScrollSpeedZ ("Scroll Speed Z", Float) = 0.07
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            half _Brightness;
            half _Saturation;
            half _HueSpeed;
            half _PatternScale;
            half _ScrollSpeedX;
            half _ScrollSpeedZ;

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            float3 hsv2rgb(float3 c)
            {
                float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 w = i.worldPos.xz * _PatternScale;
                float t = _Time.y;
                w.x += t * _ScrollSpeedX;
                w.y += t * _ScrollSpeedZ;
                float r = length(w);
                float ang = atan2(w.y, w.x);
                float rings = sin(r * 16.0 + t * 2.1) * 0.5 + 0.5;
                float swirl = sin(ang * 6.0 + r * 2.4 - t * 1.8);
                float h = frac(
                    rings * 0.27 + swirl * 0.19 + t * _HueSpeed
                    + dot(i.worldPos.xz, float2(0.053, 0.071)));
                float3 col = hsv2rgb(float3(h, _Saturation, _Brightness));
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
