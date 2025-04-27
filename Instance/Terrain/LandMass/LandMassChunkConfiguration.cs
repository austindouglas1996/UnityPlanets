using System;
using UnityEngine;

[Serializable]
public class LandMassChunkConfiguration : GenericChunkConfiguration
{
    [SerializeField] private int chunkViewDistance = 6;
    [SerializeField] private int surfaceMin = 0;
    [SerializeField] private int surfaceMax = 0;

    public int ChunkViewDistance => chunkViewDistance;
    public int SurfaceMin => surfaceMin;
    public int SurfaceMax => surfaceMax;
}