#ifndef CHUNK_HELPER_INCLUDED
#define CHUNK_HELPER_INCLUDED

struct ChunkInput
{
    float3 CoordPos;
    float3 WorldPos;
    int stepSize;
    int isAir;
};

struct ChunkAccessInfo
{
    int chunkIndex;
    int mapIndex;
    int3 voxelCoord;  
    float3 WorldPos;
    ChunkInput chunk;  
};
#endif

ChunkAccessInfo GetChunkAccess(uint3 id, int sizeX, int sizeY, int sizeZ, StructuredBuffer<ChunkInput> chunkInputs)
{
    ChunkAccessInfo result;

    int chunkSize = sizeX;
    int voxelCount = sizeX * sizeY * sizeZ;

    result.chunkIndex = id.x / chunkSize;
    result.voxelCoord = int3(id.x % chunkSize, id.y, id.z);

    if (result.voxelCoord.x >= sizeX || result.voxelCoord.y >= sizeY || result.voxelCoord.z >= sizeZ)
    {
        result.mapIndex = -1;
        return result;
    }

    result.mapIndex = result.chunkIndex * voxelCount +
                         (result.voxelCoord.x + result.voxelCoord.y * sizeX + result.voxelCoord.z * sizeX * sizeY);

    ChunkInput input = chunkInputs[result.chunkIndex];
    float3 inputWorld = input.WorldPos;
    
    result.chunk = input;
    result.WorldPos = float3(
        inputWorld.x + result.voxelCoord.x * result.chunk.stepSize,
        inputWorld.y + result.voxelCoord.y * result.chunk.stepSize,
        inputWorld.z + result.voxelCoord.z * result.chunk.stepSize);
    
    return result;
}