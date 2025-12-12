namespace GingerVoxelSystem.Engine
{
    using System.Runtime.InteropServices;
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
        public Vector3 NormalA;
        public Vector3 NormalB;
        public Vector3 NormalC;
        public uint LodIndex;
        public uint KeyIndex;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkDetailDataGPU
    {
        public uint Biome;
        public uint Foliage;
        public Vector4 a;
        public Vector4 b;
        public Vector4 c;
    }
}