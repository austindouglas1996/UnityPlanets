using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(IChunkConfiguration configuration, DensityMapOptions options) : base(configuration, options)
    {
    }
}