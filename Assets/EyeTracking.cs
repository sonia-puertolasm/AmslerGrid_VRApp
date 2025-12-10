using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EyeTracking : MonoBehaviour
{
    private MainGrid mainGrid;
    private ProbeDots probeDots;
    private GridRebuildManager gridRebuildManager;
    private FocusSystem focusSystem;
    private IterationManager iterationManager;
    private ProbeDotConstraints constraintManager;
    private DisplacementTracker displacementTracker;

    private GameObject centerFixationPoint;
    private Transform centerOriginalParent;

    private bool hideAllExceptCenter = false;

    private EyeTrackingToolbox eyetracker;
    public float gazeThresholdAngle;

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

    private IEnumerator InitializeReferences()
    {
        yield return new WaitForEndOfFrame();

        if (mainGrid != null)
        {
            Transform centerTransform = mainGrid.transform.Find("GridPoints/CenterFixationPoint");
            if (centerTransform != null)
            {
                centerFixationPoint = centerTransform.gameObject;
                centerOriginalParent = centerTransform.parent;
            }
        }
    }

    void LateUpdate()
    {
        if (eyetracker == null) return;

        GazeData gaze = eyetracker.GetGazeData();

        Debug.Log("Gaze Direction: " + gaze.combinedRayLocal.direction + " Angle with Forward: " + Vector3.Angle(Vector3.forward, gaze.combinedRayLocal.direction));
        if (Vector3.Angle(Vector3.forward, gaze.combinedRayLocal.direction) < gazeThresholdAngle)
        {
            SetHideAllExceptCenter(false);
        }
        else
        {
            SetHideAllExceptCenter(true);
        }
    }

    private void SetHideAllExceptCenter(bool hideState)
    {
        if (hideAllExceptCenter == hideState)
            return;

        hideAllExceptCenter = hideState;

        if (hideAllExceptCenter)
        {
            HideAllExceptCenter();
        }
        else
        {
            ShowAllElements();
        }
    }

    private void HideAllExceptCenter()
    {
        if (focusSystem != null)
        {
            focusSystem.ExitFocusMode();
        }

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
    }

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
    }
}