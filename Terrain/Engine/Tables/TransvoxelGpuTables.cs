/*
 * This implementation is based on the Transvoxel reference implementation:
 * https://github.com/bbQsauce5/transvoxel-unity/blob/main/Runtime/Mesher/TransvoxelMesher.cs
 *
 * The goal here is not to re-invent Transvoxel, but to adapt the tables and data
 * into a GPU-friendly format that works cleanly with Unity compute shaders.
 */
namespace GingerVoxelSystem.Engine.Helpers
{
    using System.Collections.Generic;
    using System.Drawing;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// GPU-friendly version of regular cell data.
    /// This mirrors the CPU-side Transvoxel tables as closely as possible,
    /// but stays within HLSL / StructuredBuffer limitations.
    /// </summary>
    public struct RegularCellDataGPU
    {
        public int VertexCount;  
        public int TriangleCount;   

        public int IndicesStart;  
        public int IndicesCount;  
    }

    /// <summary>
    /// Describes a range into the packed vertex data buffer.
    /// The original Transvoxel vertex data is jagged, so it must be flattened
    /// before it can be accessed cleanly from HLSL.
    /// </summary>
    public struct VertexData
    {
        public uint VertexStart;
        public uint VertexCount;
    }

    /// <summary>
    /// Owns and manages all GPU lookup tables required by the Transvoxel algorithm.
    /// This class is responsible for creating, populating, binding, and disposing
    /// all related ComputeBuffers.
    /// </summary>
    public static class TransvoxelGpuTables
    {
        /// <summary>
        /// Tracks whether the regular Transvoxel buffers have been initialized.
        /// </summary>
        private static bool isInitialized = false;

        /*
         * ===============================================================
         * Regular Transvoxel lookup tables
         * ===============================================================
         */
        private static ComputeBuffer RegularCornerOffset;
        private static ComputeBuffer TransitionCornerOffset;
        private static ComputeBuffer RegularCellClass;

        // RegularCellData
        private static ComputeBuffer RegularCellTable;
        private static ComputeBuffer RegularCellIndices;

        // RegularVertexData
        private static ComputeBuffer RegularVertexRanges;
        private static ComputeBuffer RegularVertexData;

        /*
         * ===============================================================
         * Transition Transvoxel lookup tables (WIP)
         * ===============================================================
         */
        private static ComputeBuffer TransitionCellClass;
        private static ComputeBuffer TransitionCornerData;

        // TransitionRegularCellData
        private static ComputeBuffer TransitionCellTable;
        private static ComputeBuffer TransitionCellIndices;

        // TransitionVertexData
        private static ComputeBuffer TransitionVertexRanges;
        private static ComputeBuffer TransitionVertexData;

        /// <summary>
        /// Binds all required Transvoxel buffers to a compute shader kernel.
        /// Buffers are lazily initialized on first use.
        /// </summary>
        /// <param name="shader"></param>
        /// <param name="kernelId"></param>
        public static void SetBuffer(ComputeShader shader, int kernelId)
        {
            if (!isInitialized)
            {
                LoadRegular();
                LoadTransition();
                isInitialized = true;
            }

            shader.SetBuffer(kernelId, "RegularCornerOffset", RegularCornerOffset);
            shader.SetBuffer(kernelId, "TransitionCornerOffset", TransitionCornerOffset);
            shader.SetBuffer(kernelId, "RegularCellClass", RegularCellClass);
            shader.SetBuffer(kernelId, "RegularCellTable", RegularCellTable);
            shader.SetBuffer(kernelId, "RegularCellIndices", RegularCellIndices);
            shader.SetBuffer(kernelId, "RegularVertexRanges", RegularVertexRanges);
            shader.SetBuffer(kernelId, "RegularVertexData", RegularVertexData);

            shader.SetBuffer(kernelId, "TransitionCellClass", TransitionCellClass);
            shader.SetBuffer(kernelId, "TransitionCornerData", TransitionCornerData);
            shader.SetBuffer(kernelId, "TransitionCellTable", TransitionCellTable);
            shader.SetBuffer(kernelId, "TransitionCellIndices", TransitionCellIndices);
            shader.SetBuffer(kernelId, "TransitionVertexRanges", TransitionVertexRanges);
            shader.SetBuffer(kernelId, "TransitionVertexData", TransitionVertexData);
        }

        /// <summary>
        /// Releases all Transvoxel-related GPU buffers.
        /// Should be called on shutdown or domain reload.
        /// </summary>
        public static void Dispose()
        {
            ReleaseBuffer(ref RegularCornerOffset);
            ReleaseBuffer(ref TransitionCornerOffset);
            ReleaseBuffer(ref RegularCellClass);
            ReleaseBuffer(ref RegularCellTable);
            ReleaseBuffer(ref RegularCellIndices);
            ReleaseBuffer(ref RegularVertexRanges);
            ReleaseBuffer(ref RegularVertexData);
            ReleaseBuffer(ref TransitionCellClass);
            ReleaseBuffer(ref TransitionCornerData);
            ReleaseBuffer(ref TransitionCellTable);
            ReleaseBuffer(ref TransitionCellIndices);
            ReleaseBuffer(ref TransitionVertexRanges);
            ReleaseBuffer(ref TransitionVertexData);
        }

        #region Regular
        /// <summary>
        /// Allocates and uploads all regular Transvoxel lookup tables to the GPU.
        /// </summary>
        private static void LoadRegular()
        {
            int v3Size = Marshal.SizeOf<Vector3Int>();

            RegularCornerOffset = new ComputeBuffer(TransvoxelTables.RegularCornerOffset.Length, v3Size);
            TransitionCornerOffset = new ComputeBuffer(TransvoxelTables.TransitionCornerOffset.Length, v3Size);
            RegularCellClass = new ComputeBuffer(256, sizeof(int));

            // Regular Cell Data.
            RegularCellTable = new ComputeBuffer(TransvoxelTables.RegularCellData.Length, Marshal.SizeOf<RegularCellDataGPU>());
            RegularCellIndices = new ComputeBuffer(156, Marshal.SizeOf<uint>());

            // Regular Vertex Data
            RegularVertexRanges = new ComputeBuffer(TransvoxelTables.RegularVertexData.Length, Marshal.SizeOf<VertexData>());
            RegularVertexData = new ComputeBuffer(1536, sizeof(uint));

            RegularCellClass.SetData(TransvoxelTables.RegularCellClass);
            RegularCornerOffset.SetData(TransvoxelTables.RegularCornerOffset);
            TransitionCornerOffset.SetData(TransvoxelTables.TransitionCornerOffset);

            // RegularCellData
            LoadRegularCellTable();

            // RegularVertexData
            LoadRegularVertexData();
        }

        /// <summary>
        /// Flattens and uploads regular cell topology data into GPU buffers.
        /// </summary>
        private static void LoadRegularCellTable()
        {
            List<RegularCellDataGPU> dataGPU = new();
            List<uint> indices = new();

            int startIndex = 0;

            foreach (var data in TransvoxelTables.RegularCellData)
            {
                RegularCellDataGPU newData = new RegularCellDataGPU();
                newData.VertexCount = (int)data.GetVertexCount();
                newData.TriangleCount = (int)data.GetTriangleCount();
                newData.IndicesStart = startIndex;
                newData.IndicesCount = newData.TriangleCount * 3;

                var lindices = data.GetIndices();
                foreach (var indice in lindices)
                {
                    indices.Add(indice);
                }

                startIndex += newData.IndicesCount;
                dataGPU.Add(newData);
            }

            Debug.Assert(indices.Count == 156);

            RegularCellTable.SetData(dataGPU.ToArray());
            RegularCellIndices.SetData(indices.ToArray());
        }

        /// <summary>
        /// Flattens and uploads packed vertex descriptor data into GPU buffers.
        /// </summary>
        private static void LoadRegularVertexData()
        {
            List<VertexData> vertexRanges = new();
            List<uint> vertexData = new();

            uint start = 0;
            foreach (var vertexList in TransvoxelTables.RegularVertexData)
            {
                VertexData newData = new VertexData();
                newData.VertexStart = start;
                newData.VertexCount = (uint)vertexList.Length;

                vertexRanges.Add(newData);

                // Add packed vertex descriptors
                foreach (ushort v in vertexList)
                {
                    vertexData.Add((uint)v);
                }

                start += newData.VertexCount;
            }

            Debug.Assert(vertexData.Count == 1536);
            Debug.Assert(vertexRanges.Count == TransvoxelTables.RegularVertexData.Length);

            RegularVertexRanges.SetData(vertexRanges.ToArray());
            RegularVertexData.SetData(vertexData.ToArray());
        }
        #endregion

        #region Transition
        private static void LoadTransition()
        {
            // MAY need to be converted to int.
            TransitionCellClass = new ComputeBuffer(TransvoxelTables.TransitionCellClass.Length, sizeof(int));
            TransitionCornerData = new ComputeBuffer(TransvoxelTables.TransitionCornerData.Length, sizeof(int));

            // Transition Cell Data.
            TransitionCellTable = new ComputeBuffer(TransvoxelTables.TransitionRegularCellData.Length, Marshal.SizeOf<RegularCellDataGPU>());
            TransitionCellIndices = new ComputeBuffer(924, Marshal.SizeOf<uint>());

            // Transition Vertex Data
            TransitionVertexRanges = new ComputeBuffer(TransvoxelTables.TransitionVertexData.Length, Marshal.SizeOf<VertexData>());
            TransitionVertexData = new ComputeBuffer(4096, sizeof(uint));

            TransitionCellClass.SetData(TransvoxelTables.TransitionCellClass);
            TransitionCornerData.SetData(TransvoxelTables.TransitionCornerData);

            LoadTransitionCellTable();
            LoadTransitionVertexData();
        }

        /// <summary>
        /// Flattens and uploads regular cell topology data into GPU buffers.
        /// </summary>
        private static void LoadTransitionCellTable()
        {
            List<RegularCellDataGPU> dataGPU = new();
            List<uint> indices = new();

            int startIndex = 0;

            foreach (var data in TransvoxelTables.TransitionRegularCellData)
            {
                RegularCellDataGPU newData = new RegularCellDataGPU();
                newData.VertexCount = (int)data.GetVertexCount();
                newData.TriangleCount = (int)data.GetTriangleCount();
                newData.IndicesStart = startIndex;
                newData.IndicesCount = newData.TriangleCount * 3;

                var lindices = data.GetIndices();
                foreach (var indice in lindices)
                {
                    indices.Add(indice);
                }

                startIndex += newData.IndicesCount;
                dataGPU.Add(newData);
            }

            Debug.Assert(indices.Count == 924);

            TransitionCellTable.SetData(dataGPU.ToArray());
            TransitionCellIndices.SetData(indices.ToArray());
        }

        /// <summary>
        /// Flattens and uploads packed vertex descriptor data into GPU buffers.
        /// </summary>
        private static void LoadTransitionVertexData()
        {
            List<VertexData> vertexRanges = new();
            List<uint> vertexData = new();

            uint start = 0;
            foreach (var vertexList in TransvoxelTables.TransitionVertexData)
            {
                VertexData newData = new VertexData();
                newData.VertexStart = start;
                newData.VertexCount = (uint)vertexList.Length;

                vertexRanges.Add(newData);

                // Add packed vertex descriptors
                foreach (ushort v in vertexList)
                {
                    vertexData.Add((uint)v);
                }

                start += newData.VertexCount;
            }

            Debug.Assert(vertexData.Count == 4096);
            Debug.Assert(vertexRanges.Count == TransvoxelTables.TransitionVertexData.Length);

            TransitionVertexRanges.SetData(vertexRanges.ToArray());
            TransitionVertexData.SetData(vertexData.ToArray());
        }
        #endregion

        /// <summary>
        /// Safely releases and disposes a ComputeBuffer.
        /// </summary>
        /// <param name="buffer"></param>
        private static void ReleaseBuffer(ref ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer.Dispose();
                buffer = null;
            }
        }
    }
}
