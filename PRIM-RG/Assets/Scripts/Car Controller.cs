//using UnityEngine;

//public class CarController
//{

//}
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Prometheus;
using static System.Runtime.CompilerServices.RuntimeHelpers;
using M2MqttUnity;
using System.Text;

public class CarController : M2MqttUnityClient
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

    private int carEvent;

    private class CarData
    {
        public float speed;
        public float distanceTraveled;

        public int carEvent;
        public CarData(float speed, float distanceTraveled, int carEvent)
        {
            this.speed = speed;
            this.distanceTraveled = distanceTraveled;
            this.carEvent = carEvent;
        }
    }

    protected override void Start()
    {
        base.Start();

        rb = GetComponent<Rigidbody>();
        lastPosition = transform.position;
    }

    protected override void Update()
    {
        base.Update();

        GetInput();
        HandleMotor();
        HandleSteering();
        UpdateWheels();

        try
        {
            CarData car = new CarData(getCarSpeed(), getDistanceTraveld(), carEvent);
            client.Publish("game/player", Encoding.ASCII.GetBytes(JsonUtility.ToJson(car)));

            MQTTManager.CarSpeed.Set(car.speed);
            MQTTManager.DistanceTraveled.Set(car.distanceTraveled);
            MQTTManager.CarState.Set(car.carEvent);
        }
        catch (Exception e)
        {
            Debug.LogError("MQTT Publishing Error: " + e.Message);
        }
    }

    private void GetInput()
    {
        // Steering Input
        horizontalInput = Input.GetAxis("Horizontal");

        // Acceleration Input
        verticalInput = Input.GetAxis("Vertical");

        // Breaking Input
        isBreaking = Input.GetKey(KeyCode.Space);

        if(horizontalInput > 0)  
           carEvent = 3;
        else if(verticalInput > 0)
            carEvent = 2;
        else if (isBreaking)  
            carEvent = 1; 
        else 
            carEvent = 0; 
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