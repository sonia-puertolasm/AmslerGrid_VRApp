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

        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>();
        foreach (LineRenderer lr in allLines)
        {
            if (lr != null)
                lr.enabled = false;
        }

        if (probeDots != null && probeDots.probes != null)
        {
            foreach (GameObject probe in probeDots.probes)
            {
                if (probe != null && probe != centerFixationPoint)
                {
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.enabled = false;
                }
            }
        }

        if (centerFixationPoint != null)
        {
            Renderer centerRenderer = centerFixationPoint.GetComponent<Renderer>();
            if (centerRenderer != null)
                centerRenderer.enabled = true;
        }
    }

    private void ShowAllElements()
    {
        LineRenderer[] allLines = FindObjectsOfType<LineRenderer>(true);
        foreach (LineRenderer lr in allLines)
        {
            if (lr != null)
                lr.enabled = true;
        }

        if (probeDots != null && probeDots.probes != null)
        {
            foreach (GameObject probe in probeDots.probes)
            {
                if (probe != null)
                {
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.enabled = true;
                }
            }
        }

        if (centerFixationPoint != null)
        {
            Renderer centerRenderer = centerFixationPoint.GetComponent<Renderer>();
            if (centerRenderer != null)
                centerRenderer.enabled = true;
        }

        if (gridRebuildManager != null)
        {
            gridRebuildManager.ForceRebuild();
        }
    }
}