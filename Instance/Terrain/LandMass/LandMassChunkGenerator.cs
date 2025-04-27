using UnityEngine;

public class LandMassChunkGenerator : GenericChunkGenerator
{
    private HeightDensityMapGenerator mapGenerator;
    protected override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        if (mapGenerator == null)
            mapGenerator = new HeightDensityMapGenerator(config.MapOptions);

        return mapGenerator;
    }
}