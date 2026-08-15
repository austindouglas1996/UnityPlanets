#ifndef WORLD_SHAPING_INCLUDED
#define WORLD_SHAPING_INCLUDED

#include "ChunkFunctions.hlsl"   // TerrainDensityOptions cbuffer (via ChunkCommon) + grid math
#include "Lib/PerlinNoise.hlsl"  // fbm2D / N01

// ============================================================================
// WorldShaping
//
// Shared terrain-shaping primitives, used by BOTH:
//   - WorldNoise.hlsl          → the density / surface-height field
//   - Compute/Details.compute  → per-vertex coloring
//
// Coloring reconstructs the ocean and mountain masks to tint terrain by *type*
// (sea, coast, mountain) instead of by absolute height. Keeping those masks in
// ONE place here means the colors can never fall out of sync with the shape.
//
// Everything is a pure function of world-space XZ plus the TerrainDensityOptions
// cbuffer, so generation stays fully deterministic.
// ============================================================================

// Each layer samples the noise field at a fixed, distinct base offset so features
// (hills, mountain regions, lakes, continents) do NOT stack on top of one another
// by default. The exact values are arbitrary but constant.
static const float2 DOMAIN_BASE       = float2( 13.1,  37.4);
static const float2 DOMAIN_HILL       = float2( 72.8,   5.3);
static const float2 DOMAIN_MTN_REGION = float2( 47.3,  91.7);
static const float2 DOMAIN_MTN_DETAIL = float2(  3.2,  88.1);
static const float2 DOMAIN_LAKE       = float2(129.7, 641.2);
static const float2 DOMAIN_CONTINENT  = float2(511.4, 205.9);

// Spread a per-layer scalar SeedOffset into a 2D domain shift. Different x/z
// multipliers avoid a purely diagonal shift when the user nudges one layer.
inline float2 LayerShift(float seedOffset)
{
    return float2(seedOffset, seedOffset * 1.37);
}

// The global Seed shifts EVERY layer's domain, so changing Seed yields a whole new
// world. The seed is HASHED into a small, bounded offset in [0,256) — it must NOT be
// multiplied straight into the domain: a large seed (this project's is in the
// millions) would push the Perlin sample coordinates to ~1e6, where a 32-bit float
// can only resolve ~0.25-unit steps. That precision loss quantizes the tiny
// (worldXZ * frequency) detail and bakes the terrain into flat, cell-shaped shelves.
// A bounded offset keeps sample coordinates near the origin (full precision) while
// still fully decorrelating one seed from another.
inline float2 GlobalSeedShift()
{
    uint s = (uint) Seed;

    // Two independent integer hashes of the seed (bit-mixing only, no large-magnitude
    // float math). Constants are Wellons' lowbias32 mixers.
    uint hx = s * 0x9E3779B1u; hx ^= hx >> 16; hx *= 0x7FEB352Du; hx ^= hx >> 15;
    uint hy = s * 0x85EBCA77u; hy ^= hy >> 16; hy *= 0x846CA68Bu; hy ^= hy >> 15;

    // Low 16 bits → [0,256) with a fractional part. Bounded, deterministic, precise.
    return float2(hx & 0xFFFFu, hy & 0xFFFFu) * (256.0 / 65536.0);
}

// Assemble the final 2D sample coordinate for a layer:
//   world XZ * frequency, then offset into an isolated, seed-shifted region.
inline float2 LayerCoord(float2 uv, float frequency, float2 domain, float seedOffset)
{
    return uv * frequency + domain + LayerShift(seedOffset) + GlobalSeedShift();
}

// ── Continent field (0..1) — drives the optional Ocean layer ────────────────
// Deliberately low-octave: continents should be broad and smooth, not detailed.
inline float ContinentValue(float2 uv)
{
    return N01(fbm2D(LayerCoord(uv, ContinentFrequency, DOMAIN_CONTINENT, OceanSeedOffset), 3));
}

// Ocean coverage in [0,1]: 0 = dry land, 1 = open sea. Zero when oceans disabled.
// Below SeaLevelThreshold => sea; CoastSoftness widens the shoreline blend so the
// waterline is a beach, not a cliff.
inline float OceanMask(float2 uv)
{
    if (OceansEnabled == 0)
        return 0.0;

    float continent = ContinentValue(uv);
    return 1.0 - smoothstep(SeaLevelThreshold - CoastSoftness,
                            SeaLevelThreshold + CoastSoftness,
                            continent);
}

// Mountain region coverage in [0,1]: where tall terrain is *allowed* to appear.
// Zero when mountains disabled. Higher MountainCoverage lowers the bar to qualify,
// so more of the land becomes mountainous; MountainRangeSoftness is the edge blend.
inline float MountainRegionMask(float2 uv)
{
    if (MountainsEnabled == 0)
        return 0.0;

    float region = N01(fbm2D(LayerCoord(uv, MountainRangeFrequency, DOMAIN_MTN_REGION, MountainSeedOffset), 3));
    float thresh = 1.0 - saturate(MountainCoverage);
    return smoothstep(thresh, thresh + MountainRangeSoftness, region);
}

#endif
