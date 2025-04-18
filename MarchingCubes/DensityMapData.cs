public class DensityMapData
{
    public float[,,] DensityMap;
    public float[,] SurfaceMap;
    public float[,,] FoliageMask;

    public DensityMapData(float[,,] density, float[,] surface, float[,,] foliage)
    {
        DensityMap = density;
        SurfaceMap = surface;
        FoliageMask = foliage;
    }
}