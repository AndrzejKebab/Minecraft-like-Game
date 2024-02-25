Texture2D g_baseTexture : register(t0);              // Base color texture
Texture2D g_nmhTexture : register(t1);               // Normal map and height map texture pair

SamplerState tNormalHeightMap : register(s0);
SamplerState tBase : register(s1);

// render time (width, height)

/*
bool g_bAddSpecular = false;
//float g_fSpecularExponent = 1.f;
float g_fHeightMapScale = 1.f;
float4 g_materialAmbientColor = 1.0f; // Material's diffuse color
float4 g_materialSpecularColor = 1.0f; // Material's specular color
*/
//float g_fSpecularExponent = 1.f;
cbuffer cbPerObject : register(b0)
{
	float4 g_materialAmbientColor : packoffset(c0); // Material's ambient color
	float4 g_materialDiffuseColor : packoffset(c1); // Material's diffuse color
	float4 g_materialSpecularColor : packoffset(c2); // Material's specular color
	float g_fHeightMapScale : packoffset(c3); // Describes the useful range of values for the height field
	float g_fSpecularExponent : packoffset(c3.w); // Material's specular exponent
	bool g_bAddSpecular : packoffset(c5.y); // Toggles rendering with specular or without
};

cbuffer cbPerFrame : register(b1)
{
	float		g_fAmbient : packoffset(c0);
};

//--------------------------------------------------------------------------------------
// Input / Output structures
//--------------------------------------------------------------------------------------
struct PS_INPUT
{
	float3 position          : POSITION;
	float2 texCoord          : TEXCOORD0;
	float3 vLightTS          : TEXCOORD1;   // light vector in tangent space, denormalized
	float3 vViewTS           : TEXCOORD2;   // view vector in tangent space, denormalized
	float2 vParallaxOffsetTS : TEXCOORD3;   // Parallax offset vector in tangent space
	float3 vNormalWS         : TEXCOORD4;   // Normal vector in world space
	float3 vViewWS           : TEXCOORD5;   // View vector in world space
};

float PixelHeight(float4 color)
{
	return color.x / 255;
}

//--------------------------------------------------------------------------------------
// Function:    ComputeIllumination
// 
// Description: Computes phong illumination for the given pixel using its attribute 
//              textures and a light vector.
//--------------------------------------------------------------------------------------
float4 ComputeIllumination(float2 texCoord, float3 vLightTS, float3 vViewTS, float fOcclusionShadow)
{
	// Sample the normal from the normal map for the given texture sample:
	float3 vNormalTS = normalize(g_nmhTexture.SampleLevel(tNormalHeightMap, texCoord, 0) * 2 - 1);

		// Sample base map:
		float4 cBaseColor = g_baseTexture.SampleLevel(tBase, texCoord, 0);

		// Compute diffuse color component:
		float3 vLightTSAdj = float3(vLightTS.x, -vLightTS.y, vLightTS.z);

		float4 cDiffuse = saturate(dot(vNormalTS, vLightTSAdj)) * g_materialDiffuseColor;
		
		// Compute the specular component if desired:  
		float4 cSpecular = 0;
	if (g_bAddSpecular)
	{
		float3 vReflectionTS = normalize(2 * dot(vViewTS, vNormalTS) * vNormalTS - vViewTS);

			float fRdotL = saturate(dot(vReflectionTS, vLightTSAdj));
		cSpecular = saturate(pow(fRdotL, g_fSpecularExponent)) * g_materialSpecularColor;
	}

	// Composite the final color:
	float4 cFinalColor = ((g_materialAmbientColor + cDiffuse) * cBaseColor + cSpecular) * fOcclusionShadow;

		return cFinalColor;
}

//--------------------------------------------------------------------------------------
// Pixel Shader
//--------------------------------------------------------------------------------------
float4 main_half(PS_INPUT i) : SV_TARGET
{
	float4 cBaseColor = g_baseTexture.SampleLevel(tBase, i.texCoord, 0).a;
	//return cBaseColor;

	bool     g_bVisualizeLOD = false;           // Toggles visualization of level of detail colors
	bool     g_bVisualizeMipLevel = false;      // Toggles visualization of mip level
	bool     g_bDisplayShadows = true;         // Toggles display of self-occlusion based shadows

	//float2   g_vTextureDims = float2(0, 0);            // Specifies texture dimensions for computation of mip level at 

	int      g_nLODThreshold = 3;           // The mip level id for transitioning between the full computation
	// for parallax occlusion mapping and the bump mapping computation

	float    g_fShadowSoftening = 0.58f;        // Blurring factor for the soft shadows computation

	int      g_nMinSamples = 8;             // The minimum number of samples for sampling the height field profile
	int      g_nMaxSamples = 50;             // The maximum number of samples for sampling the height field profile
	/*

	float4 vDiffuse = g_baseTexture.Sample(tBase, i.texCoord);

	float fLighting = saturate(dot(vLightTS_p, i.vNormalWS));
	fLighting = max(fLighting, g_fAmbient);

	//return vDiffuse * fLighting;

	return vDiffuse;// float4(i.vParallaxOffsetTS.x, 0, 0, 1);
	*/
	//  Normalize the interpolated vectors:
	float3 vViewTS = normalize(i.vViewTS);
		float3 vViewWS = normalize(i.vViewWS);
		float3 vLightTS = normalize(i.vLightTS);
		float3 vNormalWS = normalize(i.vNormalWS);

		float4 cResultColor = float4(0, 0, 0, 1);

		// Adaptive in-shader level-of-detail system implementation. Compute the 
		// current mip level explicitly in the pixel shader and use this information 
		// to transition between different levels of detail from the full effect to 
		// simple bump mapping. See the above paper for more discussion of the approach
		// and its benefits.

		// Compute the current gradients:
		//float2 fTexCoordsPerSize = i.texCoord * g_vTextureDims;
		float2 fTexCoordsPerSize = i.texCoord;

		// Compute all 4 derivatives in x and y in a single instruction to optimize:
		float2 dxSize, dySize;
	float2 dx, dy;

	float4 result_dx = ddx(float4(fTexCoordsPerSize, i.texCoord));
	dxSize = float2(result_dx.w, result_dx.x);
	dx = float2(result_dx.y, result_dx.z);

	float4 result_dy = ddy(float4(fTexCoordsPerSize, i.texCoord));
	dySize = float2(result_dy.w, result_dy.x);
	dy = float2(result_dy.y, result_dy.z);
	/*
	dxSize = float2(tmp1.w, tmp1.x);
	dx = float2(tmp1.y, tmp1.z);

	dySize = float2(tmp2.w, tmp2.x);
	dy = float2(tmp2.y, tmp2.z);
	*/
	float  fMipLevel;
	float  fMipLevelInt;    // mip level integer portion
	float  fMipLevelFrac;   // mip level fractional amount for blending in between levels

	float  fMinTexCoordDelta;
	float2 dTexCoords;

	// Find min of change in u and v across quad: compute du and dv magnitude across quad
	dTexCoords = dxSize * dxSize + dySize * dySize;

	// Standard mipmapping uses max here
	fMinTexCoordDelta = max(dTexCoords.x, dTexCoords.y);

	// Compute the current mip level  (* 0.5 is effectively computing a square root before )
	fMipLevel = max(0.5 * log2(fMinTexCoordDelta), 0);

	// Start the current sample located at the input texture coordinate, which would correspond
	// to computing a bump mapping result:
	float2 texSample = i.texCoord;

		// Multiplier for visualizing the level of detail (see notes for 'nLODThreshold' variable
		// for how that is done visually)
		float4 cLODColoring = float4(1, 1, 3, 1);

		float fOcclusionShadow = 1.0;

	if (fMipLevel <= (float)g_nLODThreshold)
	{

		//===============================================//
		// Parallax occlusion mapping offset computation //
		//===============================================//

		// Utilize dynamic flow control to change the number of samples per ray 
		// depending on the viewing angle for the surface. Oblique angles require 
		// smaller step sizes to achieve more accurate precision for computing displacement.
		// We express the sampling rate as a linear function of the angle between 
		// the geometric normal and the view direction ray:
		int nNumSteps = (int)lerp(g_nMaxSamples, g_nMinSamples, dot(vViewWS, vNormalWS));

		// Intersect the view ray with the height field profile along the direction of
		// the parallax offset ray (computed in the vertex shader. Note that the code is
		// designed specifically to take advantage of the dynamic flow control constructs
		// in HLSL and is very sensitive to specific syntax. When converting to other examples,
		// if still want to use dynamic flow control in the resulting assembly shader,
		// care must be applied.
		// 
		// In the below steps we approximate the height field profile as piecewise linear
		// curve. We find the pair of endpoints between which the intersection between the 
		// height field profile and the view ray is found and then compute line segment
		// intersection for the view ray and the line segment formed by the two endpoints.
		// This intersection is the displacement offset from the original texture coordinate.
		// See the above paper for more details about the process and derivation.
		//

		float fCurrHeight = 0.0;
		float fStepSize = 1.0 / (float)nNumSteps;
		float fPrevHeight = 1.0;
		float fNextHeight = 0.0;

		int    nStepIndex = 0;
		bool   bCondition = true;

		float2 vTexOffsetPerStep = fStepSize * i.vParallaxOffsetTS;
			float2 vTexCurrentOffset = i.texCoord;
			float  fCurrentBound = 1.0;
		float  fParallaxAmount = 0.0;

		float2 pt1 = 0;
			float2 pt2 = 0;

			float2 texOffset2 = 0;

			[unroll(10)] while (nStepIndex < nNumSteps)
		{
			vTexCurrentOffset -= vTexOffsetPerStep;

			// Sample height map which in this case is stored in the alpha channel of the normal map:
			fCurrHeight = g_nmhTexture.SampleGrad(tNormalHeightMap, vTexCurrentOffset, dx, dy).a;
			
			fCurrentBound -= fStepSize;
			
			if (fCurrHeight > fCurrentBound)
			{
				pt1 = float2(fCurrentBound, fCurrHeight);
				pt2 = float2(fCurrentBound + fStepSize, fPrevHeight);

				texOffset2 = vTexCurrentOffset - vTexOffsetPerStep;

				nStepIndex = nNumSteps + 1;
				fPrevHeight = fCurrHeight;
			}
			else
			{
				nStepIndex++;
				fPrevHeight = fCurrHeight;
			}
			}

		float fDelta2 = pt2.x - pt2.y;
		float fDelta1 = pt1.x - pt1.y;
		//return float4(i.vParallaxOffsetTS, 0, 1);
		float fDenominator = fDelta2 - fDelta1;

		// SM 3.0 requires a check for divide by zero, since that operation will generate
		// an 'Inf' number instead of 0, as previous models (conveniently) did:
		if (fDenominator == 0.0f)
		{
			fParallaxAmount = 0.0f;
		}
		else
		{
			fParallaxAmount = (pt1.x * fDelta2 - pt2.x * fDelta1) / fDenominator;
		}

		float2 vParallaxOffset = i.vParallaxOffsetTS * (1 - fParallaxAmount);

			// The computed texture offset for the displaced point on the pseudo-extruded surface:
			float2 texSampleBase = i.texCoord - vParallaxOffset;
			texSample = texSampleBase;

		// Lerp to bump mapping only if we are in between, transition section:

		cLODColoring = float4(1, 1, 1, 1);

		if (fMipLevel > (float)(g_nLODThreshold - 1))
		{
			// Lerp based on the fractional part:
			fMipLevelFrac = modf(fMipLevel, fMipLevelInt);

			if (g_bVisualizeLOD)
			{
				// For visualizing: lerping from regular POM-resulted color through blue color for transition layer:
				cLODColoring = float4(1, 1, max(1, 2 * fMipLevelFrac), 1);
			}

			// Lerp the texture coordinate from parallax occlusion mapped coordinate to bump mapping
			// smoothly based on the current mip level:
			texSample = lerp(texSampleBase, i.texCoord, fMipLevelFrac);
		}

		if (g_bDisplayShadows == true)
		{
			float2 vLightRayTS = vLightTS.xy * g_fHeightMapScale;

				// Compute the soft blurry shadows taking into account self-occlusion for 
				// features of the height field:

			float sh0 = g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase, dx, dy).a;
			float shA = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.88, dx, dy).a - sh0 - 0.88) * 1 * g_fShadowSoftening;
			float sh9 = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.77, dx, dy).a - sh0 - 0.77) * 2 * g_fShadowSoftening;
			float sh8 = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.66, dx, dy).a - sh0 - 0.66) * 4 * g_fShadowSoftening;
			float sh7 = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.55, dx, dy).a - sh0 - 0.55) * 6 * g_fShadowSoftening;
			float sh6 = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.44, dx, dy).a - sh0 - 0.44) * 8 * g_fShadowSoftening;
			float sh5 = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.33, dx, dy).a - sh0 - 0.33) * 10 * g_fShadowSoftening;
			float sh4 = (g_nmhTexture.SampleGrad(tNormalHeightMap, texSampleBase + vLightRayTS * 0.22, dx, dy).a - sh0 - 0.22) * 12 * g_fShadowSoftening;
			//return sh0;
			// Compute the actual shadow strength:
			fOcclusionShadow = 1 - max(max(max(max(max(max(shA, sh9), sh8), sh7), sh6), sh5), sh4);
			
			// The previous computation overbrightens the image, let's adjust for that:
			fOcclusionShadow = fOcclusionShadow * 0.6 + 0.4;
		}
	}

	// Compute resulting color for the pixel:
	cResultColor = ComputeIllumination(texSample, vLightTS, vViewTS, fOcclusionShadow);

	if (g_bVisualizeLOD)
	{
		cResultColor *= cLODColoring;
	}

	// Visualize currently computed mip level, tinting the color blue if we are in 
	// the region outside of the threshold level:
	if (g_bVisualizeMipLevel)
	{
		cResultColor = fMipLevel.xxxx;
	}

	// If using HDR rendering, make sure to tonemap the resuld color prior to outputting it.
	// But since this example isn't doing that, we just output the computed result color here:
	return cResultColor;
}