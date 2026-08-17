namespace MarchingTerrain
{
    /// <summary>
    /// Global tuning constants for the voxel terrain engine.
    /// Used by generation, batching, and scheduling systems to
    /// keep processing behavior consistent across the engine.
    /// </summary>
    public static class ChunkEngineSettings
    {
        // --------------------------------------------------------------------
        // Processing settings
        // --------------------------------------------------------------------
        // Number of surface-check jobs processed per batch.
        // See <see cref="ChunkGenerationProcessor"/>.
        public const int SurfaceJobsPerBatch = 1024;

        // Number of generation jobs processed per batch.
        // Used when dispatching chunk density/mesh generation work.
        public const int GenerationJobsPerBatch = 64;

        // --------------------------------------------------------------------
        // Generation settings
        // --------------------------------------------------------------------
        // Maximum number of raw triangles produced per chunk
        // before packing or LOD filtering is applied.
        // See <see cref="MarchingCubesChunkGenerator"/>.
        public const int RawTrianglesPerChunk = 1500;

        // Maximum number of triangles after packing/compression.
        // This represents the upper bound for GPU memory allocation.
        public const int TrianglesPerChunkPacked = 1250;

        // Maximum number of edits allowed per generation job.
        public const int EditJobsPerJob = 512;

        // Edit bounds per object inflation to allow for false positives
        // but to guarantee there is never a false negative. 
        public const float EditBoundsInflation = 3f;
    }
}
