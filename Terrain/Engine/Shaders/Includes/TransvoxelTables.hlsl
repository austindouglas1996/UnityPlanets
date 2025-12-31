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
    // First corner index (0..12) used for interpolation.
    uint Corner0;

    // Second corner index (0..12) used for interpolation.
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

// RegularCornerOffset
// Defines the local corner offsets for a standard Marching Cubes cell.
//
// Coordinates are in local cell space (0 or 1 on each axis).
// These offsets are applied relative to the cell's minimum corner
// to compute the world-space position of each cube corner.
//
// Corner indices:
//
//        6-------7
//       /|      /|
//      / |     / |
//     4-------5  |
//     |  2----|--3
//     | /     | /
//     |/      |/
//     0-------1
//
// Axes:
//   x → right
//   y ↑ up
//   z → forward
static const int3 RegularCornerOffset[8] =
{
    int3(0, 0, 0), // 0
    int3(1, 0, 0), // 1
    int3(0, 0, 1), // 2
    int3(1, 0, 1), // 3

    int3(0, 1, 0), // 4
    int3(1, 1, 0), // 5
    int3(0, 1, 1), // 6
    int3(1, 1, 1) // 7
};

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

// TransitionCornerOffset
// Defines the canonical 3x3 transition-grid corner positions.
//
// These coordinates are in a local 0..2 grid and are later remapped
// per-face using RemapTransitionCorner(). The layout matches the
// standard Transvoxel transition corner ordering.
//
// Corner indices:
//
//     B-----------C
//    /|          /|
//   / |         / |
//  6-----7-----8  |
//  |   | |     |  |
//  |   9 |-----|--A
//  3-----4-----5 /
//  | /   |     |/
//  0-----1-----2
//
// Axes:
//   x → right
//   y ↑ up
//   z → out of the face
static const int3 TransitionCornerOffset[13] =
{
    int3(0, 0, 0), // 0
    int3(1, 0, 0), // 1
    int3(2, 0, 0), // 2

    int3(0, 1, 0), // 3
    int3(1, 1, 0), // 4
    int3(2, 1, 0), // 5

    int3(0, 2, 0), // 6
    int3(1, 2, 0), // 7
    int3(2, 2, 0), // 8

    // Anchor corners (duplicated outer corners)
    int3(0, 0, 2), // 9
    int3(2, 0, 2), // A
    int3(0, 2, 2), // B
    int3(2, 2, 2)  // C
};

// -----------------------------------------------------------------------------
// Packed transition corner metadata.
// Each entry encodes which regular/transition corners participate
// in the transition cell configuration.
// -----------------------------------------------------------------------------
static const int TransitionCornerData[13] =
{
    0x30, 0x21, 0x20, 0x12, 0x40, 0x82,
    0x10, 0x81, 0x80, 0x37, 0x27, 0x17, 0x87
};

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
    // Convert raw 9-bit case into a symmetry-reduced class + flip flag
    uint transitionClass = TransitionCellClass[transitionCase];

    // Mask off flip bit and fetch canonical triangle topology
    return TransitionCellTable[transitionClass & 0x7F];
}

// RemapTransitionCorner
// Remaps a transition-grid corner coordinate (0..2) into the correct
// orientation for the specified face.
//
// The transition grid is defined in a canonical orientation, but each
// chunk face has a different axis alignment. This function rotates and
// mirrors the corner coordinates so the same transition tables can be
// reused for all faces.
int3 RemapTransitionCorner(uint face, int3 c)
{
    switch (face)
    {
        case 0:
            return int3(0, c.y, c.x); // -X
        case 1:
            return int3(2, c.y, 2 - c.x); // +X
        case 2:
            return int3(c.x, 0, c.y); // -Y
        case 3:
            return int3(c.x, 2, 2 - c.y); // +Y
        case 4:
            return int3(2 - c.x, c.y, 0); // -Z
        case 5:
            return int3(c.x, c.y, 2); // +Z
    }
    return c;
}
