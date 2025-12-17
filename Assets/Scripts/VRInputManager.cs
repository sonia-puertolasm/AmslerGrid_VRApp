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

    // Button state properties
    public bool TriggerPressed { get; private set; } // Single trigger press
    public bool TriggerDoubleClicked { get; private set; } // Double trigger press

    // Fine-tuning specific properties for controller use
    private float deadzone = 0.15f; // Threshold to ignore tiny axis movements possible from the trackpad
    private float smoothing = 0.7f; // Dampening factor for steadier motion
    private Vector2 smoothedInput; // Vector for smoothing of input

    // State tracking for trigger detection
    private bool triggerWasPressed = false;
    private float lastTriggerPressTime = 0f;
    private float triggerDoubleClickThreshold = 0.3f; // Maximum time between trigger presses
    private float triggerPressTime = 0f;
    private float quickTriggerThreshold = 0.2f; // Maximum hold time to count as a "click"

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

    // METHOD: Reads input of the trackpad (PRESS + MOVE for displacement)
    private void ReadTrackpadInput()
    {
        if (!controllerFound || !activeController.isValid)
        {
            IsTrackpadPressed = false;
            TrackpadInput = Vector2.zero;
            smoothedInput = Vector2.zero;
            return;
        }

        // Check if trackpad is pressed
        bool pressed;
        if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out pressed))
        {
            IsTrackpadPressed = pressed;
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

    // METHOD: Reads the input of the trigger button (single and double press)
    private void ReadTriggerInput()
    {
        if (!controllerFound || !activeController.isValid)
        {
            TriggerPressed = false;
            TriggerDoubleClicked = false;
            return;
        }

        // Reset flags at start of frame
        TriggerPressed = false;
        TriggerDoubleClicked = false;

        bool triggerIsPressed;
        if (activeController.TryGetFeatureValue(CommonUsages.triggerButton, out triggerIsPressed))
        {
            // Track when trigger is first pressed
            if (triggerIsPressed && !triggerWasPressed)
            {
                triggerPressTime = Time.time;
            }
            
            // Track when trigger is released
            if (!triggerIsPressed && triggerWasPressed)
            {
                float holdDuration = Time.time - triggerPressTime;
                
                // Only count as a "press" if it was a quick pull-and-release
                if (holdDuration < quickTriggerThreshold)
                {
                    float timeSinceLastPress = Time.time - lastTriggerPressTime;
                    
                    // Check if this is a double-press
                    if (timeSinceLastPress <= triggerDoubleClickThreshold)
                    {
                        TriggerDoubleClicked = true;
                        lastTriggerPressTime = 0f; // Reset to prevent triple-press detection
                        UnityEngine.Debug.Log("VR Input: TRIGGER DOUBLE-PRESS DETECTED!");
                    }
                    else
                    {
                        // This is a single press
                        TriggerPressed = true;
                        lastTriggerPressTime = Time.time; // Record for potential double-press
                        UnityEngine.Debug.Log("VR Input: Trigger single press");
                    }
                }
            }
            
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