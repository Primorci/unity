using UnityEngine;

public class IntersectionsHandle
{
    public RoadManager roadManager;

    void onTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            int choice = Random.Range(0, roadManager.roadPrefabs.Count);
            roadManager.SpawnRoadSegment(roadManager.roadPrefabs[choice]);
        }
    }
}
