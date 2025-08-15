using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BaseChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private TerrainDensityOptions densityMapOptions;
    public TerrainDensityOptions DensityOptions => densityMapOptions;

    /// <summary>
    /// A collection of biomes to use throughout the generation.
    /// </summary>
    public List<Biome> Biomes => biomes;
    [SerializeField] private List<Biome> biomes = new();
}