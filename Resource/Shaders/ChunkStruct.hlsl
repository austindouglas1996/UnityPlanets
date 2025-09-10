#ifndef CHUNK_COMMON_STRUCT_INCLUDED
#define CHUNK_COMMON_STRUCT_INCLUDED

//  __      ________ _______     __  _____ __  __ _____   ____  _____ _______       _   _ _______ 
//  \ \    / /  ____|  __ \ \   / / |_   _|  \/  |  __ \ / __ \|  __ \__   __|/\   | \ | |__   __|
//   \ \  / /| |__  | |__) \ \_/ /    | | | \  / | |__) | |  | | |__) | | |  /  \  |  \| |  | |   
//    \ \/ / |  __| |  _  / \   /     | | | |\/| |  ___/| |  | |  _  /  | | / /\ \ | . ` |  | |   
//     \  /  | |____| | \ \  | |     _| |_| |  | | |    | |__| | | \ \  | |/ ____ \| |\  |  | |   
//      \/   |______|_|  \_\ |_|    |_____|_|  |_|_|     \____/|_|  \_\ |_/_/    \_\_| \_|  |_|   
//
// Must stay in sync with the C# side struct of each if using a structured buffer.:
//   - Field order
//   - Data type sizes/alignment
//
//   _____  ______ __  __ ______ __  __ ____  ______ _____  
// |  __ \|  ____|  \/  |  ____|  \/  |  _ \|  ____|  __ \ 
// | |__) | |__  | \  / | |__  | \  / | |_) | |__  | |__) |
// |  _  /|  __| | |\/| |  __| | |\/| |  _ <|  __| |  _  / 
// | | \ \| |____| |  | | |____| |  | | |_) | |____| | \ \ 
// |_|  \_\ _____|_|  |_|_____|_|  |_|____/|_____|_| |  \_\

// Key that identifies a chunk for dispatch.
// CoordPos = logical world-space position of the chunk
// LodIndex = step size based on current LOD
struct ChunkDispatchKey
{
    float3 CoordPos;
    int LodIndex;
};

// Returned by GetChunkAccess(). Holds everything needed
// for compute dispatch: the chunk index, map index, voxel coord,
// world position, and the original chunk key.
struct ChunkDispatchKeyInfo
{
    int chunkIndex;
    int mapIndex;
    int3 voxelCoord;
    float3 WorldPos;
    ChunkDispatchKey chunk;
};

// Triangle data generated during marching.
// Holds 3 vertex positions (world space), the LOD they came from,
// and the biome is attached to the triangle packed into a 8 bytes
struct ChunkTriangleData
{
    float3 a;
    float3 b;
    float3 c;
    uint Biome;
};

// Biome data table entry.
// Defines the classification (height/temp/humidity/foliage)
// and the color palette for that biome.
struct ChunkBiomeData
{
    uint BiomeHeight;
    uint BiomeTemperature;
    uint BiomeHumidty;
    uint BiomeFoliage;

    float4 Highlight;
    float4 Light;
    float4 MidLight;
    float4 Mid;
    float4 Dark;
    float4 Shadow;
};

#endif