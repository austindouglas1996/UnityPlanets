namespace GingerVoxelSystem.Engine.Stage
{
    using GingerVoxelSystem;
    using GingerVoxelSystem.Engine;
    using GingerVoxelSystem.Engine.Options;
    using GingerVoxelSystem.Systems.Rendering;
    using System;
    using System.Runtime.InteropServices;
    using Unity.Collections;
    using UnityEngine;
    using UnityEngine.Rendering;

    /// <summary>
    /// A small staging class that handles all dispatches for the Density.compute shader.
    /// This keeps density-related kernels isolated from the main pipeline.
    /// </summary>
    public class DensityStage
    {
        private readonly int generateDensityKernel;
        private readonly int surfaceKernel;

        private readonly ChunkBuffers buffers;
        private readonly ComputeShader densityShader;

        /// <summary>
        /// Reused array for surface mask readbacks. Avoids allocating a new array each time.
        /// </summary>
        private uint[] surfaceMaskCache = new uint[ChunkEngineSettings.SurfaceJobsPerBatch];

        /// <summary>
        /// Creates a new <see cref="DensityStage"/> and binds all static buffers used by the density and surface kernels.
        /// </summary>
        /// <param name="densityShader">The compute shader containing density kernels.</param>
        /// <param name="Buffers">Shared GPU buffer container.</param>
        public DensityStage(ComputeShader densityShader, ChunkBuffers Buffers)
        {
            if (densityShader == null)
            {
                throw new System.ArgumentNullException("DensityShader is null.");
            }

            this.densityShader = densityShader;
            this.buffers = Buffers;

            // Kernels
            surfaceKernel = this.densityShader.FindKernel("GenerateSurfaceMask");
            generateDensityKernel = this.densityShader.FindKernel("GenerateDensityMap");

            // Constants
            this.densityShader.SetConstantBuffer("TerrainDensityOptions", buffers.DensityOptionsBuffer, 0, Marshal.SizeOf<TerrainDensityOptions>());
            this.densityShader.SetConstantBuffer("PlanetDensityOptions", buffers.PlanetOptionsBuffer, 0, Marshal.SizeOf<PlanetDensityOptions>());

            // Stable buffers for SurfaceMask.
            this.densityShader.SetBuffer(surfaceKernel, "ChunkInputs", buffers.SurfaceChunkInputBuffer);
            this.densityShader.SetBuffer(surfaceKernel, "SurfaceMask", buffers.SurfaceMaskBuffer);

            // Stable buffers for Density.
            this.densityShader.SetBuffer(generateDensityKernel, "ChunkInputs", buffers.GenerateChunkInputBuffer);

        }

        /// <summary>
        /// Runs the surface-mask kernel. This detects which chunks actually contain terrain
        /// so we can skip further work on empty chunks.
        /// </summary>
        /// <param name="jobCount">Number of chunk jobs in the batch.</param>
        /// <param name="onSuccess">Callback invoked with the surface mask results.</param>
        public void DispatchSurfaceChecks(int jobCount, Action<uint[]> onSuccess)
        {
            densityShader.Dispatch(surfaceKernel, jobCount, 1, 1);

            AsyncGPUReadback.Request(buffers.SurfaceMaskBuffer, r =>
            {
                if (r.hasError) return;

                var src = r.GetData<uint>();
                int count = Mathf.Min(src.Length, surfaceMaskCache.Length);

                // Copy into the existing array (no new allocations)
                src.Slice(0, count).CopyTo(surfaceMaskCache);

                // Now process or invoke the callback on a background thread
                System.Threading.ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        onSuccess?.Invoke(surfaceMaskCache);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError(e);
                    }
                });
            });
        }

        /// <summary>
        /// Dispatches density generation for a <see cref="ChunkRenderBatch"/>.
        /// This writes the SDF field used by the marching-cubes and surface stages.
        /// </summary>
        public void DispatchGeneration(ChunkRenderBatch batch, int threadGroupsX, int threadGroupsY, int threadGroupsZ, int offset)
        {
            this.densityShader.SetBuffer(generateDensityKernel, "DensityMap", batch.DensityMap);
            this.densityShader.SetInt("Offset", offset);
            this.densityShader.Dispatch(generateDensityKernel, threadGroupsX, threadGroupsY, threadGroupsZ);
        }
    }
}
