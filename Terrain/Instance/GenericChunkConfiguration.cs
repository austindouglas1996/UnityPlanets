using System;
using UnityEngine;

[Serializable]
public class GenericChunkConfiguration : IChunkConfiguration
{
    [SerializeField] private int chunkSize = 32;
    [SerializeField] private DensityMapOptions densityMapOptions;

    public int ChunkSize => chunkSize;
    public DensityMapOptions MapOptions => densityMapOptions;
}