#ifndef DensityOptionsShared
#define DensityOptionsShared

struct DensityMapOptions
{
    // Global Settings
    int ChunkSize;
    int Seed;
    float ISOLevel;

    // Continent Sculpting
    float ContinentFrequency;
    float ContinentAmplitude;
    float LandThreshold;

    // Padding to align next float4 (HLSL packs in 16-byte chunks)
    float _Padding0;

    // Mountain Sculpting
    float MountainFrequency;
    float MountainAmplitude;
    float MountainSharpness;
    float _Padding1; // padding to next float4

    // Terrain Detail Sculpting
    float DetailFrequency;
    float DetailAmplitude;
    float _Padding2;
    float _Padding3;

    // Flatness Control
    float FlatnessFrequency;
    float FlatnessStrength;
    float _Padding4;
    float _Padding5;

    // Terrain Shape Remapping
    float TotalHeightScale;
    float _Padding6;
    float _Padding7;
    float _Padding8;
};

#endif