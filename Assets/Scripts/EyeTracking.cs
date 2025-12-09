using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class EyeTracking : MonoBehaviour
{
    private MainGrid mainGrid;
    private ProbeDots probeDots;

    private GameObject gridLinesParent;
    private GameObject gridPointsParent;
    private GameObject centerFixationPoint;

    public bool hideAllExceptCenter = false;
    private bool previousHideState = false;

    public bool useEyeTracking = false;
    public float gazeThreshold = 2.0f;
    public float lookAwayDelay = 0.5f;

    private InputDevice eyesDevice;
    private bool eyeTrackingAvailable = false;
    private float lookAwayTimer = 0f;
    private bool isLookingAtCenter = true;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();

        if (useEyeTracking)
        {
            InitializeEyeTracking();
        }

        StartCoroutine(InitializeReferences());
    }

    private IEnumerator InitializeReferences()
    {
        yield return new WaitForEndOfFrame();

        if (mainGrid != null)
        {
            Transform gridLinesTransform = mainGrid.transform.Find("GridLines");
            if (gridLinesTransform != null)
            {
                gridLinesParent = gridLinesTransform.gameObject;
            }

            Transform gridPointsTransform = mainGrid.transform.Find("GridPoints");
            if (gridPointsTransform != null)
            {
                gridPointsParent = gridPointsTransform.gameObject;

                Transform centerTransform = gridPointsTransform.Find("CenterFixationPoint");
                if (centerTransform != null)
                {
                    centerFixationPoint = centerTransform.gameObject;
                }
            }
        }

        UpdateVisibility();
    }

    void Update()
    {
        if (useEyeTracking && eyeTrackingAvailable)
        {
            UpdateEyeTracking();
        }

        if (hideAllExceptCenter != previousHideState)
        {
            UpdateVisibility();
            previousHideState = hideAllExceptCenter;
        }
    }

    private void InitializeEyeTracking()
    {
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, devices);

        if (devices.Count > 0)
        {
            eyesDevice = devices[0];
            eyeTrackingAvailable = true;
        }
        else
        {
            eyeTrackingAvailable = false;
        }
    }

    private void UpdateEyeTracking()
    {
        if (centerFixationPoint == null)
            return;

        Vector3 gazeDirection = Vector3.zero;
        Vector3 gazeOrigin = Vector3.zero;

        bool hasGazeDirection = eyesDevice.TryGetFeatureValue(CommonUsages.eyesData, out Eyes eyesData);

        if (hasGazeDirection)
        {
            Vector3 leftEyeDirection = eyesData.leftEyeRotation * Vector3.forward;
            Vector3 rightEyeDirection = eyesData.rightEyeRotation * Vector3.forward;
            gazeDirection = ((leftEyeDirection + rightEyeDirection) / 2f).normalized;

            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                gazeOrigin = mainCamera.transform.position;
            }

            bool isCurrentlyLookingAtCenter = IsLookingAtFixationPoint(gazeOrigin, gazeDirection);

            if (!isCurrentlyLookingAtCenter && isLookingAtCenter)
            {
                lookAwayTimer += Time.deltaTime;
                if (lookAwayTimer >= lookAwayDelay)
                {
                    hideAllExceptCenter = true;
                    isLookingAtCenter = false;
                    lookAwayTimer = 0f;
                }
            }
            else if (isCurrentlyLookingAtCenter && !isLookingAtCenter)
            {
                hideAllExceptCenter = false;
                isLookingAtCenter = true;
                lookAwayTimer = 0f;
            }
            else if (isCurrentlyLookingAtCenter)
            {
                lookAwayTimer = 0f;
            }
        }
    }

    private bool IsLookingAtFixationPoint(Vector3 eyePosition, Vector3 gazeDirection)
    {
        if (centerFixationPoint == null)
            return false;

        Vector3 toFixationPoint = (centerFixationPoint.transform.position - eyePosition).normalized;

        float angle = Vector3.Angle(gazeDirection, toFixationPoint);

        return angle <= gazeThreshold;
    }

    private void UpdateVisibility()
    {
        if (hideAllExceptCenter)
        {
            if (gridLinesParent != null)
            {
                gridLinesParent.SetActive(false);
            }

            if (probeDots != null && probeDots.probes != null)
            {
                foreach (GameObject probe in probeDots.probes)
                {
                    if (probe != null)
                    {
                        probe.SetActive(false);
                    }
                }
            }

            if (gridPointsParent != null)
            {
                foreach (Transform child in gridPointsParent.transform)
                {
                    if (child.gameObject != centerFixationPoint)
                    {
                        child.gameObject.SetActive(false);
                    }
                }
            }

            if (centerFixationPoint != null)
            {
                centerFixationPoint.SetActive(true);
            }
        }
        else
        {
            if (gridLinesParent != null)
            {
                gridLinesParent.SetActive(true);
            }

            if (probeDots != null && probeDots.probes != null)
            {
                foreach (GameObject probe in probeDots.probes)
                {
                    if (probe != null)
                    {
                        probe.SetActive(true);
                    }
                }
            }

            if (gridPointsParent != null)
            {
                foreach (Transform child in gridPointsParent.transform)
                {
                    child.gameObject.SetActive(true);
                }
            }
        }
    }

    public void SetHideAllExceptCenter(bool hideState)
    {
        hideAllExceptCenter = hideState;
    }

    public void ToggleVisibility()
    {
        hideAllExceptCenter = !hideAllExceptCenter;
    }
}
