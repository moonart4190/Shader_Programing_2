#ifndef MLS_BLENDING
#define MLS_BLENDING


void MLS_SampleLightmap_float(
    float2 objectData, 
    float2 uv, 
    out real4 backBakeDiffuseLighting)
{
    //real3 illuminance = SampleSingleLightmap(TEXTURE2D_LIGHTMAP_ARGS(lightmapTex, lightmapSampler), LIGHTMAP_EXTRA_ARGS_USE, transform, isStaticLightmap);

    // transform is scale and bias
    uv = uv * unity_LightmapST.xy + unity_LightmapST.zw;
    
    backBakeDiffuseLighting = BlendTwoTextures(0, uv, float4(objectData.x, objectData.y, 0, 0));
}

#endif