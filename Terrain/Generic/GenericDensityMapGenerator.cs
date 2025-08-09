using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class GenericDensityMapGenerator : MarchingCubesGPUDispatcher
{
    protected GenericDensityMapGenerator(IChunkServices services, IChunkConfiguration configuration, DensityMapOptions options) : base(services, configuration, options)
    {
    }
}