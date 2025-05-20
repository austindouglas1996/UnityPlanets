public class PlanetChunkGenerator : GenericChunkGenerator
{
    private Planet planet;

    public PlanetChunkGenerator(Planet planet)
    {
        this.planet = planet;
    }

    public override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        return new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.DensityOptions);
    }
}