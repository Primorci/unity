using M2MqttUnity;
using System;
using System.Text;
using UnityEngine;
using static JSONFormating;
using static System.Runtime.CompilerServices.RuntimeHelpers;

public class FPSCounter : M2MqttUnityClient
{
    private int fps;
    private float timeInterval = 1f;
    private float timeElapsed = 0f;
    private int frameCount = 0;

    protected override void Start()
    {
        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        frameCount++;
        timeElapsed += Time.deltaTime;

        if (timeElapsed >= timeInterval)
        {
            fps = (int)(frameCount / timeElapsed);
            timeElapsed = 0f;
            frameCount = 0;

            try
            {
                client.Publish("game/performance/fps", Encoding.ASCII.GetBytes(fps.ToString()));
                MQTTManager.FPS.Set(fps);
            }
            catch (Exception e)
            {
                Debug.LogError("MQTT Publishing Error: " + e.Message);
            }
        }
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), "FPS: " + fps.ToString());
    }
}
