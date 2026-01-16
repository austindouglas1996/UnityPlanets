namespace GingerVoxelSystem.Engine
{
    using GingerVoxelSystem;
    using GingerVoxelSystem.Core;
    using GingerVoxelSystem.Engine.Options;
    using GingerVoxelSystem.Engine.Stage;
    using GingerVoxelSystem.Systems.Generation;
    using GingerVoxelSystem.Systems.Rendering;
    using System;
    using System.Collections.Generic;
    using System.Runtime.InteropServices;
    using UnityEngine;

    /// <summary>
    /// High-level marching-cubes pipeline. 
    /// This wires all compute stages together and drives the full chunk generation path:
    /// Surface → Density → Triangle Count → Repack → Details.
    /// 
    /// Nothing in here does heavy work on the CPU. It just feeds data and dispatches kernels.
    /// </summary>
    [RequireComponent(typeof(IChunkServices))]
    [RequireComponent(typeof(ChunkMaterialSettings))]
    public class MCTerrainOrchestrator : MonoBehaviour, IChunkGenerator
    {
        [Header("World")]
        [SerializeField] private Transform Player;

        [Header("Materials")]
        private ChunkMaterialSettings materialManager;
        private Material chunkMaterial;

        [Header("Shaders")]
        [SerializeField] private ComputeShader DensityShader;
        [SerializeField] private ComputeShader MarchingCubesShader;
        [SerializeField] private ComputeShader TransvoxelShader;
        [SerializeField] private ComputeShader RepackShader;
        [SerializeField] private ComputeShader DetailsShader;
        [SerializeField] private ComputeShader UtilityShader;

        private DensityStage density;
        private IMarchingShader marchingCubes;
        private RepackStage repack;
        private DetailsStage details;
        private UtilityStage utility;

        [Header("Services")]
        private IChunkServices chunkServices;

        /// <summary>
        /// Contains common buffers used throughout terrain generation.
        /// </summary>
        private ChunkBuffers chunkBuffers;

        /// <summary>
        /// Called when the game starts.
        /// </summary>
        public void Awake()
        {
            chunkServices = GetComponent<IChunkServices>();
            materialManager = GetComponent<ChunkMaterialSettings>();
            chunkMaterial = materialManager.BaseMaterial;

            // Centralized buffer container shared by every stage.
            chunkBuffers = new ChunkBuffers(chunkServices, Player);

            // Load compute stages. Each stage wires buffers/kernels internally.
            density = new DensityStage(DensityShader, chunkBuffers);

            marchingCubes = new TransVoxelsStage(TransvoxelShader, chunkBuffers);

            repack = new RepackStage(RepackShader, chunkBuffers);
            details = new DetailsStage(DetailsShader, chunkBuffers);
            utility = new UtilityStage(UtilityShader);
        }

        /// <summary>
        /// Validate on changes.
        /// </summary>
        public void OnValidate()
        {
            if (!Application.isPlaying) return;
        }

        /// <summary>
        /// Gets the material used when rendering terrain meshes.
        /// </summary>
        public Material GetMaterial
        {
            get => chunkMaterial;
            private set => chunkMaterial = value;
        }

        /// <summary>
        /// Runs the cheap surface mask pass to quickly reject empty chunks.
        /// </summary>
        public void DispatchSurfaceCheck(IReadOnlyList<ChunkGenerationJob> keys, Action<uint[]> onSuccess)
        {
            chunkBuffers.FillSurfaceChunkInputs(keys);
            density.DispatchSurfaceChecks(keys.Count, onSuccess);
        }

        /// <summary>
        /// Main MC pipeline. Runs density → triangle count → repack → details for modified ranges.
        /// </summary>
        public void DispatchGeneration(DispatchJob job)
        {
            if (job.Batch == null)
                job.Batch = CreateBatch();

            int CellsPerAxis = densityOptions.CellsPerAxis;
            int samplesPerAxis = CellsPerAxis + 1 + (2 * densityOptions.BorderSamplesPerAxis);

            // Compute-thread division for 4x4x4 kernels.
            int marchGroupSize = Mathf.CeilToInt(CellsPerAxis / 4f);
            int genGroupSurfaceSize = Mathf.CeilToInt(samplesPerAxis / 8f);
            int genGroupSize = Mathf.CeilToInt(samplesPerAxis / 4f);

            // Modified chunk ranges grouped contiguously so we don't dispatch per-chunk.
            List<(int start, int end)> ranges = chunkBuffers.GroupContiguous(job.Modifications, job.Keys, job.KeysCount);

            // Set buffer.
            chunkBuffers.FillGenerateChunkInputs(job.Keys, job.KeysCount);

            // 1) Density + ClearCount + CountTriangles
            foreach (var (start, end) in ranges)
            {                
                int length = (end - start + 1);

                // If the start contains a removal we do not need to do anything but remove entries.
                if (job.Keys[start] == null)
                {
                    // Reset old triangle counts.
                    utility.DispatchClear(job.Batch.TriangleChunkCounts, start, length);

                    continue;
                }

                // Generate density samples for all the chunks in the range.
                density.DispatchSurfaceGeneration(job.Batch, length * genGroupSurfaceSize, genGroupSurfaceSize, start);

                // Generate density for all chunks in the range.
                density.DispatchGeneration(job.Batch, length * genGroupSize, genGroupSize, genGroupSize, start);

                // Reset old triangle counts.
                utility.DispatchClear(job.Batch.TriangleChunkCounts, start, length);

                // Count how many triangles each chunk will output.
                marchingCubes.DispatchTriangleCount(job.Batch, length * marchGroupSize, marchGroupSize, marchGroupSize, start);
            }

            // 2) Repack prepass — builds draw args + packed offsets
            repack.DispatchPrePass(job.Batch, job.KeysCount);

            // 3) Clear cursor + run marching for actual triangles
            foreach (var (start, end) in ranges)
            {
                int length = (end - start + 1);

                // If the start contains a removal we do not need to do anything but remove entries.
                if (job.Keys[start] == null)
                {
                    // Reset write cursor for each chunk.
                    utility.DispatchClear(job.Batch.TriangleWriteCursor, start, length);

                    continue;
                }

                // Reset write cursor for each chunk.
                utility.DispatchClear(job.Batch.TriangleWriteCursor, start, length);

                // Emit triangles.
                marchingCubes.DispatchMarching(job.Batch, length * marchGroupSize, marchGroupSize, marchGroupSize, start);
            }

            // 4) Pack raw triangles into the final contiguous buffer.
            repack.DispatchRepack(job.Batch, job.KeysCount);

            // 5) Per-triangle biome/color lookup.
            details.DispatchDetailPass(job.Batch);

            // Return finished batch.
            job.OnCompleted.Invoke(job.Batch);
        }

        /// <summary>
        /// Pushes updated biome + density options into GPU buffers and the terrain material.
        /// </summary>
        public void UpdateOptions()
        {
            // Update render material properties
            chunkMaterial.SetBuffer("Biomes", chunkBuffers.BiomeBuffer);
            chunkMaterial.SetInt("_BiomesCount", chunkBuffers.BiomesCount);
            chunkMaterial.SetInt("Seed", chunkServices.Configuration.DensityOptions.Seed);

            chunkMaterial.SetFloat("_UseVertexColor", 1f);
            chunkMaterial.SetVector("PositionOffset", chunkServices.Configuration.DensityOptions.PositionOffset);
            chunkMaterial.SetVector("PlanetCenter", chunkServices.Configuration.PlanetOptions.PlanetCenter);
            chunkMaterial.SetFloat("PlanetRadius", chunkServices.Configuration.PlanetOptions.PlanetRadius);
            chunkMaterial.SetInt("SubVariant", (int)chunkServices.Configuration.DensityOptions.TerrainType);

            // Push updated biome/options tables into GPU memory.
            chunkBuffers.Update(chunkServices);
        }

        /// <summary>
        /// Allocates a new render batch with all dynamic buffers.
        /// </summary>
        private ChunkRenderBatch CreateBatch()
        {
            int maxRaw = ChunkEngineSettings.GenerationJobsPerBatch * ChunkEngineSettings.RawTrianglesPerChunk;
            int maxSimple = ChunkEngineSettings.GenerationJobsPerBatch * ChunkEngineSettings.TrianglesPerChunkPacked;

            var triangleSBuffer = new ComputeBuffer(maxRaw, Marshal.SizeOf<TriangleDataGPU>());
            var triangleDBuffer = new ComputeBuffer(maxSimple, Marshal.SizeOf<TriangleDataGPU>());
            var triangleCBuffer = new ComputeBuffer(ChunkEngineSettings.GenerationJobsPerBatch, sizeof(uint));
            var triangleCursor = new ComputeBuffer(ChunkEngineSettings.GenerationJobsPerBatch, sizeof(uint));
            var detailBuffer = new ComputeBuffer(maxSimple, Marshal.SizeOf<ChunkDetailDataGPU>(), ComputeBufferType.Append | ComputeBufferType.Structured);

            // Density map allocation (scalar field) — rough over-alloc based on max jobs.
            int samples = densityOptions.CellsPerAxis + 1 + (2 * densityOptions.BorderSamplesPerAxis);
            int samplesPerChunk = samples * samples * samples;
            int totalSamples = samplesPerChunk * ChunkEngineSettings.GenerationJobsPerBatch;
            int totalSampelsC = ChunkEngineSettings.GenerationJobsPerBatch * samples * samples;

            var densityBuffer = new ComputeBuffer(totalSamples, sizeof(float));
            var densityColumnBuffers = new ComputeBuffer(totalSampelsC, sizeof(float));

            return new ChunkRenderBatch(
                triangleSBuffer,
                triangleDBuffer,
                triangleCBuffer,
                triangleCursor,
                detailBuffer,
                densityBuffer,
                densityColumnBuffers
            );
        }

        /// <summary>
        /// Update.
        /// </summary>
        public void Update()
        {
        }

        /// <summary>
        /// Dispose of the elements and buffers.
        /// </summary>
        public void Dispose()
        {
            this.marchingCubes.Dispose();
            this.chunkBuffers.Dispose();
        }

        // Convenience alias
        private TerrainDensityOptions densityOptions => chunkServices.Configuration.DensityOptions;
    }
}
