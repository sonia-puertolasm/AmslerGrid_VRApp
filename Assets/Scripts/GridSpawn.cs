using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainGrid : MonoBehaviour
{
    private int gridSize = 8;
    private float totalGridWidth = 10f;
    private float lineWidth = 0.15f;  // Made thicker so it's visible
    private Color lineColor = Color.white;

    private float centerDotSize = 0.3f;
    private Color centerDotColor = Color.red;

    public Vector3 gridCenterPosition = Vector3.zero;

    private List<GameObject> allLines = new List<GameObject>();
    private GameObject centerDot;

    public int GridSize => gridSize;
    public float TotalGridWidth => totalGridWidth;
    public float CellSize => totalGridWidth / gridSize;
    public Vector3 GridCenterPosition => gridCenterPosition;

    void Start()
    {
        DrawGrid();
        DrawCenterDot();
        SetupCamera();

    }

    private void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("No Main Camera found!");
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

    void DrawGrid()
    {
        int numLines = gridSize + 1;
        float cellSize = totalGridWidth / gridSize;
        float halfWidth = totalGridWidth / 2f;

        // Draw horizontal lines
        for (int i = 0; i < numLines; i++)
        {
            float y = i * cellSize - halfWidth + gridCenterPosition.y;

            GameObject line = new GameObject("HLine_" + i);
            line.transform.SetParent(transform);
            LineRenderer lr = line.AddComponent<LineRenderer>();

            // Use the shader that works: Unlit/Color
            lr.material = new Material(Shader.Find("Unlit/Color"));
            lr.material.color = lineColor;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.useWorldSpace = true;
            lr.numCapVertices = 5;
            lr.numCornerVertices = 5;
            lr.positionCount = 2;

            lr.SetPosition(0, new Vector3(-halfWidth + gridCenterPosition.x, y, gridCenterPosition.z));
            lr.SetPosition(1, new Vector3(halfWidth + gridCenterPosition.x, y, gridCenterPosition.z));

            allLines.Add(line);
        }

        // Draw vertical lines
        for (int i = 0; i < numLines; i++)
        {
            float x = i * cellSize - halfWidth + gridCenterPosition.x;

            GameObject line = new GameObject("VLine_" + i);
            line.transform.SetParent(transform);
            LineRenderer lr = line.AddComponent<LineRenderer>();

            // Use the shader that works: Unlit/Color
            lr.material = new Material(Shader.Find("Unlit/Color"));
            lr.material.color = lineColor;
            lr.startColor = lineColor;
            lr.endColor = lineColor;
            lr.startWidth = lineWidth;
            lr.endWidth = lineWidth;
            lr.useWorldSpace = true;
            lr.numCapVertices = 5;
            lr.numCornerVertices = 5;
            lr.positionCount = 2;

            lr.SetPosition(0, new Vector3(x, -halfWidth + gridCenterPosition.y, gridCenterPosition.z));
            lr.SetPosition(1, new Vector3(x, halfWidth + gridCenterPosition.y, gridCenterPosition.z));

            allLines.Add(line);
        }
    }
    private void DrawCenterDot()
    {
        centerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        centerDot.name = "CenterDot";
        centerDot.transform.SetParent(transform);
        centerDot.transform.localScale = Vector3.one * centerDotSize;
        centerDot.transform.position = gridCenterPosition + new Vector3(0, 0, -0.2f);

        centerDot.GetComponent<Renderer>().material.color = centerDotColor;
    }
}