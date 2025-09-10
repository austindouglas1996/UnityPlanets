using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Triangle output from marching cubes with baked vertex colors.
/// Each chunk spits out a bunch of these for rendering or collision.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct ChunkTriangleData
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c;
    public uint Biome;
}