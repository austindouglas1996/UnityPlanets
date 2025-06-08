using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class GenericDensityMapGenerator : BaseMarchingCubeGenerator
{
    protected GenericDensityMapGenerator(IChunkColorizer color, DensityMapOptions options) : base(color, options)
    {
    }
}