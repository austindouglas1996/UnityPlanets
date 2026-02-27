#ifndef SIMPLEDENSITY_INCLUDED
#define SIMPLEDENSITY_INCLUDED

#include "ChunkFunctions.hlsl"
#include "Lib/PerlinNoise.hlsl"

float GenerateNoiseValue(float3 p)
{
    float continent = N01(fbm2D(p.xz * 0.0005, 4));

    float landW = smoothstep(0.45, 0.55, continent);

    float oceanHeight =
        -130.0
        + fbm2D(p.xz * 0.002, 3) * 25.0;

    float landHeight =
        160.0
        + fbm2D(p.xz * 0.001, 5) * 25.0;

    float height = lerp(oceanHeight, landHeight, landW);

    return p.y - height;
}

#endif