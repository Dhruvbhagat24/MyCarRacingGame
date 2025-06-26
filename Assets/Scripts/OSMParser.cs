using System.Xml.Linq;
using System.IO;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;

public class Node
{
    public string id { get; set; }
    public Vector2 position { get; set; }
    public List<Node> Neighbors { get; set; } = new List<Node>();

    public Node(string id, Vector2 position)
    {
        this.id = id;
        this.position = position;
    }
}

public class Way
{
    public string id { get; set; }
    public List<string> nodeRefs { get; set; }

    public Way(string id)
    {
        this.id = id;
        nodeRefs = new List<string>();
    }
}

public class OSMParser
{
    public Dictionary<string, Node> nodes = new Dictionary<string, Node>();
    public List<Way> ways = new List<Way>();
    private Dictionary<string, Node> allNodes = new Dictionary<string, Node>();
    private HashSet<string> referencedNodeIds = new HashSet<string>();
    private Vector2 referencePoint = Vector2.zero;
    private float scaleFactor = 5000f;
    private bool referencePointSet = false;

    public void LoadOSM(string filePath, float scaleFactor)
    {
        this.scaleFactor = scaleFactor;
        if (!File.Exists(filePath))
        {
            Debug.LogError("File not found");
        }
        
        XDocument doc = XDocument.Load(filePath);

        foreach (var node in doc.Descendants("node"))
        {
            string id = node.Attribute("id")?.Value;
            double lat = double.Parse(node.Attribute("lat")?.Value);
            double lon = double.Parse(node.Attribute("lon")?.Value);

            if (!referencePointSet)
            {
                referencePoint = new Vector2((float)lon, (float)lat);
                referencePointSet = true;
            }

            float normalizedLat = (float)(lat - referencePoint.y);
            float normalizedLon = (float)(lon - referencePoint.x);

            Vector2 position = new Vector2(normalizedLon, normalizedLat) / scaleFactor;
            allNodes[id] = new Node(id, position);
        }
        
        foreach (var way in doc.Descendants("way"))
        {
            string id = way.Attribute("id")?.Value;
            Way osmWay = new Way(id);
            var ndRefs = way.Descendants("nd").Select(nd => nd.Attribute("ref")?.Value).ToList();
            if (ndRefs.First() == ndRefs.Last())
            {
                // Skip closed paths
                continue;
            }
            osmWay.nodeRefs.AddRange(ndRefs);
            ways.Add(osmWay);

            foreach (var nodeRef in ndRefs)
            {
                referencedNodeIds.Add(nodeRef);
            }
        }

        foreach (var nodeId in referencedNodeIds)
        {
            if (allNodes.TryGetValue(nodeId, out var node))
            {
                nodes[nodeId] = node;
            }
        }

        foreach (var way in ways)
        {
            for (int i = 0; i < way.nodeRefs.Count - 1; i++)
            {
                if (nodes.TryGetValue(way.nodeRefs[i], out var startNode) &&
                    nodes.TryGetValue(way.nodeRefs[i + 1], out var endNode))
                {
                    startNode.Neighbors.Add(endNode);
                    endNode.Neighbors.Add(startNode);
                }
            }
        }
    }
}
