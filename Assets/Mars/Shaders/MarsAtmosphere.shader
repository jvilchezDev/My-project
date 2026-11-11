Shader "Mars/Atmosphere Rim"
{
    // Translucent fresnel rim for a thin atmosphere shell (pinkish-orange).
    // Place on a sphere slightly larger than the planet. Transparent, no depth write.
    Properties
    {
        _RimColor ("Rim Color", Color) = (1.0, 0.55, 0.42, 1)
        _Intensity ("Intensity", Range(0, 5)) = 1.6
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3.0
        _Opacity ("Max Opacity", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "AtmosphereRim"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            Blend SrcAlpha One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RimColor;
                half _Intensity;
                half _FresnelPower;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
                float3 viewDirWS  : TEXCOORD1;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionCS = TransformWorldToHClip(posWS);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS = GetWorldSpaceViewDir(posWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Cull Front shows the inside of the shell: the rim is where the surface
                // is most edge-on to the viewer. Flip the normal so it points toward the camera.
                float3 N = -normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = pow(saturate(1.0 - abs(dot(N, V))), _FresnelPower);
                half alpha = saturate(fresnel * _Intensity) * _Opacity;
                return half4(_RimColor.rgb * fresnel * _Intensity, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
