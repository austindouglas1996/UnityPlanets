public class DensityMapData
{
    public int LODIndex = -1;
    public DensityMap DensityMap;

    public DensityMapData(DensityMap density, int lODIndex)
    {
        DensityMap = density;
        LODIndex = lODIndex;
    }
}