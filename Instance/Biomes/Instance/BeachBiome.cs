using UnityEngine;

public class BeachBiome : GenericBiome
{
    public BeachBiome(BiomeOptions biomeOptions, DensityMapOptions baseOptions) : base(biomeOptions, baseOptions)
    {
    }

    public override float Evaluate(float baseVal, Vector3 worldPos)
    {
        throw new System.NotImplementedException();
    }
}