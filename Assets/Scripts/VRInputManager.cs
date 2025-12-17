using UnityEngine;
using UnityEngine.XR;
using System.Collections;
using System.Collections.Generic;

public class VRInputHandler : MonoBehaviour
{
    // VR controller state specific properties
    private InputDevice activeController;
    private bool controllerFound = false;

    // Trackpad state specific properties
    public Vector2 TrackpadInput { get; private set; }
    public bool IsTrackpadPressed { get; private set; }
    public bool TrackpadDoubleClicked { get; private set; } // Double-click event for confirmation

    // Button state properties
    public bool TriggerPressed { get; private set; }

    // Fine-tuning specific properties for controller use
    private float deadzone = 0.15f; // Threshold to ignore tiny axis movements possible from the trackpad
    private float smoothing = 0.7f; // Dampening factor for steadier motion
    private Vector2 smoothedInput; // Vector for smoothing of input

    // State tracking for button detection
    private bool triggerWasPressed = false;
    private bool trackpadWasPressed = false;

    // Double-click detection
    private float lastClickTime = 0f;
    private float doubleClickThreshold = 0.3f; // Maximum time between clicks (in seconds)
    private float trackpadPressTime = 0f; // When trackpad was pressed
    private float quickClickThreshold = 0.2f; // Maximum hold time to count as a "click" (not movement)

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
    }

    // METHOD: Identifies a usable XR controller each frame
    private void InitializeController()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

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

    // METHOD: Reads input of the trackpad (PRESS for movement and DOUBLE-CLICK for confirmation)
    private void ReadTrackpadInput()
    {
        if (!controllerFound || !activeController.isValid)
        {
            IsTrackpadPressed = false;
            TrackpadDoubleClicked = false;
            TrackpadInput = Vector2.zero;
            smoothedInput = Vector2.zero;
            return;
        }

        // Reset double-click flag at the start of each frame
        TrackpadDoubleClicked = false;

        // Check if trackpad is pressed
        bool pressed;
        if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out pressed))
        {
            // Track when trackpad is first pressed
            if (pressed && !trackpadWasPressed)
            {
                trackpadPressTime = Time.time;
            }
            
            // Track when trackpad is released
            if (!pressed && trackpadWasPressed)
            {
                float holdDuration = Time.time - trackpadPressTime;
                
                // Only count as a "click" if it was a quick press-and-release
                if (holdDuration < quickClickThreshold)
                {
                    float timeSinceLastClick = Time.time - lastClickTime;
                    
                    // Check if this is a double-click
                    if (timeSinceLastClick <= doubleClickThreshold)
                    {
                        TrackpadDoubleClicked = true;
                        lastClickTime = 0f; // Reset to prevent triple-click detection
                        UnityEngine.Debug.Log("VR Input: DOUBLE-CLICK DETECTED!");
                    }
                    else
                    {
                        // This is the first click, record the time
                        lastClickTime = Time.time;
                        UnityEngine.Debug.Log("VR Input: First click detected");
                    }
                }
                else
                {
                    // This was a hold for movement, not a click - reset double-click detection
                    lastClickTime = 0f;
                }
            }
            
            IsTrackpadPressed = pressed;
            trackpadWasPressed = pressed;
        }

        // Only read trackpad position for movement if it's pressed
        if (IsTrackpadPressed)
        {
            Vector2 rawInput = Vector2.zero;

            if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rawInput))
            {
                if (rawInput.magnitude < deadzone)
                {
                    rawInput = Vector2.zero;
                }

                smoothedInput = Vector2.Lerp(smoothedInput, rawInput, smoothing);
                TrackpadInput = smoothedInput;
            }
        }
        else
        {
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