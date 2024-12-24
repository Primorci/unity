using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class RoadManager : MonoBehaviour
{
    public Transform player;
    public List<GameObject> roadPrefabs;
    public int initialRoadCount = 5;
    public float roadLength = 10f;
    public int maxRoads = 10;

    private Queue<GameObject> activeRoads = new Queue<GameObject>();
    private Vector3 nextSpawnPos = Vector3.zero;
    private Quaternion nextSpawnRotation = Quaternion.identity;
    private GameObject lastRoadSegment;

    void Start()
    {
        for (int i = 0; i < initialRoadCount; i++)
        {
            SpawnRoadSegment(RandomRoadSegment());
        }
    }

    void Update()
    {
        if (Vector3.Distance(player.position, nextSpawnPos) < roadLength * 2)
        {
            SpawnRoadSegment(RandomRoadSegment());
        }

        if (activeRoads.Count > maxRoads)
        {
            RemoveOldestRoadSegment();
        }
    }

    public void SpawnRoadSegment(GameObject roadPrefab)
    {
        if (CheckForIntersection(roadPrefab, nextSpawnPos, nextSpawnRotation, lastRoadSegment))
        {
            Debug.LogWarning("Intersection detected! Skipping road segment.");
            return; // Skip generating this road segment
        }

        Debug.Log($"Spawning Road: {roadPrefab.name} at {nextSpawnPos}");

        GameObject newRoad = Instantiate(roadPrefab, nextSpawnPos, nextSpawnRotation);
        lastRoadSegment = newRoad;
        activeRoads.Enqueue(newRoad);

        Transform exitPoint = SelectExitPoint(newRoad);
        if(exitPoint != null)
        {
            nextSpawnPos = exitPoint.position;
            nextSpawnRotation = exitPoint.rotation;
            Debug.Log($"Next spawn position: {nextSpawnPos}");
        }
        else
        {
            Debug.LogError("Road prefab is missing an ExitPoint: " + roadPrefab.name);
        }

        Debug.Log($"Road Segment Added Succesfuly - Road Segment Count: {activeRoads.Count}");

    }

    Transform SelectExitPoint(GameObject road)
    {
        Transform selectedExit = null;

        // Find all ExitPoints in the prefab
        Transform[] exits = road.GetComponentsInChildren<Transform>();
        List<Transform> validExits = new List<Transform>();

        foreach (Transform t in exits)
        {
            if (t.name.StartsWith("ExitPoint"))
            {
                validExits.Add(t);
            }
        }

        // Choose an ExitPoint based on your logic
        if (validExits.Count > 0)
        {
            // Example: Randomly choose an ExitPoint
            selectedExit = validExits[Random.Range(0, validExits.Count)];
            Debug.Log($"Selected ExitPoint: {selectedExit.name}");
        }

        return selectedExit;
    }

    GameObject RandomRoadSegment()
    {
        return roadPrefabs[Random.Range(0, roadPrefabs.Count)];
    }

    void RemoveOldestRoadSegment()
    {
        GameObject oldestRoad = activeRoads.Dequeue();
        Debug.Log($"Road Segment Deleted: {oldestRoad.ToString()}\nRoad Segment Count: {activeRoads.Count}");
        Destroy(oldestRoad);
    }

    bool CheckForIntersection(GameObject roadPrefab, Vector3 spawnPosition, Quaternion spawnRotation, GameObject lastRoadSegment)
    {
        // Get BoxCollider of the prefab
        BoxCollider boxCollider = roadPrefab.GetComponent<BoxCollider>();
        if (boxCollider == null)
        {
            Debug.LogError("Road prefab must have a BoxCollider for intersection detection.");
            return false;
        }

        // Calculate size and adjusted center
        Vector3 size = boxCollider.size * 1.01f; // Add slight padding
        Vector3 center = spawnPosition + spawnRotation * boxCollider.center;

        // Check for overlapping objects
        Collider[] intersectingObjects = Physics.OverlapBox(center, size / 2, spawnRotation);

        // Filter intersecting objects by tag and ignore the last road segment
        foreach (Collider collider in intersectingObjects)
        {
            if (collider.CompareTag("Road") && collider.gameObject != lastRoadSegment)
            {
                Debug.Log($"Intersection detected with: {collider.name}");
                return true;
            }
        }

        // No intersections found
        return false;
    }



    void OnDrawGizmos()
    {
        // Draw trigger zone as described earlier
        if (player != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(
                new Vector3(player.position.x, player.position.y, nextSpawnPos.z),
                new Vector3(roadLength, 1, 1)
            );
        }

        // Draw Entry and Exit Points for the last spawned road
        foreach (GameObject road in activeRoads)
        {
            Transform entryPoint = road.transform.Find("EntryPoint");
            Transform exitPoint = road.transform.Find("ExitPoint");

            if (entryPoint != null)
            {
                Gizmos.color = Color.green; // Green for Entry
                Gizmos.DrawSphere(entryPoint.position, 0.5f);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.blue; // Blue for Exit
                Gizmos.DrawSphere(exitPoint.position, 0.5f);
            }
        }
    }

}
