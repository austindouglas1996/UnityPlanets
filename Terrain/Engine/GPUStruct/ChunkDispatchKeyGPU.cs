namespace GingerVoxelSystem.Engine
{
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Struct that matches GPU memory layout for HLSL. 
    /// Used only for compute/StructuredBuffer work.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkDispatchKeyGPU
    {
        public uint GlobalIndex;
        public Vector3 CoordPos;

        public int LodIndex;
        public uint LodEdgeMask;

        public uint SourceOffset;
        public uint SourceCount;
        public uint DestStart;
    }
}