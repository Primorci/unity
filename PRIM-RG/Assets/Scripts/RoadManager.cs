
using M2MqttUnity;
using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.WSA;
using uPLibrary.Networking.M2Mqtt.Messages;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using Random = UnityEngine.Random;
using UnityEngine.UI;


public class RoadManager : M2MqttUnityClient
{
    public Transform player;
    public List<GameObject> roadPrefabs;
    //public int initialRoadCount = 1;
    public float roadLength = 10f;
    public int maxRoads = 10;

    public bool con = false;

    private Queue<GameObject> activeRoads = new Queue<GameObject>();
    private Vector3 nextSpawnPosition = Vector3.zero;
    private Quaternion nextSpawnRotation = Quaternion.identity;
    private GameObject lastRoadSegment;

    private List<Vector3> nextSpawnPositions = new List<Vector3>();
    private List<Quaternion> nextSpawnRotations = new List<Quaternion>();


    private float startTime, endTime, loadTime;

    private List<Vector3> dangersOnRoadPositions = new List<Vector3>();
    //useless shit on the road
    public List<GameObject> useless_Shit;
    public Image danger_sign;

    private class RoadData
    {
        public string eventType;

        public string roadType;

        public float loadTime;
        public int exitPointCount;
        public Vector3 position;

        public RoadData() { }
        public RoadData(string eventType, string roadType, float loadTime, int exitPointCount, Vector3 position)
        {
            this.eventType = eventType;
            this.roadType = roadType;
            this.loadTime = loadTime;
            this.exitPointCount = exitPointCount;
            this.position = position;
        }
    }

    protected override void Start()
    {
        base.Start();

        startTime = Time.realtimeSinceStartup;
        //var roadPrefab = RandomRoadSegment();
        var roadPrefab = roadPrefabs[9];
        lastRoadSegment = Instantiate(roadPrefab, nextSpawnPosition, nextSpawnRotation);
        activeRoads.Enqueue(lastRoadSegment);
        var exitPointsCount = CollectExitPoints(lastRoadSegment);

        endTime = Time.realtimeSinceStartup;
        loadTime = endTime - startTime;

        try
        {
            RoadData road = new RoadData("generation", roadPrefab.name, loadTime, exitPointsCount, new Vector3(0, 0, 0));
            client.Publish("game/road/generation", Encoding.ASCII.GetBytes(JsonUtility.ToJson(road)));

            PublishPrometheus(road);
        }catch(Exception e)
        {
            Debug.LogError("MQTT Publishing Error: " + e.Message);
        }
    }

    protected override void Update()
    {
        base.Update();

        if (isInRange())
        {
            SpawnRoadSegment(RandomRoadSegment());

            con = false;

            //useless shit creation
            //int randomIndex = Random.Range(0, useless_Shit.Length);
            //Vector3 randomSpawnPosition = new Vector3(Random.Range(-10, 11), 5, Random.Range(-10, 11));

            nextSpawnPosition.y += 1.0f; 
            Instantiate(useless_Shit[Random.Range(0, useless_Shit.Count)], nextSpawnPosition, Quaternion.identity);
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
            startTime = Time.realtimeSinceStartup;

            string name = RemoveOldestRoadSegment();

            endTime = Time.realtimeSinceStartup;
            loadTime = endTime - startTime;

            try
            {
                RoadData road = new RoadData("degeneration", name, loadTime, -1, new Vector3(0, 0, 0));
                client.Publish("game/road/degeneration", Encoding.ASCII.GetBytes(JsonUtility.ToJson(road)));

                PublishPrometheus(road);
            }
            catch (Exception e)
            {
                Debug.LogError("MQTT Publishing Error: " + e.Message);
            }
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
        startTime = Time.realtimeSinceStartup;
        if (CheckForIntersection(roadPrefab, nextSpawnPosition, nextSpawnRotation, lastRoadSegment))
        {
            Debug.LogWarning("Intersection detected! Skipping road segment.");
            endTime = Time.realtimeSinceStartup;
            loadTime = endTime - startTime;

            try
            {
                RoadData errorRoad = new RoadData("error", roadPrefab.name, loadTime, -1, nextSpawnPosition);
                client.Publish("game/road/generation/error", Encoding.ASCII.GetBytes(JsonUtility.ToJson(errorRoad)));

                PublishPrometheus(errorRoad);
            }
            catch (Exception e)
            {
                Debug.LogError("MQTT Publishing Error: " + e.Message);
            }

            return; // Skip generating this road segment
        }

        //StartCoroutine(SelectExitPoint(roadPrefab));

        Debug.Log($"Spawning Road: {roadPrefab.name} at {nextSpawnPosition}");

        GameObject newRoad = Instantiate(roadPrefab, nextSpawnPosition, nextSpawnRotation);
        lastRoadSegment = newRoad;
        activeRoads.Enqueue(newRoad);

        Vector3 lastPosition = nextSpawnPosition;
      

        var exitPointsCount = CollectExitPoints(newRoad);

        Debug.Log($"Road Segment Added Succesfuly - Road Segment Count: {activeRoads.Count}");

        endTime = Time.realtimeSinceStartup;
        loadTime = endTime - startTime;
        
        try
        {
            RoadData road = new RoadData("generation", roadPrefab.name, loadTime, exitPointsCount, lastPosition);
            client.Publish("game/road/generation", Encoding.ASCII.GetBytes(JsonUtility.ToJson(road)));

            PublishPrometheus(road);
        }
        catch (Exception e)
        {
            Debug.LogError("MQTT Publishing Error: " + e.Message);
        }
    }

    int CollectExitPoints(GameObject road)
    {
        Transform[] exitPoints = road.GetComponentsInChildren<Transform>();
        if (exitPoints == null)
        {
            Debug.LogWarning($"Couldn't find any exit points in {road}");
            return -1;
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

        return exitPoints.Length;
    }

    GameObject RandomRoadSegment()
    {
        return roadPrefabs[Random.Range(0, roadPrefabs.Count)];
    }

    string RemoveOldestRoadSegment()
    {
        GameObject oldestRoad = activeRoads.Dequeue();
        string name = oldestRoad.name;

        Debug.Log($"Road Segment Deleted: {oldestRoad.ToString()}\nRoad Segment Count: {activeRoads.Count}");
        Destroy(oldestRoad);

        return name;
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

    private void PublishPrometheus(RoadData road)
    {
        MQTTManager.RoadType.WithLabels(road.roadType).Inc();
        MQTTManager.RoadGenerationTime.Observe(road.loadTime);
        MQTTManager.ExitPointCount.Observe(road.exitPointCount);
        MQTTManager.RoadPositionX.Set(road.position.x);
        MQTTManager.RoadPositionY.Set(road.position.y);
        MQTTManager.RoadPositionZ.Set(road.position.z);
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
