using UnityEngine;
using System.Collections.Generic;

public class PlayerCarController1 : MonoBehaviour
{
    // --- Configurable Physics Parameters (Adjust in Inspector) ---

    [Header("Engine & Acceleration")]
    [Tooltip("Torque applied to the driven wheels to move the car. Increase for faster acceleration.")]
    public float motorTorque = 2500f;
    [Tooltip("Torque applied to stop the wheels when braking.")]
    public float brakeTorque = 3000f; // This is for full, general braking
    [Tooltip("Torque applied specifically to FRONT wheels for drifting (Spacebar + Turn).")]
    public float frontDriftBrakeTorque = 1500f; // Torque for front-brake drift
    [Tooltip("Maximum speed the car can reach.")]
    public float maxSpeed = 80f;
    [Tooltip("How quickly the car reaches max acceleration.")]
    public float accelerationSmoothness = 10f;
    [Tooltip("Maximum reverse speed the car can reach.")]
    public float maxReverseSpeed = 20f;
    [Tooltip("Multiplier for motor torque when reversing.")]
    public float reverseTorqueMultiplier = 0.5f;
    [Tooltip("Multiplier for motor torque when actively drifting (optional, for more power while sliding).")]
    public float driftPowerMultiplier = 1.0f;

    [Header("Steering & Turning")]
    [Tooltip("Maximum angle the steering wheels can turn. Increase for sharper turns.")]
    public float maxSteeringAngle = 50f;
    [Tooltip("How smoothly the steering responds to input.")]
    public float steeringSmoothness = 15f;
    [Tooltip("Reduces steering angle at higher speeds for better control.")]
    public AnimationCurve steeringCurve; // Ensure this curve is configured correctly in the Inspector

    [Header("Wheel Colliders")]
    [Tooltip("Assign your front left WheelCollider here.")]
    public WheelCollider frontLeftWheel;
    [Tooltip("Assign your front right WheelCollider here.")]
    public WheelCollider frontRightWheel;
    [Tooltip("Assign your rear left WheelCollider here.")]
    public WheelCollider rearLeftWheel;
    [Tooltip("Assign your rear right WheelCollider here.")]
    public WheelCollider rearRightWheel;

    [Header("Wheel Meshes (for visual rotation)")]
    [Tooltip("Assign the visual mesh for the front left wheel.")]
    public Transform frontLeftWheelMesh;
    [Tooltip("Assign the visual mesh for the front right wheel.")]
    public Transform frontRightWheelMesh;
    [Tooltip("Assign the visual mesh for the rear left wheel.")]
    public Transform rearLeftWheelMesh;
    [Tooltip("Assign the visual mesh for the rear right wheel.")]
    public Transform rearRightWheelMesh;

    [Header("Car Body Physics")]
    [Tooltip("Point to set the Rigidbody's Center of Mass. Drag an empty child GameObject here.")]
    public Transform centerOfMassPoint;
    [Tooltip("Multiplier for linear damping when braking.")]
    public float brakingLinearDampingMultiplier = 2.0f; // Renamed for clarity and modern Unity Rigidbody properties
    private float originalLinearDamping; // Stores the Rigidbody's initial linear damping value

    // --- Private Variables ---
    private Rigidbody carRigidbody;
    private float currentSteeringAngle;
    private float currentMotorInputAxis;
    private float appliedMotorTorque;
    private float currentSpeed;
    private bool isDrifting = false;

    void Awake()
    {
        carRigidbody = GetComponent<Rigidbody>(); 
        
        if (carRigidbody != null)
        {
            // Use linearDamping for controlling linear resistance more explicitly
            originalLinearDamping = carRigidbody.linearDamping; 
        }

        if (carRigidbody != null && centerOfMassPoint != null)
        {
            carRigidbody.centerOfMass = transform.InverseTransformPoint(centerOfMassPoint.position);
            Debug.Log("Center of Mass set to: " + carRigidbody.centerOfMass);
        }
        else
        {
            Debug.LogWarning("Rigidbody or CenterOfMassPoint not found. Center of Mass might not be optimized. Please assign the Rigidbody to the same GameObject as this script, and assign a CenterOfMassPoint.");
        }

        // IMPORTANT: Ensure this default curve allows for steering at max speed
        // Removed AnimationUtility calls as they are for editor scripting only.
        if (steeringCurve == null || steeringCurve.length == 0)
        {
            // Default: Full steering at 0 speed, 30% steering at maxSpeed
            // You can adjust these keyframes in the Inspector after the first run.
            steeringCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(maxSpeed, 0.3f));
            // You will need to manually adjust tangents in the Unity Inspector if desired
        }

        SetWheelFriction(frontLeftWheel, 1.2f, 1.2f);
        SetWheelFriction(frontRightWheel, 1.2f, 1.2f);
        SetWheelFriction(rearLeftWheel, 1.0f, 1.0f);
        SetWheelFriction(rearRightWheel, 1.0f, 1.0f);
    }

    void SetWheelFriction(WheelCollider wheel, float sidewaysStiffness, float forwardStiffness)
    {
        if (wheel == null) return; // Prevent NullReferenceException if wheel is not assigned

        WheelFrictionCurve sFriction = wheel.sidewaysFriction;
        sFriction.stiffness = sidewaysStiffness;
        wheel.sidewaysFriction = sFriction;

        WheelFrictionCurve fFriction = wheel.forwardFriction;
        fFriction.stiffness = forwardStiffness;
        wheel.forwardFriction = fFriction;
    }

    void FixedUpdate() 
    {
        // Calculate current speed from local Z velocity (forward/backward)
        Vector3 localVelocity = transform.InverseTransformDirection(carRigidbody.linearVelocity);
        currentSpeed = localVelocity.z * 3.6f; // Convert m/s to km/h

        // Optional: Debugging logs to see input and speed values
        // Debug.Log($"Speed: {currentSpeed:F2} km/h, Horizontal Input: {Input.GetAxis("Horizontal"):F2}, Steering Angle: {currentSteeringAngle:F2}");

        HandleInput();
        ApplyMotor();
        ApplySteering();
        ApplyBrake();
        UpdateWheelMeshes(); // This is responsible for visual wheel movement and steering
    }

    void HandleInput()
    {
        currentMotorInputAxis = Input.GetAxis("Vertical"); // W/S or Up/Down arrows
        float horizontalInput = Input.GetAxis("Horizontal"); // A/D or Left/Right arrows

        // Drifting condition: Spacebar + significant turning input + moving forward at a certain speed
        isDrifting = Input.GetKey(KeyCode.Space) && Mathf.Abs(horizontalInput) > 0.1f && currentSpeed > 10f;

        // Calculate steering angle based on horizontal input and speed curve
        float speedFactor = steeringCurve.Evaluate(Mathf.Abs(currentSpeed)); 
        float targetSteeringAngle = horizontalInput * maxSteeringAngle * speedFactor;
        currentSteeringAngle = Mathf.Lerp(currentSteeringAngle, targetSteeringAngle, Time.fixedDeltaTime * steeringSmoothness);
    }

    void ApplyMotor()
    {
        appliedMotorTorque = 0f;

        // Determine if car is moving generally forward or backward relative to its orientation
        float dotProduct = Vector3.Dot(transform.forward, carRigidbody.linearVelocity.normalized);

        if (isDrifting)
        {
            if (currentMotorInputAxis >= 0) // Accelerating while drifting
            {
                 if (currentSpeed < maxSpeed)
                 {
                    appliedMotorTorque = Mathf.Lerp(appliedMotorTorque, currentMotorInputAxis * motorTorque * driftPowerMultiplier, Time.fixedDeltaTime * accelerationSmoothness);
                 }
            }
            else if (currentMotorInputAxis < 0) // Trying to reverse while drifting (no motor applied)
            {
                appliedMotorTorque = 0;
            }

            // If car is moving backwards significantly while trying to drift, apply full brake (stop drift)
            if (currentSpeed < -0.1f)
            {
                ApplyBrakeTorqueToWheels(brakeTorque, brakeTorque);
                return; 
            }
        }
        else // Not drifting (normal driving)
        {
            if (currentMotorInputAxis > 0.1f) // Accelerating forward
            {
                if (currentSpeed < maxSpeed)
                {
                    // If trying to accelerate forward but moving backward, apply brake
                    if (currentSpeed < -0.1f && dotProduct < 0)
                    {
                        ApplyBrakeTorqueToWheels(brakeTorque, brakeTorque);
                        return;
                    }
                    else // Otherwise, apply motor torque for acceleration
                    {
                        appliedMotorTorque = Mathf.Lerp(appliedMotorTorque, currentMotorInputAxis * motorTorque, Time.fixedDeltaTime * accelerationSmoothness);
                    }
                }
            }
            else if (currentMotorInputAxis < -0.1f) // Accelerating backward (reverse)
            {
                if (currentSpeed > -maxReverseSpeed)
                {
                    // If trying to accelerate backward but moving forward, apply brake
                    if (currentSpeed > 0.1f && dotProduct > 0)
                    {
                        ApplyBrakeTorqueToWheels(brakeTorque, brakeTorque);
                        return;
                    }
                    else // Otherwise, apply motor torque for reverse
                    {
                        appliedMotorTorque = Mathf.Lerp(appliedMotorTorque, currentMotorInputAxis * motorTorque * reverseTorqueMultiplier, Time.fixedDeltaTime * accelerationSmoothness);
                    }
                }
            }
        }
        
        // Apply calculated motor torque to rear wheels
        if (rearLeftWheel != null) rearLeftWheel.motorTorque = appliedMotorTorque;
        if (rearRightWheel != null) rearRightWheel.motorTorque = appliedMotorTorque;
    }

    void ApplySteering()
    {
        // Apply calculated steering angle to front wheels
        if (frontLeftWheel != null) frontLeftWheel.steerAngle = currentSteeringAngle;
        if (frontRightWheel != null) frontRightWheel.steerAngle = currentSteeringAngle;
    }

    void ApplyBrake()
    {
        if (isDrifting)
        {
            // When drifting, apply brake torque only to front wheels
            if (frontLeftWheel != null) frontLeftWheel.brakeTorque = frontDriftBrakeTorque;
            if (frontRightWheel != null) frontRightWheel.brakeTorque = frontDriftBrakeTorque;

            // Rear wheels have no brake torque during drift (to allow sliding)
            if (rearLeftWheel != null) rearLeftWheel.brakeTorque = 0;
            if (rearRightWheel != null) rearRightWheel.brakeTorque = 0;

            // Increase linear damping for extra drag during braking/drifting
            if (carRigidbody != null) carRigidbody.linearDamping = originalLinearDamping * brakingLinearDampingMultiplier;
        }
        else // Not drifting
        {
            // Check for explicit brake input (Spacebar)
            bool explicitBraking = Input.GetKey(KeyCode.Space);
            
            // Auto-braking when trying to move opposite to current direction
            bool autoBraking = false;
            if (currentMotorInputAxis > 0.1f && currentSpeed < -0.1f) // Accelerating forward, but moving backward
                autoBraking = true;
            else if (currentMotorInputAxis < -0.1f && currentSpeed > 0.1f) // Accelerating backward, but moving forward
                autoBraking = true;
            
            // Engine braking (gentle slowdown when no input is given and car is moving)
            bool engineBraking = false;
            if (!explicitBraking && Mathf.Abs(currentMotorInputAxis) < 0.1f && Mathf.Abs(currentSpeed) > 0.5f)
            {
                engineBraking = true;
            }

            if (explicitBraking || autoBraking)
            {
                // Apply full brake torque to all wheels
                ApplyBrakeTorqueToWheels(brakeTorque, brakeTorque);
                if (carRigidbody != null) carRigidbody.linearDamping = originalLinearDamping * brakingLinearDampingMultiplier;
            }
            else if (engineBraking)
            {
                // Apply a small brake torque for engine braking
                ApplyBrakeTorqueToWheels(motorTorque * 0.1f, motorTorque * 0.1f);
                if (carRigidbody != null) carRigidbody.linearDamping = originalLinearDamping;
            }
            else
            {
                // No brake torque applied
                ApplyBrakeTorqueToWheels(0, 0);
                if (carRigidbody != null) carRigidbody.linearDamping = originalLinearDamping; // Reset damping
            }
        }
    }

    void ApplyBrakeTorqueToWheels(float frontTorque, float rearTorque)
    {
        // Apply brake torque to specified wheels
        if (frontLeftWheel != null) frontLeftWheel.brakeTorque = frontTorque;
        if (frontRightWheel != null) frontRightWheel.brakeTorque = frontTorque;
        if (rearLeftWheel != null) rearLeftWheel.brakeTorque = rearTorque;
        if (rearRightWheel != null) rearRightWheel.brakeTorque = rearTorque;
    }

    void UpdateWheelMeshes()
    {
        // Call the update function for each wheel mesh
        UpdateWheelTransform(frontLeftWheel, frontLeftWheelMesh);
        UpdateWheelTransform(frontRightWheel, frontRightWheelMesh);
        UpdateWheelTransform(rearLeftWheel, rearLeftWheelMesh);
        UpdateWheelTransform(rearRightWheel, rearRightWheelMesh);
    }

    void UpdateWheelTransform(WheelCollider wheelCollider, Transform wheelMesh)
    {
        if (wheelCollider == null || wheelMesh == null) return;

        Vector3 pos;
        Quaternion rot;
        // Get the world position and rotation calculated by the WheelCollider
        wheelCollider.GetWorldPose(out pos, out rot); 

        // Apply the collider's position and rotation to the visual mesh
        wheelMesh.position = pos;
        wheelMesh.rotation = rot;
    }

    void OnDrawGizmos()
    {
        // Draw a sphere at the center of mass for visual debugging
        if (carRigidbody != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(carRigidbody.worldCenterOfMass, 0.2f);
        }
    }
}