using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class RoadManager : MonoBehaviour
{
    public Transform player;
    public List<GameObject> roadPrefabs;
    public int initialRoadCount = 1;
    public float roadLength = 10f;
    public int maxRoads = 10;

    public bool con = false;

    private Queue<GameObject> activeRoads = new Queue<GameObject>();
    private Vector3 nextSpawnPosition = Vector3.zero;
    private Quaternion nextSpawnRotation = Quaternion.identity;
    private GameObject lastRoadSegment;

    private List<Vector3> nextSpawnPositions = new List<Vector3>();
    private List<Quaternion> nextSpawnRotations = new List<Quaternion>();

    private List<Vector3> dangersOnRoadPositions = new List<Vector3>();

    //useless shit on the road
    public GameObject[] useless_Shit;
    public Image danger_sign;


    void Start()
    {
        lastRoadSegment = Instantiate(roadPrefabs[16], nextSpawnPosition, nextSpawnRotation);
        activeRoads.Enqueue(lastRoadSegment);
        CollectExitPoints(lastRoadSegment);
        //SelectExitPoint(lastRoadSegment);

        //for (int i = 0; i < initialRoadCount - 1; i++)
        //{
        //    SpawnRoadSegment(RandomRoadSegment());
        //}
    }

    void Update()
    {
        //if (Vector3.Distance(player.position, nextSpawnPosition) < roadLength * 2 && con == true)
        if (isInRange())
        {
            SpawnRoadSegment(RandomRoadSegment());
            con = false;

            //useless shit creation
            int randomIndex = Random.Range(0, useless_Shit.Length);
            //Vector3 randomSpawnPosition = new Vector3(Random.Range(-10, 11), 5, Random.Range(-10, 11));

            nextSpawnPosition.y += 1.0f; 
            Instantiate(useless_Shit[randomIndex], nextSpawnPosition, Quaternion.identity);
            dangersOnRoadPositions.Add(nextSpawnPosition);
            nextSpawnPosition.y -= 1.0f;
        }

        if (isInDANGER_Range())
        {
            danger_sign.color = new Color32(255, 0, 0, 255);
        }
        else
        {
            danger_sign.color = new Color32(255, 255, 255, 255);
        }

        if (activeRoads.Count > maxRoads)
        {
            RemoveOldestRoadSegment();
        }
    }

    bool isInRange()
    {
        foreach (Vector3 spawn in nextSpawnPositions)
        {
            if (Vector3.Distance(player.position, spawn) < roadLength * 2)
            {
                nextSpawnPosition = spawn;
                nextSpawnRotation = nextSpawnRotations[nextSpawnPositions.IndexOf(spawn)];
                return true;
            }
        }
        return false;
    }

    bool isInDANGER_Range()
    {
        foreach (Vector3 spawn in dangersOnRoadPositions)
        {
            if (Vector3.Distance(player.position, spawn) < roadLength * 2)
            {
                return true;
            }
        }
        return false;
    }


    public void SpawnRoadSegment(GameObject roadPrefab)
    {
        if (CheckForIntersection(roadPrefab, nextSpawnPosition, nextSpawnRotation, lastRoadSegment))
        {
            Debug.LogWarning("Intersection detected! Skipping road segment.");
            return; // Skip generating this road segment
        }

        //StartCoroutine(SelectExitPoint(roadPrefab));

        Debug.Log($"Spawning Road: {roadPrefab.name} at {nextSpawnPosition}");

        GameObject newRoad = Instantiate(roadPrefab, nextSpawnPosition, nextSpawnRotation);
        lastRoadSegment = newRoad;
        activeRoads.Enqueue(newRoad);

        CollectExitPoints(newRoad);

        Debug.Log($"Road Segment Added Succesfuly - Road Segment Count: {activeRoads.Count}");
    }

    void CollectExitPoints(GameObject road)
    {
        Transform[] exitPoints = road.GetComponentsInChildren<Transform>();
        if (exitPoints == null)
        {
            Debug.LogWarning($"Couldn't find any exit points in {road}");
            return;
        }

        nextSpawnPositions.Clear();
        nextSpawnRotations.Clear();

        foreach (Transform t in exitPoints)
        {
            if (t.name.StartsWith("ExitPoint"))
            {
                nextSpawnPositions.Add(t.position);
                nextSpawnRotations.Add(t.rotation);
            }
        }

        Debug.Log($"Collected {nextSpawnPositions.Count} unique exit points.");
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

            // Draw the cube at the next spawn position, not relative to the player
            foreach (Vector3 spawnPos in nextSpawnPositions)
            {
                Gizmos.color = (Vector3.Distance(player.position, spawnPos) < roadLength * 2) ? Color.green : Color.red;
                Gizmos.DrawWireCube(spawnPos, new Vector3(roadLength, 1, roadLength));

                // Optionally, draw a connecting line from the player to the next spawn position
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(player.position, spawnPos);
            }
        }

        // Draw Entry and Exit Points for the last spawned road
        foreach (GameObject road in activeRoads)
        {
            Transform entryPoint = road.transform.Find("EntryPoint");
            //Transform exitPoint = road.transform.Find("ExitPoint");
            Transform[] exitPoint = road.GetComponentsInChildren<Transform>();
            List<Transform> validExits = new List<Transform>();

            foreach (Transform t in exitPoint)
            {
                if (t.name.StartsWith("ExitPoint"))
                {
                    validExits.Add(t);
                }
            }

            if (entryPoint != null)
            {
                Gizmos.color = Color.green; // Green for Entry
                Gizmos.DrawSphere(entryPoint.position, 0.5f);
            }

            if (exitPoint != null)
            {
                Gizmos.color = Color.blue; // Blue for Exit
                foreach (Transform exit in validExits)
                {
                    Gizmos.DrawSphere(exit.position, 0.5f);
                }
            }  
        }
    }

}
