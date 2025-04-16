using System.Collections.Generic;
using UnityEngine;

public class MicroChunkGenerator : GenericChunkGenerator
{
    private ChunkController Controller;
    private List<Vector3> Points;
    private Vector3 LocalSize;
    private Vector3 controlelrpos;

    public MicroChunkGenerator(ChunkController owner, List<Vector3> points, Vector3 localSize)
    {
        this.Points = points;
        this.Controller = owner;
        this.LocalSize = localSize;
        this.controlelrpos = owner.transform.position;
    }

    protected override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        return new PathMicroDensityMapGenerator(Controller, this.controlelrpos, Points, LocalSize, config.MapOptions);
    }
}