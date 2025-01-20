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
using UnityEditor.ShaderGraph.Serialization;
using System.Collections.Generic;

public class MQTTManager : M2MqttUnityClient
{
    public class Yolo
    {
        public bool detected_danger;
        public int distance_danger;
        public List<string> road_type;
    }

    #region Prometheus Variables
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
    #endregion

    private float sessionTimeStart = 0f;
    private string topic = "/YOLO/unity";

    private new void Start()
    {
        sessionTimeStart = Time.realtimeSinceStartup;

        base.Start();

        try
        {
            var server = new MetricServer(hostname: "10.8.1.3", port: 5555);
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
    }

    protected override void SubscribeTopics()
    {
        client.Subscribe(new string[] { topic }, new byte[] { MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
        Debug.Log("Subscribed to topic: " + topic);
    }

    protected override void UnsubscribeTopics()
    {
        client.Unsubscribe(new string[] { topic });
        Debug.Log("Unsubscribed from topic: " + topic);
    }

    protected override void DecodeMessage(string topic, byte[] message)
    {
        // Convert byte array to string
        string payload = Encoding.UTF8.GetString(message);
        Debug.Log($"Message received on topic {topic}: {payload}");

        // Deserialize JSON using Newtonsoft.Json
        Yolo yoloResult = JsonUtility.FromJson<Yolo>(payload);

        // Update your results object with the deserialized values
        YoloResults.distance = yoloResult.distance_danger;
        YoloResults.isDanger = yoloResult.detected_danger;
        YoloResults.roadType = yoloResult.road_type;

        // Log the results
        Debug.Log(YoloResults.isDanger.ToString() + " " + YoloResults.roadType + " " + YoloResults.distance);
    }

    private void OnDestroy()
    {
        SessionDuration.Observe(Time.realtimeSinceStartup - sessionTimeStart);

        // Clean up the Mqtt when the application quits
        if (client != null && client.IsConnected)
        {
            client.Disconnect();
        }
    }
}
