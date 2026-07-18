Shader "Custom/TerrainVoxelShader"
{
    Properties
    {
        [NoScaleOffset] _BaseMapArray("Base Texture Array", 2DArray) = "white" {}
        [NoScaleOffset] _OverlayMapArray("Overlay Texture Array", 2DArray) = "white" {}
        [NoScaleOffset] _NormalMapArray("Normal Texture Array", 2DArray) = "bump" {}
        [NoScaleOffset] _SpecularMapArray("Specular Texture Array", 2DArray) = "black" {}
        
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
    }

    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "TransparentCutout" 
            "Queue" = "AlphaTest" 
            "DisableBatching" = "False"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Required for BatchRendererGroup / Entities Graphics
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            // Include your custom unpacking function
            #include "Assets/_Project/Shaders/GetVertexData.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION; // Required by the GPU input layout
                float4 uv7 : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for DOTS/BRG
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : NORMAL;
                float4 tangentWS : TANGENT;
                float2 uv : TEXCOORD1;
                
                float4 texIDs : TEXCOORD3; 
                float ao : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID // Required for DOTS/BRG
            };

            TEXTURE2D_ARRAY(_BaseMapArray); SAMPLER(sampler_BaseMapArray);
            TEXTURE2D_ARRAY(_OverlayMapArray); SAMPLER(sampler_OverlayMapArray);
            TEXTURE2D_ARRAY(_NormalMapArray); SAMPLER(sampler_NormalMapArray);
            TEXTURE2D_ARRAY(_SpecularMapArray); SAMPLER(sampler_SpecularMapArray);

            // Fixes the SRP Batcher Compatibility Error
            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // Initialize instancing variables
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                
                float3 posOS, normOS, tanOS;
                float2 uv;
                float4 color;
                float texBase, texOverlay, texNormal, texSpec, ao;

                // Unpack the data sent from your C# Chunk Mesher
                UnpackBlockVertex_float(input.uv7, posOS, normOS, tanOS, uv, color, texBase, texOverlay, texNormal, texSpec, ao);

                // Safely transform positions using the matrix sent by the BatchRendererGroup
                output.positionWS = TransformObjectToWorld(posOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                
                output.normalWS = TransformObjectToWorldNormal(normOS);
                output.tangentWS = float4(TransformObjectToWorldDir(tanOS), 1.0);

                output.uv = uv;
                output.ao = ao;
                output.texIDs = float4(texBase, texOverlay, texNormal, texSpec);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input); // Allow frag shader to access instance data

                // 1. Sample Textures
                float4 baseCol = SAMPLE_TEXTURE2D_ARRAY(_BaseMapArray, sampler_BaseMapArray, input.uv, input.texIDs.x);
                float4 overlayCol = SAMPLE_TEXTURE2D_ARRAY(_OverlayMapArray, sampler_OverlayMapArray, input.uv, input.texIDs.y);
                
                // 2. Alpha Cutout (Transparency)
                // Looks at BOTH textures. If both are transparent, it cuts a hole (perfect for leaves).
                float finalAlpha = max(baseCol.a, overlayCol.a);
                clip(finalAlpha - _Cutoff);

                // 3. Calculate Green Height Gradient
                float h = saturate(input.positionWS.y / 32.0);
                float3 grassBottom = float3(0.076, 0.273, 0.076);
                float3 grassTop = float3(0.211, 0.886, 0.079);
                float3 heightTint = lerp(grassBottom, grassTop, h);

                // 4. Tint ALL Overlays automatically
                overlayCol.rgb *= heightTint;

                // 5. Blend Base and Overlay
                // If Overlay Alpha is 0 (like your blank texture at index 0), it cleanly outputs the Base Texture.
                float3 finalAlbedo = lerp(baseCol.rgb, overlayCol.rgb, overlayCol.a);

                // 6. Basic Voxel Lighting & AO
                Light mainLight = GetMainLight();
                float NdotL = saturate(dot(normalize(input.normalWS), mainLight.direction));
                
                float3 lighting = mainLight.color * NdotL * input.ao;
                float3 ambient = float3(0.2, 0.2, 0.2) * input.ao; 
                
                float3 finalColor = finalAlbedo * (lighting + ambient);

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        
        // -----------------------------------------------------
        // SHADOW CASTER PASS
        // -----------------------------------------------------
        Pass
        {
            Name "ShadowCaster"
            Tags{"LightMode" = "ShadowCaster"}

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // Required for BatchRendererGroup / Entities Graphics
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/_Project/Shaders/GetVertexData.hlsl"

            struct Attributes
            {
                float3 positionOS : POSITION;
                float4 uv7 : TEXCOORD7;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 texIDs : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            TEXTURE2D_ARRAY(_BaseMapArray); SAMPLER(sampler_BaseMapArray);
            TEXTURE2D_ARRAY(_OverlayMapArray); SAMPLER(sampler_OverlayMapArray);
            
            CBUFFER_START(UnityPerMaterial)
                float _Cutoff;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 posOS, normOS, tanOS;
                float2 uv;
                float4 color;
                float texBase, texOverlay, texNormal, texSpec, ao;

                UnpackBlockVertex_float(input.uv7, posOS, normOS, tanOS, uv, color, texBase, texOverlay, texNormal, texSpec, ao);

                output.positionCS = TransformObjectToHClip(posOS);
                output.uv = uv;
                output.texIDs = float4(texBase, texOverlay, texNormal, texSpec);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                float4 baseCol = SAMPLE_TEXTURE2D_ARRAY(_BaseMapArray, sampler_BaseMapArray, input.uv, input.texIDs.x);
                float4 overlayCol = SAMPLE_TEXTURE2D_ARRAY(_OverlayMapArray, sampler_OverlayMapArray, input.uv, input.texIDs.y);
                
                // Cut holes in the shadows to perfectly match the leaves
                float finalAlpha = max(baseCol.a, overlayCol.a);
                clip(finalAlpha - _Cutoff);

                return 0;
            }
            ENDHLSL
        }
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}