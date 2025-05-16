public class DensityMapData
{
    public int LODIndex = -1;
    public float[,,] DensityMap;

    public DensityMapData(float[,,] density, int lODIndex)
    {
        DensityMap = density;
        LODIndex = lODIndex;
    }
}