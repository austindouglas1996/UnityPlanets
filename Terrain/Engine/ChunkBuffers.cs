namespace GingerVoxelSystem.Engine
{
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Engine.Options;
    using GingerVoxelSystem.Systems.Generation;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// Central buffer container shared across all terrain-generation stages.
    /// Holds static options (density/planet), biome tables, and per-batch input buffers.
    /// Stages reference this instead of owning their own copies.
    /// </summary>
    public class ChunkBuffers : IDisposable
    {
        // Reused staging lists -> avoids per-dispatch allocations.
        private readonly List<ChunkWorkDescriptorGPU> InputSurface = new(ChunkEngineSettings.SurfaceJobsPerBatch);
        private readonly List<ChunkWorkDescriptorGPU> InputGenerate = new(ChunkEngineSettings.GenerationJobsPerBatch);
        private List<ChunkEditData> InputEdits = new(ChunkEngineSettings.EditJobsPerJob);

        // GPU buffers used by various stages.
        public ComputeBuffer SurfaceChunkInputBuffer;   // Per-chunk metadata for surface mask pass
        public ComputeBuffer GenerateChunkInputBuffer;  // Per-chunk metadata for full generation
        public ComputeBuffer GenerateChunkEditBuffer;   // Per-chunk metadata for edit generation

        public int BiomesCount;
        public ComputeBuffer BiomeBuffer;               // Table of all biome definitions
        public ComputeBuffer DensityOptionsBuffer;      // Single density-options struct
        public ComputeBuffer PlanetOptionsBuffer;       // Single planet-options struct

        public ComputeBuffer SurfaceMaskBuffer;         // 1 flag per chunk from the surface check

        private IChunkServices chunkServices;
        private Transform player;

        /// <summary>
        /// Creates a new <see cref="ChunkBuffers"/> instance configured using the supplied services.
        /// This allocates all GPU buffers and uploads initial option/biome data.
        /// </summary>
        public ChunkBuffers(IChunkServices services, Transform player)
        {
            this.chunkServices = services;
            this.player = player;

            BiomeBuffer = new ComputeBuffer(services.Configuration.BiomeLibrary.Biomes.Count, Marshal.SizeOf<ChunkBiomeGPU>());

            // Single struct (Structured buffer of length 1)
            DensityOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<TerrainDensityOptions>(), ComputeBufferType.Constant);
            PlanetOptionsBuffer = new ComputeBuffer(1, Marshal.SizeOf<PlanetDensityOptions>(), ComputeBufferType.Constant);

            SurfaceChunkInputBuffer = new ComputeBuffer(ChunkEngineSettings.SurfaceJobsPerBatch, Marshal.SizeOf<ChunkWorkDescriptorGPU>());
            GenerateChunkInputBuffer = new ComputeBuffer(ChunkEngineSettings.GenerationJobsPerBatch, Marshal.SizeOf<ChunkWorkDescriptorGPU>());
            GenerateChunkEditBuffer = new ComputeBuffer(ChunkEngineSettings.EditJobsPerJob, Marshal.SizeOf<ChunkEditData>());
            SurfaceMaskBuffer = new ComputeBuffer(ChunkEngineSettings.SurfaceJobsPerBatch, sizeof(uint));

            UpdateConfiguration(services);
        }

        /// <summary>
        /// Populates the per-chunk input buffer used by the surface-mask pass.
        /// This list is reused every frame to avoid GC pressure.
        /// </summary>
        public void FillSurfaceChunkInputs(IReadOnlyList<ChunkGenerationJob> keys)
        {
            int n = keys.Count;
            InputSurface.Clear();
            InputEdits.Clear();

            for (int i = 0; i < n; i++)
            {
                // Grab the edit modifications needed.
                int start = InputEdits.Count;
                this.chunkServices.EditStore.Query(keys[i].Key, ref InputEdits);
                int range = InputEdits.Count - start;

                Debug.Assert(range >= 0);

                var ctx = keys[i];
                InputSurface.Add(new ChunkWorkDescriptorGPU
                {
                    Origin = ctx.Key.Origin,
                    LodIndex = (uint)ctx.Key.LODIndex,
                    EditStart = (uint)start,
                    EditCount = (uint)range
                });
            }

            // Upload only the valid range.
            SurfaceChunkInputBuffer.SetData(InputSurface, 0, 0, n);
            GenerateChunkEditBuffer.SetData(InputEdits, 0, 0, InputEdits.Count);
        }

        /// <summary>
        /// Populates the per-chunk input buffer for the full generation path (density + marching).
        /// Same approach as surface input—reuse the backing list to avoid allocations.
        /// </summary>
        public void FillGenerateChunkInputs(ChunkKey?[] keys, int n)
        {
            InputGenerate.Clear();
            InputEdits.Clear();

            int found = 0;
            for (int i = 0; i < keys.Length && found < n; i++)
            {
                if (!keys[i].HasValue)
                    continue;

                // Grab the edit modifications needed.
                int start = InputEdits.Count;
                this.chunkServices.EditStore.Query(keys[i].Value, ref InputEdits);
                int range = InputEdits.Count - start;

                Debug.Assert(range >= 0);

                var ctx = keys[i].Value;
                InputGenerate.Add(new ChunkWorkDescriptorGPU
                {
                    GlobalIndex = (uint)i,  // Used by some kernels as an index hint
                    Origin = ctx.Origin,
                    LodIndex = (uint)ctx.LODIndex,
                    LodEdgeMask = chunkServices.Octree.GetLODEdgeMask(ctx, player.position),
                    EditStart = (uint)start,
                    EditCount = (uint)range
                });

                found++;
            }

            if (found != n)
                Debug.LogWarning("FilLGenerateChunkInputs: Found keys does not match total keys.");

            GenerateChunkInputBuffer.SetData(InputGenerate, 0, 0, found);
            GenerateChunkEditBuffer.SetData(InputEdits, 0, 0, InputEdits.Count);
        }

        /// <summary>
        /// Groups modification indices into contiguous ranges for efficient job dispatch.
        /// </summary>
        public List<(int start, int end)> GroupContiguous(Dictionary<int, ChunkKey?> mods,ChunkKey?[] keys,int keysCount)
        {
            List<(int start, int end)> groups = new();

            // Make sure there is modifications.
            if (mods.Count == 0)
                return groups;

            static bool IsValid(int idx, ChunkKey?[] keys, int keysCount) => keys[idx] != null;

            // Sort the keys as we want the ranges to be in order.
            var sorted = mods.Keys.OrderBy(i => i);

            using var e = sorted.GetEnumerator();
            if (!e.MoveNext())
                return groups;

            int rangeStart = e.Current;
            int prev = e.Current;

            // An invalid is defined as null.
            bool prevIsValid = IsValid(prev, keys, keysCount);

            while (e.MoveNext())
            {
                int idx = e.Current;
                bool isValid = IsValid(idx, keys, keysCount);

                // If this index is not in order OR if this
                // entry is not the same type as the previous
                // then this ends our current group.
                if (idx != prev + 1 || isValid != prevIsValid)
                {
                    groups.Add((rangeStart, prev));
                    rangeStart = idx;
                }

                prev = idx;
                prevIsValid = isValid;
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
        public void UpdateConfiguration(IChunkServices services)
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
