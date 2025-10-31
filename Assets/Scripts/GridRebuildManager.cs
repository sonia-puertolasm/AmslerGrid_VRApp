using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Simplified grid deformation system that rebuilds the grid from scratch based on probe positions.
///
/// ALGORITHM:
/// 1. Iterate through all grid points (9x9 grid)
/// 2. For each probe that has moved:
///    - Find neighboring probes in all 4 directions (up, down, left, right)
///    - Deform grid points with falloff pattern (100% → 66% → 33%)
///    - STOP at neighboring probes (deformation does not pass through other probes)
///    - Maximum extent: 2 cells or nearest probe, whichever is closer
/// 3. Lines automatically connect the displaced grid points
///
/// KEY FEATURES:
/// - Simple iteration: No complex influence calculations or tuneable variables
/// - Neighbor-limited: Deformation stops at neighboring probes
/// - Localized deformation: Only affects 2 cells max in each direction
/// - Fixed boundaries: Outer edges never deform, maintaining grid shape
/// - Hardcoded falloff: 100% → 66% → 33% → 0% (no configuration needed)
/// - Object pooling: Reuses LineRenderers for efficiency (no GC allocations)
///
/// Much simpler than the complex 1095-line GridDeformation system.
/// </summary>
public class GridRebuildManager : MonoBehaviour
{
    // References to required components
    private MainGrid mainGrid;
    private ProbeDots probeDots;

    // Grid configuration (copied from MainGrid during initialization)
    private int gridSize; // Number of cells (e.g., 8 for 8x8 grid)
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    // Original grid point positions (9x9 for 8x8 grid)
    private Vector3[,] originalGridPoints;

    // Current (deformed) grid point positions
    private Vector3[,] currentGridPoints;

    // LineRenderer pools for efficient rebuild
    private List<LineRenderer> horizontalLinePool = new List<LineRenderer>();
    private List<LineRenderer> verticalLinePool = new List<LineRenderer>();

    // Deformation settings
    [Header("Deformation Settings")]
    [Tooltip("Enable/disable grid deformation")]
    public bool enableDeformation = true;

    // Dirty flag system - only rebuild when needed
    private bool needsRebuild = false;
    private Vector3[] lastProbePositions;

    // Store probe original positions (captured at initialization)
    private Dictionary<GameObject, Vector3> probeOriginalPositions = new Dictionary<GameObject, Vector3>();

    // Line appearance settings
    private float lineWidth = 0.15f;
    private Color lineColor = Color.white;

    void Start()
    {
        // Find required components
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();

        if (mainGrid == null || probeDots == null)
        {
            Debug.LogError("GridRebuildManager: Missing MainGrid or ProbeDots component!");
            enabled = false;
            return;
        }

        // Wait for grid initialization then setup
        StartCoroutine(InitializeSystem());
    }

    private System.Collections.IEnumerator InitializeSystem()
    {
        // Wait for grid to be created
        yield return new WaitForSeconds(0.1f);

        // Copy grid configuration
        gridSize = mainGrid.GridSize;
        cellSize = mainGrid.CellSize;
        gridCenter = mainGrid.GridCenterPosition;
        halfWidth = mainGrid.TotalGridWidth / 2f;

        // Initialize grid point arrays
        int pointCount = gridSize + 1; // 9 points for 8x8 grid
        originalGridPoints = new Vector3[pointCount, pointCount];
        currentGridPoints = new Vector3[pointCount, pointCount];

        // Calculate original grid point positions
        CalculateOriginalGridPoints();

        // Hide the original grid lines (we'll create our own)
        HideOriginalGridLines();

        // Create LineRenderer pool
        CreateLineRendererPool();

        // Initialize probe position tracking and store original positions
        lastProbePositions = new Vector3[probeDots.probes.Count];
        for (int i = 0; i < probeDots.probes.Count; i++)
        {
            GameObject probe = probeDots.probes[i];
            Vector3 originalPos = probe.transform.position;
            lastProbePositions[i] = originalPos;
            probeOriginalPositions[probe] = originalPos;
        }

        // Initial rebuild
        needsRebuild = true;
        RebuildGrid();
    }

    void LateUpdate()
    {
        if (!enableDeformation)
            return;

        // Check if any probe has moved
        if (CheckProbesMoved())
        {
            needsRebuild = true;
        }

        // Rebuild grid if needed
        if (needsRebuild)
        {
            RebuildGrid();
            needsRebuild = false;
        }
    }

    /// <summary>
    /// Calculate the original (undeformed) grid point positions
    /// </summary>
    private void CalculateOriginalGridPoints()
    {
        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                float x = originX + col * cellSize;
                float y = originY + row * cellSize;
                float z = gridCenter.z;

                originalGridPoints[row, col] = new Vector3(x, y, z);
                currentGridPoints[row, col] = originalGridPoints[row, col];
            }
        }
    }

    /// <summary>
    /// Hide the original grid lines created by MainGrid
    /// </summary>
    private void HideOriginalGridLines()
    {
        Transform gridLinesParent = mainGrid.transform.Find("GridLines");
        if (gridLinesParent != null)
        {
            gridLinesParent.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Create a pool of LineRenderers for efficient grid rebuilding
    /// </summary>
    private void CreateLineRendererPool()
    {
        int pointCount = gridSize + 1;

        // Create horizontal line renderers (one for each row)
        for (int row = 0; row < pointCount; row++)
        {
            GameObject lineObj = new GameObject($"HorizontalLine_{row}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, pointCount);
            horizontalLinePool.Add(lr);
        }

        // Create vertical line renderers (one for each column)
        for (int col = 0; col < pointCount; col++)
        {
            GameObject lineObj = new GameObject($"VerticalLine_{col}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, pointCount);
            verticalLinePool.Add(lr);
        }
    }

    /// <summary>
    /// Configure a LineRenderer with standard settings
    /// </summary>
    private void ConfigureLineRenderer(LineRenderer lr, int pointCount)
    {
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.positionCount = pointCount;
        lr.useWorldSpace = true;
    }

    /// <summary>
    /// Check if any probe has moved since last frame
    /// </summary>
    private bool CheckProbesMoved()
    {
        // Safety check: ensure arrays are initialized and sized correctly
        if (lastProbePositions == null || probeDots == null || probeDots.probes == null)
            return false;

        // If probe count changed, reinitialize the tracking array
        if (lastProbePositions.Length != probeDots.probes.Count)
        {
            lastProbePositions = new Vector3[probeDots.probes.Count];
            for (int i = 0; i < probeDots.probes.Count; i++)
            {
                GameObject probe = probeDots.probes[i];
                if (probe != null && probe.transform != null)
                {
                    lastProbePositions[i] = probe.transform.position;
                }
            }
            return false;
        }

        for (int i = 0; i < probeDots.probes.Count; i++)
        {
            GameObject probe = probeDots.probes[i];

            // Multiple safety checks for Unity destroyed objects
            if (probe == null)
                continue;

            if (probe.transform == null)
                continue;

            if (!probe.activeInHierarchy)
                continue;

            Vector3 currentPos = probe.transform.position;
            if (Vector3.Distance(currentPos, lastProbePositions[i]) > 0.001f)
            {
                lastProbePositions[i] = currentPos;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Main rebuild function - recalculates all grid points and updates all lines
    /// </summary>
    private void RebuildGrid()
    {
        // Step 1: Calculate deformed grid point positions
        CalculateDeformedGridPoints();

        // Step 2: Update all horizontal lines
        UpdateHorizontalLines();

        // Step 3: Update all vertical lines
        UpdateVerticalLines();
    }

    /// <summary>
    /// Calculate grid point positions based on probe displacements
    /// Deformation stops at neighboring probes
    /// </summary>
    private void CalculateDeformedGridPoints()
    {
        int pointCount = gridSize + 1;

        // Reset all points to original positions first
        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                currentGridPoints[row, col] = originalGridPoints[row, col];
            }
        }

        // For each probe, deform the grid but stop at neighboring probes
        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null || !probe.activeInHierarchy)
                continue;

            // Get probe's current position and original position
            Vector3 probeCurrentPos = probe.transform.position;
            Vector3 probeOriginalPos = GetProbeOriginalPosition(probe);
            Vector3 probeDisplacement = probeCurrentPos - probeOriginalPos;

            // Skip if probe hasn't moved significantly
            if (probeDisplacement.magnitude < 0.001f)
                continue;

            // Get probe's grid indices (which row/col intersection it's on)
            Vector2Int probeGridIndex = GetProbeGridIndex(probe);
            int probeRow = probeGridIndex.y;
            int probeCol = probeGridIndex.x;

            // Find neighboring probes to limit deformation extent
            int leftLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, -1, 2);  // Left
            int rightLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, 1, 2);  // Right
            int upLimit = FindNearestProbeInDirection(probeRow, probeCol, 1, 0, 2);     // Up
            int downLimit = FindNearestProbeInDirection(probeRow, probeCol, -1, 0, 2);  // Down

            // Deform horizontal line (same row, varying columns)
            // LEFT direction
            for (int col = probeCol; col >= probeCol - leftLimit; col--)
            {
                if (col < 0 || col >= pointCount) continue;
                if (col == 0 || col == gridSize) continue; // Boundary constraint

                int distance = probeCol - col;
                float weight = GetDeformationWeight(distance);
                currentGridPoints[probeRow, col] += probeDisplacement * weight;
            }

            // RIGHT direction
            for (int col = probeCol + 1; col <= probeCol + rightLimit; col++)
            {
                if (col < 0 || col >= pointCount) continue;
                if (col == 0 || col == gridSize) continue; // Boundary constraint

                int distance = col - probeCol;
                float weight = GetDeformationWeight(distance);
                currentGridPoints[probeRow, col] += probeDisplacement * weight;
            }

            // Deform vertical line (same column, varying rows)
            // DOWN direction
            for (int row = probeRow - 1; row >= probeRow - downLimit; row--)
            {
                if (row < 0 || row >= pointCount) continue;
                if (row == 0 || row == gridSize) continue; // Boundary constraint

                int distance = probeRow - row;
                float weight = GetDeformationWeight(distance);
                currentGridPoints[row, probeCol] += probeDisplacement * weight;
            }

            // UP direction
            for (int row = probeRow + 1; row <= probeRow + upLimit; row++)
            {
                if (row < 0 || row >= pointCount) continue;
                if (row == 0 || row == gridSize) continue; // Boundary constraint

                int distance = row - probeRow;
                float weight = GetDeformationWeight(distance);
                currentGridPoints[row, probeCol] += probeDisplacement * weight;
            }
        }
    }

    /// <summary>
    /// Get deformation weight based on distance from probe
    /// </summary>
    private float GetDeformationWeight(int distance)
    {
        if (distance == 0) return 1.0f;   // 100% at probe
        if (distance == 1) return 0.66f;  // 66% at 1 cell
        if (distance == 2) return 0.33f;  // 33% at 2 cells
        return 0f;                         // No deformation beyond 2 cells
    }

    /// <summary>
    /// Find nearest probe in a given direction (row/col direction)
    /// Returns the maximum distance to deform (stops BEFORE neighboring probe)
    /// </summary>
    private int FindNearestProbeInDirection(int startRow, int startCol, int rowDir, int colDir, int maxDistance)
    {
        // Check each position in the direction up to maxDistance
        for (int dist = 1; dist <= maxDistance; dist++)
        {
            int checkRow = startRow + (rowDir * dist);
            int checkCol = startCol + (colDir * dist);

            // Check if there's a probe at this position
            foreach (GameObject otherProbe in probeDots.probes)
            {
                if (otherProbe == null || !otherProbe.activeInHierarchy)
                    continue;

                Vector2Int otherProbeIndex = GetProbeGridIndex(otherProbe);

                // If we found a probe at this position, stop BEFORE it
                if (otherProbeIndex.y == checkRow && otherProbeIndex.x == checkCol)
                {
                    // Return dist - 1 to stop before the probe (never negative)
                    return Mathf.Max(0, dist - 1);
                }
            }
        }

        // No probe found within maxDistance, return full distance
        return maxDistance;
    }

    /// <summary>
    /// Get the original position of a probe (before any movement)
    /// </summary>
    private Vector3 GetProbeOriginalPosition(GameObject probe)
    {
        // Return stored original position if available
        if (probeOriginalPositions.ContainsKey(probe))
        {
            return probeOriginalPositions[probe];
        }

        // Fallback: return current position (shouldn't happen in normal operation)
        Debug.LogWarning($"GridRebuildManager: No original position found for probe {probe.name}");
        return probe.transform.position;
    }

    /// <summary>
    /// Get the grid row/column indices that a probe is on (based on original position)
    /// </summary>
    private Vector2Int GetProbeGridIndex(GameObject probe)
    {
        Vector3 probeOriginalPos = GetProbeOriginalPosition(probe);

        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        // Calculate which grid line indices the probe is on
        int col = Mathf.RoundToInt((probeOriginalPos.x - originX) / cellSize);
        int row = Mathf.RoundToInt((probeOriginalPos.y - originY) / cellSize);

        // Clamp to valid range
        col = Mathf.Clamp(col, 0, gridSize);
        row = Mathf.Clamp(row, 0, gridSize);

        return new Vector2Int(col, row);
    }

    /// <summary>
    /// Update all horizontal lines based on current grid points
    /// </summary>
    private void UpdateHorizontalLines()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            LineRenderer lr = horizontalLinePool[row];

            // Set all points along this row
            for (int col = 0; col < pointCount; col++)
            {
                lr.SetPosition(col, currentGridPoints[row, col]);
            }
        }
    }

    /// <summary>
    /// Update all vertical lines based on current grid points
    /// </summary>
    private void UpdateVerticalLines()
    {
        int pointCount = gridSize + 1;

        for (int col = 0; col < pointCount; col++)
        {
            LineRenderer lr = verticalLinePool[col];

            // Set all points along this column
            for (int row = 0; row < pointCount; row++)
            {
                lr.SetPosition(row, currentGridPoints[row, col]);
            }
        }
    }

    /// <summary>
    /// Force an immediate rebuild (called from external scripts)
    /// </summary>
    public void ForceRebuild()
    {
        needsRebuild = true;
    }

    /// <summary>
    /// Reset grid to original undeformed state
    /// </summary>
    public void ResetGrid()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                currentGridPoints[row, col] = originalGridPoints[row, col];
            }
        }

        needsRebuild = true;
    }
}
