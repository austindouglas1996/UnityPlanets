using UnityEngine;

public class PlanetChunkControllerFactory : GenericChunkControllerFactory
{
    private Planet planet;

    public PlanetChunkControllerFactory(Planet planet)
    {
        this.planet = planet;
    }

    public new ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent)
    {
        ChunkController newController = base.CreateChunkController(coordinates, config, parent);
        newController.Initialize(new PlanetChunkGenerator(planet), new PlanetChunkColorizer(planet), config, coordinates);

        return newController;
    }
}
