using System.Collections.Generic;
using UnityEngine;

public class MeshBatchItem
{
    public MeshBatchItem(int meshIndex)
    {
        this.MeshIndex = meshIndex;
    }

    public int MeshIndex;
    public List<Matrix4x4> Positions = new List<Matrix4x4>();
    public List<Vector4> Colors = new();
}