using System.Threading;
using UnityEngine;

public class LandMassChunkControllerFactory : GenericChunkControllerFactory
{
    public override ChunkController CreateChunkController(Vector3Int coordinates, ChunkManager manager, IChunkConfiguration config, Transform parent, CancellationToken cancellationToken)
    {
        ChunkController newController = base.CreateChunkController(coordinates, manager, config, parent, cancellationToken);
        newController.Initialize(manager, Colorizer, config, coordinates, 0, cancellationToken);

        return newController;
    }

    public override IChunkGenerator CreateGenerator()
    {
        if (generator == null)
            generator = new LandMassChunkGenerator();

        return this.generator;
    }

    private LandMassChunkColorizer Colorizer
    {
        get
        {
            if (colorizer == null)
                colorizer = new LandMassChunkColorizer();

            return colorizer;
        }
    }

    private LandMassChunkColorizer colorizer;
    private LandMassChunkGenerator generator;
}
