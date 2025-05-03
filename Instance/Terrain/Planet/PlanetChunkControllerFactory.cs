using System.Threading;
using UnityEngine;

public class PlanetChunkControllerFactory : GenericChunkControllerFactory
{
    private Planet planet;

    public PlanetChunkControllerFactory(Planet planet)
    {
        this.planet = planet;
    }

    public override ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent, CancellationToken cancellationToken)
    {
        ChunkController newController = base.CreateChunkController(coordinates, config, parent, cancellationToken);
        newController.Initialize(new PlanetChunkGenerator(planet), new PlanetChunkColorizer(planet), config, coordinates, cancellationToken);

        return newController;
    }
}
