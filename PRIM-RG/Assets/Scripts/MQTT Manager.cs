using System;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using M2MqttUnity;

public class MQTTManager : M2MqttUnityClient
{
    public string MqttAddres = "test.mosquitto.org";
    private MqttClient client;

    private void Start()
    {
        try
        {
            client = new MqttClient(MqttAddres);
            client.MqttMsgPublishReceived += onMessageReceived;
            string clientId = Guid.NewGuid().ToString();
            client.Connect(clientId);
        }
        catch(Exception e)
        {
            Debug.LogError($"Unable to connect to MQTT addres {MqttAddres}");
        }

        if (client.IsConnected)
        {
            client.Subscribe(new string[] { "test/topic" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

            client.Publish("test/topic", System.Text.Encoding.UTF8.GetBytes("Hello World!"));
        }
    }

    void onMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        Debug.Log("Received message: " + System.Text.Encoding.UTF8.GetString(e.Message));
    }

    private void OnDestroy()
    {
        client.Disconnect();
    }
}
