public class LandMassChunkGenerator : GenericChunkGenerator
{
    protected override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        return new HeightDensityMapGenerator(config.MapOptions);
    }
}