using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class HeightDensityMapGenerator : GenericDensityMapGenerator
{
    public HeightDensityMapGenerator(IChunkColorizer color, DensityMapOptions options) : base(color, options)
    {
    }
}