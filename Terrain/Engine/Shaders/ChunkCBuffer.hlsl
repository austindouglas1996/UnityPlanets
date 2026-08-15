#ifndef CHUNK_CBUFFER_STRUCT_INCLUDED
#define CHUNK_CBUFFER_STRUCT_INCLUDED

// ============================================================================
// TerrainDensityOptions (constant buffer)
//
// MUST match the C# struct `TerrainDensityOptions` EXACTLY: field order, types,
// and 16-byte layout. The struct is uploaded raw via SetConstantBuffer, so the
// GPU reads it with HLSL cbuffer packing rules (16-byte rows; a float3 may not
// straddle a 16-byte boundary).
//
// To keep the two layouts identical the block is built from 4-byte scalars only
// (uint/int/float — enums are int on the C# side). The single float3
// (PositionOffset, consumed by the render material) is placed on a 16-byte
// boundary and bracketed by explicit padding, giving a clean 160-byte total.
//
// Byte offsets are annotated so the C#/HLSL correspondence can be checked by eye.
// ============================================================================
cbuffer TerrainDensityOptions
{
    // ── Global (engine) ───────────────────────────────────────
    uint  CellsPerAxis;            //   0  logical voxel width of a chunk (do not change meaning)
    uint  BaseCellStep;            //   4  world size of a LOD0 voxel     (do not change meaning)
    uint  BorderSamplesPerAxis;    //   8  extra edge samples             (do not change meaning)
    int   Seed;                    //  12  world seed; shifts every layer's noise domain
    float ISOLevel;                //  16  marching iso threshold (surface at density == ISOLevel)
    float WorldHeightAmplitude;    //  20  global vertical scale (1.0 = per-layer heights are literal)

    // ── Base land (always on) ─────────────────────────────────
    float BaseElevation;           //  24  reference ground height in world units
    float BaseFrequency;           //  28  base undulation scale (low = broad)
    float BaseAmplitude;           //  32  +/- variation of the base ground
    float BaseSeedOffset;          //  36  domain nudge for the base layer

    // ── Hills ─────────────────────────────────────────────────
    uint  HillsEnabled;            //  40  0 = off, 1 = on
    float HillFrequency;           //  44
    float HillHeight;              //  48  max hill rise (world units)
    float HillThreshold;           //  52  0..1; higher = flatter land, rarer hills
    float HillSeedOffset;          //  56

    // ── Mountains ─────────────────────────────────────────────
    uint  MountainsEnabled;        //  60  0 = off, 1 = on
    float MountainRangeFrequency;  //  64  scale of the region mask (where ranges sit)
    float MountainDetailFrequency; //  68  scale of the peak detail
    float MountainHeight;          //  72  max peak rise (world units)
    float MountainCoverage;        //  76  0..1 fraction of land that becomes mountainous
    float MountainRangeSoftness;   //  80  edge blend of the mountain region (0.01..0.5)
    float MountainSharpness;       //  84  peak sharpening exponent (1 = round, higher = peaky)
    float MountainSeedOffset;      //  88

    // ── Lakes ─────────────────────────────────────────────────
    uint  LakesEnabled;            //  92  0 = off, 1 = on
    float LakeFrequency;           //  96
    float LakeDepth;               // 100  how far a basin carves down (world units)
    float LakeThreshold;           // 104  0..1; keep HIGH so lakes stay isolated blobs
    float LakeEdgeSoftness;        // 108  shore blend (0.01..0.5)
    float LakeSeedOffset;          // 112

    // ── Oceans ────────────────────────────────────────────────
    uint  OceansEnabled;           // 116  0 = off, 1 = on
    float ContinentFrequency;      // 120  continental scale (very low = big seas)
    float SeaLevelThreshold;       // 124  0..1; higher = more ocean coverage
    float CoastSoftness;           // 128  shoreline blend width (0.01..0.5)
    float OceanFloorHeight;        // 132  sea-floor height in world units (usually negative)
    float OceanSeedOffset;         // 136

    float _Pad0;                   // 140  padding → aligns PositionOffset to a 16-byte boundary

    // ── World / render-material ───────────────────────────────
    float3 PositionOffset;         // 144  world offset (read by the render material, not density)
    float  _Pad1;                  // 156  tail padding → 160 bytes total (multiple of 16)
};

#endif
