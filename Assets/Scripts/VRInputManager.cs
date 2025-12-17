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
    public bool TriggerPressed { get; private set; }

    // Fine-tuning specific properties for controller use
    private float deadzone = 0.15f;
    private float smoothing = 0.7f;
    private Vector2 smoothedInput;

    // State tracking for trigger detection
    private bool triggerWasPressed = false;

    // Initialization
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

    // METHOD: Identifies a usable XR controller
    private void InitializeController()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        // Try right controller first
        InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightController.isValid)
        {
            activeController = rightController;
            controllerFound = true;
            UnityEngine.Debug.Log("VR Input: RIGHT controller detected");
            return;
        }

        // Try left controller
        InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        if (leftController.isValid)
        {
            activeController = leftController;
            controllerFound = true;
            UnityEngine.Debug.Log("VR Input: LEFT controller detected");
            return;
        }

        controllerFound = false;
    }

    // METHOD: Reads trackpad input (PRESS + MOVE for displacement)
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

    // METHOD: Reads trigger input (simple press detection)
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

    // METHOD: Extract movement direction from trackpad
    public Vector3 GetMovementDirection(float sensitivity = 1f)
    {
        if (!IsTrackpadPressed)
            return Vector3.zero;

        return new Vector3(
            TrackpadInput.x * sensitivity,
            TrackpadInput.y * sensitivity,
            0f
        );
    }

    // HELPER METHOD: Check if controller is available
    public bool IsControllerAvailable() 
    {
        return controllerFound && activeController.isValid;
    }
}