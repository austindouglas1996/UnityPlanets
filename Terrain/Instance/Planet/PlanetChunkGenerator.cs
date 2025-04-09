public class PlanetChunkGenerator : GenericChunkGenerator
{
    private Planet planet;

    public PlanetChunkGenerator(Planet planet)
    {
        this.planet = planet;
    }

    protected override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        return new SphereDensityMapGenerator(planet.Center, planet.PlanetRadius, config.MapOptions);
    }
}