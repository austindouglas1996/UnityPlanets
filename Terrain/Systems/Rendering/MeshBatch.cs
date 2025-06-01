using System.Collections.Generic;
using UnityEngine;

public class MeshBatch
{
    public MeshBatch(Vector3Int region, Bounds bounds)
    {
        this.ChunkRegion = region;
        this.RegionBounds = bounds;
    }

    public Vector3Int ChunkRegion { get; set; }
    public Bounds RegionBounds { get; set; }

    public Dictionary<int, MeshBatchItem> Entries = new();

    public void Add(int meshIndex, Vector3 position, Quaternion rotation, Vector3 scale, Color customColor)
    {
        if (meshIndex == -1)
        {
            throw new System.ArgumentException("MeshIndex cannot be set to -1.");
        }

        if (!Entries.ContainsKey(meshIndex))
        {
            Entries.Add(meshIndex, new MeshBatchItem(meshIndex));
        }

        MeshBatchItem currentBatch = Entries[meshIndex];
        currentBatch.Positions.Add(Matrix4x4.TRS(position, rotation, scale));
        currentBatch.Colors.Add(customColor);
    }

    public bool InView(Plane[] frustumPlanes, Vector3 followerPosition)
    {
        if (Entries.Count == 0) return false;

        return GeometryUtility.TestPlanesAABB(frustumPlanes, this.RegionBounds);
    }
}
