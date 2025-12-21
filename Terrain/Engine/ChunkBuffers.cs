using GingerVoxelSystem.Core;
using GingerVoxelSystem.Engine.Options;
using GingerVoxelSystem.Systems.Generation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;

namespace GingerVoxelSystem.Engine
{
    /// <summary>
    /// Central buffer container shared across all terrain-generation stages.
    /// Holds static options (density/planet), biome tables, and per-batch input buffers.
    /// Stages reference this instead of owning their own copies.
    /// </summary>
    public class ChunkBuffers : IDisposable
    {
        // Reused staging lists -> avoids per-dispatch allocations.
        private readonly List<ChunkDispatchKeyGPU> InputSurface = new(ChunkEngineSettings.SurfaceJobsPerBatch);
        private readonly List<ChunkDispatchKeyGPU> InputGenerate = new(ChunkEngineSettings.GenerationJobsPerBatch);

        private IChunkServices ChunkServices;

        // GPU buffers used by various stages.
        public ComputeBuffer SurfaceChunkInputBuffer;   // Per-chunk metadata for surface mask pass
        public ComputeBuffer GenerateChunkInputBuffer;  // Per-chunk metadata for full generation

        public int BiomesCount;
        public ComputeBuffer BiomeBuffer;               // Table of all biome definitions
        public ComputeBuffer DensityOptionsBuffer;      // Single density-options struct
        public ComputeBuffer PlanetOptionsBuffer;       // Single planet-options struct

        public ComputeBuffer SurfaceMaskBuffer;         // 1 flag per chunk from the surface check

        /// <summary>
        /// Creates a new <see cref="ChunkBuffers"/> instance configured using the supplied services.
        /// This allocates all GPU buffers and uploads initial option/biome data.
        /// </summary>
        public ChunkBuffers(IChunkServices services)
        {
            this.ChunkServices = services;

            BiomeBuffer = new ComputeBuffer(services.Configuration.BiomeLibrary.Biomes.Count, Marshal.SizeOf<ChunkBiomeGPU>());

            // Single struct (Structured buffer of length 1)
            DensityOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<TerrainDensityOptions>(), ComputeBufferType.Constant);
            PlanetOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<PlanetDensityOptions>(), ComputeBufferType.Constant);

            SurfaceChunkInputBuffer = new ComputeBuffer(ChunkEngineSettings.SurfaceJobsPerBatch, Marshal.SizeOf<ChunkDispatchKeyGPU>());
            GenerateChunkInputBuffer = new ComputeBuffer(ChunkEngineSettings.GenerationJobsPerBatch, Marshal.SizeOf<ChunkDispatchKeyGPU>());
            SurfaceMaskBuffer = new ComputeBuffer(ChunkEngineSettings.SurfaceJobsPerBatch, sizeof(uint));

            Update(services);
        }

        /// <summary>
        /// Populates the per-chunk input buffer used by the surface-mask pass.
        /// This list is reused every frame to avoid GC pressure.
        /// </summary>
        public void FillSurfaceChunkInputs(IReadOnlyList<ChunkGenerationJob> keys)
        {
            int n = keys.Count;
            InputSurface.Clear();

            for (int i = 0; i < n; i++)
            {
                var ctx = keys[i];
                InputSurface.Add(new ChunkDispatchKeyGPU
                {
                    CoordPos = ctx.Key.Coordinates,
                    LodIndex = ctx.Key.LODIndex
                });
            }

            // Upload only the valid range.
            SurfaceChunkInputBuffer.SetData(InputSurface, 0, 0, n);
        }

        /// <summary>
        /// Populates the per-chunk input buffer for the full generation path (density + marching).
        /// Same approach as surface input—reuse the backing list to avoid allocations.
        /// </summary>
        public void FillGenerateChunkInputs(ChunkKey?[] keys, int n)
        {
            InputGenerate.Clear();

            for (int i = 0; i < n; i++)
            {
                var ctx = keys[i].Value;
                InputGenerate.Add(new ChunkDispatchKeyGPU
                {
                    GlobalIndex = (uint)i,  // Used by some kernels as an index hint
                    CoordPos = ctx.Coordinates,
                    LodIndex = ctx.LODIndex,
                    LodEdgeMask = ChunkServices.Octree.GetLODEdgeMask(ctx),
                });
            }

            GenerateChunkInputBuffer.SetData(InputGenerate, 0, 0, n);
        }

        /// <summary>
        /// Groups modification indices into contiguous ranges for efficient job dispatch.
        /// </summary>
        public List<(int start, int end)> GroupContiguous(Dictionary<int, ChunkKey?> mods)
        {
            if (mods.Count == 0)
                return new List<(int, int)>();

            var sorted = mods.Keys.OrderBy(i => i);
            List<(int start, int end)> groups = new();
            int rangeStart = -1, prev = -1;

            foreach (int idx in sorted)
            {
                if (rangeStart == -1)
                {
                    rangeStart = prev = idx;
                    continue;
                }

                if (idx == prev + 1)
                {
                    // contiguous, extend current range
                    prev = idx;
                }
                else
                {
                    // gap detected.
                    groups.Add((rangeStart, prev));
                    rangeStart = prev = idx;
                }
            }

            groups.Add((rangeStart, prev));
            return groups;
        }

        /// <summary>
        /// Frees all GPU buffers and clears staging lists.
        /// </summary>
        public void Dispose()
        {
            InputSurface.Clear();
            InputGenerate.Clear();

            SurfaceChunkInputBuffer.Dispose();
            GenerateChunkInputBuffer.Dispose();
            BiomeBuffer.Dispose();
            DensityOptionsBuffer.Dispose();
            PlanetOptionsBuffer.Dispose();
            SurfaceMaskBuffer.Dispose();
        }

        /// <summary>
        /// Updates option buffers and rebuilds the biome table.
        /// Call this whenever terrain settings or biome data change.
        /// </summary>
        public void Update(IChunkServices services)
        {
            // Options (single struct each)
            DensityOptionsBuffer.SetData(new[] { services.Configuration.DensityOptions });
            PlanetOptionsBuffer.SetData(new[] { services.Configuration.PlanetOptions });

            // Biome table (small, rebuilt rarely)
            var biomes = services.Configuration.BiomeLibrary.Biomes;
            BiomesCount = biomes.Count;

            var biomeData = new ChunkBiomeGPU[BiomesCount];

            for (int i = 0; i < BiomesCount; i++)
            {
                biomeData[i] = new ChunkBiomeGPU
                {
                    Height = (uint)biomes[i].Height,
                    Temperature = (uint)biomes[i].Temperature,
                    Humidity = (uint)biomes[i].Humidity,
                    Foliage = (uint)biomes[i].Foliage,

                    Highlight = biomes[i].Highlight,
                    Light = biomes[i].Light,
                    MidLight = biomes[i].MidLight,
                    Mid = biomes[i].Mid,
                    Dark = biomes[i].Dark,
                    Shadow = biomes[i].Shadow
                };
            }

            BiomeBuffer.SetData(biomeData);
        }
    }
}
