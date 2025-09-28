using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Struct that matches GPU memory layout for HLSL. 
/// Used only for compute/StructuredBuffer work.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct TriangleDataGPU
{
    public Vector3 a;
    public Vector3 b;
    public Vector3 c; 
    public Vector3 Normal;
    public uint KeyIndex;
}

[StructLayout(LayoutKind.Sequential)]
public struct ChunkDetailDataGPU
{
    public uint Biome;
    public uint Foliage;
}