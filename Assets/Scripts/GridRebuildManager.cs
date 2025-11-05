using System.Collections.Generic;
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

    private Dictionary<GameObject, Vector3> probeOriginalPositions = new Dictionary<GameObject, Vector3>();

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
            probeOriginalPositions[probe] = originalPos;
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
        if (lastProbePositions == null || probeDots == null || probeDots.probes == null)
            return false;

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

            if (probe == null)
                continue;

            if (probe.transform == null)
                continue;

            // Track movement even for hidden probes (focus system)
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

        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null)
                continue;

            // Include probes even if they're hidden by the focus system
            Vector3 probeCurrentPos = probe.transform.position;
            Vector3 probeOriginalPos = GetProbeOriginalPosition(probe);
            Vector3 probeDisplacement = probeCurrentPos - probeOriginalPos;

            if (probeDisplacement.magnitude < 0.001f)
                continue;

            Vector2Int probeGridIndex = GetProbeGridIndex(probe);
            int probeRow = probeGridIndex.y;
            int probeCol = probeGridIndex.x;

            int leftLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, -1, 1);
            int rightLimit = FindNearestProbeInDirection(probeRow, probeCol, 0, 1, 1);
            int upLimit = FindNearestProbeInDirection(probeRow, probeCol, 1, 0, 1);
            int downLimit = FindNearestProbeInDirection(probeRow, probeCol, -1, 0, 1); 

            int minCol = Mathf.Max(1, probeCol - leftLimit);    
            int maxCol = Mathf.Min(gridSize - 1, probeCol + rightLimit); 
            int minRow = Mathf.Max(1, probeRow - downLimit);    
            int maxRow = Mathf.Min(gridSize - 1, probeRow + upLimit);   

            for (int row = minRow; row <= maxRow; row++)
            {
                for (int col = minCol; col <= maxCol; col++)
                {
                    if (row == 0 || row == gridSize || col == 0 || col == gridSize)
                        continue;

                    int deltaRow = Mathf.Abs(row - probeRow);
                    int deltaCol = Mathf.Abs(col - probeCol);

                    int distance = Mathf.Max(deltaRow, deltaCol);

                    if (IsProbeAtGridPoint(row, col, probe))
                        continue;

                    if (IsCenterFixationAtGridPoint(row, col))
                        continue;

                    float weight = GetDeformationWeight(distance);

                    float rowWeight = 1.0f;
                    float colWeight = 1.0f;

                    if (deltaRow > 0 && deltaCol > 0)
                    {
                        rowWeight = GetDeformationWeight(deltaRow);
                        colWeight = GetDeformationWeight(deltaCol);
                        weight = weight * 0.7f; // 
                    }

                    currentGridPoints[row, col] += probeDisplacement * weight * rowWeight * colWeight;
                }
            }
        }
    }

    private float GetDeformationWeight(int distance)
    {
        if (distance == 0) return 1.0f;   // At probe position: 100%
        if (distance == 1) return 0.66f;  // 1 cell away: 66%
        return 0f;                         // Beyond 1 cell: 0%
    }

    private bool IsProbeAtGridPoint(int row, int col, GameObject currentProbe)
    {
        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null)
                continue;

            if (probe == currentProbe)
                continue;

            // Check probe positions even if they're hidden by focus system
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

    private int FindNearestProbeInDirection(int startRow, int startCol, int rowDir, int colDir, int maxDistance)
    {
        
        for (int dist = 1; dist <= maxDistance; dist++)
        {
            int checkRow = startRow + (rowDir * dist);
            int checkCol = startCol + (colDir * dist);

           
            if (centerFixationPoint != null && centerFixationPoint.activeInHierarchy)
            {
                Vector2Int centerIndex = GetProbeGridIndex(centerFixationPoint);
                if (centerIndex.y == checkRow && centerIndex.x == checkCol)
                {
                    
                    return Mathf.Max(0, dist - 1);
                }
            }


            foreach (GameObject otherProbe in probeDots.probes)
            {
                if (otherProbe == null)
                    continue;

                // Check probe positions even if they're hidden by focus system
                Vector2Int otherProbeIndex = GetProbeGridIndex(otherProbe);


                if (otherProbeIndex.y == checkRow && otherProbeIndex.x == checkCol)
                {

                    return Mathf.Max(0, dist - 1);
                }
            }
        }

        return maxDistance;
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
}
