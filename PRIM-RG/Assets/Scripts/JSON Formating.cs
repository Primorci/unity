using System;
using UnityEngine;
using static MQTTManager;
using UnityEngine.UIElements;
using System.Data.Common;
using UnityEngine.InputSystem;
using System.Reflection;
using System.Text;
using UnityEditor.ShaderGraph.Internal;

public class JSONFormating
{
    public static string CreatePrometheusFormat<T>(T data)
    {
        StringBuilder prometheusData = new StringBuilder();

        // Get the type of the class (the "data" parameter)
        Type dataType = data.GetType();

        // Get all the public fields of the class
        FieldInfo[] fields = dataType.GetFields(BindingFlags.Public | BindingFlags.Instance);

        // Iterate over the fields and convert to Prometheus metrics format
        foreach (var field in fields)
        {
            // Get the value of the field
            object value = field.GetValue(data);

            // Generate the metric name
            string metricName = $"{dataType.Name.ToLower()}_{field.Name}";

            // Append HELP and TYPE lines
            prometheusData.AppendLine($"# HELP {metricName} The value of {field.Name} in {dataType.Name}");

            // Decide the metric type based on the field type
            string metricType;
            if (value is int || value is long) // Integer type - use Counter or Gauge
            {
                metricType = "counter";  // Or "gauge" based on your data
            }
            else if (value is float || value is double) // Float type - use Gauge
            {
                metricType = "gauge";
            }
            else if (value is string) // String - treat as a Gauge with a label
            {
                metricType = "gauge";
                // For string values, treat them as labels, e.g., state="accelerating"
                prometheusData.AppendLine($"# TYPE {metricName} {metricType}");
                prometheusData.AppendLine($"{metricName}{{state=\"{value}\"}} 1");
                continue; // Skip to the next field, since we've handled it as a label
            }
            else if (value is Vector3 vector)
            {
                // Create individual metrics for x, y, and z components
                prometheusData.AppendLine($"# HELP {metricName}_x The x position in {dataType.Name}");
                prometheusData.AppendLine($"# TYPE {metricName}_x gauge");
                prometheusData.AppendLine($"{metricName}_x {vector.x}");

                prometheusData.AppendLine($"# HELP {metricName}_y The y position in {dataType.Name}");
                prometheusData.AppendLine($"# TYPE {metricName}_y gauge");
                prometheusData.AppendLine($"{metricName}_y {vector.y}");

                prometheusData.AppendLine($"# HELP {metricName}_z The z position in {dataType.Name}");
                prometheusData.AppendLine($"# TYPE {metricName}_z gauge");
                prometheusData.AppendLine($"{metricName}_z {vector.z}");

                continue;  // Skip to next field, since we've processed the Vector3 components
            }
            else
            {
                // Default case for unsupported types (handle accordingly)
                continue;
            }

            // Standard case for numeric fields
            prometheusData.AppendLine($"# TYPE {metricName} {metricType}");
            prometheusData.AppendLine($"{metricName} {value}");
        }

        return prometheusData.ToString();
    }

    [Serializable]
    public class PlayerData
    {
        public float speed;
        public float distanceTraveled;

        public string carEvent;
        public string keyPressed;
        public PlayerData(float speed, float distanceTraveled, string eventType, string keyPressed)
        {
            this.speed = speed;
            this.distanceTraveled = distanceTraveled;
            this.carEvent = eventType;
            this.keyPressed = keyPressed;
        }
    }

    [Serializable]
    public class FPSData
    {
        public int fps;

        public FPSData(int fps)
        {
            this.fps = fps;
        }
    }

    [Serializable]
    public class LoadSpeedData
    {
        public string eventType;
        public float load_speed;

        public LoadSpeedData(string eventType, float load_speed)
        {
            this.eventType = eventType;
            this.load_speed = load_speed;
        }
    }

    [Serializable]
    public class SessionData
    {
        public float time;

        public SessionData(float time)
        {
            this.time = time;
        }
    }

    [Serializable]
    public class RoadData
    {
        public string eventType;

        public string roadType;
        public int exitPointCount;
        public Vector3 position;

        public RoadData(string eventType, string roadType, int exitPointCount, Vector3 position)
        {
            this.eventType = eventType;
            this.roadType = roadType;
            this.exitPointCount = exitPointCount;
            this.position = position;
        }
    }

    [Serializable]
    public class ObstacleData
    {
        public string eventType;

        public string obstacleType;
        public Vector3 position;

        public ObstacleData(string eventType, string obstacleType, Vector3 position)
        {
            this.eventType = eventType;
            this.obstacleType = obstacleType;
            this.position = position;
        }
    }

    [Serializable]
    public class ErrorData
    {
        public int errorCount;
        public int warningCount;

        public ErrorData(int errorCount, int warningCount)
        {
            this.errorCount = errorCount;
            this.warningCount = warningCount;
        }
    }
}
