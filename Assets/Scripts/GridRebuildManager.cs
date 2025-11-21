using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class GridRebuildManager : MonoBehaviour
{
    private MainGrid mainGrid;
    private ProbeDots probeDots;
    private GameObject centerFixationPoint;

    private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    private Vector3[,] originalGridPoints;
    private Vector3[,] currentGridPoints;

    public List<LineRenderer> horizontalLinePool = new List<LineRenderer>();
    public List<LineRenderer> verticalLinePool = new List<LineRenderer>();

    public bool enableDeformation = true;

    private bool needsRebuild = false;
    private Vector3[] lastProbePositions;

    public Dictionary<GameObject, Vector3> probeOriginalPositions = new Dictionary<GameObject, Vector3>();

    private List<GameObject> allProbes = new List<GameObject>();
    
    private Dictionary<GameObject, int> probeInfluenceRadius = new Dictionary<GameObject, int>();
    
    private Dictionary<GameObject, Vector2Int> probeGridIndices = new Dictionary<GameObject, Vector2Int>();

    private float lineWidth = 0.15f;
    private Color lineColor = Color.white;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();

        if (mainGrid == null || probeDots == null)
        {
            enabled = false;
            return;
        }

        StartCoroutine(InitializeSystem());
    }

    private System.Collections.IEnumerator InitializeSystem()
    {
        yield return new WaitForSeconds(0.1f);

        gridSize = mainGrid.GridSize;
        cellSize = mainGrid.CellSize;
        gridCenter = mainGrid.GridCenterPosition;
        halfWidth = mainGrid.TotalGridWidth / 2f;

        centerFixationPoint = GameObject.Find("CenterFixationPoint");
        if (centerFixationPoint == null) yield break;

        int pointCount = gridSize + 1;
        originalGridPoints = new Vector3[pointCount, pointCount];
        currentGridPoints = new Vector3[pointCount, pointCount];

        CalculateOriginalGridPoints();

        HideOriginalGridLines();

        CreateLineRendererPool();

        lastProbePositions = new Vector3[probeDots.probes.Count];
        for (int i = 0; i < probeDots.probes.Count; i++)
        {
            GameObject probe = probeDots.probes[i];
            Vector3 originalPos = probe.transform.position;
            lastProbePositions[i] = originalPos;
            RegisterProbe(probe, originalPos);
        }

        needsRebuild = true;
        RebuildGrid();
    }

    void LateUpdate()
    {
        if (!enableDeformation)
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

    private void HideOriginalGridLines()
    {
        Transform gridLinesParent = mainGrid.transform.Find("GridLines");
        if (gridLinesParent != null)
        {
            Destroy(gridLinesParent.gameObject);
        }
    }

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

    private bool CheckProbesMoved()
    {
        if (allProbes == null || allProbes.Count == 0)
            return false;

        if (lastProbePositions == null || lastProbePositions.Length != allProbes.Count)
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

        for (int i = 0; i < allProbes.Count; i++)
        {
            GameObject probe = allProbes[i];

            if (probe == null)
                continue;

            if (probe.transform == null)
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

    private void RebuildGrid()
    {
        CalculateDeformedGridPoints();

        UpdateHorizontalLines();

        UpdateVerticalLines();
        
        UpdateProbePositionsToGrid();
    }

    private void UpdateProbePositionsToGrid()
    {
        if (probeDots == null)
            return;

        foreach (GameObject probe in allProbes)
        {
            if (probe == null || !probe.activeInHierarchy)
                continue;

            int probeIndex = probeDots.probes.IndexOf(probe);
            if (probeIndex == probeDots.selectedProbeIndex)
                continue;

            if (!probeGridIndices.ContainsKey(probe))
                continue;

            Vector2Int gridIndex = probeGridIndices[probe];
            
            if (gridIndex.y < 0 || gridIndex.y > gridSize || gridIndex.x < 0 || gridIndex.x > gridSize)
                continue;
            
            Vector3 deformedPos = currentGridPoints[gridIndex.y, gridIndex.x];
            
            float probeZ = gridCenter.z - 0.15f;
            deformedPos.z = probeZ;
            
            Vector3 currentProbePos = probe.transform.position;
            Vector3 expectedDeformedPos = deformedPos;
            
            float manualAdjustmentThreshold = 0.01f;
            if (Vector3.Distance(currentProbePos, expectedDeformedPos) < manualAdjustmentThreshold)
            {
                probe.transform.position = deformedPos;
            }
        }
    }

    private void CalculateDeformedGridPoints()
    {
        int pointCount = gridSize + 1;

        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                currentGridPoints[row, col] = originalGridPoints[row, col];
            }
        }

        foreach (GameObject probe in allProbes)
        {
            if (probe == null)
                continue;

            Vector3 probeCurrentPos = probe.transform.position;
            Vector3 probeOriginalPos = GetProbeOriginalPosition(probe);
            Vector3 probeDisplacement = probeCurrentPos - probeOriginalPos;

            if (probeDisplacement.magnitude < 0.001f)
                continue;

            Vector2Int probeGridIndex = GetProbeGridIndex(probe);
            int probeRow = probeGridIndex.y;
            int probeCol = probeGridIndex.x;

            int maxInfluenceRadius = 2;
            if (probeInfluenceRadius.ContainsKey(probe))
            {
                maxInfluenceRadius = probeInfluenceRadius[probe];
            }

            int leftLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, -1, maxInfluenceRadius, probe);
            int rightLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, 1, maxInfluenceRadius, probe);
            int upLimit = FindNearestProbeInDirection(probeRow, probeCol, 1, 0, maxInfluenceRadius, probe);
            int downLimit = FindNearestProbeInDirection(probeRow, probeCol, -1, 0, maxInfluenceRadius, probe);

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

                    if (IsProbeAtGridPoint(row, col, probe))
                        continue;

                    if (IsCenterFixationAtGridPoint(row, col))
                        continue;

                    int deltaRow = row - probeRow;
                    int deltaCol = col - probeCol;

                    float colWeight = CalculateAdaptiveWeight(deltaCol, -leftLimit, rightLimit);
                    float rowWeight = CalculateAdaptiveWeight(deltaRow, -downLimit, upLimit);

                    float combinedWeight = colWeight * rowWeight;

                    currentGridPoints[row, col] += probeDisplacement * combinedWeight;
                }
            }
        }
    }

    private float CalculateAdaptiveWeight(int delta, int negativeLimit, int positiveLimit)
    {
        if (delta == 0)
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

    private bool IsProbeAtGridPoint(int row, int col, GameObject currentProbe)
    {
        foreach (GameObject probe in allProbes)
        {
            if (probe == null)
                continue;

            if (probe == currentProbe)
                continue;

            Vector2Int probeIndex = GetProbeGridIndex(probe);
            if (probeIndex.y == row && probeIndex.x == col)
                return true;
        }
        return false;
    }

    private bool IsCenterFixationAtGridPoint(int row, int col)
    {
        if (centerFixationPoint == null || !centerFixationPoint.activeInHierarchy)
            return false;

        Vector2Int centerIndex = GetProbeGridIndex(centerFixationPoint);
        return (centerIndex.y == row && centerIndex.x == col);
    }

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

            if (centerFixationPoint != null && centerFixationPoint.activeInHierarchy)
            {
                Vector2Int centerIndex = GetProbeGridIndex(centerFixationPoint);
                if (centerIndex.y == checkRow && centerIndex.x == checkCol)
                {
                    return dist;
                }
            }

            foreach (GameObject otherProbe in allProbes)
            {
                if (otherProbe == null)
                    continue;

                if (otherProbe == currentProbe)
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

    private int GetProbeIteration(GameObject probe)
    {
        if (probe == null)
            return 1;

        if (probeInfluenceRadius.ContainsKey(probe))
        {
            int radius = probeInfluenceRadius[probe];
            return (radius == 2) ? 1 : 2;
        }

        return 1;
    }

    private Vector3 GetProbeOriginalPosition(GameObject probe)
    {
        if (probeOriginalPositions.ContainsKey(probe))
        {
            return probeOriginalPositions[probe];
        }
        return probe.transform.position;
    }

    private Vector2Int GetProbeGridIndex(GameObject obj)
    {
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

    public void ForceRebuild()
    {
        needsRebuild = true;
    }

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

    public Vector2Int GetProbeGridCell(GameObject probe)
    {
        return GetProbeGridIndex(probe);
    }

    public Vector3 GetDeformedGridPoint(int row, int col)
    {
        if (currentGridPoints != null && row >= 0 && row <= gridSize && col >= 0 && col <= gridSize)
        {
            return currentGridPoints[row, col];
        }
        return Vector3.zero;
    }

    public int GetGridSize()
    {
        return gridSize;
    }

    public Vector3 GetOriginalGridPoint(int row, int col)
    {
        if (originalGridPoints != null && row >= 0 && row <= gridSize && col >= 0 && col <= gridSize)
        {
            return originalGridPoints[row, col];
        }
        return Vector3.zero;
    }

    public void RegisterProbe(GameObject probe, Vector3 originalPosition, int iterationLevel = 1, Vector2Int? gridIndex = null)
    {
        if (probe != null && !allProbes.Contains(probe))
        {
            allProbes.Add(probe);
            probeOriginalPositions[probe] = originalPosition;
            
            int influenceRadius = (iterationLevel == 1) ? 2 : 1;
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

    public int GetProbeInfluenceRadius(GameObject probe)
    {
        if (probe != null && probeInfluenceRadius.ContainsKey(probe))
        {
            return probeInfluenceRadius[probe];
        }
        return 2;
    }
}