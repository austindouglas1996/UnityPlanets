namespace MarchingTerrain.Core
{
    using MarchingTerrain.EditorSupport;
    using MarchingTerrain.Engine.Options;
    using System;
    using UnityEngine;

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

        [Header("Rendering")]
        [SerializeField] private ChunkLODThresholdAsset lodThresholds;
        public ChunkLODThresholdAsset LODThresholds => lodThresholds;

        /// <summary>
        /// Overwrites the density SHAPING layers with the recommended "ordinary land + gentle
        /// hills" preset, while preserving the engine dimensions and the global values you have
        /// already tuned (Seed, ISOLevel, WorldHeightAmplitude, PositionOffset). Use this to
        /// repopulate the options after the field rename, then tweak from a known-good baseline.
        /// </summary>
        public void ResetDensityToOrdinaryLand()
        {
            TerrainDensityOptions current = densityMapOptions;

            TerrainDensityOptions preset = TerrainDensityOptions.CreateOrdinaryLand(
                current.CellsPerAxis,
                current.BaseCellStep,
                current.BorderSamplesPerAxis);

            // Keep the globals the user has dialed in; only the shaping layers reset.
            preset.Seed = current.Seed;
            preset.ISOLevel = current.ISOLevel;
            preset.WorldHeightAmplitude = current.WorldHeightAmplitude;
            preset.PositionOffset = current.PositionOffset;

            densityMapOptions = preset;
        }
    }
}