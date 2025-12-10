using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EyeTracking : MonoBehaviour
{
    private MainGrid mainGrid;
    private ProbeDots probeDots;
    private GridRebuildManager gridRebuildManager;
    private FocusSystem focusSystem;
    private GameObject centerFixationPoint;
    private GameObject gridLinesParent;
    private GameObject probesParent;

    private bool hideAllExceptCenter = false;

    private EyeTrackingToolbox eyetracker;
    public float gazeThresholdAngle;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        focusSystem = FindObjectOfType<FocusSystem>();
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
            }

            Transform gridLinesTransform = mainGrid.transform.Find("GridLines");
            if (gridLinesTransform != null)
            {
                gridLinesParent = gridLinesTransform.gameObject;
            }
        }

        if (probeDots != null)
        {
            probesParent = probeDots.gameObject;
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

        // Disable grid lines parent
        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(false);
        }

        // Disable probes parent (all probes will be hidden)
        if (probesParent != null)
        {
            probesParent.SetActive(false);
        }

        // Ensure center fixation point remains visible
        if (centerFixationPoint != null)
        {
            centerFixationPoint.SetActive(true);
        }
    }

    private void ShowAllElements()
    {
        // Enable grid lines parent
        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(true);
        }

        // Enable probes parent (all probes will be shown)
        if (probesParent != null)
        {
            probesParent.SetActive(true);
        }

        // Ensure center fixation point remains visible
        if (centerFixationPoint != null)
        {
            centerFixationPoint.SetActive(true);
        }

        if (gridRebuildManager != null)
        {
            gridRebuildManager.ForceRebuild();
        }
    }
}