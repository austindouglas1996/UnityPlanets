using UnityEngine;

public class LandMass : ChunkVariantBase<LandMassChunkConfiguration>
{
    protected override IChunkColorizer CreateColorizer() => new LandMassChunkColorizer(ChunkConfiguration);
    protected override IChunkGenerator CreateGenerator() => new LandMassChunkGenerator(this);
    protected override IChunkLayout CreateLayout() => new LandMassChunkLayout(ChunkConfiguration);
    protected override IChunkControllerFactory CreateFactory() => new LandMassChunkControllerFactory(200, this, chunkManager.transform);
}
