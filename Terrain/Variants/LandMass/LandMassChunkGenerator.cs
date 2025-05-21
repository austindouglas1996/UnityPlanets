using UnityEngine;

public class LandMassChunkGenerator : GenericChunkGenerator
{
    private IChunkColorizer colorizer;
    public LandMassChunkGenerator(IChunkServices services)
        : base(services.Configuration)
    {
        this.colorizer = services.Colorizer;
    }

    private HeightDensityMapGenerator mapGenerator;
    public override BaseMarchingCubeGenerator CreateMapGenerator()
    {
        if (mapGenerator == null)
            mapGenerator = new HeightDensityMapGenerator(colorizer,Configuration.DensityOptions);

        return mapGenerator;
    }
}