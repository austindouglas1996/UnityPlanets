using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

[StructLayout(LayoutKind.Sequential)]
public struct ChunkEdgeNeighbor
{
    public ChunkEdgeNeighbor(Vector3Int coord, int index, int face, int lodIndex)
    {
        this.CoordPos = coord;
        this.Index = index;
        this.Face = face;
        this.LodIndex = lodIndex;
    }

    public Vector3Int CoordPos;
    public int Index;
    public int Face;
    public int LodIndex;
}