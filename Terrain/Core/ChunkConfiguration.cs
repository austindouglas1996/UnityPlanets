namespace GingerVoxelSystem.Core
{
    using System;
    using UnityEngine;
    using GingerVoxelSystem.EditorSupport;
    using GingerVoxelSystem.Engine;

    /// <summary>
    /// Central configuration for how the world is generated and interpreted.
    /// Holds all static settings the terrain system depends on: density noise,
    /// planet shaping, biome rules, and LOD thresholds. 
    /// This data is read by generation, LOD, and math systems to keep everything 
    /// consistent and deterministic across the entire world.
    /// </summary>
    [Serializable]
    public class ChunkConfiguration : IChunkConfiguration
    {
        [Header("Density")]
        [SerializeField] private TerrainDensityOptions densityMapOptions;
        public TerrainDensityOptions DensityOptions => densityMapOptions;

        [SerializeField] private PlanetDensityOptions planetMapOptions;
        public PlanetDensityOptions PlanetOptions => planetMapOptions;

        [Header("Generation")]
        [SerializeField] private BiomeLibraryAsset biomeLibrary;
        public BiomeLibraryAsset BiomeLibrary => biomeLibrary;

        [Header("Rendering")]
        [SerializeField] private ChunkLODThresholdAsset lodThresholds;
        public ChunkLODThresholdAsset LODThresholds => lodThresholds;
    }
}