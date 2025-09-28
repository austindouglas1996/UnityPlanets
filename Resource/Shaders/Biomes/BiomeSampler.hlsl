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

    uint h = QuantizeN(hVal,3);
    uint t = QuantizeN(tVal,4);
    uint m = QuantizeN(mVal,3); 
    uint f = QuantizeN(fVal,3);

    return FindBiomeIndex(h, t, m, f);
}

uint GetTriangleBiomePacked(TriangleData triData, int lodIndex)
{
    uint a = SampleBiomeIndex(triData.a);
    uint b = SampleBiomeIndex(triData.b);
    uint c = SampleBiomeIndex(triData.c);

    return PackBiomeIndices(a, b, c, lodIndex);
}

#endif