using UnityEngine;
using UnityEngine.XR;

public class VRInputHandler : MonoBehaviour
{
    private InputDevice rightController;
    private bool controllerFound = false;

    public Vector2 TrackpadInput { get; private set; }
    public bool IsTrackpadTouched { get; private set; }
    
    private float deadzone = 0.15f;
    private float smoothing = 0.15f;
    private Vector2 smoothedInput;

    void Start()
    {
        InitializeController();
    }

    void Update()
    {
        if (!controllerFound)
        {
            InitializeController();
        }
        ReadTrackpadInput();
    }

    private void InitializeController()
    {
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        
        if (rightController.isValid)
        {
            controllerFound = true;
            Debug.Log($"VIVE Controller found: {rightController.name}");
        }
    }

    private void ReadTrackpadInput()
    {
        if (!controllerFound || !rightController.isValid)
            return;

        Vector2 rawInput = Vector2.zero;
        if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxis, out rawInput))
        {
            if (rawInput.magnitude < deadzone)
            {
                rawInput = Vector2.zero;
            }

            smoothedInput = Vector2.Lerp(smoothedInput, rawInput, smoothing);
            TrackpadInput = smoothedInput;
        }

        bool touched;
        if (rightController.TryGetFeatureValue(CommonUsages.primary2DAxisTouch, out touched))
        {
            IsTrackpadTouched = touched;
        }
    }

    public Vector3 GetMovementDirection(float sensitivity = 1f)
    {
        if (!IsTrackpadTouched)
            return Vector3.zero;

        return new Vector3(
            TrackpadInput.x * sensitivity,
            TrackpadInput.y * sensitivity,
            0f
        );
    }

    public bool IsControllerAvailable()
    {
        return controllerFound && rightController.isValid;
    }
}