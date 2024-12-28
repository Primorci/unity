using System;
using UnityEngine;
using uPLibrary.Networking.M2Mqtt;
using uPLibrary.Networking.M2Mqtt.Messages;
using M2MqttUnity;
using System.Text;
using System.Net;
using System.Threading;
using System.Reflection;
using static UnityEngine.Rendering.DebugUI;
using UnityEditor;
using Prometheus;
using System.Collections;

public class MQTTManager : M2MqttUnityClient
{
    public string MqttAddres = "10.8.1.6";
    private static new MqttClient client = null;

    private HttpListener listener;
    private const string url = "http://localhost:5555/";
    private bool running = true;
    private static StringBuilder prometheusResponse = new StringBuilder();

    private static float timeInterval = 0.5f;
    private static float timeElapsed = 0f;

    private float sessionTimeStart = 0f;

    // Player metrics --------------------------------------
    public static readonly Gauge CarSpeed = Metrics.CreateGauge(
        "player_speed", 
        "Speed of the player's car"
    );

    public static readonly Gauge DistanceTraveled = Metrics.CreateGauge(
        "player_distance_traveled", 
        "Distance traveled with the car"
    );

    public static readonly Gauge CarState = Metrics.CreateGauge(
        "player_car_state",
        "Current state of the car (1 = idle, 2 = braking, 3 = accelerating, 4 = steering)"
    );

    public static readonly Counter KeyPressCounter = Metrics.CreateCounter(
        "player_key_presses_total",
        "Total number of key presses, categorized by key",
        new CounterConfiguration
        {
            LabelNames = new[] { "key" } // Label to differentiate keys
        }
    );

    // Performance metrics --------------------------------------
    public static readonly Gauge FPS = Metrics.CreateGauge(
        "performance_fps", 
        "Current frames per second"
    );

    // Session metrics --------------------------------------
    public static readonly Histogram SessionDuration = Metrics.CreateHistogram(
        "session_duration_seconds",
        "Duration of user sessions in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.ExponentialBuckets(start: 30, factor: 2, count: 10)
            // Buckets: 30s, 60s, 120s, 240s, ..., 15360s (4+ hours) and then +Inf
        }
    );

    // Road metrics --------------------------------------
    public static readonly Histogram RoadGenerationTime = Metrics.CreateHistogram(
        "road_generation_time_seconds",
        "Time taken to generate a road segment in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 20) // Buckets from 0.1s to 2.0s
        }
    );

    public static readonly Histogram RoadUnloadTime = Metrics.CreateHistogram(
        "road_unload_time_seconds",
        "Time taken to unload a road segment in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 20)
        }
    );

    // Metric to count the number of events, with labels for eventType and roadType
    public static readonly Counter RoadType = Metrics.CreateCounter(
       "road_type_total",
       "Total number of roads categorized by road type",
       new CounterConfiguration
       {
           LabelNames = new[] { "road_type" } // Label for categorizing road types (e.g., straight, curve)
       }
    );

    // Metric to observe the exit point count as part of road data
    public static readonly Histogram ExitPointCount = Metrics.CreateHistogram(
        "road_exit_point_count",
        "Distribution of exit point counts for road events",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 1, width: 1, count: 10) // Example buckets: 1, 2, ..., 10
        }
    );

    // Metric to capture the position (e.g., in a 2D context, like x and z)
    public static readonly Gauge RoadPositionX = Metrics.CreateGauge(
        "road_position_x",
        "X position of the road"
    );

    public static readonly Gauge RoadPositionY = Metrics.CreateGauge(
        "road_position_x",
        "Y position of the road"
    );

    public static readonly Gauge RoadPositionZ = Metrics.CreateGauge(
        "road_position_z",
        "Z position of the road"
    );

    // Obstacle metrics --------------------------------------
    public static readonly Histogram ObstacleGenerationTime = Metrics.CreateHistogram(
        "obstacle_generation_time_seconds",
        "Time taken to generate obstacle on the road in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 20) // Buckets from 0.1s to 1.0s
        }
    );

    public static readonly Histogram ObstacleDestructionTime = Metrics.CreateHistogram(
        "obstacle_destruction_time_seconds",
        "Time taken to destroy Obstacle on the road in seconds",
        new HistogramConfiguration
        {
            Buckets = Histogram.LinearBuckets(start: 0.1, width: 0.1, count: 20)
        }
    );

    // Metric to count the number of events, with labels for eventType and obstacleType
    public static readonly Counter ObstacleType = Metrics.CreateCounter(
       "obstacle_type_total",
       "Total number of obstacles categorized by obstacle type",
       new CounterConfiguration
       {
           LabelNames = new[] { "obstacle_type" } // Label for categorizing obstacle types (e.g., straight, curve)
       }
    );

    // Metric to capture the position (e.g., in a 2D context, like x and z)
    public static readonly Gauge ObstaclePositionX = Metrics.CreateGauge(
        "obstacle_position_x",
        "X position of the obstacle"
    );

    public static readonly Gauge ObstaclePositionY = Metrics.CreateGauge(
        "obstacle_position_x",
        "Y position of the obstacle"
    );

    public static readonly Gauge ObstaclePositionZ = Metrics.CreateGauge(
        "obstacle_position_z",
        "Z position of the obstacle"
    );

    // Health metrics --------------------------------------
    public static readonly Counter ErrorCount = Metrics.CreateCounter(
        "health_error_count",
        "Total number of errors in a session",
        new CounterConfiguration
        {
            LabelNames = new[] { "type" } // E.g., "type" = "critical", "minor"
        }
    );

    public static readonly Counter WarningCount = Metrics.CreateCounter(
        "health_warning_count",
        "Total number of warnings in a session",
        new CounterConfiguration
        {
            LabelNames = new[] { "type" }
        }
    );

    private new void Start()
    {
        base.Start();

        sessionTimeStart = Time.realtimeSinceStartup;

        // Initialize the MQTT broker and test the connection
        client = new MqttClient(MqttAddres);
        client.MqttMsgPublishReceived += onMessageReceived;
        string clientId = Guid.NewGuid().ToString();
        //client.Connect(clientId);

        //if (client.IsConnected)
        //{
        //    client.Subscribe(new string[] { "test/topic" }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });

        //    client.Publish("test/topic", System.Text.Encoding.UTF8.GetBytes("Hello World!"));
        //}
        //else
        //{
        //    Debug.LogError($"Unable to connect to MQTT addres {MqttAddres}");

        //}
        try
        {
            client.Connect(clientId);
            if (client.IsConnected)
            {
                Debug.Log("MQTT Client connected successfully!");
            }
            else
            {
                Debug.LogError("Failed to connect to MQTT broker.");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Exception during MQTT connection: {ex.Message}");
        }

        try
        {
            var server = new MetricServer(port: 5555);
            server.Start();
            Debug.Log("Prometheus metric server started successfully.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Failed to start Prometheus metric server: {ex.Message}");
        }
    }

    private new void Update()
    {
        base.Update();

        timeElapsed += Time.deltaTime;
    }

    public static void PublishData(string topic, string jsonData, string prometheusData)
    {

        if (timeElapsed >= timeInterval)
        {
            try
            {
                // Send the jsonData to MQTT broker
                byte[] message = Encoding.UTF8.GetBytes(jsonData);
                client.Publish(topic, message, MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE, false);

                prometheusResponse.Append(prometheusData);

                Debug.Log($"Published to topic {topic}: {jsonData}");
            }
            catch (Exception e)
            {
                Debug.LogError("Cannot publish, Exception: ." + e.Message);
            }
            timeElapsed = 0f;
        }
    }

    private void onMessageReceived(object sender, MqttMsgPublishEventArgs e)
    {
        Debug.Log("Received message: " + System.Text.Encoding.UTF8.GetString(e.Message));
    }

    private void OnDestroy()
    {
        SessionDuration.Observe(Time.realtimeSinceStartup - sessionTimeStart);
        //JSONFormating.SessionData data = new JSONFormating.SessionData(Time.realtimeSinceStartup);
        //MQTTManager.PublishData(
        //    "game/Session/duration",
        //    JsonUtility.ToJson(data),
        //    JSONFormating.CreatePrometheusFormat<JSONFormating.SessionData>(data)
        //);

        // Clean up the Mqtt when the application quits
        if (client != null && client.IsConnected)
        {
            client.Disconnect();
        }

        // Clean up the listener when the application quits
        if (listener != null && listener.IsListening)
        {
            listener.Stop();
        }
    }
}
