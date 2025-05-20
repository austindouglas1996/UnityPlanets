using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private DensityMapOptions densityMapOptions;
    [SerializeField] private Material vertexColorMaterial;
    public DensityMapOptions DensityOptions => densityMapOptions;

    /// <summary>
    /// A collection of biomes to use throughout the generation.
    /// </summary>
    public List<Biome> Biomes => biomes;
    [SerializeField] private List<Biome> biomes = new();

    /// <summary>
    /// Runtime only variable.
    /// </summary>
    public List<ITerrainModifier> Modifiers => modifiers; 
    [SerializeField] private List<ITerrainModifier> modifiers = new()
    {
        new PathModifer()
    };
}