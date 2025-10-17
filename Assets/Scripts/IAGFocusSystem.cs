using UnityEngine;
using System.Collections.Generic;

public class IAGFocusSystem : MonoBehaviour
{
    private Transform gridParent;
    private Transform probeParent;

    private float focusSquareSize = 1.5f;
    private float lineWidth = 0.02f;
    private Color focusLineColor = Color.white;
    private Color boundaryColor = Color.gray;

    private Material focusLineMaterial;

    private GameObject selectedProbe = null;
    private GameObject focusVisualizationParent = null;
    private GameObject focusSquare = null;
    private List<GameObject> focusCrossLines = new List<GameObject>();
    private bool isFocused = false;

    private int currentIteration = 1;

    private Dictionary<GameObject, bool> originalGridVisibility = new Dictionary<GameObject, bool>();
    private Dictionary<GameObject, bool> originalProbeVisibility = new Dictionary<GameObject, bool>();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && isFocused)
        {
            ExitFocusMode();
        }
    }

    public void EnterFocusMode(GameObject probe)
    {
        if (probe == null) return;

        selectedProbe = probe;
        isFocused = true;

        if (probe.name.Contains("Iter1"))
        {
            currentIteration = 1;
        }
        else
        {
            currentIteration = 2;
        }

        HideAllGridLines();
        HideAllProbesExcept(probe);
        CreateFocusVisualization(probe.transform.position);
    }

    public void ExitFocusMode()
    {
        if (!isFocused) return;

        DestroyFocusVisualization();
        RestoreGridVisibility();
        RestoreProbeVisibility();

        selectedProbe = null;
        isFocused = false;
    }

    void HideAllGridLines()
    {
        if (gridParent == null)
        {
            return;
        }

        originalGridVisibility.Clear();

        foreach (Transform child in gridParent)
        {
            if (child.name.Contains("CenterDot") || child.name == "CenterDot")
            {
                continue;
            }

            LineRenderer lr = child.GetComponent<LineRenderer>();
            if (lr != null)
            {
                originalGridVisibility[child.gameObject] = lr.enabled;
                lr.enabled = false;
            }
            else
            {
                originalGridVisibility[child.gameObject] = child.gameObject.activeSelf;
                child.gameObject.SetActive(false);
            }
        }
    }

    void HideAllProbesExcept(GameObject exception)
    {
        if (probeParent == null) return;

        originalProbeVisibility.Clear();

        foreach (Transform child in probeParent)
        {
            if (child.gameObject == exception)
                continue;

            originalProbeVisibility[child.gameObject] = child.gameObject.activeSelf;
            child.gameObject.SetActive(false);
        }
    }

    void CreateFocusVisualization(Vector3 probePosition)
    {
        focusVisualizationParent = new GameObject("FocusVisualization");
        focusVisualizationParent.transform.position = probePosition;
        focusVisualizationParent.transform.parent = transform;

        float halfSize = focusSquareSize / 2f;
        Vector3 topLeft = probePosition + new Vector3(-halfSize, halfSize, 0);
        Vector3 topRight = probePosition + new Vector3(halfSize, halfSize, 0);
        Vector3 bottomLeft = probePosition + new Vector3(-halfSize, -halfSize, 0);
        Vector3 bottomRight = probePosition + new Vector3(halfSize, -halfSize, 0);

        focusSquare = CreateSquareBoundary(topLeft, topRight, bottomLeft, bottomRight, focusVisualizationParent.transform);

        CreateCrossLine(
            probePosition + new Vector3(-halfSize, 0, 0),
            probePosition + new Vector3(halfSize, 0, 0),
            focusVisualizationParent.transform
        );

        CreateCrossLine(
            probePosition + new Vector3(0, -halfSize, 0),
            probePosition + new Vector3(0, halfSize, 0),
            focusVisualizationParent.transform
        );
    }

    GameObject CreateSquareBoundary(Vector3 topLeft, Vector3 topRight, Vector3 bottomLeft, Vector3 bottomRight, Transform parent)
    {
        GameObject square = new GameObject("FocusSquare");
        square.transform.parent = parent;

        LineRenderer lr = square.AddComponent<LineRenderer>();
        lr.material = focusLineMaterial != null ? focusLineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = boundaryColor;
        lr.endColor = boundaryColor;
        lr.startWidth = lineWidth * 1.5f;
        lr.endWidth = lineWidth * 1.5f;
        lr.positionCount = 5;
        lr.loop = true;
        lr.useWorldSpace = true;

        lr.SetPosition(0, bottomLeft);
        lr.SetPosition(1, bottomRight);
        lr.SetPosition(2, topRight);
        lr.SetPosition(3, topLeft);
        lr.SetPosition(4, bottomLeft);

        return square;
    }

    void CreateCrossLine(Vector3 start, Vector3 end, Transform parent)
    {
        GameObject lineObj = new GameObject("CrossLine");
        lineObj.transform.parent = parent;

        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = focusLineMaterial != null ? focusLineMaterial : new Material(Shader.Find("Sprites/Default"));
        lr.startColor = focusLineColor;
        lr.endColor = focusLineColor;
        lr.startWidth = lineWidth;
        lr.endWidth = lineWidth;
        lr.positionCount = 2;
        lr.useWorldSpace = true;

        lr.SetPosition(0, start);
        lr.SetPosition(1, end);

        focusCrossLines.Add(lineObj);
    }

    void DestroyFocusVisualization()
    {
        if (focusSquare != null)
        {
            Destroy(focusVisualizationParent);
            focusVisualizationParent = null;
        }

        focusSquare = null;
        focusCrossLines.Clear();
    }

    void RestoreGridVisibility()
    {
        foreach (var kvp in originalGridVisibility)
        {
            if (kvp.Key != null)
            {
                LineRenderer lr = kvp.Key.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = kvp.Value;
                }
                else
                {
                    kvp.Key.SetActive(kvp.Value);
                }
            }
        }
        originalGridVisibility.Clear();
    }

    void RestoreProbeVisibility()
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

    public void UpdateFocusPosition(Vector3 newPosition)
    {
        if (!isFocused || focusVisualizationParent == null) return;

        DestroyFocusVisualization();
        CreateFocusVisualization(newPosition);
    }

    public bool IsFocused()
    {
        return isFocused;
    }

    public GameObject GetSelectedProbe()
    {
        return selectedProbe;
    }

    public void SetGridParent(Transform parent)
    {
        gridParent = parent;
    }

    public void SetProbeParent(Transform parent)
    {
        probeParent = parent;
    }

    public void SetCurrentIteration(int iteration)
    {
        currentIteration = iteration;
    }
}