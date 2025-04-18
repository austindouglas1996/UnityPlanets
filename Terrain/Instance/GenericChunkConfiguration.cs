using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private DensityMapOptions densityMapOptions;
    [SerializeField] private List<ITerrainModifier> modifiers = new();

    public int ChunkSize => chunkSize;
    public DensityMapOptions MapOptions => densityMapOptions;
    public List<ITerrainModifier> Modifiers => modifiers;

    public GenericChunkConfiguration()
    {
        this.modifiers.Add(new PathModifer());
    }
}