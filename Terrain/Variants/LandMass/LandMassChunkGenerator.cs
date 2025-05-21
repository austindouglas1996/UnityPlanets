using UnityEngine;

public class LandMassChunkGenerator : GenericChunkGenerator
{
    private IChunkColorizer colorizer;
    public LandMassChunkGenerator(IChunkServices services)
        : base(services.Configuration)
    {
        this.colorizer = services.Colorizer;
    }

    protected override BaseMarchingCubeGenerator Generator
    {
        get 
        { 
            if (this.generator == null)
                this.generator = new HeightDensityMapGenerator(colorizer, Configuration.DensityOptions);
            return this.generator;
        }
    }
    private HeightDensityMapGenerator generator;
}