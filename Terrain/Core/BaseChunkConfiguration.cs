namespace GingerVoxelSystem.Core
{
    using System;
    using UnityEngine;
    using GingerVoxelSystem.EditorSupport;
    using GingerVoxelSystem.Engine;

    [Serializable]
    public class BaseChunkConfiguration : IChunkConfiguration
    {
        [SerializeField] private TerrainDensityOptions densityMapOptions;
        public TerrainDensityOptions DensityOptions => densityMapOptions;


        [SerializeField] private PlanetDensityOptions planetMapOptions;
        public PlanetDensityOptions PlanetOptions => planetMapOptions;


        [SerializeField] private BiomeLibraryAsset biomeLibrary;
        public BiomeLibraryAsset BiomeLibrary => biomeLibrary;


        [SerializeField] private ChunkLODThresholdAsset lodThresholds;
        public ChunkLODThresholdAsset LODThresholds => lodThresholds;
    }
}