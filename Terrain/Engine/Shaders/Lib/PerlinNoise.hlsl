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

static const int perm[512] =
{
    151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225,
    140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148,
    247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32,
    57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175,
    74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122,
    60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54,
    65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169,
    200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64,
    52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212,
    207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213,
    119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
    129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104,
    218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241,
    81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157,
    184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93,
    222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180,
    151, 160, 137, 91, 90, 15, 131, 13, 201, 95, 96, 53, 194, 233, 7, 225,
    140, 36, 103, 30, 69, 142, 8, 99, 37, 240, 21, 10, 23, 190, 6, 148,
    247, 120, 234, 75, 0, 26, 197, 62, 94, 252, 219, 203, 117, 35, 11, 32,
    57, 177, 33, 88, 237, 149, 56, 87, 174, 20, 125, 136, 171, 168, 68, 175,
    74, 165, 71, 134, 139, 48, 27, 166, 77, 146, 158, 231, 83, 111, 229, 122,
    60, 211, 133, 230, 220, 105, 92, 41, 55, 46, 245, 40, 244, 102, 143, 54,
    65, 25, 63, 161, 1, 216, 80, 73, 209, 76, 132, 187, 208, 89, 18, 169,
    200, 196, 135, 130, 116, 188, 159, 86, 164, 100, 109, 198, 173, 186, 3, 64,
    52, 217, 226, 250, 124, 123, 5, 202, 38, 147, 118, 126, 255, 82, 85, 212,
    207, 206, 59, 227, 47, 16, 58, 17, 182, 189, 28, 42, 223, 183, 170, 213,
    119, 248, 152, 2, 44, 154, 163, 70, 221, 153, 101, 155, 167, 43, 172, 9,
    129, 22, 39, 253, 19, 98, 108, 110, 79, 113, 224, 232, 178, 185, 112, 104,
    218, 246, 97, 228, 251, 34, 242, 193, 238, 210, 144, 12, 191, 179, 162, 241,
    81, 51, 145, 235, 249, 14, 239, 107, 49, 192, 214, 31, 181, 199, 106, 157,
    184, 84, 204, 176, 115, 121, 50, 45, 127, 4, 150, 254, 138, 236, 205, 93,
    222, 114, 67, 29, 24, 72, 243, 141, 128, 195, 78, 66, 215, 61, 156, 180
};

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