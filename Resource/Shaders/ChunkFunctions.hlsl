#ifndef CHUNK_COMMON_FUNCTIONS_INCLUDED
#define CHUNK_COMMON_FUNCTIONS_INCLUDED

#include "ChunkCommon.hlsl"

// Computes the flat index into the density map for a voxel within a chunk batch
int GetVoxelMapIndex(int3 pos, int chunkIndex, int3 logicalSize)
{
    int voxelCountPerChunk = logicalSize.x * logicalSize.y * logicalSize.z;
    int localIndex = pos.x + pos.y * logicalSize.x + pos.z * logicalSize.x * logicalSize.y;
    return chunkIndex * voxelCountPerChunk + localIndex;
}

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
//   size.x, size.y, size.z : Chunk dimensions in voxels (per axis)
//   keys   : Structured buffer of all active chunks to process
// ============================================================================
ChunkDispatchKeyInfo GetChunkAccess(uint3 id, int3 size, StructuredBuffer<ChunkDispatchKey> keys)
{
    ChunkDispatchKeyInfo r;

    // Get number of chunks in this batch
    uint keyCount, strideBytes;
    keys.GetDimensions(keyCount, strideBytes); // batchSize = keyCount

    const int voxelCount = size.x * size.y * size.z;
    const uint logicalX = keyCount * (uint) size.x;

    // Guard extra threads from ceil() in Dispatch()
    if (id.x >= logicalX || id.y >= (uint) size.y || id.z >= (uint) size.z)
    {
        r.mapIndex = -1;
        return r;
    }

    // Map X → (chunkIndex, localX)
    r.chunkIndex = (int) (id.x / (uint) size.x);
    r.voxelCoord = int3((int) (id.x - (uint) (r.chunkIndex * size.x)), (int) id.y, (int) id.z);

    // Flat index into the packed voxel buffer
    r.mapIndex = GetVoxelMapIndex(r.voxelCoord, r.chunkIndex, size);

    // Fetch key and compute world position
    ChunkDispatchKey key = keys[r.chunkIndex];
    r.chunk = key;
    r.WorldPos = ToWorld(key.CoordPos, key.LodIndex) + float3(r.voxelCoord) * GetChunkSizeStep(r.chunk.LodIndex);

    return r;
}

static const int LODRings[5] = { 4, 12, 29, 60, 124 };

// Convert chunk coordinate to world min position at the given LOD
float3 GetWorldMin(int3 coordinates, int lodIndex)
{
    int chunkSize = GetChunkSize(lodIndex);
    return coordinates * chunkSize;
}

int DesiredLodFromRings(int dChunks0)
{
    for (int i = 0; i < 5; i++)
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










bool IsEdgeVoxel(int3 voxelCoord)
{
    return voxelCoord.x == ChunkSize - 1 ||
    voxelCoord.y == ChunkSize - 1 ||
    voxelCoord.z == ChunkSize - 1 ||
    voxelCoord.x == 0 ||
    voxelCoord.y == 0 ||
    voxelCoord.z == 0;
}

void ChunkNeighbors(float3 C, out float3 nC[8])
{
    nC[0] = float3(C.x, C.y, C.z + 1); // top (Z+)
    nC[1] = float3(C.x + 1, C.y, C.z); // right (X+)
    nC[2] = float3(C.x, C.y, C.z - 1); // bottom (Z-)
    nC[3] = float3(C.x - 1, C.y, C.z); // left (X-)

    nC[4] = float3(C.x + 1, C.y, C.z + 1); // top-right (X+, Z+)
    nC[5] = float3(C.x + 1, C.y, C.z - 1); // bottom-right (X+, Z-)
    nC[6] = float3(C.x - 1, C.y, C.z - 1); // bottom-left (X-, Z-)
    nC[7] = float3(C.x - 1, C.y, C.z + 1); // top-left (X-, Z+)
}

int GetEdgeSideXZ(int3 voxelCoord)
{
    bool top = voxelCoord.z == ChunkSize - 1;
    bool bottom = voxelCoord.z == 0;
    bool right = voxelCoord.x == ChunkSize - 1;
    bool left = voxelCoord.x == 0;

    if (top && right)
        return 4; // top-right
    if (bottom && right)
        return 5; // bottom-right
    if (bottom && left)
        return 6; // bottom-left
    if (top && left)
        return 7; // top-left

    if (top)
        return 0;
    if (right)
        return 1;
    if (bottom)
        return 2;
    if (left)
        return 3;

    return -1;
}

// Return true only if THIS marching-cubes cell lies on an X/Z side
// whose same-LOD neighbor's WORLD position wants a different LOD.
bool IsEdgeCell(ChunkDispatchKeyInfo key)
{
    int thisLod = key.chunk.LodIndex;
    
    // Build face mask by comparing neighbor desired LOD to *our* desired LOD
    float3 nC[8];
    ChunkNeighbors(key.chunk.CoordPos, nC);
    
    [unroll]
    for (int f = 0; f < 8; ++f)
    {
        int nWant = GetLODForChunk(nC[f], thisLod);
        
        if (nWant < thisLod && GetEdgeSideXZ(key.voxelCoord) == f)
        {
            return true;
        }
    }
    
    return false;
}


#endif