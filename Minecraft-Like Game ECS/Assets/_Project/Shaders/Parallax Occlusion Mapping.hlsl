void CustomPOM_float(
    Texture2DArray PackedNormalHeightArray, 
    SamplerState Sampler, 
    float2 UVs, 
    float ArrayIndex, 
    float3 ViewDir, 
    float Amplitude,
    float numSteps,
    out float2 ParallaxUVs)
{
    // 1. Setup raymarching parameters
    float stepSize = Amplitude;
    float currentLayerDepth = 0.0;
    
    // Calculate how much to shift the UVs per step based on view angle
    float2 deltaUV = (ViewDir.xy * Amplitude) / (ViewDir.z * numSteps);
    float2 currentUV = UVs;

    // Sample the HEIGHT (Alpha channel) from the first point
    // Note: If your height is in the Red channel, change .a to .r
    float currentDepthMapValue = SAMPLE_TEXTURE2D_ARRAY(PackedNormalHeightArray, Sampler, currentUV, ArrayIndex).a;

    // Variables to store the previous step for interpolation later
    float2 prevUV = currentUV;
    float prevDepthMapValue = currentDepthMapValue;

    // 2. The Raymarch Loop
    for (int i = 0; i < numSteps; i++)
    {
        // If our virtual ray is deeper than the texture's height, we hit the surface!
        if (currentLayerDepth >= currentDepthMapValue)
            break; 

        // Save current values as "previous" before we take the next step
        prevUV = currentUV;
        prevDepthMapValue = currentDepthMapValue;

        // Step forward: Shift UVs and increase virtual depth
        currentUV -= deltaUV;
        currentLayerDepth += stepSize;

        // Sample the Array again at the new UVs
        currentDepthMapValue = SAMPLE_TEXTURE2D_ARRAY(PackedNormalHeightArray, Sampler, currentUV, ArrayIndex).a;
    }

    // 3. The Interpolator (Smoothing the stair-steps)
    float prevLayerDepth = currentLayerDepth - stepSize;
    
    // Calculate the distance from the surface for both the current and previous step
    float afterDepth = currentDepthMapValue - currentLayerDepth; 
    float beforeDepth = prevDepthMapValue - prevLayerDepth;
    
    // Calculate a weight based on those distances
    float weight = afterDepth / (afterDepth - beforeDepth);
    
    // Linearly interpolate between the previous UV and current UV to find the exact intersection
    ParallaxUVs = prevUV * weight + currentUV * (1.0 - weight);
}