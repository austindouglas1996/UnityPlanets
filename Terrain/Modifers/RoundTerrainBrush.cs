using UnityEngine;

public class RoundTerrainBrush : TerrainBrush
{
    public float Radius = 5f;

    public RoundTerrainBrush(Vector3 worldPos) : base(worldPos)
    {
    }

    public override Vector3 Min => WorldHitPoint - Vector3.one * Radius;

    public override Vector3 Max => WorldHitPoint + Vector3.one * Radius;

    public override Bounds GetBrushBounds()
    {
        return new Bounds(WorldHitPoint, Vector3.one * Radius * 2);
    }

    public override float GetEffectAmount(Vector3 voxelWorldPosition, Vector3 brushCenter)
    {
        // Using squared distance for performance
        float sqrDist = (voxelWorldPosition - brushCenter).sqrMagnitude;
        float sqrRadius = Radius * Radius;
        if (sqrDist > sqrRadius)
            return 0f;

        // Calculate falloff without calling sqrt() every time
        float falloff = 1 - (Mathf.Sqrt(sqrDist) / Radius);
        return Intensity * falloff;
    }
}