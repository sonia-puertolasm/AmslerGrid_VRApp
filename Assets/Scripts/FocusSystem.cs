using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FocusSystem : MonoBehaviour
{
    // Enable/disable the focus system functionality
    public bool focusSystemEnabled = false;

    // Reference to the main grid, probe dots and deformation system
    private ProbeDots probeDots;
    private GridDeformation gridDeformation;
    private GameObject centerFixationPoint;

    // Definition of parameters focus system related
    private bool isInFocusMode = false;
    private Vector2Int lastProbeGridPosition = new Vector2Int(-1, -1);

    // Store original probe visibility states
    private Dictionary<GameObject, bool> originalProbeVisibility = new Dictionary<GameObject, bool>();

    // Grid configuration parameters
    private int gridSize;
    private float cellSize;

    void Start()
    {
        // Locate and introduce previously defined components
        probeDots = FindObjectOfType<ProbeDots>();
        gridDeformation = FindObjectOfType<GridDeformation>();
        centerFixationPoint = GameObject.Find("CenterFixationPoint");

        MainGrid mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid != null)
        {
            gridSize = mainGrid.GridSize;
            cellSize = mainGrid.CellSize;
        }
    }

    void Update()
    {
        // Early return if focus system is disabled
        if (!focusSystemEnabled)
        {
            return;
        }

        if (probeDots == null || gridDeformation == null) // Safety check in case the probe dots and/or deformation don't exist
        {
            return;
        }

        // Check if probe selection has changed
        int currentSelectedIndex = probeDots.selectedProbeIndex;

        // Enter focus mode with probe dot selection
        if (currentSelectedIndex != -1 && !isInFocusMode)
        {
            EnterFocusMode(currentSelectedIndex);
        }

        // Exit focus mode with probe dot deselection
        else if (isInFocusMode && currentSelectedIndex == -1)
        {
            ExitFocusMode();
        }

        // Update of focus mode with probe dot displacement

        else if (isInFocusMode && currentSelectedIndex != -1)
        {
            UpdateFocusArea(currentSelectedIndex);
        }
    }

    // FUNCTION: Entering of focus mode
    private void EnterFocusMode(int probeIndex)
    {
        isInFocusMode = true;
        HideAllProbesExcept(probeIndex);
        UpdateFocusArea(probeIndex);
    }

    // FUNCTION: Exit of focus mode as long as the focus mode was previously activated
    public void ExitFocusMode()
    {
        if (!isInFocusMode) // Safety check
        {
            return;
        }

        isInFocusMode = false;
        lastProbeGridPosition = new Vector2Int(-1, -1); // Resets the position to an impossible index to ensure successful detection afterwards

        RestoreAllLines(); // Call function for restoring of the full grid
        RestoreProbeVisibility(); // Restore visibility of all probe dots
    }

    // FUNCTION: Update of the focus area while remaining in focus mode with the displacement of the probe dot
    private void UpdateFocusArea(int probeIndex)
    {
        Vector2Int gridPos = GetProbeGridCoordinates(probeIndex);
        if (gridPos != lastProbeGridPosition)
        {
            ShowOnlyFocusLines(gridPos);
            lastProbeGridPosition = gridPos;
        }
    }

    // Return the grid row/column for the currently selected probe dot
    private Vector2Int GetProbeGridCoordinates(int probeIndex)
    {

        // Safety check to ensure that the probeIndex is valid -> return invalid coordinates
        if (probeIndex < 0 || probeIndex >= probeDots.probes.Count)
        {
            return new Vector2Int(-1, -1);
        }

        // Get the probe GameObject at this specific index
        GameObject probe = probeDots.probes[probeIndex];

        // Obtain the GridPointData component -> stores row/col information
        GridPointData pointData = probe?.GetComponent<GridPointData>();
        if (pointData != null)
        {
            return new Vector2Int(pointData.row, pointData.col);
        }
        return new Vector2Int(-1, -1); // If there is no data found, return invalid vector
    }

    // Display only the grid line segments for a 2x2 area around the probe, plus external boundaries
    private void ShowOnlyFocusLines(Vector2Int gridPos)
    {
        int row = gridPos.x; // X-axis: rows
        int col = gridPos.y; // Y-axis: columns

        // Calculate world position boundaries for the 2x2 focus area
        MainGrid mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid == null) return;

        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;
        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        // Calculate focus area bounds in world coordinates (2x2 cells)
        float focusMinX = originX + (col - 1) * cellSize;
        float focusMaxX = originX + (col + 1) * cellSize;
        float focusMinY = originY + (row - 1) * cellSize;
        float focusMaxY = originY + (row + 1) * cellSize;

        // Calculate grid boundaries
        float gridMinX = originX;
        float gridMaxX = originX + gridSize * cellSize;
        float gridMinY = originY;
        float gridMaxY = originY + gridSize * cellSize;

        float epsilon = 0.01f; // Tolerance for float comparison

        // Update visibility of all horizontal line segments
        foreach (var kvp in gridDeformation.horizontalLines)
        {
            foreach (var lineInfo in kvp.Value)
            {
                if (lineInfo.lineRenderer == null) continue;

                Vector3 start = lineInfo.originalStart;
                Vector3 end = lineInfo.originalEnd;

                float lineMinX = Mathf.Min(start.x, end.x);
                float lineMaxX = Mathf.Max(start.x, end.x);
                float lineY = start.y;

                // Check if segment is within focus area
                bool inFocusArea = (lineY >= focusMinY - epsilon && lineY <= focusMaxY + epsilon) &&
                                   (lineMaxX >= focusMinX - epsilon && lineMinX <= focusMaxX + epsilon);

                // Check if segment is part of external boundaries (top or bottom edge)
                bool isTopBoundary = Mathf.Abs(lineY - gridMaxY) < epsilon;
                bool isBottomBoundary = Mathf.Abs(lineY - gridMinY) < epsilon;

                lineInfo.lineRenderer.enabled = inFocusArea || isTopBoundary || isBottomBoundary;
            }
        }

        // Update visibility of all vertical line segments
        foreach (var kvp in gridDeformation.verticalLines)
        {
            foreach (var lineInfo in kvp.Value)
            {
                if (lineInfo.lineRenderer == null) continue;

                Vector3 start = lineInfo.originalStart;
                Vector3 end = lineInfo.originalEnd;

                float lineMinY = Mathf.Min(start.y, end.y);
                float lineMaxY = Mathf.Max(start.y, end.y);
                float lineX = start.x;

                // Check if segment is within focus area
                bool inFocusArea = (lineX >= focusMinX - epsilon && lineX <= focusMaxX + epsilon) &&
                                   (lineMaxY >= focusMinY - epsilon && lineMinY <= focusMaxY + epsilon);

                // Check if segment is part of external boundaries (left or right edge)
                bool isLeftBoundary = Mathf.Abs(lineX - gridMinX) < epsilon;
                bool isRightBoundary = Mathf.Abs(lineX - gridMaxX) < epsilon;

                lineInfo.lineRenderer.enabled = inFocusArea || isLeftBoundary || isRightBoundary;
            }
        }

        // Keep ALWAYS the fixation point visible
        if (centerFixationPoint != null)
        {
            Renderer renderer = centerFixationPoint.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    // FUNCTION: Restore all lines after exiting focus mode
    private void RestoreAllLines()
    {
        foreach (var kvp in gridDeformation.horizontalLines) // Iterate over every horizontal line
            foreach (var lineInfo in kvp.Value)
                if (lineInfo.lineRenderer != null) lineInfo.lineRenderer.enabled = true;

        foreach (var kvp in gridDeformation.verticalLines) // Iterate over every vertical line
            foreach (var lineInfo in kvp.Value)
                if (lineInfo.lineRenderer != null) lineInfo.lineRenderer.enabled = true;

        if (centerFixationPoint != null) // Maintain fixation point in SAME position
        {
            Renderer renderer = centerFixationPoint.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    // FUNCTION: Hide all probe dots except the selected one
    private void HideAllProbesExcept(int selectedIndex)
    {
        if (probeDots == null || probeDots.probes == null)
            return;

        originalProbeVisibility.Clear();

        for (int i = 0; i < probeDots.probes.Count; i++)
        {
            GameObject probe = probeDots.probes[i];
            if (probe == null)
                continue;

            // Store original visibility state
            originalProbeVisibility[probe] = probe.activeSelf;

            // Hide all probes except the selected one
            if (i != selectedIndex)
            {
                probe.SetActive(false);
            }
        }
    }

    // FUNCTION: Restore visibility of all probe dots
    private void RestoreProbeVisibility()
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
}
