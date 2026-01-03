/*
    TransvoxelTables.hlsl
    Author: Austin Douglas
    Date: 2026-01-01

    Based on the Transvoxel algorithm and adapted for HLSL usage.
    Reference implementation and inspiration:
    https://github.com/bbQsauce5/transvoxel-unity
    https://transvoxel.org/

    This file contains lookup tables for Transvoxel mesh generation,
    an extension of the marching cubes algorithm used to stitch
    meshes across LOD boundaries.

    No public HLSL-native implementation was available, so these
    tables were derived and adapted manually to work within HLSL
    constraints (no dynamic allocation, limited struct support,
    and GPU-friendly access patterns).
*/

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
    int3(2, 2, 2) // C
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

// TransitionCaseCornerMap
// Defines which of the 13 transition corners participate in the
// 9-bit transition case code, and in what order.
//
// IMPORTANT:
// The transition case bits DO NOT map directly to TransitionCornerOffset[0..8].
// The Transvoxel vertex tables expect a specific winding-based ordering of
// corners around the 3x3 transition grid, followed by the center.
//
// Case bit layout (bit index → corner index):
//
//     bit 0  → 0  (bottom-left)
//     bit 1  → 1  (bottom-mid)
//     bit 2  → 2  (bottom-right)
//     bit 3  → 5  (mid-right)
//     bit 4  → 8  (top-right)
//     bit 5  → 7  (top-mid)
//     bit 6  → 6  (top-left)
//     bit 7  → 3  (mid-left)
//     bit 8  → 4  (center)
//
// Visual order (clockwise around the grid, center last):
//
//     6 ---- 7 ---- 8
//     |      |      |
//     3 ---- 4 ---- 5
//     |      |      |
//     0 ---- 1 ---- 2
//
// This ordering MUST match the Transvoxel transition vertex tables.
// Changing this will produce incorrect triangulation (diagonal spikes,
// stretched triangles, LOD-dependent artifacts).
//
static const uint TransitionCaseCornerMap[9] =
{
    0, // bottom-left
    1, // bottom-mid
    2, // bottom-right
    5, // mid-right
    8, // top-right
    7, // top-mid
    6, // top-left
    3, // mid-left
    4 // center
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
// There is 256 in the vertexRanges.
// -----------------------------------------------------------------------------
uint GetPackedVertexU16(uint regularClass, uint vertexIndex)
{
    uint baseIndex = RegularVertexRanges[regularClass].VertexStart;
    return RegularVertexData[baseIndex + vertexIndex];
}

// -----------------------------------------------------------------------------
// Fetches a packed 16-bit transition vertex descriptor for a given
// transition cell class and local vertex index. 
// There is 512 in the vertexRanges.
// -----------------------------------------------------------------------------
uint GetTransitionPackedVertexU16(uint transitionCase, uint vertexIndex)
{
    uint baseIndex = TransitionVertexRanges[transitionCase].VertexStart;
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

    v.Corner0 = packed & 0x0F;
    v.Corner1 = (packed >> 4) & 0x0F;
    v.CacheIndex = (packed >> 8) & 0x0F;
    v.CacheDir = (packed >> 12) & 0x07;

    return v;
}

// GetRegularCellData
// HLSL has limited support for complex data structures, so this helper
// resolves a regular marching-cubes case into its canonical cell data.
RegularCellData GetRegularCellData(uint regularCase)
{
    uint regularClass = RegularCellClass[regularCase];
    return RegularCellTable[regularClass & 0x7F];
}

// GetTransitionCellData
// Resolves a raw 9-bit Transvoxel transition case into its canonical
// cell data by first mapping it to a symmetry-reduced class.
RegularCellData GetTransitionCellData(uint transitionCase)
{
    // Convert raw 9-bit case into a symmetry-reduced class + flip flag
    uint transitionClass = TransitionCellClass[transitionCase];

    // Mask off flip bit and fetch canonical triangle topology
    return TransitionCellTable[transitionClass & 0x7F];
}

// FaceToLocalSpace
// Rotates canonical transition-corner coordinates into local cube space
// based on which face is being stitched.
//
// This applies a full 3D cube rotation (NOT a 2D face projection).
// Corner offsets are defined in canonical -Z space, and this function
// remaps (x, y, z) so the transition tables see a consistent orientation
// regardless of face.
//
// IMPORTANT:
// - Preserves cornerOffset.z so anchor corners remain valid.
// - +Z requires both axis swap AND depth inversion (2 - z).
// - This matches the source Transvoxel implementation semantics.
// - Do NOT clamp or flatten Z here; doing so breaks +Z transitions.
//
// Source: https://github.com/bbQsauce5/transvoxel-unity/blob/main/Runtime/Mesher/TransvoxelMesher.Transitions.cs
int3 FaceToLocalSpace(uint face, int3 cornerOffset)
{
    uint x = cornerOffset.x;
    uint y = cornerOffset.y;
    uint z = cornerOffset.z;
    
    switch (face)
    {
        case 0:
            return int3(z, x, y); // -X 
        case 1:
            return int3(2 - z, y, x); // +X
        case 2:
            return int3(y, z, x); // -Y
        case 3:
            return int3(x, 2 - z, y); // +Y
        case 4:
            return int3(x, y, z); // -Z (Canoncial)
        case 5:
            return int3(y, x, 2 - z); // +Z
        default:
            return int3(x, y, z);
    }
}