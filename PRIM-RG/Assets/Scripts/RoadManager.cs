
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
    public float roadLength = 10f;
    public int maxRoads = 10;

    public List<GameObject> dangerPrefabs;
    public Image danger_sign;

    public bool con = false;

    private Queue<GameObject> activeRoads = new Queue<GameObject>();
    private Vector3 nextSpawnPosition = Vector3.zero;
    private Quaternion nextSpawnRotation = Quaternion.identity;
    private GameObject lastRoadSegment;

    private List<Vector3> nextSpawnPositions = new List<Vector3>();
    private List<Quaternion> nextSpawnRotations = new List<Quaternion>();

    private List<Vector3> nextSpawnPositionsDanger = new List<Vector3>();
    private List<Quaternion> nextSpawnRotationsDanger = new List<Quaternion>();

    private float startTime, endTime, loadTime;

    private List<Vector3> dangersOnRoadPositions = new List<Vector3>();

    private class RoadData
    {
        public string eventType;

        public string roadType;
        public string roadName;

        public float loadTime;
        public int exitPointCount;
        public Vector3 position;

        public RoadData() { }
        public RoadData(string eventType, string roadName, string roadType, float loadTime, int exitPointCount, Vector3 position)
        {
            this.eventType = eventType;
            this.roadName = roadName;
            this.roadType = roadType;
            this.loadTime = loadTime;
            this.exitPointCount = exitPointCount;
            this.position = position;
        }
    }

    private class DangerData
    {
        public string eventType;

        public string dangerType;

        public float loadTime;
        public Vector3 position;

        public DangerData() { }
        public DangerData(string eventType, string DangerType, float loadTime, Vector3 position)
        {
            this.eventType = eventType;
            this.dangerType = DangerType;
            this.loadTime = loadTime;
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
            RoadData road = new RoadData("generation", roadPrefab.name, roadPrefab.tag, loadTime, exitPointsCount, new Vector3(0, 0, 0));
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

        if (activeRoads.Count > maxRoads)
        {
            startTime = Time.realtimeSinceStartup;

            Tuple<string, string> oldRoad = RemoveOldestRoadSegment();

            endTime = Time.realtimeSinceStartup;
            loadTime = endTime - startTime;

            try
            {
                RoadData road = new RoadData("degeneration", oldRoad.Item1, oldRoad.Item2, loadTime, -1, new Vector3(0, 0, 0));
                client.Publish("game/road/degeneration", Encoding.ASCII.GetBytes(JsonUtility.ToJson(road)));

                PublishPrometheus(road);
            }
            catch (Exception e)
            {
                Debug.LogError("MQTT Publishing Error: " + e.Message);
            }
        }
    }

    private void FixedUpdate()
    {
        if (isInRange())
        {
            SpawnRoadSegment(RandomRoadSegment());
        }

        if (YoloResults.distance == 2)
        {
            danger_sign.color = new Color32(255, 0, 0, 255);
        }
        else if (YoloResults.distance == 1)
        {
            danger_sign.color = new Color32(255, 165, 0, 255);
        }
        else
        {
            danger_sign.color = new Color32(255, 255, 255, 255);
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
                RoadData errorRoad = new RoadData("error", roadPrefab.name, roadPrefab.tag, loadTime, -1, nextSpawnPosition);
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

        GameObject danger = spawnDanger(newRoad);

        endTime = Time.realtimeSinceStartup;
        loadTime = endTime - startTime;
        DangerData dangerData = new DangerData("generation", danger.name, loadTime, danger.transform.position);

        try
        {
            RoadData road = new RoadData("generation", roadPrefab.name,roadPrefab.tag, loadTime, exitPointsCount, lastPosition);
            client.Publish("game/road/generation", Encoding.ASCII.GetBytes(JsonUtility.ToJson(road)));
            client.Publish("game/road/danger", Encoding.ASCII.GetBytes(JsonUtility.ToJson(dangerData)));

            PublishPrometheus(road);
            PublishPrometheus(dangerData);
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

    Tuple<string, string> RemoveOldestRoadSegment()
    {
        GameObject oldestRoad = activeRoads.Dequeue();
        string name = oldestRoad.name;
        string tag = oldestRoad.tag;

        Debug.Log($"Road Segment Deleted: {oldestRoad.ToString()}\nRoad Segment Count: {activeRoads.Count}");
        Destroy(oldestRoad);

        return new Tuple<string, string>(name, tag);
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

    GameObject spawnDanger(GameObject roadPrefab)
    {
        int spawnPos = CollectSpawnPoints(roadPrefab);
        if (spawnPos <= 0) // Ensure there are valid spawn points
        {
            Debug.LogError($"No valid spawn points found in {roadPrefab.name}. spawnPos = {spawnPos}");
            return null;
        }

        try
        {
            int index = Random.Range(0, nextSpawnPositionsDanger.Count);
            GameObject dangerSpawned = Instantiate(
                dangerPrefabs[Random.Range(0, dangerPrefabs.Count)],
                nextSpawnPositionsDanger[index],
                nextSpawnRotationsDanger[index]
            );
            dangersOnRoadPositions.Add(nextSpawnPositionsDanger[index]);

            return dangerSpawned;
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
            Debug.LogError($"nextSpawnPositionsDanger.Count = {nextSpawnPositionsDanger.Count}");
        }

        return null;
    }

    int CollectSpawnPoints(GameObject danger)
    {
        Transform[] spawnPoints = danger.GetComponentsInChildren<Transform>();

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"Couldn't find any spawn points in {danger.name}");
            return 0;
        }

        nextSpawnPositionsDanger.Clear();
        nextSpawnRotationsDanger.Clear();

        foreach (Transform t in spawnPoints)
        {
            if (t.name.StartsWith("spawnDanger"))
            {
                // Add position and rotation
                nextSpawnPositionsDanger.Add(new Vector3(t.position.x, t.position.y + 1, t.position.z));
                nextSpawnRotationsDanger.Add(Quaternion.Euler(t.rotation.eulerAngles.x, Random.Range(0f, 360f), t.rotation.eulerAngles.z));
            }
        }

        int count = nextSpawnPositionsDanger.Count;
        Debug.Log($"Collected {count} valid spawn points.");
        return count; // Return the number of valid spawn points
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

    private void PublishPrometheus(DangerData danger)
    {
        MQTTManager.ObstacleType.WithLabels(danger.dangerType).Inc();
        MQTTManager.ObstacleGenerationTime.Observe(danger.loadTime);
        MQTTManager.ObstaclePositionX.Set(danger.position.x);
        MQTTManager.ObstaclePositionY.Set(danger.position.y);
        MQTTManager.ObstaclePositionZ.Set(danger.position.z);
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
