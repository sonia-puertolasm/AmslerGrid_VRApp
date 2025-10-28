using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainGrid_Old : MonoBehaviour
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
        GameObject line = new GameObject(name);
        line.transform.SetParent(parent);
        LineRenderer lr = line.AddComponent<LineRenderer>();

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

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        allLines.Add(line);
    }
    private void DrawCenterDot()
    {
        Transform existingCenterDot = transform.Find("CenterDot");
        if (existingCenterDot != null)
        {
            Destroy(existingCenterDot.gameObject);
        }

        centerDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        centerDot.name = "CenterDot";
        centerDot.transform.SetParent(transform);
        centerDot.transform.localScale = Vector3.one * centerDotSize;
        centerDot.transform.position = gridCenterPosition + new Vector3(0, 0, -0.2f);

        centerDot.GetComponent<Renderer>().material.color = centerDotColor;
    }
}