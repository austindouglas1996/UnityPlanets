using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class PlanetChunkLayout : GenericChunkLayout
{
    public PlanetChunkLayout(Planet planet, PlanetChunkGenerator generator, PlanetChunkConfiguration configuration)
        : base(configuration)
    {
    }
}