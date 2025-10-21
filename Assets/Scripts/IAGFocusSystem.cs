using UnityEngine;
using System.Collections.Generic;

public class IAGFocusSystem : MonoBehaviour
{
    private Transform gridParent;
    private Transform probeParent;
    private MainGrid mainGrid;

    private GameObject selectedProbe = null;
    private bool isFocused = false;

    private int currentIteration = 1;

    private Dictionary<GameObject, bool> originalGridVisibility = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, bool> originalProbeVisibility = new Dictionary<GameObject, bool>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isFocused)
        {
            ExitFocusMode();
        }
    }

    public void EnterFocusMode(GameObject probe)
    {
        if (probe == null) return;

        selectedProbe = probe;
        isFocused = true;

        if (probe.name.Contains("Iter1"))
        {
            currentIteration = 1;
        }
        else
        {
            currentIteration = 2;
        }

        HideAllGridLinesExceptFocusArea(probe.transform.position);
        HideAllProbesExcept(probe);
    }

    public void ExitFocusMode()
    {
        if (!isFocused) return;

        RestoreGridVisibility();
        RestoreProbeVisibility();

        selectedProbe = null;
        isFocused = false;
    }

    void HideAllGridLinesExceptFocusArea(Vector3 probePosition)
    {
        if (gridParent == null)
        {
            return;
        }

        if (mainGrid == null)
        {
            mainGrid = gridParent.GetComponent<MainGrid>();
            if (mainGrid == null) return;
        }

        originalGridVisibility.Clear();

        float cellSize = mainGrid.CellSize;
        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        float localX = probePosition.x - gridCenter.x + halfWidth;
        float localY = probePosition.y - gridCenter.y + halfWidth;
        int probeCellX = Mathf.FloorToInt(localX / cellSize);
        int probeCellY = Mathf.FloorToInt(localY / cellSize);

        int minCellX = Mathf.Max(0, probeCellX - 1);
        int maxCellX = Mathf.Min(mainGrid.GridSize, probeCellX + 2);
        int minCellY = Mathf.Max(0, probeCellY - 1);
        int maxCellY = Mathf.Min(mainGrid.GridSize, probeCellY + 2);

        HashSet<int> visibleHLines = new HashSet<int>();
        HashSet<int> visibleVLines = new HashSet<int>();

        for (int i = minCellY; i <= maxCellY; i++)
        {
            visibleHLines.Add(i);
        }
        for (int i = minCellX; i <= maxCellX; i++)
        {
            visibleVLines.Add(i);
        }

        foreach (Transform child in gridParent)
        {
            if (child.name.Contains("CenterDot") || child.name == "CenterDot")
            {
                continue;
            }

            bool shouldBeVisible = false;


            if (child.name.StartsWith("HLine_"))
            {
                int lineIndex = int.Parse(child.name.Replace("HLine_", ""));
                shouldBeVisible = visibleHLines.Contains(lineIndex);
            }
            else if (child.name.StartsWith("VLine_"))
            {
                int lineIndex = int.Parse(child.name.Replace("VLine_", ""));
                shouldBeVisible = visibleVLines.Contains(lineIndex);
            }

            LineRenderer lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                originalGridVisibility[child.gameObject] = lr.enabled;
                lr.enabled = shouldBeVisible;
            }
            else
            {
                originalGridVisibility[child.gameObject] = child.gameObject.activeSelf;
                child.gameObject.SetActive(shouldBeVisible);
            }
        }
    }

    void HideAllProbesExcept(GameObject exception)
    {
        if (probeParent == null) return;

        originalProbeVisibility.Clear();

        foreach (Transform child in probeParent)
        {
            if (child.gameObject == exception)
                continue;

            originalProbeVisibility[child.gameObject] = child.gameObject.activeSelf;
            child.gameObject.SetActive(false);
        }
    }


    void RestoreGridVisibility()
    {
        foreach (var kvp in originalGridVisibility)
        {
            if (kvp.Key != null)
            {
                LineRenderer lr = kvp.Key.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = kvp.Value;
                }
                else
                {
                    kvp.Key.SetActive(kvp.Value);
                }
            }
        }
        originalGridVisibility.Clear();
    }

    void RestoreProbeVisibility()
    {
        foreach (var kvp in originalProbeVisibility)
        {
            if (kvp.Key != null)
            {
                kvp.Key.SetActive(kvp.Value);
            }
        }
        originalProbeVisibility.Clear();
    }

    public void UpdateFocusPosition(Vector3 newPosition)
    {
        if (!isFocused) return;

        HideAllGridLinesExceptFocusArea(newPosition);
    }

    public bool IsFocused()
    {
        return isFocused;
    }

    public GameObject GetSelectedProbe()
    {
        return selectedProbe;
    }

    public void SetGridParent(Transform parent)
    {
        gridParent = parent;
    }

    public void SetProbeParent(Transform parent)
    {
        probeParent = parent;
    }

    public void SetCurrentIteration(int iteration)
    {
        currentIteration = iteration;
    }
}