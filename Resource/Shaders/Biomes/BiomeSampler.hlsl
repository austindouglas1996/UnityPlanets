#ifndef BIOMESAMPLER
#define BIOMESAMPLER

#include "../ChunkFunctions.hlsl"
#include "../Algo/SimpleDensityGen.hlsl"
#include "BiomeLookup.hlsl"

uint SampleBiomeIndex(float3 worldPos)
{
    float hVal = SampleHeight(worldPos);
    float tVal = SampleTemperature(worldPos, hVal);
    float mVal = SampleHumidity(worldPos, hVal, tVal);
    float fVal = SampleFoliage(worldPos, hVal, tVal, mVal);

    uint h = Quantize013(hVal);
    uint t = Quantize014(tVal);
    uint m = Quantize013(mVal); 
    uint f = Quantize013(fVal);

    return FindBiomeIndex(h, t, m, f);
}

uint GetTriangleBiomePacked(ChunkTriangleData triData, int lodIndex)
{
    uint a = SampleBiomeIndex(triData.a);
    uint b = SampleBiomeIndex(triData.b);
    uint c = SampleBiomeIndex(triData.c);

    return PackBiomeIndices(a, b, c, lodIndex);
}

#endif