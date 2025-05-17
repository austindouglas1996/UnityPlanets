using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class PlanetChunkLayout : GenericChunkLayout
{
    private Planet Planet;
    private BaseMarchingCubeGenerator Generator;

    public PlanetChunkLayout(Planet planet, PlanetChunkGenerator generator, PlanetChunkConfiguration configuration)
        : base(configuration)
    {
        this.Planet = planet;
        Generator = generator.CreateMapGenerator(configuration);
    }

    public new PlanetChunkConfiguration Configuration
    {
        get { return (PlanetChunkConfiguration)base.Configuration; }
    }
}