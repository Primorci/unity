using UnityEngine;

public class CameraSwitch : MonoBehaviour
{
    public Camera[] cameras; // Assign your cameras in the Inspector
    private int currentCameraIndex = 0;

    void Start()
    {
        // Ensure only the first camera is active at the start
        ActivateCamera(currentCameraIndex);
    }

    void Update()
    {
        // Switch cameras with a key press (e.g., Tab key)
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            currentCameraIndex = (currentCameraIndex + 1) % cameras.Length;
            ActivateCamera(currentCameraIndex);
        }
    }

    void ActivateCamera(int index)
    {
        for (int i = 0; i < cameras.Length; i++)
        {
            cameras[i].enabled = (i == index); // Enable the selected camera, disable others
        }
    }
}
