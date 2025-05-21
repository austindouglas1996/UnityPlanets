using System;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

/// <summary>
/// Base class for implementing marching cube terrain generation.
/// Handles mesh generation, density map modification, and interpolation.
/// </summary>
public abstract class BaseMarchingCubeGenerator : IDensityMapGenerator
{
    /// <summary>
    /// Used to help with vertex coloring.
    /// </summary>
    private IChunkColorizer _colorizer;

    /// <summary>
    /// Creates a new marching cube generator with the given density options.
    /// </summary>
    /// <param name="options">The configuration used for density generation and surface thresholds.</param>
    public BaseMarchingCubeGenerator(IChunkColorizer colorizer, DensityMapOptions options)
    {
        if (colorizer == null)
            throw new ArgumentNullException("colorizer is null.");
        if (options == null)
            throw new System.ArgumentNullException("options is null.");

        this._colorizer = colorizer;
        this.Options = options;
    }

    /// <summary>
    /// The options used for generating density and controlling surface behavior.
    /// </summary>
    public DensityMapOptions Options { get; set; }

    /// <summary>
    /// Generates a 3D density map for the chunk at the specified coordinates.
    /// </summary>
    /// <param name="chunkSize">Size of the chunk (assumed cubic).</param>
    /// <param name="chunkCoordinates">Coordinates of the chunk in chunk space.</param>
    /// <returns>A 3D float array representing density values.</returns>
    public abstract DensityMap Generate(Vector3Int chunkCoordinates, int lodIndex);

    /// <summary>
    /// Generates mesh data from a given density map using the marching cubes algorithm.
    /// Automatically skips generation if the entire chunk is empty or solid.
    /// </summary>
    /// <param name="densityMap">3D density values for the chunk (includes +1 padding).</param>
    /// <param name="chunkOffset">World-space offset for this chunk's origin.</param>
    /// <returns>MeshData containing vertices, triangles, and optional UVs.</returns>
    public virtual MeshData GenerateMeshData(DensityMap densityMap, Vector3 chunkOffset, int lodIndex = 5)
    {
        int stepSize = 1 << lodIndex;

        int densityWidth = densityMap.SizeX;
        int densityHeight = densityMap.SizeY;
        int densityDepth = densityMap.SizeZ;

        int width = densityWidth - stepSize;
        int height = densityHeight - stepSize;
        int depth = densityDepth - stepSize;

        float minDensity = float.MaxValue;
        float maxDensity = float.MinValue;

        for (int x = 0; x < densityWidth; x += stepSize)
            for (int y = 0; y < densityHeight; y += stepSize)
                for (int z = 0; z < densityDepth; z += stepSize)
                {
                    float d = densityMap.GetLocal(x,y,z);
                    if (d < minDensity) minDensity = d;
                    if (d > maxDensity) maxDensity = d;
                }

        if (minDensity > Options.ISOLevel || maxDensity < Options.ISOLevel)
        {
            return new MeshData(new(), new(), new());
        }

        var Vertices = new List<Vector3>();
        var Colors = new List<Color32>();
        var Triangles = new List<int>();
        var UVs = new List<Vector2>(); // unused, can be removed if not used

        for (int x = 0; x < width; x += stepSize)
        {
            for (int y = 0; y < height; y += stepSize)
            {
                for (int z = 0; z < depth; z += stepSize)
                {
                    float[] cornerVals = new float[8];
                    Vector3[] cornerPos = new Vector3[8];

                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 offset = CornerOffsets[i] * stepSize;

                        int cx = x + (int)offset.x;
                        int cy = y + (int)offset.y;
                        int cz = z + (int)offset.z;

                        cornerVals[i] = densityMap.GetLocal(cx, cy, cz);
                        cornerPos[i] = new Vector3(cx, cy, cz) * stepSize + chunkOffset;
                    }

                    int cubeIndex = 0;
                    for (int i = 0; i < 8; i++)
                    {
                        if (cornerVals[i] > Options.ISOLevel)
                            cubeIndex |= 1 << i;
                    }

                    if (TriangleTable[cubeIndex, 0] == -1)
                        continue;

                    for (int t = 0; TriangleTable[cubeIndex, t] != -1; t += 3)
                    {
                        int ei0 = TriangleTable[cubeIndex, t];
                        int ei1 = TriangleTable[cubeIndex, t + 1];
                        int ei2 = TriangleTable[cubeIndex, t + 2];

                        Vector3 v1 = InterpolateEdge(
                            Options.ISOLevel,
                            cornerPos[EdgeConnections[ei0, 0]],
                            cornerPos[EdgeConnections[ei0, 1]],
                            cornerVals[EdgeConnections[ei0, 0]],
                            cornerVals[EdgeConnections[ei0, 1]]
                        );
                        Vector3 v2 = InterpolateEdge(
                            Options.ISOLevel,
                            cornerPos[EdgeConnections[ei1, 0]],
                            cornerPos[EdgeConnections[ei1, 1]],
                            cornerVals[EdgeConnections[ei1, 0]],
                            cornerVals[EdgeConnections[ei1, 1]]
                        );
                        Vector3 v3 = InterpolateEdge(
                            Options.ISOLevel,
                            cornerPos[EdgeConnections[ei2, 0]],
                            cornerPos[EdgeConnections[ei2, 1]],
                            cornerVals[EdgeConnections[ei2, 0]],
                            cornerVals[EdgeConnections[ei2, 1]]
                        );

                        int baseIndex = Vertices.Count;
                        Vertices.Add(v1);
                        Vertices.Add(v2);
                        Vertices.Add(v3);

                        Colors.Add(this._colorizer.GetColorForVertice(v1));
                        Colors.Add(this._colorizer.GetColorForVertice(v2));
                        Colors.Add(this._colorizer.GetColorForVertice(v3));

                        Triangles.Add(baseIndex + 0);
                        Triangles.Add(baseIndex + 1);
                        Triangles.Add(baseIndex + 2);

                    }
                }
            }
        }

        MeshData data = new MeshData(Vertices, Triangles, UVs);
        data.Colors = Colors.ToArray();

        Flatten(densityMap, data, lodIndex);

        return data;
    }

    private void Flatten(DensityMap densityMap, MeshData data, int lodIndex)
    {
        List<Vector3> flatVertices = new List<Vector3>();
        List<Vector3> flatNormals = new List<Vector3>();
        List<Vector2> flatUVs = new List<Vector2>();
        List<int> flatTriangles = new List<int>();

        for (int i = 0; i < data.Triangles.Count; i += 3)
        {
            int i0 = data.Triangles[i];
            int i1 = data.Triangles[i + 1];
            int i2 = data.Triangles[i + 2];

            Vector3 v0 = data.Vertices[i0];
            Vector3 v1 = data.Vertices[i1];
            Vector3 v2 = data.Vertices[i2];

            // Inside the flat shading loop:
            Vector3 gradient0 = lodIndex == 0 ? SampleDensityGradientLOD0(v0, densityMap) : SampleDensityGradient(v0, lodIndex);
            Vector3 gradient1 = lodIndex == 0 ? SampleDensityGradientLOD0(v1, densityMap) : SampleDensityGradient(v1, lodIndex);
            Vector3 gradient2 = lodIndex == 0 ? SampleDensityGradientLOD0(v2, densityMap) : SampleDensityGradient(v2, lodIndex);

            // Average gradient for the face
            Vector3 faceNormal = -(gradient0 + gradient1 + gradient2) / 3f;
            faceNormal.Normalize();

            // Duplicate vertices
            int startIndex = flatVertices.Count;
            flatVertices.Add(v0);
            flatVertices.Add(v1);
            flatVertices.Add(v2);

            flatNormals.Add(faceNormal);
            flatNormals.Add(faceNormal);
            flatNormals.Add(faceNormal);

            // Add new UV's
            flatUVs.Add(data.UVs.Count > i0 ? data.UVs[i0] : Vector2.zero);
            flatUVs.Add(data.UVs.Count > i1 ? data.UVs[i1] : Vector2.zero);
            flatUVs.Add(data.UVs.Count > i2 ? data.UVs[i2] : Vector2.zero);

            flatTriangles.Add(startIndex);
            flatTriangles.Add(startIndex + 1);
            flatTriangles.Add(startIndex + 2);
        }

        data.Vertices = flatVertices;
        data.UVs = flatUVs;
        data.Normals = flatNormals;
        data.Triangles = flatTriangles;
    }

    /// <summary>
    /// Modifies the density map in place using a terrain brush.
    /// Adds or subtracts density values based on brush settings and hit point.
    /// </summary>
    /// <param name="brush">The brush to apply to the chunk.</param>
    /// <param name="densityMap">The density map to modify.</param>
    /// <param name="chunkPos">Chunk position in chunk space.</param>
    /// <param name="hitPoint">World-space location the brush is applied to.</param>
    /// <param name="isAdding">If true, adds density; otherwise subtracts.</param>
    public virtual void ModifyMapWithBrush(TerrainBrush brush, ref DensityMap densityMap, Vector3Int chunkPos, bool isAdding)
    {
        int width = densityMap.SizeX - 1;
        int height = densityMap.SizeY - 1;
        int depth = densityMap.SizeZ - 1;

        Vector3 chunkWorldOrigin = new Vector3(
            chunkPos.x * width,
            chunkPos.y * height,
            chunkPos.z * depth);

        Vector3 localMin = brush.Min - chunkWorldOrigin;
        Vector3 localMax = brush.Max - chunkWorldOrigin;

        // Clamp to valid voxel range
        int minX = Mathf.Max(0, Mathf.FloorToInt(localMin.x));
        int minY = Mathf.Max(0, Mathf.FloorToInt(localMin.y));
        int minZ = Mathf.Max(0, Mathf.FloorToInt(localMin.z));

        int maxX = Mathf.Min(width, Mathf.CeilToInt(localMax.x));
        int maxY = Mathf.Min(height, Mathf.CeilToInt(localMax.y));
        int maxZ = Mathf.Min(depth, Mathf.CeilToInt(localMax.z));

        for (int x = minX; x <= maxX; x++)
        {
            for (int y = minY; y <= maxY; y++)
            {
                for (int z = minZ; z <= maxZ; z++)
                {
                    Vector3 voxelWorldPos = chunkWorldOrigin + new Vector3(x, y, z);
                    float effect = brush.GetEffectAmount(voxelWorldPos, brush.WorldHitPoint);

                    if (effect == 0) continue;

                    if (isAdding)
                        densityMap[x, y, z] += effect;
                    else
                        densityMap[x, y, z] -= effect;

                    densityMap[x, y, z] = Mathf.Clamp(densityMap[x, y, z], 0f, 1f);
                }
            }
        }
    }

    private System.Random random = new System.Random();
    public bool ShouldGenerateChunk(Vector3Int chunkCoords)
    {
        int chunkSize = this.Options.ChunkSize;
        int step = chunkSize / 2;

        int worldX = chunkCoords.x * chunkSize;
        int worldY = chunkCoords.y * chunkSize;
        int worldZ = chunkCoords.z * chunkSize;

        float iso = Options.ISOLevel;

        float[] values = new float[8];

        for (int x = 0; x < chunkSize; x += step)
        {
            for (int y = 0; y < chunkSize; y += step)
            {
                for (int z = 0; z < chunkSize; z += step)
                {
                    bool hasAbove = false;
                    bool hasBelow = false;

                    // Sample the 8 corners of a voxel cube
                    for (int i = 0; i < 8; i++)
                    {
                        Vector3 offset = CornerOffsets[i]; // defined below
                        int sx = (int)(worldX + x + offset.x * step);
                        int sy = (int)(worldY + y + offset.y * step);
                        int sz = (int)(worldZ + z + offset.z * step);

                        float v = GetValueForWorldPosition(sx, sy, sz);
                        if (v >= iso) hasAbove = true;
                        else hasBelow = true;

                        if (hasAbove && hasBelow)
                            return true; // surface detected
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Retrieve the value for a given position.
    /// </summary>
    /// <param name="worldX"></param>
    /// <param name="worldY"></param>
    /// <param name="worldZ"></param>
    /// <returns></returns>
    protected abstract float GetValueForWorldPosition(float worldX, float worldY, float worldZ);

    /// <summary>
    /// Retrieve the height value for a given X/Y position. Added to help with performance since
    /// the height will not change over an X/Y range.
    /// </summary>
    /// <param name="worldX"></param>
    /// <param name="worldZ"></param>
    /// <returns></returns>
    protected abstract float GetHeightForWorldPosition(float worldX, float worldZ);

    /// <summary>
    /// Sample a world position to find the normals for lighting.
    /// </summary>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    private Vector3 SampleDensityGradient(Vector3 worldPos, int lodIndex)
    {
        float eps = (1 << lodIndex) * 1f;

        float dx = GetValueForWorldPosition(worldPos.x + eps, worldPos.y, worldPos.z)
                  - GetValueForWorldPosition(worldPos.x - eps, worldPos.y, worldPos.z);
        float dy = GetValueForWorldPosition(worldPos.x, worldPos.y + eps, worldPos.z)
                 - GetValueForWorldPosition(worldPos.x, worldPos.y - eps, worldPos.z);
        float dz = GetValueForWorldPosition(worldPos.x, worldPos.y, worldPos.z + eps)
                 - GetValueForWorldPosition(worldPos.x, worldPos.y, worldPos.z - eps);

        return new Vector3(dx, dy, dz).normalized;
    }

    /// <summary>
    /// Sample a world position find the normals for a LOD0 normal for lighting
    /// (LOD0 does not have its collection filled with no data making this a bit cheaper)
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="densityMap"></param>
    /// <returns></returns>
    private Vector3 SampleDensityGradientLOD0(Vector3 pos, DensityMap densityMap)
    {
        float delta = 0.01f;

        float dx = SampleDensity(pos + new Vector3(delta, 0, 0), densityMap)
                 - SampleDensity(pos - new Vector3(delta, 0, 0), densityMap);
        float dy = SampleDensity(pos + new Vector3(0, delta, 0), densityMap)
                 - SampleDensity(pos - new Vector3(0, delta, 0), densityMap);
        float dz = SampleDensity(pos + new Vector3(0, 0, delta), densityMap)
                 - SampleDensity(pos - new Vector3(0, 0, delta), densityMap);

        return new Vector3(dx, dy, dz);
    }

    /// <summary>
    /// Sample a density value point to find normals for lighting.
    /// </summary>
    /// <param name="pos"></param>
    /// <param name="densityMap"></param>
    /// <returns></returns>
    private float SampleDensity(Vector3 pos, DensityMap densityMap)
    {
        int x0 = Mathf.FloorToInt(pos.x);
        int y0 = Mathf.FloorToInt(pos.y);
        int z0 = Mathf.FloorToInt(pos.z);

        int x1 = x0 + 1;
        int y1 = y0 + 1;
        int z1 = z0 + 1;

        float tx = pos.x - x0;
        float ty = pos.y - y0;
        float tz = pos.z - z0;

        x0 = Mathf.Clamp(x0, 0, densityMap.SizeX - 1);
        x1 = Mathf.Clamp(x1, 0, densityMap.SizeX - 1);
        y0 = Mathf.Clamp(y0, 0, densityMap.SizeY - 1);
        y1 = Mathf.Clamp(y1, 0, densityMap.SizeY - 1);
        z0 = Mathf.Clamp(z0, 0, densityMap.SizeZ - 1);
        z1 = Mathf.Clamp(z1, 0, densityMap.SizeZ - 1);

        float c000 = densityMap[x0, y0, z0];
        float c100 = densityMap[x1, y0, z0];
        float c010 = densityMap[x0, y1, z0];
        float c110 = densityMap[x1, y1, z0];
        float c001 = densityMap[x0, y0, z1];
        float c101 = densityMap[x1, y0, z1];
        float c011 = densityMap[x0, y1, z1];
        float c111 = densityMap[x1, y1, z1];

        float c00 = Mathf.Lerp(c000, c100, tx);
        float c01 = Mathf.Lerp(c001, c101, tx);
        float c10 = Mathf.Lerp(c010, c110, tx);
        float c11 = Mathf.Lerp(c011, c111, tx);

        float c0 = Mathf.Lerp(c00, c10, ty);
        float c1 = Mathf.Lerp(c01, c11, ty);

        return Mathf.Lerp(c0, c1, tz);
    }

    /// <summary>
    /// Interpolates a point along an edge between two positions based on density threshold.
    /// Used to calculate surface intersections in marching cubes.
    /// </summary>
    /// <param name="threshold">The surface level (ISO level).</param>
    /// <param name="p1">First corner position.</param>
    /// <param name="p2">Second corner position.</param>
    /// <param name="valP1">Density at first corner.</param>
    /// <param name="valP2">Density at second corner.</param>
    /// <returns>Interpolated position along the edge.</returns>
    protected virtual Vector3 InterpolateEdge(float threshold, Vector3 p1, Vector3 p2, float valP1, float valP2)
    {
        // If values are nearly equal (flat), return midpoint instead of just one side
        if (Mathf.Approximately(valP1, valP2))
        {
            return (p1 + p2) * 0.5f;
        }

        float t = (threshold - valP1) / (valP2 - valP1);
        return Vector3.Lerp(p1, p2, t);
    }

    /// <summary>
    /// Internal mesh logic that someone smarter than me made.
    /// </summary>
    protected static readonly Vector3[] CornerOffsets = new Vector3[]
    {
        new Vector3(0, 0, 0), new Vector3(1, 0, 0),
        new Vector3(1, 0, 1), new Vector3(0, 0, 1),
        new Vector3(0, 1, 0), new Vector3(1, 1, 0),
        new Vector3(1, 1, 1), new Vector3(0, 1, 1)
    };

    /// <summary>
    /// Internal mesh logic that someone smarter than me made.
    /// </summary>
    protected static readonly int[,] EdgeConnections = new int[,]
    {
        {0, 1}, {1, 2}, {2, 3}, {3, 0},
        {4, 5}, {5, 6}, {6, 7}, {7, 4},
        {0, 4}, {1, 5}, {2, 6}, {3, 7}
    };

    /// <summary>
    /// Internal mesh logic that someone much smarter than me made.
    /// </summary>
    protected static readonly int[,] TriangleTable = new int[,]
    {
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
        {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
        {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
        {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
        {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
        {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
        {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
        {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
        {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
        {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
        {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
        {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
        {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
        {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
        {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
        {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
        {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
        {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
        {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
        {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
        {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
        {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
        {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
        {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
        {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
        {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
        {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
        {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
        {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
        {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
        {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
        {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
        {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
        {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
        {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
        {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
        {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
        {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
        {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
        {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
        {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
        {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
        {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
        {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
        {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
        {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
        {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
        {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
        {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
        {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
        {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
        {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
        {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
        {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
        {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
        {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
        {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
        {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
        {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
        {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
        {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
        {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
        {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
        {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
        {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
        {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
        {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
        {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
        {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
        {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
        {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
        {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
        {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
        {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
        {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
        {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
        {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
        {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
        {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
        {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
        {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
        {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
        {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
        {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
        {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
        {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
        {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
        {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
        {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
        {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
        {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
        {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
        {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
        {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
        {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
        {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
        {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
        {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
        {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
        {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
        {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
        {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
        {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
        {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
        {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
        {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
        {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
        {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
        {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
        {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
        {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
        {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
        {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
        {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
        {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
        {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
        {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
        {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
        {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
        {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
        {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
        {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
        {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
        {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
        {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
        {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
        {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
        {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
        {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
        };
}