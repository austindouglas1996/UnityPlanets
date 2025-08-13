using NUnit.Framework;
using System;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseDensityMapGenerator : MarchingCubesGPUDispatcher
{
    protected BaseDensityMapGenerator(IChunkServices services, IChunkConfiguration configuration, DensityMapOptions options) : base(services, configuration, options)
    {
    }
}