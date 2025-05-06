using System.Threading;
using UnityEngine;

public class PlanetChunkControllerFactory : GenericChunkControllerFactory
{
    private Planet planet;

    public PlanetChunkControllerFactory(Planet planet)
    {
        this.planet = planet;
    }

    public override ChunkController CreateChunkController(Vector3Int coordinates, ChunkManager manager, IChunkConfiguration config, Transform parent, CancellationToken cancellationToken)
    {
        ChunkController newController = base.CreateChunkController(coordinates, manager, config, parent, cancellationToken);
        newController.Initialize(manager, new PlanetChunkColorizer(planet), config, coordinates, 0, cancellationToken);

        return newController;
    }

    public override IChunkGenerator CreateGenerator()
    {
        return new PlanetChunkGenerator(planet);
    }
}
