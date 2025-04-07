using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GenericStore : MonoBehaviour
{
    private static GenericStore _Instance;

    [SerializeField]
    private List<GenericStoreEntry> entries = new List<GenericStoreEntry>();

    private void Awake()
    {
        if (_Instance == null)
            _Instance = this;
    }

    public static GenericStore Instance { get { return _Instance; } }

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
    public string Name;
    public List<GameObject> Entries;
}