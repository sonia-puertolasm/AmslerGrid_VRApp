using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages the main grid generation and configuration
public class MainGrid : MonoBehaviour
{
    // Definition of grid configuration parameters
    private int gridSize = 8;
    private float totalGridWidth = 10f;
    private float lineWidth = 0.15f;
    private Color lineColor = Color.white;

    // Definition of center fixation point parameters
    private float centerDotSize = 0.3f;
    private Color centerDotColor = Color.red;

    internal Vector3 gridCenterPosition = Vector3.zero;

    // Definition of storage for grid points

    // Store all grid intersection points
    private GameObject[,] gridPoints;
    private float pointSize = 0.1f;

    // Public accessors for properties of the grid
    public int GridSize => gridSize;
    public float TotalGridWidth => totalGridWidth;
    public float CellSize => totalGridWidth / gridSize;
    public Vector3 GridCenterPosition => gridCenterPosition;

    // Public accessor for grid points
    public GameObject[,] GridPoints => gridPoints;

    // Initialization of all grid-generation functions
    void Start()
    {
        CreateGridPoints();
        DrawGrid();
        SetupCenterFixationPoint();
        SetupCamera();
    }

    // FUNCTION: Camera setup in perspective mode to ensure full grid visibility
    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        cam.orthographic = false;
        cam.backgroundColor = Color.black;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 60f;
        
        float margin = 1.05f;
        float gridWidth = totalGridWidth * margin;
        float gridHeight = totalGridWidth * margin;

        float vFovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float aspect = cam.aspect;

        float hFovRad = 2f * Mathf.Atan(Mathf.Tan(vFovRad / 2f) * aspect);

        float distForHeight = (gridHeight * 0.5f) / Mathf.Tan(vFovRad / 2f);
        float distForWidth = (gridWidth * 0.5f) / Mathf.Tan(hFovRad / 2f);

        float requiredDistance = Mathf.Max(distForHeight, distForWidth);

        cam.transform.position = new Vector3(gridCenterPosition.x, gridCenterPosition.y, gridCenterPosition.z - requiredDistance);
        cam.transform.rotation = Quaternion.identity;
        cam.transform.LookAt(gridCenterPosition);

        cam.transform.position += new Vector3(0, 0, -0.5f);
    }

    // FUNCTION: Creation of grid intersection points as invisible spheres
    private void CreateGridPoints()
    {
        int pointsPerDimension = gridSize + 1; // Example: for gridSize 8, we need 9 points per dimension
        gridPoints = new GameObject[pointsPerDimension, pointsPerDimension]; // Generation of GameObject for each grid point to include inside the 'Main Grid ' and 'Grid Points' parents

        float cellSize = totalGridWidth / gridSize; // Calculation of the size of each cell in the grid -> ALL cells have the same initial size
        float halfWidth = totalGridWidth / 2f; // Calculation of half the total grid width to center the grid around the specified position

        Vector3 origin = new Vector3(
            gridCenterPosition.x - halfWidth,
            gridCenterPosition.y - halfWidth,
            gridCenterPosition.z
        ); // Calculation of the origin point (bottom-left corner) of the grid

        GameObject gridPointsParent = new GameObject("GridPoints"); // Definition of a parent GameObject to hold all grid points
        gridPointsParent.transform.SetParent(transform); // Set the parent to the current GameObject
        gridPointsParent.transform.localPosition = Vector3.zero; // Local position set to zero to align with the parent

        for (int row = 0; row < pointsPerDimension; row++) // Loop through every row and column to create grid points
        {
            for (int col = 0; col < pointsPerDimension; col++)
            {
                Vector3 pointPosition = origin + new Vector3(col * cellSize, row * cellSize, 0); // Calculation of the position for each grid point

                GameObject gridPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere); // Creation of a sphere primitive to represent the grid point
                gridPoint.name = $"GridPoint_r{row}_c{col}"; // Assignment of a unique name to each grid point based on its row and column position
                gridPoint.transform.SetParent(gridPointsParent.transform); // Set the parent to the grid points parent GameObject
                gridPoint.transform.position = pointPosition; // Set the position of the grid point
                gridPoint.transform.localScale = Vector3.one * pointSize; // Set the scale of the grid point

                Renderer renderer = gridPoint.GetComponent<Renderer>(); // Get the Renderer component to modify its visibility
                renderer.enabled = false; // Initially set the grid point to be invisible

                gridPoints[row, col] = gridPoint; // Store the grid point in the 2D array for being able to afterwards access it

                // Add a custom component to store grid coordinates
                GridPointData pointData = gridPoint.AddComponent<GridPointData>(); // Add a custom component to store grid coordinates
                pointData.row = row; // Row coordinate
                pointData.col = col; // Column coordinate
                pointData.isInteractable = true; // Initially set as interactable
            }
        }
    }

    // FUNCTION: Drawing of grid lines using LineRenderer components
    void DrawGrid()
    {
        Transform existingGridLines = transform.Find("GridLines"); // Check for existing grid lines and remove them if they exist
        if (existingGridLines != null) // If existing grid lines are found, destroy them to avoid duplication
        {
            Destroy(existingGridLines.gameObject);
        }

        GameObject gridLinesParent = new GameObject("GridLines"); // Creation of a parent GameObject to hold all grid lines
        gridLinesParent.transform.SetParent(transform); // Set the parent to the current GameObject
        gridLinesParent.transform.localPosition = Vector3.zero; // Local position set to zero to align with the parent

        float cellSize = totalGridWidth / gridSize; // Calculation of the size of each cell in the grid
        float halfWidth = totalGridWidth / 2f; // Calculation of half the total grid width to center the grid around the specified position

        Vector3 origin = new Vector3(
            gridCenterPosition.x - halfWidth,
            gridCenterPosition.y - halfWidth,
            gridCenterPosition.z
        ); // Calculation of the origin point (bottom-left corner) of the grid

        // Number of rows and columns based on grid size
        int rows = gridSize;
        int cols = gridSize;

        // Loop through each cell (row, column) to create the grid lines
        for (int i = 0; i < rows; i++)
        {
            for (int k = 0; k < cols; k++)
            {
                Vector3 BL = origin + new Vector3(k * cellSize, i * cellSize, 0); // Bottom-Left corner of the cell
                Vector3 BR = origin + new Vector3((k + 1) * cellSize, i * cellSize, 0); // Bottom-Right corner of the cell
                Vector3 TL = origin + new Vector3(k * cellSize, (i + 1) * cellSize, 0); // Top-Left corner of the cell
                Vector3 TR = origin + new Vector3((k + 1) * cellSize, (i + 1) * cellSize, 0); // Top-Right corner of the cell

                CreateLine($"Line_r{i}_c{k}_TOP", TL, TR, gridLinesParent.transform); // Top line of the cell
                CreateLine($"Line_r{i}_c{k}_RIGHT", BR, TR, gridLinesParent.transform); // Right line of the cell

                if (i == 0) // Draw bottom line only for the first row to avoid duplication
                {
                    CreateLine($"Line_r{i}_c{k}_BOTTOM", BL, BR, gridLinesParent.transform);
                }

                if (k == 0) // Draw left line only for the first column to avoid duplication
                {
                    CreateLine($"Line_r{i}_c{k}_LEFT", BL, TL, gridLinesParent.transform);
                }
            }
        }
    }

    // FUNCTION: Helper method to create a line between two points using LineRenderer -> used in DrawGrid() FUNCTION
    private void CreateLine(string name, Vector3 start, Vector3 end, Transform parent)
    {
        GameObject lineObj = new GameObject(name); // Creation of a new GameObject to hold the LineRenderer
        lineObj.transform.SetParent(parent); // Set the parent to the specified parent GameObject
        LineRenderer lineRenderer = lineObj.AddComponent<LineRenderer>(); // Add a LineRenderer component to the GameObject

        // Definition of properties for the LineRenderer to work as desired
        lineRenderer.material = new Material(Shader.Find("Unlit/Color"));
        lineRenderer.material.color = lineColor;
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true; // Use world space coordinates
        lineRenderer.numCapVertices = 5; // Rounded line caps
        lineRenderer.numCornerVertices = 5; // Rounded line corners
        lineRenderer.positionCount = 2; // Two points for the line

        // Set the start and end positions of the line
        lineRenderer.SetPosition(0, start);
        lineRenderer.SetPosition(1, end);
    }

    // FUNCTION: Setup of the center fixation point in the middle of the grid
    private void SetupCenterFixationPoint()
    {
        int centerIndex = gridSize / 2; // Calculation of the center index based on grid size

        GameObject centerGridPoint = gridPoints[centerIndex, centerIndex]; // Access the center grid point from the grid points array

        Renderer renderer = centerGridPoint.GetComponent<Renderer>(); // Get the Renderer component to modify its appearance
        renderer.enabled = true; // Make the center point visible
        renderer.material.color = centerDotColor; // Set the color of the center point
        centerGridPoint.transform.localScale = Vector3.one * centerDotSize; // Set the size of the center point
        centerGridPoint.transform.position += new Vector3(0, 0, -0.2f); // Slightly offset in Z to ensure visibility over grid lines

        GridPointData pointData = centerGridPoint.GetComponent<GridPointData>(); // Access the custom GridPointData component
        pointData.isInteractable = false; // Set as non-interactable
        pointData.isCenterFixation = true; // Mark as center fixation point

        centerGridPoint.name = "CenterFixationPoint"; // Rename for posterior identification
    }
}

// Custom component to store additional data for each grid point
public class GridPointData : MonoBehaviour
{
    // Grid coordinates
    public int row;
    public int col;

    // State flags
    public bool isInteractable = true;
    public bool isCenterFixation = false;

    public bool isDeformed = false;

    // Original and adjusted positions for deformation tracking
    public Vector3 originalPosition;
    public Vector3 adjustedPosition;

    // Initialization of original and adjusted positions
    void Awake()
    {
        originalPosition = transform.position;
        adjustedPosition = transform.position;
    }
}