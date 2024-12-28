//using UnityEngine;

//public class CarController
//{

//}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Prometheus;

public class CarController : MonoBehaviour
{
    private float horizontalInput, verticalInput;
    private float currentSteerAngle, currentbreakForce;
    private bool isBreaking;

    // Settings
    [SerializeField] private float motorForce, breakForce, maxSteerAngle;

    // Wheel Colliders
    [SerializeField] private WheelCollider frontLeftWheelCollider, frontRightWheelCollider;
    [SerializeField] private WheelCollider rearLeftWheelCollider, rearRightWheelCollider;

    // Wheels
    [SerializeField] private Transform frontLeftWheelTransform, frontRightWheelTransform;
    [SerializeField] private Transform rearLeftWheelTransform, rearRightWheelTransform;

    private Vector3 lastPosition; // Stores the last position of the car
    private float totalDistance = 0f; // Total distance traveled

    private static Rigidbody rb;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        lastPosition = transform.position;
    }

    private void FixedUpdate()
    {
        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();

        MQTTManager.CarSpeed.Set(getCarSpeed());
        MQTTManager.DistanceTraveled.Set(getDistanceTraveld());
    }

    private void GetInput()
    {
        // Steering Input
        horizontalInput = Input.GetAxis("Horizontal");

        // Acceleration Input
        verticalInput = Input.GetAxis("Vertical");

        // Breaking Input
        isBreaking = Input.GetKey(KeyCode.Space);

        string eventType = null;
        string keyPressed = null;

        if(horizontalInput > 0)
        {
            MQTTManager.CarState.Set(4);
     
        }else if(verticalInput > 0)
        {
            MQTTManager.CarState.Set(3);
        }else if (isBreaking)
        {
            MQTTManager.CarState.Set(2);
        }else
        {
            MQTTManager.CarState.Set(1);

        }

        foreach (KeyCode keyCode in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (Input.GetKeyDown(keyCode))
            {
                MQTTManager.KeyPressCounter.WithLabels(keyCode.ToString()).Inc();
            }
        }

        //JSONFormating.PlayerData data = new JSONFormating.PlayerData(getCarSpeed(), getDistanceTraveld(), eventType, keyPressed);
        //MQTTManager.PublishData(
        //    "game/player",
        //    JsonUtility.ToJson(data),
        //    JSONFormating.CreatePrometheusFormat<JSONFormating.PlayerData>(data)
        //);
    }

    private void HandleMotor()
    {
        frontLeftWheelCollider.motorTorque = verticalInput * motorForce;
        frontRightWheelCollider.motorTorque = verticalInput * motorForce;
        currentbreakForce = isBreaking ? breakForce : 0f;
        ApplyBreaking();
    }

    private void ApplyBreaking()
    {
        frontRightWheelCollider.brakeTorque = currentbreakForce;
        frontLeftWheelCollider.brakeTorque = currentbreakForce;
        rearLeftWheelCollider.brakeTorque = currentbreakForce;
        rearRightWheelCollider.brakeTorque = currentbreakForce;
    }

    private void HandleSteering()
    {
        currentSteerAngle = maxSteerAngle * horizontalInput;
        frontLeftWheelCollider.steerAngle = currentSteerAngle;
        frontRightWheelCollider.steerAngle = currentSteerAngle;
    }

    private void UpdateWheels()
    {
        UpdateSingleWheel(frontLeftWheelCollider, frontLeftWheelTransform);
        UpdateSingleWheel(frontRightWheelCollider, frontRightWheelTransform);
        UpdateSingleWheel(rearRightWheelCollider, rearRightWheelTransform);
        UpdateSingleWheel(rearLeftWheelCollider, rearLeftWheelTransform);
    }

    private void UpdateSingleWheel(WheelCollider wheelCollider, Transform wheelTransform)
    {
        Vector3 pos;
        Quaternion rot;
        wheelCollider.GetWorldPose(out pos, out rot);
        wheelTransform.rotation = rot;
        wheelTransform.position = pos;
    }

    public static float getCarSpeed()
    {
        return rb.linearVelocity.magnitude * 3.6f;
    }

    public float getDistanceTraveld()
    {
        // Calculate the distance moved since the last frame
        float distanceThisFrame = Vector3.Distance(transform.position, lastPosition);

        // Add this distance to the total
        totalDistance += distanceThisFrame;

        // Update the last position
        lastPosition = transform.position;

        return totalDistance;
    }
}