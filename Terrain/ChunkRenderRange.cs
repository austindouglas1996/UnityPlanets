using System;
using System.Collections.Generic;

[Serializable]
public class ChunkDistanceAxisRange
{
    public int Min;
    public int Max;

    public int Size => Max - Min + 1;

    public IEnumerable<int> Values()
    {
        for (int i = Min; i <= Max; i++)
            yield return i;
    }

    public bool Contains(int value) => value >= Min && value <= Max;
}

[Serializable]
public class ChunkRenderRange
{
    public ChunkDistanceAxisRange X;
    public ChunkDistanceAxisRange Y;
    public ChunkDistanceAxisRange Z;
}
