using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;

public class VRInputHandler : MonoBehaviour
{
    // VR controller state specific properties
    private InputDevice activeController;
    private bool controllerFound = false;

    // Trackpad state specific properties
    public Vector2 TrackpadInput { get; private set; }
    public bool IsTrackpadPressed { get; private set; }
    public bool IsTrackpadCenterPressed { get; private set; }
    [SerializedField] private float centerClickRadius = 0.3f; // How close to the center counts as center

    // Button state properties
    public bool TriggerPressed { get; private set; }
    public bool GripPressed { get; private set; }
    public bool MenuPressed { get; private set; }
    
    // Fine-tuning specific properties for controller use
    private float deadzone = 0.15f; // Threshold to ignore tiny axis movements possible from the trackpad
    private float smoothing = 0.7f; // Dampening factor for steadier motion
    private Vector2 smoothedInput; // Vector for smoothing of input

    // State tracking for button detection
    private bool triggerWasPressed = false;
    private bool gripWasPressed = false;
    private bool menuWasPressed = false;

    // Initialization of all methods
    void Start()
    {
        StartCoroutine(DelayedInitialize());
    }

    private IEnumerator DelayedInitialize()
    {
        yield return new WaitForSeconds(1f);
        InitializeController();
    }

    // Update is called once per frame
    void Update()
    {
        if (!controllerFound)
        {
            InitializeController();
        }
        ReadTrackpadInput();
        ReadTriggerInput();
        ReadGripInput();
    }

    // METHOD: Identifies a usable XR controller each frame
    private void InitializeController()
    {
        UnityEngine.Debug.Log("VRInputHandler: Looking for controllers...");

        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);
        UnityEngine.Debug.Log($"VRInputHandler: Total devices found: {devices.Count}");

        // Right controller
        InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        
        if (rightController.isValid)
        {
            activeController = rightController;
            controllerFound = true;
            UnityEngine.Debug.Log("VR Input: Controller detected (RIGHT)");
            return;
        }

        // Left controller
        InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        
        if (leftController.isValid)
        {
            activeController = leftController;
            controllerFound = true;
            UnityEngine.Debug.Log("VR Input: Controller detected (LEFT)");
            return;
        }

        controllerFound = false;
    }

    // METHOD: Reads input of the trackpad (PRESS, not touch)
    private void ReadTrackpadInput()
    {
        if (!controllerFound || !activeController.isValid) // Safety: Returns early if no controller 
            return;

        // Check if trackpad is pressed (clicked)
        bool pressed;
        if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out pressed))
        {
            IsTrackpadPressed = pressed;
        }

        // Detect center click
        if (IsTrackpadPressed && !wasPressed)
        {
            if (rawInput.magnitude < centerClickRadius)
            {
                IsTrackpadCenterPressed = true;
            }
            else
            {
                IsTrackpadCenterPressed = false;
            }
        }

        // Only read trackpad position if it's pressed
        if (IsTrackpadPressed)
        {
            Vector2 rawInput = Vector2.zero; // Pulls current axis reading

            if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rawInput))
            {
                if (rawInput.magnitude < deadzone) // Check if movement goes beyond threshold
                {
                    rawInput = Vector2.zero;
                }

                smoothedInput = Vector2.Lerp(smoothedInput, rawInput, smoothing); // Linear interpolation of the reading for higher smoothing
                TrackpadInput = smoothedInput;
            }
        }
        else
        {
            // Reset when not pressed
            TrackpadInput = Vector2.zero;
            smoothedInput = Vector2.zero;
        }
    }

    // METHOD: Reads the input of the trigger button
    private void ReadTriggerInput()
    {
        if (!controllerFound || !activeController.isValid)
        {
            TriggerPressed = false;
            return;
        }

        bool triggerIsPressed;
        if (activeController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerIsPressed))
        {
            // Detect "just pressed" (like Input.GetKeyDown)
            TriggerPressed = triggerIsPressed && !triggerWasPressed;
            triggerWasPressed = triggerIsPressed;
        }
    }

    // METHOD: Reads the input of the grip button
    private void ReadGripInput()
    {
        if (!controllerFound || !activeController.isValid)
        {
            GripPressed = false;
            return;
        }

        bool gripIsPressed;
        if (activeController.TryGetFeatureValue(CommonUsages.gripButton, out gripIsPressed))
        {
            // Detect "just pressed" (like Input.GetKeyDown)
            GripPressed = gripIsPressed && !gripWasPressed;
            gripWasPressed = gripIsPressed;
        }
    }

    // METHOD: Extract movement direction
    public Vector3 GetMovementDirection(float sensitivity = 1f)
    {
        if (!IsTrackpadPressed) // Safety: Returns null if the trackpad is not pressed
            return Vector3.zero;

        return new Vector3(
            TrackpadInput.x * sensitivity,
            TrackpadInput.y * sensitivity,
            0f
        );
    }

    // HELPER METHOD: Assesses availability of controller
    public bool IsControllerAvailable() 
    {
        return controllerFound && activeController.isValid;
    }
}