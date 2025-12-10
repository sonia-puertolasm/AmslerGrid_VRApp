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
    private GameObject gridPointsParent;
    private GameObject probesParent;
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

            Transform gridLinesTransform = mainGrid.transform.Find("GridLines");
            if (gridLinesTransform != null)
            {
                gridLinesParent = gridLinesTransform.gameObject;
            }

            Transform gridPointsTransform = mainGrid.transform.Find("GridPoints");
            if (gridPointsTransform != null)
            {
                gridPointsParent = gridPointsTransform.gameObject;
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

        if (centerFixationPoint != null && centerOriginalParent != null)
        {
            centerFixationPoint.transform.SetParent(null);
        }

        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(false);
        }

        if (gridPointsParent != null)
        {
            gridPointsParent.SetActive(false);
        }

        if (probesParent != null)
        {
            probesParent.SetActive(false);
        }
    }

    private void ShowAllElements()
    {
        if (gridPointsParent != null)
        {
            gridPointsParent.SetActive(true);
        }

        if (centerFixationPoint != null && centerOriginalParent != null)
        {
            centerFixationPoint.transform.SetParent(centerOriginalParent);
        }

        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(true);
        }

        if (probesParent != null)
        {
            probesParent.SetActive(true);
        }

        if (gridRebuildManager != null)
        {
            gridRebuildManager.ForceRebuild();
        }
    }
}