using UnityEngine;
using System.Collections.Generic;

public class IAGFocusSystem : MonoBehaviour
{
    private Transform gridParent;  
    private Transform gridLinesParent; 
    private Transform probeParent;
    private MainGrid mainGrid;

    private GameObject selectedProbe = null;
    private bool isFocused = false;

    private int currentIteration = 1;

    private Dictionary<GameObject, bool> originalGridVisibility = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, bool> originalProbeVisibility = new Dictionary<GameObject, bool>();

    private int focusedStartRow = -1;
    private int focusedStartCol = -1;

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
        focusedStartRow = -1;
        focusedStartCol = -1;
    }

    void HideAllGridLinesExceptFocusArea(Vector3 probePosition)
    {
        if (gridLinesParent == null)
        {
            return;
        }

        if (mainGrid == null)
        {
            return;
        }

        originalGridVisibility.Clear();

        float cellSize = mainGrid.CellSize;
        int gridSize = mainGrid.GridSize;
        float totalGridWidth = mainGrid.TotalGridWidth;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        float halfWidth = totalGridWidth / 2f;
        Vector3 origin = new Vector3(
            gridCenter.x - halfWidth,
            gridCenter.y - halfWidth,
            gridCenter.z
        );

        int probeCol = Mathf.FloorToInt((probePosition.x - origin.x) / cellSize);
        int probeRow = Mathf.FloorToInt((probePosition.y - origin.y) / cellSize);

        probeCol = Mathf.Clamp(probeCol, 0, gridSize - 1);
        probeRow = Mathf.Clamp(probeRow, 0, gridSize - 1);

        focusedStartCol = probeCol;
        focusedStartRow = probeRow;


        if (focusedStartCol + 2 > gridSize)
        {
            focusedStartCol = gridSize - 2;
        }
        if (focusedStartRow + 2 > gridSize)
        {
            focusedStartRow = gridSize - 2;
        }

        focusedStartCol = Mathf.Max(0, focusedStartCol);
        focusedStartRow = Mathf.Max(0, focusedStartRow);


        float minX = origin.x + focusedStartCol * cellSize;
        float maxX = origin.x + (focusedStartCol + 2) * cellSize;
        float minY = origin.y + focusedStartRow * cellSize;
        float maxY = origin.y + (focusedStartRow + 2) * cellSize;

        float epsilon = cellSize * 0.01f;

        int visibleCount = 0;
        int hiddenCount = 0;
        int totalCount = 0;

        foreach (Transform gridLine in gridLinesParent)
        {
            LineRenderer lr = gridLine.GetComponent<LineRenderer>();
            if (lr == null) continue;

            totalCount++;

            Vector3 lineStart = lr.GetPosition(0);
            Vector3 lineEnd = lr.GetPosition(1);

            bool isVisible = IsLineInFocusedRegion(lineStart, lineEnd, minX, maxX, minY, maxY, epsilon);

            originalGridVisibility[gridLine.gameObject] = gridLine.gameObject.activeSelf;
            gridLine.gameObject.SetActive(isVisible);

            if (isVisible)
            {
                visibleCount++;
            }
            else
            {
                hiddenCount++;

                if (hiddenCount <= 3)
                {
                }
            }
        }
    }

    private bool IsLineInFocusedRegion(Vector3 lineStart, Vector3 lineEnd, float minX, float maxX, float minY, float maxY, float epsilon)
    {
        if (Mathf.Abs(lineStart.y - lineEnd.y) < epsilon)
        {
            float lineY = lineStart.y;
            float lineMinX = Mathf.Min(lineStart.x, lineEnd.x);
            float lineMaxX = Mathf.Max(lineStart.x, lineEnd.x);

            bool yInRange = (Mathf.Abs(lineY - minY) < epsilon) ||
                           (Mathf.Abs(lineY - (minY + maxY) / 2f) < epsilon) ||
                           (Mathf.Abs(lineY - maxY) < epsilon);

            bool xOverlaps = (lineMinX < maxX + epsilon) && (lineMaxX > minX - epsilon);

            bool withinRegion = (lineMinX >= minX - epsilon && lineMaxX <= maxX + epsilon);

            return yInRange && withinRegion;
        }
        else if (Mathf.Abs(lineStart.x - lineEnd.x) < epsilon)
        {
            float lineX = lineStart.x;
            float lineMinY = Mathf.Min(lineStart.y, lineEnd.y);
            float lineMaxY = Mathf.Max(lineStart.y, lineEnd.y);

            bool xInRange = (Mathf.Abs(lineX - minX) < epsilon) ||
                           (Mathf.Abs(lineX - (minX + maxX) / 2f) < epsilon) ||
                           (Mathf.Abs(lineX - maxX) < epsilon);

            bool withinRegion = (lineMinY >= minY - epsilon && lineMaxY <= maxY + epsilon);

            return xInRange && withinRegion;
        }

        return false;
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
                kvp.Key.SetActive(kvp.Value);
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

        Transform gridLinesChild = parent.Find("GridLines");
        if (gridLinesChild != null)
        {
            gridLinesParent = gridLinesChild;
        }
        else
        {
            gridLinesParent = parent;
        }

        mainGrid = gridParent.GetComponent<MainGrid>();
    }

    public void SetProbeParent(Transform parent)
    {
        probeParent = parent;
    }

    public void SetCurrentIteration(int iteration)
    {
        currentIteration = iteration;
    }

    public void GetFocusedRegionBounds(out int startRow, out int startCol, out int endRow, out int endCol)
    {
        startRow = focusedStartRow;
        startCol = focusedStartCol;
        endRow = focusedStartRow + 2;  
        endCol = focusedStartCol + 2;
    }

    public void GetFocusedRegionWorldBounds(out Vector3 minBounds, out Vector3 maxBounds)
    {
        minBounds = Vector3.zero;
        maxBounds = Vector3.zero;

        if (mainGrid == null) return;

        float cellSize = mainGrid.CellSize;
        float totalGridWidth = mainGrid.TotalGridWidth;
        Vector3 gridCenter = mainGrid.GridCenterPosition;
        float halfWidth = totalGridWidth / 2f;

        Vector3 origin = new Vector3(
            gridCenter.x - halfWidth,
            gridCenter.y - halfWidth,
            gridCenter.z
        );

        minBounds = origin + new Vector3(focusedStartCol * cellSize, focusedStartRow * cellSize, 0);
        maxBounds = origin + new Vector3((focusedStartCol + 2) * cellSize, (focusedStartRow + 2) * cellSize, 0);
    }
}
