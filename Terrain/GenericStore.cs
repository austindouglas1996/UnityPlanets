using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GenericStore
{
    private List<GenericStoreEntry> entries = new List<GenericStoreEntry>();

    public List<GameObject> Get(string name)
    {
        return entries.FirstOrDefault(r => r.Name == name).Entries;
    }

    public GameObject GetOneRandom(string name)
    {
        return Get(name).Random();
    }
}

[Serializable]
public class GenericStoreEntry
{
    public string Name { get; set; }
    public List<GameObject> Entries { get; set; }
}