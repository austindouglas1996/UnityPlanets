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

static const uint FaceCorners[6][4] =
{
    { 1, 2, 6, 5 }, // +X
    { 0, 4, 7, 3 }, // -X
    { 3, 7, 6, 2 }, // +Y
    { 0, 1, 5, 4 }, // -Y
    { 4, 5, 6, 7 }, // +Z
    { 0, 3, 2, 1 } // -Z
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
uint GetPackedVertexU16(uint classId, uint vertexIndex)
{
    uint baseIndex = RegularVertexRanges[classId].VertexStart;
    return RegularVertexData[baseIndex + vertexIndex];
}

// -----------------------------------------------------------------------------
// Fetches a packed 16-bit transition vertex descriptor for a given
// transition cell class and local vertex index.
// -----------------------------------------------------------------------------
uint GetTransitionPackedVertexU16(uint classId, uint vertexIndex)
{
    uint baseIndex = TransitionVertexRanges[classId].VertexStart;
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
