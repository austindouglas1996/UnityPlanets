#ifndef CHUNK_CBUFFER_STRUCT_INCLUDED
#define CHUNK_CBUFFER_STRUCT_INCLUDED

// ============================================================================
// DensityMapOptions
// Must match C# struct `TerrainDensityOptions` exactly in:
//   - Field order
//   - Data type size/alignment
//   - Purpose
// This is the parameter block used in density generation kernels.
// ============================================================================

cbuffer TerrainDensityOptions
{ 
    // Global
    uint CellsPerAxis;
    uint BaseCellStep;
    uint BorderSamplesPerAxis;
    int Seed;
    float ISOLevel;
    float WorldHeightAmplitude;

    // Climate
    int TemperatureBias;
    int HumidityBias;
    int FoliageBias;
    int SeaLevelBias;

    // Macro Land
    float MacroFreq;
    float MacroHeight;
    float SeaLevel;

    // Hills
    float HillFreq;
    float HillHeight;

    // Mountains
    float MountainFreq;
    float MountainHeight;
    float MountainCoverage;

    // Detail
    float DetailFreq;
    float DetailHeight;

    // Domain Offsets
    float3 PositionOffset;
    float3 BaseOffset;
    float3 DetailOffset;
    float3 FlatMaskOffset;
};


#endif