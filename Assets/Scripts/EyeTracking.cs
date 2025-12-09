using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EyeTracking : MonoBehaviour
{
    private MainGrid mainGrid;
    private ProbeDots probeDots;

    private GameObject gridLinesParent;
    private GameObject gridPointsParent;
    private GameObject centerFixationPoint;

    public bool hideAllExceptCenter = false; // Variable to set depending on the eye tracking

    private bool previousHideState = false;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();

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
        if (hideAllExceptCenter != previousHideState)
        {
            UpdateVisibility();
            previousHideState = hideAllExceptCenter;
        }
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
