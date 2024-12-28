using UnityEngine;

public class FPSCounter : MonoBehaviour
{
    private int fps;
    private float timeInterval = 1f;
    private float timeElapsed = 0f;
    private int frameCount = 0;

    private void Update()
    {
        frameCount++;
        timeElapsed += Time.deltaTime;

        if (timeElapsed >= timeInterval)
        {
            fps = (int)(frameCount / timeElapsed);
            timeElapsed = 0f;
            frameCount = 0;

            //JSONFormating.FPSData data = new JSONFormating.FPSData(fps);
            //MQTTManager.PublishData(
            //    "game/performance/fps",
            //    JsonUtility.ToJson(data),
            //    JSONFormating.CreatePrometheusFormat<JSONFormating.FPSData>(data)
            //);
            MQTTManager.FPS.Set(fps);
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), "FPS: " + fps.ToString());
    }
}
