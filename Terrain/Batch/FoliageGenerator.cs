﻿using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SocialPlatforms;
using static MeshHelper;
public class FoliageGenerator : MonoBehaviour
{
    private MeshBatchDrawer foliageDrawer;

    public float maxGrassHeight = 2.3f;
    public float grassDensity = 10f;

    public GenericStore Store;

    private void Update()
    {
        if (foliageDrawer != null)
            foliageDrawer.Update();
    }

    public void ApplyMap(GenericStore store)
    {
        this.Store = store;

        foliageDrawer = null;
        foliageDrawer = new MeshBatchDrawer(Camera.main);

        ProcessGrassPositions();
    }

    private void ProcessGrassPositions()
    {
        MeshFilter meshFilter = this.GetComponent<MeshFilter>();

        foreach (TrianglePOS tria in MeshHelper.GetRandomPositionsInTriangles(meshFilter.sharedMesh, this.transform, 2, true, "Default"))
        {
            float roll = Random.value;
            float flowerChance = 0.05f;
            float rockChance = 0.002f;

            float averageHeight = tria.Position.y;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, tria.Normal) * Quaternion.Euler(0, Random.Range(0, 360), 0);

            if (roll < rockChance)
            {
                Vector3 rockScale = Vector3.one * Random.Range(0.2f, 3f);
                foliageDrawer.Add(this.Store.GetOneRandom("Rocks"), tria.Position, rotation, rockScale);
            }

            if ((int)averageHeight > 32)
            {
                Vector3 scale = Vector3.one * Random.Range(0.7f, 1.4f);
                foliageDrawer.Add(this.Store.GetOneRandom("Grass"), tria.Position, rotation, scale);

                if (roll < flowerChance + rockChance) // Flower spawn, only if rock didn't spawn
                {
                    Vector3 flowerScale = Vector3.one * Random.Range(1.3f, 2.5f);
                    foliageDrawer.Add(this.Store.GetOneRandom("Flowers"), tria.Position, rotation, flowerScale);
                }
            }
        }
    }
}