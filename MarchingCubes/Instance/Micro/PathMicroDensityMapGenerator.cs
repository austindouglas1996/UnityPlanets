using System.Collections.Generic;
using UnityEngine;

public class PathMicroDensityMapGenerator : MicroDensityMapGenerator
{
    private ChunkController Controller;
    private List<Vector3> Points;
    private Vector3 LocalSize;

    private Vector3 controllerPosition;

    public PathMicroDensityMapGenerator(ChunkController owner, Vector3 controllerPos, List<Vector3> points, Vector3 localSize, DensityMapOptions options) : base(owner, options)
    {
        this.Controller = owner;
        this.Points = points;
        this.LocalSize = localSize;

        this.controllerPosition = controllerPos;
    }

    protected override float GetMapValue(Vector3 worldPos)
    {
        float pathRadius = 2.75f; // Small buffer around the line path

        Vector3 localWorldPos = worldPos - controllerPosition;

        int lx = Mathf.FloorToInt(localWorldPos.x);
        int ly = Mathf.FloorToInt(localWorldPos.y);
        int lz = Mathf.FloorToInt(localWorldPos.z);

        float[,,] sourceDensity = Controller.ChunkData.DensityMap;

        // Bounds check
        if (lx < 0 || ly < 0 || lz < 0 ||
            lx >= sourceDensity.GetLength(0) ||
            ly >= sourceDensity.GetLength(1) ||
            lz >= sourceDensity.GetLength(2))
            return 0f;

        foreach (var point in Points)
        {
            float distXZ = Vector2.Distance(
                new Vector2(worldPos.x, worldPos.z),
                new Vector2(point.x, point.z));

            if (distXZ <= pathRadius)
            {
                return sourceDensity[lx, ly, lz]; // copy exactly from the terrain at this voxel
            }
        }

        return 0f; // outside the path
    }







}