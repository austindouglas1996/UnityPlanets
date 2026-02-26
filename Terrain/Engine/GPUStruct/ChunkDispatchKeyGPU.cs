namespace GingerVoxelSystem.Engine
{
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Struct that matches GPU memory layout for HLSL. 
    /// Used only for compute/StructuredBuffer work.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ChunkWorkDescriptorGPU
    {
        public uint GlobalIndex;
        public Vector3 Origin;

        public uint LodIndex;
        public uint LodEdgeMask;

        // Used in repacking operations.
        public uint SourceOffset;
        public uint SourceCount;
        public uint DestStart;

        public uint EditStart;
        public uint EditCount;
    }
}