using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EyeTracking : MonoBehaviour
{
    // Definition of GOs of interest
    private MainGrid mainGrid;
    private ProbeDots probeDots;
    private GridRebuildManager gridRebuildManager;
    private FocusSystem focusSystem;
    private IterationManager iterationManager;
    private ProbeDotConstraints constraintManager;
    private DisplacementTracker displacementTracker;

    // Definition of center fixation point
    private GameObject centerFixationPoint;
    private Transform centerOriginalParent;

    // Definition of GO-hiding variable
    private bool hideAllExceptCenter = false;

    // Definition of grace period parameters
    private float startupGracePeriod = 5f;
    private float startupTimer = 0f;
    private bool isGracePeriodActive = true;

    // Definition of eye tracking specific parameters
    private EyeTrackingToolbox eyetracker;
    public float gazeThresholdAngle;

    // Initialization of all methods
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        focusSystem = FindObjectOfType<FocusSystem>();
        iterationManager = FindObjectOfType<IterationManager>();
        constraintManager = FindObjectOfType<ProbeDotConstraints>();
        displacementTracker = FindObjectOfType<DisplacementTracker>();
        eyetracker = GetComponent<EyeTrackingToolbox>();

        StartCoroutine(InitializeReferences());
    }

    // METHOD: Coroutine for delaying eye tracking until initialization of all scene objects
    private IEnumerator InitializeReferences()
    {
        yield return new WaitForEndOfFrame();

        if (mainGrid != null) // Safety: Proceeds if the main grid exists
        {
            Transform centerTransform = mainGrid.transform.Find("GridPoints/CenterFixationPoint"); // Look for center fixation point as a reference
            if (centerTransform != null)
            {
                centerFixationPoint = centerTransform.gameObject;
                centerOriginalParent = centerTransform.parent;
            }
        }
    }

    // METHOD: Once per rendered frame the main eye tracking functionality is performed
    void LateUpdate()
    {
        if (eyetracker == null) return;

        if (isGracePeriodActive) // Enforcing of grace period
        {
            startupTimer += Time.deltaTime;
            if (startupTimer >= startupGracePeriod)
            {
                isGracePeriodActive = false;
            }
            return;
        }

        GazeData gaze = eyetracker.GetGazeData(); // Extraction of gaze-related information

        if (Vector3.Angle(Vector3.forward, gaze.combinedRayLocal.direction) < gazeThresholdAngle) // If the angle of the gaze is below the threshold, all GOs are still visualizable. Otherwise, everything is hidden.
        {
            SetHideAllExceptCenter(false);
        }
        else
        {
            SetHideAllExceptCenter(true);
        }
    }

    // METHOD: Sets the hiding status for posteriously hiding all GOs (exc. center fixation point)
    private void SetHideAllExceptCenter(bool hideState)
    {
        if (hideAllExceptCenter == hideState) // Safety: Early exit in case the status = hide
            return;

        hideAllExceptCenter = hideState; // Status = hide

        if (hideAllExceptCenter) // If status = hide
        {
            HideAllExceptCenter();
        }
        else
        {
            ShowAllElements();
        }
    }

    // METHOD: Hides all GOs except center fixation point while safety-checking that ALL GOs exist
    private void HideAllExceptCenter()
    {
        if (centerFixationPoint != null && centerOriginalParent != null)
        {
            centerFixationPoint.transform.SetParent(null);
        }

        if (mainGrid != null)
        {
            mainGrid.gameObject.SetActive(false);
        }

        if (probeDots != null)
        {
            probeDots.gameObject.SetActive(false);
        }

        if (gridRebuildManager != null)
        {
            gridRebuildManager.gameObject.SetActive(false);
        }

        if (iterationManager != null)
        {
            iterationManager.gameObject.SetActive(false);
        }

        if (constraintManager != null)
        {
            constraintManager.gameObject.SetActive(false);
        }

        if (displacementTracker != null)
        {
            displacementTracker.gameObject.SetActive(false);
        }

        if (focusSystem != null)
        {
            focusSystem.gameObject.SetActive(false);
        }
    }

    // METHOD: Restores visibility of all GOs except center fixation point while safety-checking that ALL GOs exist
    private void ShowAllElements()
    {
        if (mainGrid != null)
        {
            mainGrid.gameObject.SetActive(true);
        }

        if (probeDots != null)
        {
            probeDots.gameObject.SetActive(true);
        }

        if (gridRebuildManager != null)
        {
            gridRebuildManager.gameObject.SetActive(true);
            gridRebuildManager.ForceRebuild();
        }

        if (iterationManager != null)
        {
            iterationManager.gameObject.SetActive(true);
        }

        if (constraintManager != null)
        {
            constraintManager.gameObject.SetActive(true);
        }

        if (displacementTracker != null)
        {
            displacementTracker.gameObject.SetActive(true);
        }

        if (centerFixationPoint != null && centerOriginalParent != null)
        {
            centerFixationPoint.transform.SetParent(centerOriginalParent);
        }

        if (focusSystem != null)
        {
            focusSystem.gameObject.SetActive(true);
        }
    }
}