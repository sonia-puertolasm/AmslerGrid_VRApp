using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainGrid_Test : MonoBehaviour
{
    private int gridSize = 8;
    private float totalGridWidth = 10f;
    private float lineWidth = 0.15f;  
    private Color lineColor = Color.white;

    private float centerDotSize = 0.3f;
    private Color centerDotColor = Color.red;

    public Vector3 gridCenterPosition = Vector3.zero;

    private List<GameObject> allLines = new List<GameObject>();
    private GameObject centerDot;
    
    // Store all grid intersection points
    private GameObject[,] gridPoints;
    private float pointSize = 0.1f;

    public int GridSize => gridSize;
    public float TotalGridWidth => totalGridWidth;
    public float CellSize => totalGridWidth / gridSize;
    public Vector3 GridCenterPosition => gridCenterPosition;

    // Public accessor for grid points
    public GameObject[,] GridPoints => gridPoints;

    void Start()
    {
        CreateGridPoints();
        DrawGrid();
        SetupCenterFixationPoint();
        SetupCamera();
    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.transform.position = new Vector3(gridCenterPosition.x, gridCenterPosition.y, -10f);
        cam.transform.rotation = Quaternion.identity;

        cam.orthographic = true;
        cam.orthographicSize = (totalGridWidth / 2f) * 1.2f;

        cam.backgroundColor = Color.black;

        cam.nearClipPlane = 0.3f;
        cam.farClipPlane = 1000f;

        cam.cullingMask = -1;
    }

    private void CreateGridPoints()
    {
        int pointsPerDimension = gridSize + 1;
        gridPoints = new GameObject[pointsPerDimension, pointsPerDimension];

        float cellSize = totalGridWidth / gridSize;
        float halfWidth = totalGridWidth / 2f;

        Vector3 origin = new Vector3(
            gridCenterPosition.x - halfWidth,
            gridCenterPosition.y - halfWidth,
            gridCenterPosition.z
        );

        GameObject gridPointsParent = new GameObject("GridPoints");
        gridPointsParent.transform.SetParent(transform);
        gridPointsParent.transform.localPosition = Vector3.zero;

        for (int row = 0; row < pointsPerDimension; row++)
        {
            for (int col = 0; col < pointsPerDimension; col++)
            {
                Vector3 pointPosition = origin + new Vector3(col * cellSize, row * cellSize, 0);
                
                GameObject gridPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                gridPoint.name = $"GridPoint_r{row}_c{col}";
                gridPoint.transform.SetParent(gridPointsParent.transform);
                gridPoint.transform.position = pointPosition;
                gridPoint.transform.localScale = Vector3.one * pointSize;
                
                Renderer renderer = gridPoint.GetComponent<Renderer>();
                renderer.enabled = false;
                
                gridPoints[row, col] = gridPoint;
                
                // Add a custom component to store grid coordinates
                GridPointData pointData = gridPoint.AddComponent<GridPointData>();
                pointData.row = row;
                pointData.col = col;
                pointData.isInteractable = true;
            }
        }
    }

    void DrawGrid()
    {
        Transform existingGridLines = transform.Find("GridLines");
        if (existingGridLines != null)
        {
            Destroy(existingGridLines.gameObject);
        }

        GameObject gridLinesParent = new GameObject("GridLines");
        gridLinesParent.transform.SetParent(transform);
        gridLinesParent.transform.localPosition = Vector3.zero;

        float cellSize = totalGridWidth / gridSize;
        float halfWidth = totalGridWidth / 2f;

        Vector3 origin = new Vector3(
            gridCenterPosition.x - halfWidth,
            gridCenterPosition.y - halfWidth,
            gridCenterPosition.z
        );

        int rows = gridSize;
        int cols = gridSize;

        for (int i = 0; i < rows; i++)
        {
            for (int k = 0; k < cols; k++)
            {
                Vector3 BL = origin + new Vector3(k * cellSize, i * cellSize, 0);
                Vector3 BR = origin + new Vector3((k + 1) * cellSize, i * cellSize, 0);
                Vector3 TL = origin + new Vector3(k * cellSize, (i + 1) * cellSize, 0);
                Vector3 TR = origin + new Vector3((k + 1) * cellSize, (i + 1) * cellSize, 0);

                CreateLine($"Line_r{i}_c{k}_TOP", TL, TR, gridLinesParent.transform);
                CreateLine($"Line_r{i}_c{k}_RIGHT", BR, TR, gridLinesParent.transform);

                if (i == 0)
                {
                    CreateLine($"Line_r{i}_c{k}_BOTTOM", BL, BR, gridLinesParent.transform);
                }

                if (k == 0)
                {
                    CreateLine($"Line_r{i}_c{k}_LEFT", BL, TL, gridLinesParent.transform);
                }
            }
        }
    }

    private void CreateLine(string name, Vector3 start, Vector3 end, Transform parent)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(parent);
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>();

        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = lineColor;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.numCapVertices = 5;
        lineRenderer.numCornerVertices = 5;
        lineRenderer.positionCount = 2;

        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);

        allLines.Add(lineObj);
    }

    private void SetupCenterFixationPoint()
    {
        int centerIndex = gridSize / 2;
        
        GameObject centerGridPoint = gridPoints[centerIndex, centerIndex];
        
        Renderer renderer = centerGridPoint.GetComponent<Renderer>();
        renderer.enabled = true;
        renderer.material.color = centerDotColor;
        centerGridPoint.transform.localScale = Vector3.one * centerDotSize;
        centerGridPoint.transform.position += new Vector3(0, 0, -0.2f);
        
        GridPointData pointData = centerGridPoint.GetComponent<GridPointData>();
        pointData.isInteractable = false;
        pointData.isCenterFixation = true;
        
        centerGridPoint.name = "CenterFixationPoint";
        
        centerDot = centerGridPoint;
    }
}

public class GridPointData : MonoBehaviour
{
    public int row;
    public int col;
    public bool isInteractable = true;
    public bool isCenterFixation = false;
    
    public bool isDeformed = false;
    public Vector3 originalPosition;
    public Vector3 adjustedPosition;

    void Awake()
    {
        originalPosition = transform.position;
        adjustedPosition = transform.position;
    }
}