using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FocusSystem : MonoBehaviour
{
    // Enable/disable the focus system functionality
    public bool focusSystemEnabled = false;

    // Reference to the main grid, probe dots and deformation system
    private ProbeDots probeDots;
    private GridRebuildManager gridRebuildManager;
    private GameObject centerFixationPoint;

    // Definition of parameters focus system related
    private bool isInFocusMode = false;

    // Store original probe visibility states
    private Dictionary<GameObject, bool> originalProbeVisibility = new Dictionary<GameObject, bool>();

    // Grid configuration parameters
    private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    // Store probe original positions for grid index calculation
    private Dictionary<GameObject, Vector3> probeOriginalPositions = new Dictionary<GameObject, Vector3>();

    void Start()
    {
        // Locate and introduce previously defined components
        probeDots = FindObjectOfType<ProbeDots>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        centerFixationPoint = GameObject.Find("CenterFixationPoint");

        MainGrid mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid != null)
        {
            gridSize = mainGrid.GridSize;
            cellSize = mainGrid.CellSize;
            gridCenter = mainGrid.GridCenterPosition;
            halfWidth = mainGrid.TotalGridWidth / 2f;
        }

        // Store original probe positions for grid index calculations
        if (probeDots != null && probeDots.probes != null)
        {
            foreach (GameObject probe in probeDots.probes)
            {
                if (probe != null)
                {
                    probeOriginalPositions[probe] = probe.transform.position;
                }
            }
        }
    }

    void Update()
    {
        // Early return if focus system is disabled
        if (!focusSystemEnabled)
        {
            return;
        }

        if (probeDots == null || gridRebuildManager == null) // Safety check in case the probe dots and/or deformation don't exist
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
    }

    // FUNCTION: Entering of focus mode
    private void EnterFocusMode(int probeIndex)
    {
        isInFocusMode = true;
        HideAllProbesExcept(probeIndex);
        // Show only the lines around the selected probe
        ShowOnlyLinesAroundProbe(probeIndex);
    }

    // FUNCTION: Exit of focus mode as long as the focus mode was previously activated
    public void ExitFocusMode()
    {
        if (!isInFocusMode) // Safety check
        {
            return;
        }

        isInFocusMode = false;

        RestoreAllLines(); // Call function for restoring of the full grid
        RestoreProbeVisibility(); // Restore visibility of all probe dots
    }

    // FUNCTION: Show only the lines around the selected probe (3 horizontal + 3 vertical)
    private void ShowOnlyLinesAroundProbe(int probeIndex)
    {
        if (gridRebuildManager == null || probeDots == null || probeDots.probes == null)
            return;

        if (probeIndex < 0 || probeIndex >= probeDots.probes.Count)
            return;

        GameObject selectedProbe = probeDots.probes[probeIndex];
        if (selectedProbe == null)
            return;

        // Get the grid position of the selected probe
        Vector2Int probeGridPos = GetProbeGridIndex(selectedProbe);
        int probeRow = probeGridPos.y;
        int probeCol = probeGridPos.x;

        // Hide all horizontal lines first
        foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool)
        {
            if (lr != null)
                lr.enabled = false;
        }

        // Hide all vertical lines first
        foreach (LineRenderer lr in gridRebuildManager.verticalLinePool)
        {
            if (lr != null)
                lr.enabled = false;
        }

        // Show 3 horizontal lines (rows: probeRow-1, probeRow, probeRow+1)
        for (int row = probeRow - 1; row <= probeRow + 1; row++)
        {
            if (row >= 0 && row < gridRebuildManager.horizontalLinePool.Count)
            {
                LineRenderer lr = gridRebuildManager.horizontalLinePool[row];
                if (lr != null)
                {
                    lr.enabled = true;
                }
            }
        }

        // Show 3 vertical lines (cols: probeCol-1, probeCol, probeCol+1)
        for (int col = probeCol - 1; col <= probeCol + 1; col++)
        {
            if (col >= 0 && col < gridRebuildManager.verticalLinePool.Count)
            {
                LineRenderer lr = gridRebuildManager.verticalLinePool[col];
                if (lr != null)
                {
                    lr.enabled = true;
                }
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
        if (gridRebuildManager == null)
            return;

        // Re-enable all GridRebuildManager lines
        foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool)
        {
            if (lr != null)
                lr.enabled = true;
        }

        foreach (LineRenderer lr in gridRebuildManager.verticalLinePool)
        {
            if (lr != null)
                lr.enabled = true;
        }

        // Maintain fixation point visibility
        if (centerFixationPoint != null)
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

    // FUNCTION: Get the grid index (row, col) of a probe based on its original position
    private Vector2Int GetProbeGridIndex(GameObject probe)
    {
        Vector3 probeOriginalPos;

        if (probeOriginalPositions.ContainsKey(probe))
        {
            probeOriginalPos = probeOriginalPositions[probe];
        }
        else
        {
            probeOriginalPos = probe.transform.position;
        }

        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        int col = Mathf.RoundToInt((probeOriginalPos.x - originX) / cellSize);
        int row = Mathf.RoundToInt((probeOriginalPos.y - originY) / cellSize);

        col = Mathf.Clamp(col, 0, gridSize);
        row = Mathf.Clamp(row, 0, gridSize);

        return new Vector2Int(col, row);
    }
}
