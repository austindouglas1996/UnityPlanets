using UnityEngine;

public interface IBiome
{
    int Id { get; }
    string Name { get; }
    float PreferredHeight { get; }
    DensityMapOptions DensityMapOptions { get; }
    float Evaluate(float baseVal, Vector3 worldPos);
}