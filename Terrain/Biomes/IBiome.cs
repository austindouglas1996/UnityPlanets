using UnityEngine;

public interface IBiome
{
    string Name { get; }
    DensityMapOptions DensityMapOptions { get; }
    float Evaulate(float baseVal, Vector3 worldPos);
}