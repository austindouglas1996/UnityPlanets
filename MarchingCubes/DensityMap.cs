using System.Runtime.CompilerServices;

public class DensityMap
{
    private readonly float[] _values;
    public readonly int SizeX, SizeY, SizeZ;

    public DensityMap(int x, int y, int z)
    {
        SizeX = x;
        SizeY = y;
        SizeZ = z;
        _values = new float[x * y * z];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetIndex(int x, int y, int z)
    {
        return x + SizeX * (y + SizeY * z);
    }

    public float Get(int x, int y, int z)
    {
        return _values[GetIndex(x, y, z)];
    }

    public void Set(int x, int y, int z, float value)
    {
        _values[GetIndex(x, y, z)] = value;
    }

    public float this[int x, int y, int z]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _values[GetIndex(x, y, z)];

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _values[GetIndex(x, y, z)] = value;
    }

    public float[] Raw => _values;
}
