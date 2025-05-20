using UnityEngine;

public class LandMassChunkGenerator : GenericChunkGenerator
{
    private HeightDensityMapGenerator mapGenerator;
    public override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        if (mapGenerator == null)
            mapGenerator = new HeightDensityMapGenerator(config.DensityOptions);

        return mapGenerator;
    }
}