namespace GingerVoxelSystem.Core
{
    using System;
    using UnityEngine;
    using GingerVoxelSystem.EditorSupport;
    using GingerVoxelSystem.Engine;

    [Serializable]
    public class ChunkConfiguration : IChunkConfiguration
    {
        [Tooltip("Density")]
        [SerializeField] private TerrainDensityOptions densityMapOptions;
        public TerrainDensityOptions DensityOptions => densityMapOptions;

        [SerializeField] private PlanetDensityOptions planetMapOptions;
        public PlanetDensityOptions PlanetOptions => planetMapOptions;

        [Tooltip("Generation")]
        [SerializeField] private BiomeLibraryAsset biomeLibrary;
        public BiomeLibraryAsset BiomeLibrary => biomeLibrary;

        [Tooltip("Rendering")]
        [SerializeField] private ChunkLODThresholdAsset lodThresholds;
        public ChunkLODThresholdAsset LODThresholds => lodThresholds;
    }
}