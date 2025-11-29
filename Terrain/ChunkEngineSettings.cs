using System;
using System.Collections.Generic;
using System.Text;

namespace GingerVoxelSystem
{
    public static class ChunkEngineSettings
    {
        // Processing settings.
        // <see cref="ChunkGenerationProcessor"/>
        public const int SurfaceJobsPerBatch = 1024;
        public const int GenerationJobsPerBatch = 64;

        // Generation settings.
        // <see cref="MarchingCubesChunkGenerator"/>
        public const int RawTrianglesPerChunk = 2000;
        public const int TrianglesPerChunkPacked = 1000;
    }
}
