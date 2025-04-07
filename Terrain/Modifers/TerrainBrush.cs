using System;
using UnityEngine;

[Serializable]
public enum BrushType
{
    Round,
    Square,
    Triangle
}

public abstract class TerrainBrush
{
    public float Intensity = 0.1f;
    public float ISOLevel = 0.5f;
    public Vector3 WorldHitPoint = Vector3.zero;

    public TerrainBrush(Vector3 worldPos)
    {
        this.WorldHitPoint = worldPos;
    }

    public abstract Bounds GetBrushBounds();
    public abstract float GetEffectAmount(Vector3 voxelWorldPosition, Vector3 brushCenter);
}