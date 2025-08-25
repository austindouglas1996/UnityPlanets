#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

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
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, int sizeX, int sizeY, int sizeZ, StructuredBuffer<ChunkDispatchKey> keys)
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
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex) + float3(r.voxelCoord) * GetChunkSizeStep(r.chunk.LodIndex);

    return r;
}

static const int LODRings[4] = { 4, 8, 16, 32 };

// Convert chunk coordinate to world min position at the given LOD
float3 GetWorldMin(int3 coordinates, int lodIndex)
{
    int chunkSize = GetChunkSize(lodIndex);
    return coordinates * chunkSize;
}

int DesiredLodFromRings(int dChunks0)
{
    for (int i = 0; i < 4; i++)
    {
        if (dChunks0 < LODRings[i])
            return i;
    }

    return 4;
}

// Static version of GetLODForChunk (player is at 0,0,0)
int GetLODForChunk(int3 coordinates, int lodIndex)
{
    int chunkSize = GetChunkSize(lodIndex);

    float3 worldMin = GetWorldMin(coordinates, lodIndex);
    float3 worldMax = worldMin + chunkSize;

    // Player is at (0, 0, 0), clamp within chunk bounds
    float px = clamp(0.0, worldMin.x, worldMax.x);
    float pz = clamp(0.0, worldMin.z, worldMax.z);

    float dx = abs(0.0 - px);
    float dz = abs(0.0 - pz);

    float maxDist = max(dx, dz);
    int ring = (int) ceil(maxDist / 16);

    return DesiredLodFromRings(ring);
}


#endif