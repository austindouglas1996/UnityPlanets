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

    // Terrain Detail Sculpting
    float DetailFrequency;
    float DetailAmplitude;

    // Flatness Control
    float FlatnessFrequency;
    float FlatnessStrength;

    // Terrain Shape Remapping
    float TotalHeightScale;
};

#endif