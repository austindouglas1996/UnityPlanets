using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : GenericDensityMapGenerator
{
    public HeightDensityMapGenerator(IChunkConfiguration configuration, DensityMapOptions options) : base(configuration, options)
    {
    }
}