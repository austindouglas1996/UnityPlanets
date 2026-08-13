//
// Perlin noise generator for Unity
// Keijiro Takahashi, 2013, 2015
// https://github.com/keijiro/PerlinNoise
//
// Based on the original implementation by Ken Perlin
// http://mrl.nyu.edu/~perlin/noise/
//
// This was in C#, did the work to bring to a compute.

#ifndef PERLIN_NOISE_INCLUDED
#define PERLIN_NOISE_INCLUDED

// Fade function
float fade(float t)
{
    return t * t * t * (t * (t * 6 - 15) + 10);
}

float lerpCustom(float t, float a, float b)
{
    return a + t * (b - a);
}

float grad(int hash, float x)
{
    return (hash & 1) == 0 ? x : -x;
}

float grad(int hash, float x, float y)
{
    return ((hash & 1) == 0 ? x : -x) + ((hash & 2) == 0 ? y : -y);
}

float grad(int hash, float x, float y, float z)
{
    int h = hash & 15;
    float u = h < 8 ? x : y;
    float v = h < 4 ? y : (h == 12 || h == 14 ? x : z);
    return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
}

// Ken Perlin's 512-entry permutation table, uploaded from the CPU as a buffer
// (see PerlinPermTable.cs). It used to be a `static const int perm[512]`, but
// FXC compiles data-dependent indexing into a large const array extremely
// slowly (dozens of inlined noise calls -> 45-70s rebuilds). A StructuredBuffer
// load compiles instantly. Values are identical, so noise output is unchanged.
StructuredBuffer<int> perm;

float noise3D(float x, float y, float z)
{
    int X = (int) floor(x) & 255;
    int Y = (int) floor(y) & 255;
    int Z = (int) floor(z) & 255;

    x -= floor(x);
    y -= floor(y);
    z -= floor(z);

    float u = fade(x);
    float v = fade(y);
    float w = fade(z);

    int A = (perm[X] + Y) & 255;
    int AA = (perm[A] + Z) & 255;
    int AB = (perm[A + 1] + Z) & 255;
    int B = (perm[X + 1] + Y) & 255;
    int BA = (perm[B] + Z) & 255;
    int BB = (perm[B + 1] + Z) & 255;

    return lerpCustom(w,
        lerpCustom(v,
            lerpCustom(u, grad(perm[AA], x, y, z), grad(perm[BA], x - 1, y, z)),
            lerpCustom(u, grad(perm[AB], x, y - 1, z), grad(perm[BB], x - 1, y - 1, z))),
        lerpCustom(v,
            lerpCustom(u, grad(perm[AA + 1], x, y, z - 1), grad(perm[BA + 1], x - 1, y, z - 1)),
            lerpCustom(u, grad(perm[AB + 1], x, y - 1, z - 1), grad(perm[BB + 1], x - 1, y - 1, z - 1)))
    );
}

// 1D Perlin noise
float noise1D(float x)
{
    int X = (int) floor(x) & 255;
    x -= floor(x);
    float u = fade(x);
    return lerpCustom(u, grad(perm[X], x), grad(perm[X + 1], x - 1)) * 2.0;
}

// 2D Perlin noise
float noise2D(float x, float y)
{
    int X = (int) floor(x) & 255;
    int Y = (int) floor(y) & 255;

    x -= floor(x);
    y -= floor(y);

    float u = fade(x);
    float v = fade(y);

    int A = (perm[X] + Y) & 255;
    int B = (perm[X + 1] + Y) & 255;

    return lerpCustom(v,
        lerpCustom(u, grad(perm[A], x, y), grad(perm[B], x - 1, y)),
        lerpCustom(u, grad(perm[A + 1], x, y - 1), grad(perm[B + 1], x - 1, y - 1))
    );
}


float fbm1D(float x, int octaves)
{
    float f = 0.0;
    float w = 0.5;
    
    [loop]
    for (int i = 0; i < octaves; i++)
    {
        f += w * noise1D(x);
        x *= 2.0;
        w *= 0.5;
    }
    return f;
}

float fbm2D(float2 coord, int octaves)
{
    float f = 0.0;
    float w = 0.5;
    
    [loop]
    for (int i = 0; i < octaves; i++)
    {
        f += w * noise2D(coord.x, coord.y);
        coord *= 2.0;
        w *= 0.5;
    }
    return f;
}

float fbm2D(float x, float y, int octaves)
{
    return fbm2D(float2(x, y), octaves);
}

float fbm3D(float3 coord, int octaves)
{
    float f = 0.0;
    float w = 0.5;
    
    [loop]
    for (int i = 0; i < octaves; i++)
    {
        f += w * noise3D(coord.x, coord.y, coord.z);
        coord *= 2.0;
        w *= 0.5;
    }
    return f;
}

float fbm3D(float x, float y, float z, int octaves)
{
    return fbm3D(float3(x, y, z), octaves);
}

float fbmRidged(float3 p)
{
    float r = 1.0 - abs(fbm3D(p, 4));
    return r * r * r;
}

inline float Turbulence(float3 p)
{
    return abs(fbm3D(p, 5) * 2 -1);
}

float hash1(int3 p)
{
    // Large primes, no mysticism
    uint h = uint(p.x) * 374761393u
           + uint(p.y) * 668265263u
           + uint(p.z) * 2147483647u;

    h = (h ^ (h >> 13)) * 1274126177u;
    h ^= (h >> 16);

    return h * (1.0 / 4294967296.0); // 0..1
}

float hash1_2D(int2 p)
{
    // Same primes, fewer dimensions
    uint h = uint(p.x) * 374761393u
           + uint(p.y) * 668265263u;

    h = (h ^ (h >> 13)) * 1274126177u;
    h ^= (h >> 16);

    return h * (1.0 / 4294967296.0); // 0..1
}


float3 hash3(int3 p)
{
    // Large primes ensure good spatial hashing
    p = p * 1664525 + 1013904223;

    // Mix bits
    p ^= (p.yzx << 5);
    p ^= (p.zxy >> 3);

    // Convert to float and normalize to [0,1]
    return frac(float3(p) * 0.0000001192092896); // 1 / 2^23
}

float worley(float3 p)
{
    float dist = 1e9;

    int3 ip = (int3) floor(p);
    float3 fp = frac(p);

    // Check 27 neighboring cells
    [unroll]
    for (int xo = -1; xo <= 1; xo++)
        for (int yo = -1; yo <= 1; yo++)
            for (int zo = -1; zo <= 1; zo++)
            {
                float3 cell = float3(xo, yo, zo);
                float3 h = hash3(ip + cell); // Random feature point
                float3 diff = cell + h - fp;

                dist = min(dist, dot(diff, diff)); // squared distance
            }

    return saturate(dist * 3.0); // scale
}

float worleyWarped(float3 p)
{
    float warpStrength = 1.5;

    float3 warp = float3(
        fbm3D(p * 0.4, 3),
        fbm3D(p * 0.4 + 13.7, 3),
        fbm3D(p * 0.4 + 33.1, 3)
    );

    p += warp * warpStrength;

    return worley(p);
}

#endif