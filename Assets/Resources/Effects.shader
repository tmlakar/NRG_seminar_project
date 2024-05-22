Shader "Custom/Effects" {
    
    Properties {
        _MainTex ("Texture", 2D) = "white" {}
    }
    
    SubShader {
        CGINCLUDE
        #include "UnityCG.cginc"
        #include "UnityStandardBRDF.cginc"

        sampler2D _MainTex;
        Texture2D _CameraDepthTexture;
        SamplerState point_clamp_sampler;
        SamplerState linear_clamp_sampler;
        float4 _MainTex_TexelSize;
        float4 _MainTex_ST;

        struct VertexData {
            float4 vertex : POSITION;
            float2 uv : TEXCOORD0;
        };

        struct v2f {
            float2 uv : TEXCOORD0;
            float4 vertex : SV_POSITION;
        };

        v2f vp(VertexData v) {
            v2f o;
            o.vertex = UnityObjectToClipPos(v.vertex);
            o.uv = v.uv;
            return o;
        }
        ENDCG

        Pass {
            CGPROGRAM
            #pragma vertex vp
            #pragma fragment fp

            float4 fp(v2f i) : SV_Target {
                return _CameraDepthTexture.Sample(point_clamp_sampler, i.uv).r;
            }

            ENDCG
        }

        Pass {
            CGPROGRAM
            #pragma vertex vp
            #pragma fragment fp

            sampler2D _SmokeTex, _SmokeMaskTex;
            Texture2D _DepthTex;
            int _DebugView;
            float _Sharpness;

            float4 fp(v2f i) : SV_Target {
                float4 col = tex2D(_MainTex, i.uv);
                float4 smokeAlbedo = tex2D(_SmokeTex, i.uv);
                float smokeMask = saturate(tex2D(_SmokeMaskTex, i.uv).r);

                //Apply Sharpness
                float neighbor = _Sharpness * -1;
                float center = _Sharpness * 4 + 1;

                float4 n = tex2D(_SmokeTex, i.uv + _MainTex_TexelSize.xy * float2(0, 1));
                float4 e = tex2D(_SmokeTex, i.uv + _MainTex_TexelSize.xy * float2(1, 0));
                float4 s = tex2D(_SmokeTex, i.uv + _MainTex_TexelSize.xy * float2(0, -1));
                float4 w = tex2D(_SmokeTex, i.uv + _MainTex_TexelSize.xy * float2(-1, 0));

                float4 sharpenedSmoke = n * neighbor + e * neighbor + smokeAlbedo * center + s * neighbor + w * neighbor;

                switch (_DebugView) {
                    case 0:
                        // return col + smokeAlbedo (combine color of the scene beforehand with color of the smoke)
                        return lerp(col, saturate(sharpenedSmoke), 1 - smokeMask);
                    case 1:
                        return saturate(sharpenedSmoke);
                    case 2:
                        // returns smoke mask
                        return 1 - smokeMask;
                    case 3:
                        // returns depth tex
                        return _DepthTex.Sample(point_clamp_sampler, i.uv);
                }

                return float4(1, 0, 1, 0);
            }

            ENDCG
        }
    }
}