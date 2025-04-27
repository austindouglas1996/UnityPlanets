using UnityEngine;

public class LandMassChunkControllerFactory : GenericChunkControllerFactory
{
    public override ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent)
    {
        ChunkController newController = base.CreateChunkController(coordinates, config, parent);
        newController.Initialize(Generator, Colorizer, config, coordinates);

        return newController;
    }

    private LandMassChunkGenerator Generator
    {
        get
        {
            if (generator == null)
                generator = new LandMassChunkGenerator();

            return generator;
        }
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
