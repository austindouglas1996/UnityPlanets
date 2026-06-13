#ifndef VORONOI_NOISE_INCLUDED
#define VORONOI_NOISE_INCLUDED

#include "../Lib/PerlinNoise.hlsl"

float3 hash3_01(int3 p)
{
    uint x = (uint) p.x;
    uint y = (uint) p.y;
    uint z = (uint) p.z;

    // Mix with large, odd primes
    uint h = x * 0x9E3779B1u
           ^ y * 0x85EBCA77u
           ^ z * 0xC2B2AE3Du;

    // Avalanche
    h ^= h >> 16;
    h *= 0x7FEB352Du;
    h ^= h >> 15;
    h *= 0x846CA68Bu;
    h ^= h >> 16;

    // Derive 3 decorrelated values
    uint h1 = h;
    uint h2 = h * 0x27D4EB2Du;
    uint h3 = h * 0x165667B1u;

    return float3(
        h1 * (1.0 / 4294967296.0),
        h2 * (1.0 / 4294967296.0),
        h3 * (1.0 / 4294967296.0)
    );
}

struct WorleyResult
{
    float dist;
    float dist2;
    int3 cell;
};

WorleyResult worleyCell(float3 p)
{
    float minDist = 1e9;
    float secondMinDist = 1e9;
    int3 bestCell = 0;

    int3 ip = (int3) floor(p);
    float3 fp = frac(p);

    const int SEED = 1337;

    for (int xo = -1; xo <= 1; xo++)
        for (int yo = -1; yo <= 1; yo++)
            for (int zo = -1; zo <= 1; zo++)
            {
                int3 cell = ip + int3(xo, yo, zo);

                float3 h = hash3_01(cell);

                float3 diff = (cell + h) - (ip + fp);

                float d = dot(diff, diff);
                if (d < minDist)
                {
                    minDist = d;
                    bestCell = cell;
                }
                else if (d < secondMinDist)
                {
                    secondMinDist = d;
                }
            }

    WorleyResult r;
    r.dist = sqrt(minDist);
    r.dist2 = sqrt(secondMinDist);
    r.cell = bestCell;
    return r;
}


#endif