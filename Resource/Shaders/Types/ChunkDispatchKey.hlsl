// ============================================================================
// ChunkDispatchKey.hlsl
// Defines the chunk dispatch key used for chunk operations.
// Must match the C# struct `ChunkDispatchKey` exactly.
// ============================================================================

#ifndef CHUNK_DISPATCH_KEY_INCLUDED
#define CHUNK_DISPATCH_KEY_INCLUDED

struct ChunkDispatchKey
{
    // The logical coodinates of the key.
    float3 CoordPos;
    
    // The world position of this key.
    float3 WorldPos;
    
    // The LOD converted into a step size.
    int stepSize;
};

// Helper struct returned by GetChunkAccess() to provide both chunk-level
// and voxel-level access data for compute shader dispatches.
struct ChunkDispatchKeyInfo
{
    int chunkIndex;
    int mapIndex;
    int3 voxelCoord;
    float3 WorldPos;
    ChunkDispatchKey chunk;
};

#endif

// ============================================================================
// GetChunkAccess()
// Maps a dispatch thread ID (id) into:
//   - which chunk it belongs to
//   - the voxel's coordinates within that chunk
//   - its corresponding world position
//   - its flat index in the voxel map buffer
//
// Parameters:
//   id     : Dispatch thread ID (x, y, z) from the compute shader
//   sizeX, sizeY, sizeZ : Chunk dimensions in voxels (per axis)
//   keys   : Structured buffer of all active chunks to process
// ============================================================================
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, int sizeX, int sizeY, int sizeZ, RWStructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo result;

    int chunkSize = sizeX;
    int voxelCount = sizeX * sizeY * sizeZ;

    // Figure out which chunk this voxel belongs to (in keys buffer)
    result.chunkIndex = id.x / chunkSize;
    
    // Local voxel coordinate inside the chunk
    result.voxelCoord = int3(id.x % chunkSize, id.y, id.z);

    if (result.voxelCoord.x >= sizeX || result.voxelCoord.y >= sizeY || result.voxelCoord.z >= sizeZ)
    {
        result.mapIndex = -1;
        return result;
    }

    // Flat index into the voxel map buffer for this voxel
    //   mapIndex = chunk offset + local voxel index
    result.mapIndex = result.chunkIndex * voxelCount +
                         (result.voxelCoord.x + result.voxelCoord.y * sizeX + result.voxelCoord.z * sizeX * sizeY);

    // Retrieve the chunk's dispatch key (position + step size)
    ChunkDispatchKey input = keys[result.chunkIndex];
    float3 inputWorld = input.WorldPos;
    
    result.chunk = input;
    result.WorldPos = float3(
        inputWorld.x + result.voxelCoord.x * result.chunk.stepSize,
        inputWorld.y + result.voxelCoord.y * result.chunk.stepSize,
        inputWorld.z + result.voxelCoord.z * result.chunk.stepSize);
    
    return result;
}

// A simple debug function for returning a simple color based on LOD.
float4 GetLODColor(int stepSize)
{
    if (stepSize == 1)
        return float4(1, 1, 1, 1); // LOD0 - White
    if (stepSize == 2)
        return float4(1, 0, 0, 1); // LOD1 - Red
    if (stepSize == 4)
        return float4(1, 1, 0, 1); // LOD2 - Yellow
    if (stepSize == 8)
        return float4(0, 1, 0, 1); // LOD3 - Green
    if (stepSize == 16)
        return float4(0, 0, 1, 1); // LOD4 - Blue
    if (stepSize == 32)
        return float4(1, 0, 1, 1); // LOD5 - Magenta
    return float4(0, 0, 0, 1); // Unknown
}