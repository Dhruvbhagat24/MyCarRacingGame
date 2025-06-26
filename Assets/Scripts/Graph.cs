using System.Collections.Generic;
using UnityEngine;

public class Graph : MonoBehaviour
{
    public string mapFileName = "map.osm";
    public float scaleFactor = 5000f;

    // Add these fields so Editor and other scripts can access them
    public Dictionary<string, Node> nodes { get; private set; } = new Dictionary<string, Node>();
    public List<Way> ways { get; private set; } = new List<Way>();

    void Start()
    {
        string fullPath = Application.dataPath + "/Data/" + mapFileName;
        LoadMapData(fullPath, scaleFactor);
    }

    public void LoadMapData(string path, float scale)
    {
        if (System.IO.File.Exists(path))
        {
            OSMParser parser = new OSMParser();
            parser.LoadOSM(path, scale);
            nodes = parser.nodes;
            ways = parser.ways;
            Debug.Log("Loaded: " + nodes.Count + " nodes, " + ways.Count + " ways.");
        }
        else
        {
            Debug.LogError("Map file not found at: " + path);
        }
    }

    // This method matches what the Editor tool expects
    public void UpdateNodesAndWays(string filePath, float scale)
    {
        LoadMapData(filePath, scale);
    }
}
