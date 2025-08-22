using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Chunk data packaged for compute shader dispatch.
/// Contains logical coords, actual world position, and the step size
/// (LOD spacing between voxels).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ChunkDispatchKey
{
    public Vector3 CoordPos;
    public int LodIndex;

    public int Face;
    public int NeighborLOD;
}