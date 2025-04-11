using UnityEngine;

public class LandMassChunkControllerFactory : GenericChunkControllerFactory
{
    public override ChunkController CreateChunkController(Vector3Int coordinates, IChunkConfiguration config, Transform parent)
    {
        ChunkController newController = base.CreateChunkController(coordinates, config, parent);
        newController.Initialize(new LandMassChunkGenerator(), new LandMassChunkColorizer(), config, coordinates);

        return newController;
    }
}
