using UnityEngine;
using System.Collections;
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
    private int focusAreaSize = 2;
    private Vector3 focusCenterPosition;
    private float iterationProbeSpacing;

    public void EnterFocusMode(GameObject probe, Vector3? initialPosition = null, float? probeSpacing = null)
    {
        if (probe == null) return;

        selectedProbe = probe;
        isFocused = true;

        focusCenterPosition = initialPosition ?? probe.transform.position;

        currentIteration = ExtractIterationFromProbeName(probe.name);

        focusAreaSize = GetFocusAreaSize(currentIteration);

        if (probeSpacing.HasValue)
        {
            iterationProbeSpacing = probeSpacing.Value;
        }
        else if (mainGrid != null)
        {
            iterationProbeSpacing = mainGrid.CellSize;
        }
        else
        {
            iterationProbeSpacing = 1.25f;
        }

        HideAllGridLinesExceptFocusArea(focusCenterPosition);
        HideAllProbesExcept(probe);
    }

    private int ExtractIterationFromProbeName(string probeName)
    {
        if (probeName.Contains("Iter"))
        {
            int iterIndex = probeName.IndexOf("Iter") + 4;
            if (iterIndex < probeName.Length)
            {
                string iterStr = "";
                for (int i = iterIndex; i < probeName.Length && char.IsDigit(probeName[i]); i++)
                {
                    iterStr += probeName[i];
                }

                if (int.TryParse(iterStr, out int iteration))
                {
                    return iteration;
                }
            }
        }

        return 1;
    }

    private int GetFocusAreaSize(int iteration)
    {
        return iteration >= 4 ? 1 : 2;
    }

    public void ExitFocusMode()
    {
        if (!isFocused)
        {
            return;
        }

        RestoreGridVisibility();
        RestoreProbeVisibility();

        selectedProbe = null;
        isFocused = false;
        focusedStartRow = -1;
        focusedStartCol = -1;
        focusAreaSize = 2;
        focusCenterPosition = Vector3.zero;
    }

    void HideAllGridLinesExceptFocusArea(Vector3 probePosition)
    {
        if (gridLinesParent == null || mainGrid == null)
        {
            return;
        }

        if (mainGrid == null)
        {
            return;
        }

        bool isFirstCall = originalGridVisibility.Count == 0;

        float cellSize = mainGrid.CellSize;
        int gridSize = mainGrid.GridSize;
        float totalGridWidth = mainGrid.TotalGridWidth;
        Vector3 gridCenter = mainGrid.GridCenterPosition;
        float halfWidth = totalGridWidth / 2f;

        Vector3 origin = gridCenter - new Vector3(halfWidth, halfWidth, 0);
        int probeCol = Mathf.FloorToInt((probePosition.x - origin.x) / cellSize);
        int probeRow = Mathf.FloorToInt((probePosition.y - origin.y) / cellSize);

        if (focusAreaSize == 1)
        {
            focusedStartCol = Mathf.Clamp(probeCol, 0, gridSize - 1);
            focusedStartRow = Mathf.Clamp(probeRow, 0, gridSize - 1);
        }
        else
        {
            focusedStartCol = Mathf.Clamp(probeCol - 1, 0, gridSize - 2);
            focusedStartRow = Mathf.Clamp(probeRow - 1, 0, gridSize - 2);
        }

        foreach (Transform gridLine in gridLinesParent)
        {
            LineRenderer lr = gridLine.GetComponent<LineRenderer>();
            if (lr == null)
            {
                continue;
            }

            if (isFirstCall)
            {
                originalGridVisibility[gridLine.gameObject] = lr.enabled;
            }

            lr.enabled = false;
        }

        for (int row = focusedStartRow; row < focusedStartRow + focusAreaSize; row++)
        {
            for (int col = focusedStartCol; col < focusedStartCol + focusAreaSize; col++)
            {
                EnableLineByName($"Line_r{row}_c{col}_TOP");
                EnableLineByName($"Line_r{row}_c{col}_RIGHT");
            }
        }

        for (int col = focusedStartCol; col < focusedStartCol + focusAreaSize; col++)
        {
            if (focusedStartRow == 0)
            {
                EnableLineByName($"Line_r{focusedStartRow}_c{col}_BOTTOM");
            }
            else
            {
                EnableLineByName($"Line_r{focusedStartRow - 1}_c{col}_TOP");
            }
        }

        for (int row = focusedStartRow; row < focusedStartRow + focusAreaSize; row++)
        {
            if (focusedStartCol == 0)
            {
                EnableLineByName($"Line_r{row}_c{focusedStartCol}_LEFT");
            }
            else
            {
                EnableLineByName($"Line_r{row}_c{focusedStartCol - 1}_RIGHT");
            }
        }
    }

    private void EnableLineByName(string lineName)
    {
        foreach (Transform gridLine in gridLinesParent)
        {
            if (gridLine.name == lineName)
            {
                LineRenderer lr = gridLine.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = true;
                }
                return;
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
        if (gridLinesParent == null)
        {
            return;
        }

        foreach (var kvp in originalGridVisibility)
        {
            if (kvp.Key != null)
            {
                LineRenderer lr = kvp.Key.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = kvp.Value;
                }
            }
        }

        originalGridVisibility.Clear();
        focusedStartRow = -1;
        focusedStartCol = -1;
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

        Transform gridLinesChild = parent.Find("GridLines");
        if (gridLinesChild != null)
        {
            gridLinesParent = gridLinesChild;
        }
        else
        {
            StartCoroutine(RetrySetGridLines(parent));
        }

        mainGrid = gridParent.GetComponent<MainGrid>();
    }

    private IEnumerator RetrySetGridLines(Transform parent)
    {
        yield return null;

        int retries = 0;
        while (retries < 10 && gridLinesParent == null)
        {
            Transform gridLinesChild = parent.Find("GridLines");
            if (gridLinesChild != null)
            {
                gridLinesParent = gridLinesChild;
                yield break;
            }

            retries++;
            yield return null;
        }

        if (gridLinesParent == null)
        {
            gridLinesParent = parent;
        }
    }

    public void SetProbeParent(Transform parent)
    {
        probeParent = parent;
    }

    public void SetCurrentIteration(int iteration)
    {
        currentIteration = iteration;
        if (isFocused)
        {
            focusAreaSize = GetFocusAreaSize(currentIteration);
        }
    }

    public void GetFocusedRegionBounds(out int startRow, out int startCol, out int endRow, out int endCol)
    {
        startRow = focusedStartRow;
        startCol = focusedStartCol;
        endRow = focusedStartRow + focusAreaSize - 1;
        endCol = focusedStartCol + focusAreaSize - 1;
    }

    public void GetFocusedRegionWorldBounds(out Vector3 minBounds, out Vector3 maxBounds)
    {
        minBounds = Vector3.zero;
        maxBounds = Vector3.zero;

        if (mainGrid == null) return;

        float focusRadius = focusAreaSize * iterationProbeSpacing / 2f;

        minBounds = new Vector3(
            focusCenterPosition.x - focusRadius,
            focusCenterPosition.y - focusRadius,
            focusCenterPosition.z
        );

        maxBounds = new Vector3(
            focusCenterPosition.x + focusRadius,
            focusCenterPosition.y + focusRadius,
            focusCenterPosition.z
        );
    }
}
