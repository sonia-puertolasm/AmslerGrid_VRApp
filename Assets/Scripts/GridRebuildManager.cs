using System.Collections.Generic;
using UnityEngine;

public class GridRebuildManager : MonoBehaviour
{
    // Retrieval of grid configuration specific parameters
    private MainGrid mainGrid;
    private ProbeDots probeDots;
    private GameObject centerFixationPoint;
    private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    // Defintion of grid points specific parameters
    private Vector3[,] originalGridPoints;
    private Vector3[,] currentGridPoints;

    // Definition of displacement-related parameters
    private Vector3[,] accumulatedDisplacement;
    private Vector3[] lastProbePositions;

    // Creation of pool of horizontal and vertical lines for deformed grid
    internal List<LineRenderer> horizontalLinePool = new List<LineRenderer>();
    internal List<LineRenderer> verticalLinePool = new List<LineRenderer>();

    // Definition of deformation-related parameters
    internal bool enableDeformation = true;
    private bool needsRebuild = false;

    // Definition of lists and dictionaries for probe-dot related displacement and positioning
    public Dictionary<GameObject, Vector3> probeOriginalPositions = new Dictionary<GameObject, Vector3>();
    private List<GameObject> allProbes = new List<GameObject>();
    public Dictionary<GameObject, int> probeInfluenceRadius = new Dictionary<GameObject, int>();
    public Dictionary<GameObject, Vector2Int> probeGridIndices = new Dictionary<GameObject, Vector2Int>();

    // Definition of grid-design specific parameters
    private float lineWidth = 0.15f;
    private Color lineColor = Color.white;

    // METHOD: Initialization of all grid-reconstruction methods
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();

        if (mainGrid == null || probeDots == null) // Safety: Avoids execution in case the main grid and/or probe dots don't exist
        {
            enabled = false;
            return;
        }

        AlignToGridCenter();
        StartCoroutine(InitializeSystem());
    }

    // METHOD: Coroutine for obtaining of grid metadata and building of line pools
    private System.Collections.IEnumerator InitializeSystem()
    {
        yield return new WaitForSeconds(0.1f); // Safety: Waits for 0.1 seconds before proceeding for ensuring a spawn of the scene objects

        // Obtaining of core metadata from the grid
        gridSize = mainGrid.GridSize;
        cellSize = mainGrid.CellSize;
        gridCenter = mainGrid.GridCenterPosition;
        halfWidth = mainGrid.TotalGridWidth / 2f;
        centerFixationPoint = GameObject.Find("CenterFixationPoint");

        if (centerFixationPoint == null) yield break; // Safety: If center fixation point is absent -> early start to avoid null references

        int pointCount = gridSize + 1; // Calculation of points and allocation of arrays for coordinates
        originalGridPoints = new Vector3[pointCount, pointCount];
        currentGridPoints = new Vector3[pointCount, pointCount];
        accumulatedDisplacement = new Vector3[pointCount, pointCount];

        // Call of methods for definition of deformable grid
        CalculateOriginalGridPoints();
        HideOriginalGridLines();
        CreateLineRendererPool();

        lastProbePositions = new Vector3[probeDots.probes.Count];
        for (int i = 0; i < probeDots.probes.Count; i++) // Loop through every probe dot for capturing their initial state
        {
            GameObject probe = probeDots.probes[i];
            Vector3 originalPos = probe.transform.position;
            lastProbePositions[i] = originalPos;
            RegisterProbe(probe, originalPos);
        }

        needsRebuild = true;
        RebuildGrid();

        if (centerFixationPoint != null) // Safety: Ensures the action is performed only under existance of the center fixation point
        {
            Renderer renderer = centerFixationPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }
    }

    // METHOD: Aligning of reconstructed/deformed grid to center
    private void AlignToGridCenter()
    {
        if (mainGrid == null) // Safety: Exits in case of the main grid not existing
        {
            return;
        }

        transform.position = mainGrid.GridCenterPosition; // Defines center = main grid center
        transform.rotation = Quaternion.identity; // Resets rotation
    }

    // METHOD: Once per frame prevents redundant rebuilds until another probe movement is detected
    void LateUpdate()
    {
        if (!enableDeformation) // Safety: Ensures avoiding construction of deformable grid if deformation is not enabled
            return;

        if (CheckProbesMoved()) 
        {
            needsRebuild = true;
        }

        if (needsRebuild)
        {
            RebuildGrid();
            needsRebuild = false;
        }
    }

    // METHOD: Rebuilding of a square lattice of points centered in the center of the grid
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
                accumulatedDisplacement[row, col] = Vector3.zero;
            }
        }
    }

    // METHOD: Removal of original grid lines for introduction of deformed grid
    private void HideOriginalGridLines()
    {
        Transform gridLinesParent = mainGrid.transform.Find("GridLines");

        if (gridLinesParent != null)
        {
            Destroy(gridLinesParent.gameObject);
        }
    }

    // METHOD: Creation of pool of lines for deformed grid
    private void CreateLineRendererPool()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            GameObject lineObj = new GameObject($"HorizontalLine_{row}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, pointCount);
            horizontalLinePool.Add(lr);
        }

        for (int col = 0; col < pointCount; col++)
        {
            GameObject lineObj = new GameObject($"VerticalLine_{col}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();
            ConfigureLineRenderer(lr, pointCount);
            verticalLinePool.Add(lr);
        }
    }

    // HELPER METHOD: Definition of LineRenderer-specific parameters
    private void ConfigureLineRenderer(LineRenderer lr, int pointCount)
    {
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lineColor;
        lr.endColor = lineColor;
        lr.positionCount = pointCount;
        lr.useWorldSpace = false;
    }

    // METHOD: Detects whether any probe dot has shifted since the previous frame
    private bool CheckProbesMoved()
    {
        if (allProbes == null || allProbes.Count == 0) // Safety: Ensure there is a probe list to inspect, if not exit
            return false;

        if (lastProbePositions == null || lastProbePositions.Length != allProbes.Count) //
        {
            lastProbePositions = new Vector3[allProbes.Count];
            for (int i = 0; i < allProbes.Count; i++) 
            {
                GameObject probe = allProbes[i];
                if (probe != null && probe.transform != null)
                {
                    lastProbePositions[i] = probe.transform.position;
                }
            }
            return false;
        }
        for (int i = 0; i < allProbes.Count; i++) // Looping over each probe to compare positions
        {
            GameObject probe = allProbes[i];

            if (probe == null) // Safety: Avoids analysis of non-existing probes
                continue;

            if (probe.transform == null)
                continue;

            Vector3 currentPos = probe.transform.position;
            if (Vector3.Distance(currentPos, lastProbePositions[i]) > 0.001f) // If displacement > 0.001 (all cases), considered as probe dot movement
            {
                lastProbePositions[i] = currentPos;
                return true;
            }
        }
        return false;
    }

    // METHOD: Calls to all methods for grid rebuild
    private void RebuildGrid()
    {
        CalculateDisplacementContributions();
        ApplyAccumulatedDisplacement();
        UpdateHorizontalLines();
        UpdateVerticalLines();
        UpdateAllProbesToGrid();
    }

    // HELPER METHOD: Calculates the displacement performed by the probe dots
    private void CalculateDisplacementContributions()
    {
        if (probeDots == null || probeDots.selectedProbeIndex < 0) // Safety: Ensures calculation only when the probe dots are valid
            return;

        int pointCount = gridSize + 1;
        GameObject selectedProbe = probeDots.probes[probeDots.selectedProbeIndex];
        
        if (selectedProbe == null) // Safety: Avoids action in case there is no selected probe
            return;

        Vector3 probeCurrentPos = selectedProbe.transform.position;
        Vector3 probeOriginalPos = GetProbeOriginalPosition(selectedProbe);
        
        Vector3 totalProbeDisplacement = probeCurrentPos - probeOriginalPos;
        
        Vector2Int probeGridIndex = GetProbeGridIndex(selectedProbe);
        int probeRow = probeGridIndex.y;
        int probeCol = probeGridIndex.x;
        
        Vector3 gridPointCurrentDisplacement = accumulatedDisplacement[probeRow, probeCol];
        Vector3 newDisplacementFromProbe = totalProbeDisplacement - gridPointCurrentDisplacement;

        if (newDisplacementFromProbe.magnitude < 0.001f)
            return;

        int maxInfluenceRadius = 2;
        if (probeInfluenceRadius.ContainsKey(selectedProbe))
        {
            maxInfluenceRadius = probeInfluenceRadius[selectedProbe];
        }

        int leftLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, -1, maxInfluenceRadius, selectedProbe);
        int rightLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, 1, maxInfluenceRadius, selectedProbe);
        int upLimit = FindNearestProbeInDirection(probeRow, probeCol, 1, 0, maxInfluenceRadius, selectedProbe);
        int downLimit = FindNearestProbeInDirection(probeRow, probeCol, -1, 0, maxInfluenceRadius, selectedProbe);

        int minCol = Mathf.Max(0, probeCol - leftLimit);
        int maxCol = Mathf.Min(gridSize, probeCol + rightLimit);
        int minRow = Mathf.Max(0, probeRow - downLimit);
        int maxRow = Mathf.Min(gridSize, probeRow + upLimit);

        for (int row = minRow; row <= maxRow; row++)
        {
            for (int col = minCol; col <= maxCol; col++)
            {
                if (row == 0 || row == gridSize || col == 0 || col == gridSize)
                    continue;

                if (IsCenterFixationAtGridPoint(row, col))
                    continue;

                int deltaRow = row - probeRow;
                int deltaCol = col - probeCol;

                float colWeight = CalculateAdaptiveWeight(deltaCol, -leftLimit, rightLimit);
                float rowWeight = CalculateAdaptiveWeight(deltaRow, -downLimit, upLimit);
                float combinedWeight = colWeight * rowWeight;

                accumulatedDisplacement[row, col] += newDisplacementFromProbe * combinedWeight;
            }
        }
    }

    // HELPER METHOD: Incorporates displacement for each grid point
    private void ApplyAccumulatedDisplacement()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                currentGridPoints[row, col] = originalGridPoints[row, col] + accumulatedDisplacement[row, col];
            }
        }
    }

    // METHOD: Introduces the probe dots to the deformable grid
    private void UpdateAllProbesToGrid()
    {
        if (probeDots == null) // Safety: Avoids action in case of the probe dots not existing
            return;

        foreach (GameObject probe in allProbes) // Iterates over each probe dot
        {
            if (probe == null || !probe.activeInHierarchy) // Safets: Avoids use of non-valid probe dots
                continue;

            int probeIndex = probeDots.probes.IndexOf(probe);
            if (probeIndex == probeDots.selectedProbeIndex)
                continue;

            if (!probeGridIndices.ContainsKey(probe)) // Safety: Avoids invalid indexes of probe dots being used
                continue;

            Vector2Int gridIndex = probeGridIndices[probe];
            
            if (gridIndex.y < 0 || gridIndex.y > gridSize || gridIndex.x < 0 || gridIndex.x > gridSize) // If position is within boundaries
                continue;
            
            Vector3 deformedPos = currentGridPoints[gridIndex.y, gridIndex.x];
            float probeZ = gridCenter.z;
            deformedPos.z = probeZ;
            
            probe.transform.position = deformedPos;
        }
    }

    // HELPER METHOD: Contributes to a smooth approach in the reconstruction of the deformed grid
    private float CalculateAdaptiveWeight(int delta, int negativeLimit, int positiveLimit)
    {
        if (delta == 0) // When the cell offset = 0, full infleunce at the center
            return 1.0f;

        float maxDist;
        float currentDist = Mathf.Abs(delta);

        if (delta < 0)
        {
            maxDist = Mathf.Abs(negativeLimit);
        }
        else
        {
            maxDist = Mathf.Abs(positiveLimit);
        }

        if (maxDist < 0.001f)
            return 0.0f;

        float normalized = currentDist / maxDist;

        if (normalized > 1.0f)
            return 0.0f;

        float t = normalized;
        float weight = 1.0f - (t * t * (3.0f - 2.0f * t));

        return weight;
    }

    // HELPER METHOD: Defines by bool if there is another probe dot is located on top of a grid point to avoid overlapping
    private bool IsProbeAtGridPoint(int row, int col, GameObject currentProbe)
    {
        foreach (GameObject probe in allProbes)
        {
            if (probe == null) // Safety: Avoids analysing non-existing probes
                continue;

            if (probe == currentProbe) // Safety: Avoids analysing same probe
                continue;

            Vector2Int probeIndex = GetProbeGridIndex(probe);
            if (probeIndex.y == row && probeIndex.x == col)
                return true;
        }
        return false;
    }

    // HELPER METHOD: Proofs if the center fixation point is located on top of a grid point to avoid overlapping
    private bool IsCenterFixationAtGridPoint(int row, int col)
    {
        if (centerFixationPoint == null || !centerFixationPoint.activeInHierarchy) // Safety: Ensures avoiding analysis of non-existing elements
            return false;

        Vector2Int centerIndex = GetProbeGridIndex(centerFixationPoint);
        return (centerIndex.y == row && centerIndex.x == col);
    }

    // HELPER METHOD: Locate the nearest probe dot in specific direction
    private int FindNearestProbeInDirection(int startRow, int startCol, int rowDir, int colDir, int maxDistance, GameObject currentProbe)
    {
        int currentProbeIteration = GetProbeIteration(currentProbe);
        
        for (int dist = 1; dist <= maxDistance; dist++)
        {
            int checkRow = startRow + (rowDir * dist);
            int checkCol = startCol + (colDir * dist);

            if (checkRow < 0 || checkRow > gridSize || checkCol < 0 || checkCol > gridSize)
            {
                return dist;
            }

            if (centerFixationPoint != null && centerFixationPoint.activeInHierarchy) // Safety: Ensures the center fixation point exists before proceeding
            {
                Vector2Int centerIndex = GetProbeGridIndex(centerFixationPoint);
                if (centerIndex.y == checkRow && centerIndex.x == checkCol)
                {
                    return dist;
                }
            }

            foreach (GameObject otherProbe in allProbes)
            {
                if (otherProbe == null) // Safety: Avoids analysis if probe doesn't exist
                    continue; 

                if (otherProbe == currentProbe) // Safety: Avoids analysis if probe doesn't exist
                    continue;

                int otherProbeIteration = GetProbeIteration(otherProbe);
                if (otherProbeIteration > currentProbeIteration)
                    continue;

                Vector2Int otherProbeIndex = GetProbeGridIndex(otherProbe);

                if (otherProbeIndex.y == checkRow && otherProbeIndex.x == checkCol)
                {
                    return dist;
                }
            }
        }

        return maxDistance;
    }

    // HELPER METHOD: Extraction of probe iteration
    private int GetProbeIteration(GameObject probe)
    {
        if (probe == null) // Safety: Avoids analysis in case of non-existing probe
            return 1;

        if (probeInfluenceRadius.ContainsKey(probe))
        {
            int radius = probeInfluenceRadius[probe];
            return (radius == 2) ? 1 : 2;
        }

        return 1;
    }

    // HELPER METHOD: Extraction of original position of probe
    private Vector3 GetProbeOriginalPosition(GameObject probe)
    {
        if (probeOriginalPositions.ContainsKey(probe))
        {
            return probeOriginalPositions[probe];
        }
        return probe.transform.position;
    }

    // HELPER METHOD: Extraction of probe dot index
    private Vector2Int GetProbeGridIndex(GameObject obj)
    {
        if (probeGridIndices.ContainsKey(obj)) // Look for the probe dot to avoid recomputation in case the index has already been extracted
        {
            return probeGridIndices[obj];
        }

        Vector3 objOriginalPos;

        if (probeOriginalPositions.ContainsKey(obj))
        {
            objOriginalPos = probeOriginalPositions[obj];
        }
        else if (obj == centerFixationPoint)
        {
            objOriginalPos = mainGrid.GridCenterPosition;
        }
        else
        {
            objOriginalPos = obj.transform.position;
        }

        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        int col = Mathf.RoundToInt((objOriginalPos.x - originX) / cellSize);
        int row = Mathf.RoundToInt((objOriginalPos.y - originY) / cellSize);

        col = Mathf.Clamp(col, 0, gridSize);
        row = Mathf.Clamp(row, 0, gridSize);

        return new Vector2Int(col, row);
    }

    // METHOD: Updates the pool of horizontal lines
    public void UpdateHorizontalLines()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            LineRenderer lr = horizontalLinePool[row];

            for (int col = 0; col < pointCount; col++)
            {
                lr.SetPosition(col, currentGridPoints[row, col]);
            }
        }
    }

    // METHOD: Updates the pool of horizontal lines
    public void UpdateVerticalLines()
    {
        int pointCount = gridSize + 1;

        for (int col = 0; col < pointCount; col++)
        {
            LineRenderer lr = verticalLinePool[col];

            for (int row = 0; row < pointCount; row++)
            {
                lr.SetPosition(row, currentGridPoints[row, col]);
            }
        }
    }

    // HELPER METHOD: Introduces rebuild parameter
    public void ForceRebuild()
    {
        needsRebuild = true;
    }

    // METHOD: Performs a resetting of the grid through iteration over rows and columns
    public void ResetGrid()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                accumulatedDisplacement[row, col] = Vector3.zero;
                currentGridPoints[row, col] = originalGridPoints[row, col];
            }
        }

        needsRebuild = true;
    }

    // HELPER METHOD: Obtain Amsler grid cell given a probe dot
    public Vector2Int GetProbeGridCell(GameObject probe)
    {
        return GetProbeGridIndex(probe);
    }

    // HELPER METHOD: Extract deformed point given the grid row, col position
    public Vector3 GetDeformedGridPoint(int row, int col)
    {
        if (currentGridPoints != null && row >= 0 && row <= gridSize && col >= 0 && col <= gridSize)
        {
            return currentGridPoints[row, col];
        }
        return Vector3.zero;
    }

    // HELPER METHOD: Extraction of the grid size
    public int GetGridSize()
    {
        return gridSize;
    }

    // HELPER METHOD: Extraction of the original grid points
    public Vector3 GetOriginalGridPoint(int row, int col)
    {
        if (originalGridPoints != null && row >= 0 && row <= gridSize && col >= 0 && col <= gridSize)
        {
            return originalGridPoints[row, col];
        }
        return Vector3.zero;
    }

    // HELPER METHOD: Extraction of accumulated displacement data
    public Vector3 GetAccumulatedDisplacement(int row, int col)
    {
        if (accumulatedDisplacement != null && row >= 0 && row <= gridSize && col >= 0 && col <= gridSize)
        {
            return accumulatedDisplacement[row, col];
        }
        return Vector3.zero;
    }

    // METHOD: Registers probe dot given valid reference and considering if it hasn't been tracked before
    public void RegisterProbe(GameObject probe, Vector3 originalPosition, int iterationLevel = 1, Vector2Int? gridIndex = null)
    {
        if (probe != null && !allProbes.Contains(probe))
        {
            allProbes.Add(probe);
            probeOriginalPositions[probe] = originalPosition; // Storage of probe dot's original position
            
            int influenceRadius = (iterationLevel == 1) ? 2 : 1; // Determine how far the probe dot should influence the grid
            probeInfluenceRadius[probe] = influenceRadius;
            
            if (gridIndex.HasValue)
            {
                probeGridIndices[probe] = gridIndex.Value;
            }
            else
            {
                Vector2Int calculatedIndex = GetProbeGridIndex(probe);
                probeGridIndices[probe] = calculatedIndex;
            }
        }
    }

    // HELPER METHOD: Ejects a probe dot from the tracking to keep the deformation cache consistent
    public void UnregisterProbe(GameObject probe)
    {
        if (probe != null)
        {
            allProbes.Remove(probe);
            probeOriginalPositions.Remove(probe);
            probeInfluenceRadius.Remove(probe);
            probeGridIndices.Remove(probe);
        }
    }

    // HELPER METHOD: Extracts influence radius of a probe dot
    public int GetProbeInfluenceRadius(GameObject probe)
    {
        if (probe != null && probeInfluenceRadius.ContainsKey(probe))
        {
            return probeInfluenceRadius[probe];
        }
        return 2;
    }
}