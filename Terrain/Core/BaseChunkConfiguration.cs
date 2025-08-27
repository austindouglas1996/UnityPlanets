using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class BaseChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private DebugOptions debugOptions;
    public DebugOptions DebugOptions => debugOptions;


    [SerializeField] private TerrainDensityOptions densityMapOptions;
    public TerrainDensityOptions DensityOptions => densityMapOptions;


    [SerializeField] private PlanetDensityOptions planetMapOptions;
    public PlanetDensityOptions PlanetOptions => planetMapOptions;


    [SerializeField] private List<Biome> biomes = new();
    public List<Biome> Biomes => biomes;


    [SerializeField] private LODThresholds lodThresholds = new();
    public LODThresholds LODThresholds => lodThresholds;
}