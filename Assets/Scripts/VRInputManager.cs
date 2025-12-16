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
    public bool IsTrackpadCenterPressed { get; private set; }
    float centerClickRadius = 0.3f; // How close to the center counts as center

    // Button state properties
    public bool TriggerPressed { get; private set; }

    // Fine-tuning specific properties for controller use
    private float deadzone = 0.15f; // Threshold to ignore tiny axis movements possible from the trackpad
    private float smoothing = 0.7f; // Dampening factor for steadier motion
    private Vector2 smoothedInput; // Vector for smoothing of input

    // State tracking for button detection
    private bool triggerWasPressed = false;

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

    // METHOD: Reads input of the trackpad (PRESS and center detection)
    private void ReadTrackpadInput()
    {
        if (!controllerFound || !activeController.isValid)
        {
            IsTrackpadPressed = false;
            IsTrackpadCenterPressed = false;
            TrackpadInput = Vector2.zero;
            smoothedInput = Vector2.zero;
            return;
        }

        bool wasPressed = IsTrackpadPressed; // Store previous state

        // Check if trackpad is pressed
        bool pressed;
        if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out pressed))
        {
            IsTrackpadPressed = pressed;
        }

        // Detect center click: pressed while position is near center
        if (IsTrackpadPressed && !wasPressed) // Just pressed this frame
        {
            Vector2 clickPosition = Vector2.zero;
            if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxis, out clickPosition))
            {
                // Check if click position is near center
                if (clickPosition.magnitude < centerClickRadius)
                {
                    IsTrackpadCenterPressed = true;
                }
                else
                {
                    IsTrackpadCenterPressed = false;
                }
            }
        }
        else
        {
            IsTrackpadCenterPressed = false;
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