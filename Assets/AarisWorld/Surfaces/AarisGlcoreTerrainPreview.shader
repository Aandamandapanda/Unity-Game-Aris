// Linux OpenGL Core preview only. Android retains the native URP Terrain/Lit shader.
Shader "Hidden/Aaris/GlcoreTerrainPreview"
{
    Properties
    {
        [HideInInspector] _Control ("Terrain control", 2D) = "red" {}
        [HideInInspector] _Splat0 ("Terrain layer 0", 2D) = "grey" {}
        [HideInInspector] _Splat1 ("Terrain layer 1", 2D) = "grey" {}
        [HideInInspector] _Splat2 ("Terrain layer 2", 2D) = "grey" {}
        [HideInInspector] _Splat3 ("Terrain layer 3", 2D) = "grey" {}
        [HideInInspector] _Control1 ("Terrain control 1", 2D) = "black" {}
        [HideInInspector] _Control2 ("Terrain control 2", 2D) = "black" {}
        [HideInInspector] _Splat4 ("Terrain layer 4", 2D) = "grey" {}
        [HideInInspector] _Splat5 ("Terrain layer 5", 2D) = "grey" {}
        [HideInInspector] _Splat6 ("Terrain layer 6", 2D) = "grey" {}
        [HideInInspector] _Splat7 ("Terrain layer 7", 2D) = "grey" {}
        [HideInInspector] _Splat8 ("Terrain layer 8", 2D) = "grey" {}
        [HideInInspector] _Splat9 ("Terrain layer 9", 2D) = "grey" {}
    }
    SubShader
    {
        Tags { "Queue"="Geometry-100" "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "TerrainCompatible"="True" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma only_renderers glcore
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_Control); SAMPLER(sampler_Control);
            TEXTURE2D(_Splat0); SAMPLER(sampler_Splat0);
            TEXTURE2D(_Splat1); SAMPLER(sampler_Splat1);
            TEXTURE2D(_Splat2); SAMPLER(sampler_Splat2);
            TEXTURE2D(_Splat3); SAMPLER(sampler_Splat3);
            TEXTURE2D(_Control1); SAMPLER(sampler_Control1);
            TEXTURE2D(_Control2); SAMPLER(sampler_Control2);
            TEXTURE2D(_Splat4); SAMPLER(sampler_Splat4);
            TEXTURE2D(_Splat5); SAMPLER(sampler_Splat5);
            TEXTURE2D(_Splat6); SAMPLER(sampler_Splat6);
            TEXTURE2D(_Splat7); SAMPLER(sampler_Splat7);
            TEXTURE2D(_Splat8); SAMPLER(sampler_Splat8);
            TEXTURE2D(_Splat9); SAMPLER(sampler_Splat9);

            CBUFFER_START(UnityPerMaterial)
                float4 _Splat0_ST;
                float4 _Splat1_ST;
                float4 _Splat2_ST;
                float4 _Splat3_ST;
                float4 _Splat4_ST;
                float4 _Splat5_ST;
                float4 _Splat6_ST;
                float4 _Splat7_ST;
                float4 _Splat8_ST;
                float4 _Splat9_ST;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 worldPosition = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(worldPosition);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Blend the source terrain's secondary splat controls in
                // Linux GLCore previews, not in Android's native URP renderer.
                float4 w0 = max(SAMPLE_TEXTURE2D(_Control, sampler_Control, input.uv), 0);
                float4 w1 = max(SAMPLE_TEXTURE2D(_Control1, sampler_Control1, input.uv), 0);
                float4 w2 = max(SAMPLE_TEXTURE2D(_Control2, sampler_Control2, input.uv), 0);
                float total = max(dot(w0 + w1 + w2, float4(1,1,1,1)), 0.0001);
                float3 albedo =
                    w0.r * SAMPLE_TEXTURE2D(_Splat0, sampler_Splat0, input.uv * _Splat0_ST.xy + _Splat0_ST.zw).rgb +
                    w0.g * SAMPLE_TEXTURE2D(_Splat1, sampler_Splat1, input.uv * _Splat1_ST.xy + _Splat1_ST.zw).rgb +
                    w0.b * SAMPLE_TEXTURE2D(_Splat2, sampler_Splat2, input.uv * _Splat2_ST.xy + _Splat2_ST.zw).rgb +
                    w0.a * SAMPLE_TEXTURE2D(_Splat3, sampler_Splat3, input.uv * _Splat3_ST.xy + _Splat3_ST.zw).rgb +
                    w1.r * SAMPLE_TEXTURE2D(_Splat4, sampler_Splat4, input.uv * _Splat4_ST.xy + _Splat4_ST.zw).rgb +
                    w1.g * SAMPLE_TEXTURE2D(_Splat5, sampler_Splat5, input.uv * _Splat5_ST.xy + _Splat5_ST.zw).rgb +
                    w1.b * SAMPLE_TEXTURE2D(_Splat6, sampler_Splat6, input.uv * _Splat6_ST.xy + _Splat6_ST.zw).rgb +
                    w1.a * SAMPLE_TEXTURE2D(_Splat7, sampler_Splat7, input.uv * _Splat7_ST.xy + _Splat7_ST.zw).rgb +
                    w2.r * SAMPLE_TEXTURE2D(_Splat8, sampler_Splat8, input.uv * _Splat8_ST.xy + _Splat8_ST.zw).rgb +
                    w2.g * SAMPLE_TEXTURE2D(_Splat9, sampler_Splat9, input.uv * _Splat9_ST.xy + _Splat9_ST.zw).rgb;
                albedo /= total;
                float3 normalWS = normalize(input.normalWS);
                Light sun = GetMainLight();
                float3 lighting = max(SampleSH(normalWS) +
                    sun.color * saturate(dot(normalWS, sun.direction)) * sun.distanceAttenuation,
                    float3(0.24, 0.24, 0.24));
                float3 color = MixFog(albedo * lighting, input.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
