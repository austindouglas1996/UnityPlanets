// -----------------------------------------------------------------------------
// Regular Transvoxel cell metadata.
// This mirrors the CPU-side regular cell table, but is laid out in a form
// that can be consumed directly by HLSL StructuredBuffers.
// -----------------------------------------------------------------------------
struct RegularCellData
{
    // Number of unique vertices generated for this cell configuration.
    uint VertexCount;

    // Number of triangles produced by this cell configuration.
    // NOTE: This is typically IndicesCount / 3.
    uint TriangleCount;

    // Start index into the global RegularCellIndices buffer.
    uint IndicesStart;

    // Number of indices for this cell configuration.
    uint IndicesCount;
};

// -----------------------------------------------------------------------------
// Describes a range into the packed vertex descriptor buffer.
// The original Transvoxel vertex data is jagged, so it is flattened on the CPU
// and accessed on the GPU via (start + local index).
// -----------------------------------------------------------------------------
struct VertexData
{
    // Start offset into RegularVertexData.
    uint VertexStart;

    // Number of packed vertex descriptors for this cell class.
    uint VertexCount;
};

// -----------------------------------------------------------------------------
// Unpacked representation of a single packed vertex descriptor.
// Each vertex is defined by two corners and optional cache metadata.
// -----------------------------------------------------------------------------
struct VertexDataUnpacked
{
    // First corner index (0..7) used for interpolation.
    uint Corner0;

    // Second corner index (0..7) used for interpolation.
    uint Corner1;

    // Cache slot index used by the Transvoxel vertex cache.
    uint CacheIndex;

    // Cache direction / orientation flag.
    uint CacheDir;
};

// The face corners of a given cube.
static const uint FaceCorners[6][4] =
{
    { 0, 4, 7, 3 }, // -X
    { 1, 2, 6, 5 }, // +X
    { 0, 1, 5, 4 }, // -Y
    { 3, 7, 6, 2 }, // +Y
    { 0, 3, 2, 1 }, // -Z
    { 4, 5, 6, 7 } // +Z
};

// -----------------------------------------------------------------------------
// Corner offsets for regular and transition cells.
// These define the relative positions of cube corners in voxel space.
// -----------------------------------------------------------------------------
StructuredBuffer<int3> RegularCornerOffset;
StructuredBuffer<int3> TransitionCornerOffset;

// Maps a marching-cubes case code (0..255) to a regular cell class index.
StructuredBuffer<uint> RegularCellClass;

// -----------------------------------------------------------------------------
// Regular cell topology data.
// -----------------------------------------------------------------------------
StructuredBuffer<RegularCellData> RegularCellTable;
StructuredBuffer<uint> RegularCellIndices;

// -----------------------------------------------------------------------------
// Regular vertex lookup data.
// -----------------------------------------------------------------------------
StructuredBuffer<VertexData> RegularVertexRanges;
StructuredBuffer<uint> RegularVertexData;




// -----------------------------------------------------------------------------
// Transition cell class lookup.
// Maps a transition case code to a transition cell class index.
// -----------------------------------------------------------------------------
StructuredBuffer<uint> TransitionCellClass;

// -----------------------------------------------------------------------------
// Packed transition corner metadata.
// Each entry encodes which regular/transition corners participate
// in the transition cell configuration.
// -----------------------------------------------------------------------------
StructuredBuffer<uint> TransitionCornerData;

// -----------------------------------------------------------------------------
// Transition cell topology data.
// Mirrors the regular cell tables, but for LOD transition cells.
// -----------------------------------------------------------------------------
StructuredBuffer<RegularCellData> TransitionCellTable;
StructuredBuffer<uint> TransitionCellIndices;

// -----------------------------------------------------------------------------
// Transition vertex lookup data.
// Works the same way as regular vertex data, but uses transition-specific
// packed vertex descriptors.
// -----------------------------------------------------------------------------
StructuredBuffer<VertexData> TransitionVertexRanges;
StructuredBuffer<uint> TransitionVertexData;


// -----------------------------------------------------------------------------
// Fetches a packed 16-bit vertex descriptor for a given cell class and
// local vertex index.
// The descriptor is stored in a uint buffer, but only the lower 16 bits
// are meaningful.
// -----------------------------------------------------------------------------
uint GetPackedVertexU16(uint caseId, uint vertexIndex)
{
    uint baseIndex = RegularVertexRanges[caseId].VertexStart;
    return RegularVertexData[baseIndex + vertexIndex];
}

// -----------------------------------------------------------------------------
// Fetches a packed 16-bit transition vertex descriptor for a given
// transition cell class and local vertex index.
// -----------------------------------------------------------------------------
uint GetTransitionPackedVertexU16(uint caseId, uint vertexIndex)
{
    uint baseIndex = TransitionVertexRanges[caseId].VertexStart;
    return TransitionVertexData[baseIndex + vertexIndex];
}

// -----------------------------------------------------------------------------
// Unpacks a packed vertex descriptor into its individual fields.
// Bit layout (low → high):
//   bits  0..3  : Corner1
//   bits  4..7  : Corner0
//   bits  8..11 : CacheIndex
//   bits 12..14 : CacheDir
// -----------------------------------------------------------------------------
VertexDataUnpacked UnpackVertex(uint packed)
{
    VertexDataUnpacked v;

    // Mask to 16 bits to avoid any garbage in the upper half.
    packed &= 0xFFFF;

    v.Corner1 = packed & 0x0F;
    v.Corner0 = (packed >> 4) & 0x0F;
    v.CacheIndex = (packed >> 8) & 0x0F;
    v.CacheDir = (packed >> 12) & 0x07;

    return v;
}

RegularCellData GetRegularCellData(uint regularCase)
{
    uint regularClass = RegularCellClass[regularCase];
    return RegularCellTable[regularClass & 0x7F];
}

RegularCellData GetTransitionCellData(uint transitionCase)
{
    uint transitionClass = TransitionCellClass[transitionCase];
    return TransitionCellTable[transitionClass & 0x7F];
}

// Returns whether a given voxel position is on the egde of a given face.
bool CubeOnFace(uint face, int3 voxel, int max)
{
    switch (face)
    {
        case 0:
            return voxel.x == 0; // -X
        case 1:
            return voxel.x == max; // +X
        case 2:
            return voxel.y == 0; // -Y
        case 3:
            return voxel.y == max; // +Y
        case 4:
            return voxel.z == 0; // -Z
        case 5:
            return voxel.z == max; // +Z
    }
    return false;
}

int3 RemapTransitionCorner(uint face, int3 c)
{
    // Canonical transition space is Z-facing

    switch (face)
    {
        // -X (fine side)
        case 0:
            return int3(0, c.y, c.x);

        // +X (transition plane, flipped)
        case 1:
            return int3(1, c.y, 2 - c.x);

        // -Y
        case 2:
            return int3(c.x, 0, c.y);

        // +Y (transition plane, flipped)
        case 3:
            return int3(c.x, 1, 2 - c.y);

        // -Z (fine side)
        case 4:
            return int3(2 - c.x, c.y, 0);

        // +Z (transition plane)
        case 5:
            return int3(c.x, c.y, 1);
    }

    return c;
}

int3 GetTransitionCornerSamplePos(uint face,uint corner,int3 baseCube)
{
    // offset is 0,1,2 in canonical transition space
    int3 offset = RemapTransitionCorner(face, TransitionCornerOffset[corner]);

    // TransitionCornerOffset is defined in SAMPLE space
    // No step, no halfStep here
    return baseCube + offset;
}