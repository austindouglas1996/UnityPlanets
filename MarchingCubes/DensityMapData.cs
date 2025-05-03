public class DensityMapData
{
    public int LODIndex = -1;
    public float[,,] DensityMap;
    public float[,] SurfaceMap;
    public float[,,] FoliageMask;

    public DensityMapData(float[,,] density, float[,] surface, float[,,] foliage, int lODIndex)
    {
        DensityMap = density;
        SurfaceMap = surface;
        FoliageMask = foliage;
        LODIndex = lODIndex;
    }
}