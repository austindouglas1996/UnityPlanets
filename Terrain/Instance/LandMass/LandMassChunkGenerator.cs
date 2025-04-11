using UnityEngine;

public class LandMassChunkGenerator : GenericChunkGenerator
{
    private ComputeShader shader;

    public LandMassChunkGenerator(ComputeShader shader)
    {
        this.shader = shader;
    }

    protected override BaseMarchingCubeGenerator CreateMapGenerator(IChunkConfiguration config)
    {
        return new HeightDensityMapGenerator(shader, config.MapOptions);
    }
}