using System.Collections.Generic;
using UnityEngine;

public class Danger : MonoBehaviour
{
    public Transform player; // Reference to the player object
    public static bool isDanger = false; // Set to true if at least one object is in front

    private static List<Transform> targetObjects = new List<Transform>(); // List of objects to check against

    void Update()
    {
        // Reset isDanger to false at the start of each frame
        isDanger = false;

        // Loop through all the target objects
        foreach (Transform targetObject in targetObjects)
        {
            // Get the vector from the player to the target object
            Vector3 toObject = targetObject.position - player.position;

            // Get the forward direction of the player (relative to the player object)
            Vector3 playerForward = player.forward;

            // Calculate the dot product
            float dotProduct = Vector3.Dot(playerForward, toObject.normalized);

            if (dotProduct > 0)
            {
                // If any object is in front of the player, set isDanger to true
                Debug.Log("The object is in front of the player.");
                isDanger = true;
                break; // No need to check further objects if danger is already detected
            }
            else
            {
                // The object is to the left or right of the player
                Debug.Log("The object is to the left or right of the player.");
            }
        }
    }

    // Method to add a new target object to the list (e.g., called when the object is spawned)
    public static void AddTargetObject(Transform newTargetObject)
    {
        if (!targetObjects.Contains(newTargetObject))
        {
            targetObjects.Add(newTargetObject);
            Debug.Log("New target object added: " + newTargetObject.name);
        }
    }
}
