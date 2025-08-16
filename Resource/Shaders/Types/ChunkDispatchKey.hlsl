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
ChunkDispatchKeyInfo GetChunkAccess(uint3 id,int sizeX, int sizeY, int sizeZ,StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    // Get number of chunks in this batch
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes); // batchSize = keyCount

    const int voxelCount = sizeX * sizeY * sizeZ;
    const uint logicalX = keyCount * (uint) sizeX;

    // Guard extra threads from ceil() in Dispatch()
    if (id.x >= logicalX || id.y >= (uint) sizeY || id.z >= (uint) sizeZ)
    {
        r.mapIndex = -1;
        return r;
    }

    // Map X → (chunkIndex, localX)
    r.chunkIndex = (int) (id.x / (uint) sizeX);
    r.voxelCoord = int3((int) (id.x - (uint) (r.chunkIndex * sizeX)), (int) id.y, (int) id.z);

    // Flat index into the packed voxel buffer
    r.mapIndex =
        r.chunkIndex * voxelCount +
        (r.voxelCoord.x + r.voxelCoord.y * sizeX + r.voxelCoord.z * sizeX * sizeY);

    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.chunkIndex];
    r.chunk = key;
    r.WorldPos = key.WorldPos + float3(r.voxelCoord) * key.stepSize;

    return r;
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