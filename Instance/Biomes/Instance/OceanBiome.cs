using UnityEngine;

public class OceanBiome : GenericBiome
{
    public OceanBiome(BiomeOptions biomeOptions, DensityMapOptions baseOptions) : base(biomeOptions, baseOptions)
    {
    }

    public override float Evaluate(float baseVal, Vector3 worldPos)
    {
        throw new System.NotImplementedException();
    }
}