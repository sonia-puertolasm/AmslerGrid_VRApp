using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EyeTracking : MonoBehaviour
{
    private MainGrid mainGrid;
    private ProbeDots probeDots;
    private GridRebuildManager gridRebuildManager;

    private GameObject gridLinesParent;
    private GameObject gridPointsParent;
    private GameObject centerFixationPoint;

    public bool hideAllExceptCenter = false;
    private bool previousHideState = false;

    private Dictionary<GameObject, bool> originalProbeStates = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, bool> originalProbeRendererStates = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, bool> originalGridPointStates = new Dictionary<GameObject, bool>();
    private Dictionary<LineRenderer, bool> originalLineRendererStates = new Dictionary<LineRenderer, bool>();

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();

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

        CaptureOriginalStates();
    }

    void Update()
    {
        if (hideAllExceptCenter != previousHideState)
        {
            UpdateVisibility();
            previousHideState = hideAllExceptCenter;
        }
    }

    private void CaptureOriginalStates()
    {
        if (probeDots != null && probeDots.probes != null)
        {
            foreach (GameObject probe in probeDots.probes)
            {
                if (probe != null)
                {
                    originalProbeStates[probe] = probe.activeSelf;
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        originalProbeRendererStates[probe] = renderer.enabled;
                    }
                }
            }
        }

        if (gridPointsParent != null)
        {
            foreach (Transform child in gridPointsParent.transform)
            {
                if (child.gameObject != centerFixationPoint)
                {
                    originalGridPointStates[child.gameObject] = child.gameObject.activeSelf;
                }
            }
        }

        if (gridRebuildManager != null)
        {
            foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool)
            {
                if (lr != null)
                {
                    originalLineRendererStates[lr] = lr.enabled;
                }
            }
            foreach (LineRenderer lr in gridRebuildManager.verticalLinePool)
            {
                if (lr != null)
                {
                    originalLineRendererStates[lr] = lr.enabled;
                }
            }
        }
    }

    private void UpdateVisibility()
    {
        if (hideAllExceptCenter)
        {
            HideAllExceptCenter();
        }
        else
        {
            RestoreVisibility();
        }
    }

    private void HideAllExceptCenter()
    {
        CaptureOriginalStates();

        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(false);
        }

        if (gridRebuildManager != null)
        {
            foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool)
            {
                if (lr != null)
                {
                    lr.enabled = false;
                }
            }
            foreach (LineRenderer lr in gridRebuildManager.verticalLinePool)
            {
                if (lr != null)
                {
                    lr.enabled = false;
                }
            }
        }

        if (probeDots != null && probeDots.probes != null)
        {
            foreach (GameObject probe in probeDots.probes)
            {
                if (probe != null)
                {
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
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
            Renderer centerRenderer = centerFixationPoint.GetComponent<Renderer>();
            if (centerRenderer != null)
            {
                centerRenderer.enabled = true;
            }
        }
    }

    private void RestoreVisibility()
    {
        if (gridLinesParent != null)
        {
            gridLinesParent.SetActive(true);
        }

        if (gridRebuildManager != null)
        {
            foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool)
            {
                if (lr != null && originalLineRendererStates.ContainsKey(lr))
                {
                    lr.enabled = originalLineRendererStates[lr];
                }
            }
            foreach (LineRenderer lr in gridRebuildManager.verticalLinePool)
            {
                if (lr != null && originalLineRendererStates.ContainsKey(lr))
                {
                    lr.enabled = originalLineRendererStates[lr];
                }
            }
        }

        if (probeDots != null && probeDots.probes != null)
        {
            foreach (GameObject probe in probeDots.probes)
            {
                if (probe != null)
                {
                    if (originalProbeStates.ContainsKey(probe))
                    {
                        probe.SetActive(originalProbeStates[probe]);
                    }
                    else
                    {
                        probe.SetActive(true);
                    }

                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        if (originalProbeRendererStates.ContainsKey(probe))
                        {
                            renderer.enabled = originalProbeRendererStates[probe];
                        }
                        else
                        {
                            renderer.enabled = true;
                        }
                    }
                }
            }
        }

        if (gridPointsParent != null)
        {
            foreach (Transform child in gridPointsParent.transform)
            {
                if (originalGridPointStates.ContainsKey(child.gameObject))
                {
                    child.gameObject.SetActive(originalGridPointStates[child.gameObject]);
                }
                else
                {
                    child.gameObject.SetActive(true);
                }
            }
        }

        if (gridRebuildManager != null)
        {
            gridRebuildManager.ForceRebuild();
        }

        ClearStoredStates();
    }

    private void ClearStoredStates()
    {
        originalProbeStates.Clear();
        originalProbeRendererStates.Clear();
        originalGridPointStates.Clear();
        originalLineRendererStates.Clear();
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