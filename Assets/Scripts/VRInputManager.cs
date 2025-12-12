using UnityEngine;
using UnityEngine.XR;

public class VRInputHandler : MonoBehaviour
{
    // VR controller state specific properties
    private InputDevice activeController;
    private bool controllerFound = false;

    // Trackpad state specific properties
    public Vector2 TrackpadInput { get; private set; }
    public bool IsTrackpadTouched { get; private set; }
    
    // Fine-tuning specific properties for controller use
    private float deadzone = 0.15f; // Threshold to ignore tiny axis movements possible from the trackpad
    private float smoothing = 0.7f; // Dampening factor for steadier motion
    private Vector2 smoothedInput; // Vector for smoothing of input

    // Initialization of all methods
    void Start()
    {
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
    }

    // METHOD: Identifies a usable XR controller each frame
    private void InitializeController()
    {
        // Right controller

        InputDevice rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        
        if (rightController.isValid)
        {
            activeController = rightController;
            controllerFound = true;
            Debug.Log("Controller detected");
            return;
        }

        // Left controller

        InputDevice leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        
        if (leftController.isValid)
        {
            activeController = leftController;
            controllerFound = true;
            Debug.Log("Controller detected");
            return;
        }

        controllerFound = false;
        Debug.Log("Controller detected");
    }

    // METHOD: Reads input of the trackpad
    private void ReadTrackpadInput()
    {
        if (!controllerFound || !activeController.isValid) // Safety: Returns early if no controller 
            return;

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

        bool touched;
        if (activeController.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out touched)) // Detection of whereas the trackpad is being touched or not
        {
            IsTrackpadTouched = touched;
        }
    }

    // METHOD: Extract movement direction
    public Vector3 GetMovementDirection(float sensitivity = 1f)
    {
        if (!IsTrackpadTouched) // Safety: Returns null if the trackpad is not touched
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