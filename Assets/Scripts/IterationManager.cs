using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IterationManager : MonoBehaviour
{
    private ProbeDots probeDots;
    private MainGrid mainGrid;
    private GridRebuildManager gridRebuildManager;
    private DisplacementTracker displacementTracker;
    private FocusSystem focusSystem;

    private int currentIteration = 1;
    private int currentParentProbeIndex = -1;

    private Dictionary<int, List<GameObject>> iterationProbes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, GameObject> iterationFixationPoints = new Dictionary<int, GameObject>();

    private Dictionary<int, List<GameObject>> parentProbeToIteration2Probes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, Dictionary<GameObject, Vector3>> parentProbeToIteration2Positions = new Dictionary<int, Dictionary<GameObject, Vector3>>();

    // Track which grid points are already occupied by IT2 probes from any parent
    private HashSet<Vector2Int> occupiedIT2GridPoints = new HashSet<Vector2Int>();
    private Dictionary<Vector2Int, int> gridPointToParentIndex = new Dictionary<Vector2Int, int>();

    private float spacingScaleFactor = 0.5f;

    private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    void Start()
    {
        probeDots = FindObjectOfType<ProbeDots>();
        mainGrid = FindObjectOfType<MainGrid>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        displacementTracker = FindObjectOfType<DisplacementTracker>();
        focusSystem = FindObjectOfType<FocusSystem>();

        if (mainGrid != null)
        {
            gridSize = mainGrid.GridSize;
            cellSize = mainGrid.CellSize;
            gridCenter = mainGrid.GridCenterPosition;
            halfWidth = mainGrid.TotalGridWidth / 2f;
        }

        StartCoroutine(InitializeIterationSystem());
    }

    private IEnumerator InitializeIterationSystem()
    {
        yield return new WaitForSeconds(0.2f);

        if (probeDots != null && probeDots.probes != null && probeDots.probes.Count > 0)
        {
            iterationProbes[1] = new List<GameObject>(probeDots.probes);

            GameObject centerFixation = GameObject.Find("CenterFixationPoint");
            if (centerFixation != null)
            {
                iterationFixationPoints[1] = centerFixation;
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            HandleEnterKey();
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            HandleBackspaceKey();
        }
    }

    private void HandleEnterKey()
    {
        if (currentIteration == 1 && probeDots != null && probeDots.selectedProbeIndex >= 0)
        {
            int selectedIndex = probeDots.selectedProbeIndex;

            if (parentProbeToIteration2Probes.ContainsKey(selectedIndex) && 
                parentProbeToIteration2Probes[selectedIndex].Count > 0)
            {
                ReturnToIteration2(selectedIndex);
            }
            else
            {
                AdvanceToIteration2(selectedIndex);
            }
        }
    }

    private void ReturnToIteration2(int parentProbeIndex)
    {
        HideIteration1Probes();

        if (focusSystem != null && focusSystem.focusSystemEnabled)
        {
            focusSystem.ExitFocusMode();
        }

        List<GameObject> iteration2Probes = parentProbeToIteration2Probes[parentProbeIndex];
        Dictionary<GameObject, Vector3> iteration2Positions = parentProbeToIteration2Positions[parentProbeIndex];

        foreach (GameObject probe in iteration2Probes)
        {
            if (probe != null)
            {
                probe.SetActive(true);
                Renderer renderer = probe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        probeDots.probes = new List<GameObject>(iteration2Probes);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>(iteration2Positions);
        probeDots.selectedProbeIndex = -1;

        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;
    }

    private void HandleBackspaceKey()
    {
        if (currentIteration > 1 && probeDots != null && probeDots.selectedProbeIndex == -1)
        {
            if (currentIteration == 2)
            {
                ReturnToIteration1();
            }
        }
    }

    private void AdvanceToIteration2(int parentProbeIndex)
    {
        if (parentProbeIndex < 0 || parentProbeIndex >= iterationProbes[1].Count)
        {
            return;
        }

        GameObject selectedProbe = iterationProbes[1][parentProbeIndex];
        if (selectedProbe == null)
        {
            return;
        }

        // Save displacement snapshot for Iteration 1 before advancing
        if (displacementTracker != null && displacementTracker.IsInitialized)
        {
            displacementTracker.SaveIterationSnapshot(1);
        }

        Vector3 newCenterPosition = selectedProbe.transform.position;

        HideIteration1Probes();

        if (focusSystem != null && focusSystem.focusSystemEnabled)
        {
            focusSystem.ExitFocusMode();
        }

        if (iterationFixationPoints.ContainsKey(1))
        {
            GameObject fixation = iterationFixationPoints[1];
            if (fixation != null)
            {
                fixation.SetActive(true);
                Renderer renderer = fixation.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        SpawnIteration2Probes(newCenterPosition, parentProbeIndex);

        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;

        probeDots.selectedProbeIndex = -1;
    }

    private void SpawnIteration2Probes(Vector3 centerPosition, int parentProbeIndex)
{
    List<GameObject> newIteration2Probes = new List<GameObject>();
    Dictionary<GameObject, Vector3> newIteration2Positions = new Dictionary<GameObject, Vector3>();

    float originalSpacing = probeDots.probeSpacing;
    float newSpacing = originalSpacing * spacingScaleFactor;

    Vector3[] relativePositions = new Vector3[]
    {
        new Vector3(-newSpacing, -newSpacing, 0),
        new Vector3(0, -newSpacing, 0),
        new Vector3(newSpacing, -newSpacing, 0),
        new Vector3(-newSpacing, 0, 0),
        new Vector3(newSpacing, 0, 0),
        new Vector3(-newSpacing, newSpacing, 0),
        new Vector3(0, newSpacing, 0),
        new Vector3(newSpacing, newSpacing, 0)
    };

    for (int i = 0; i < relativePositions.Length; i++)
    {
        Vector3 targetPosition = centerPosition + relativePositions[i];

        Vector2Int gridIndex;
        Vector3 originalGridPosition = SnapToOriginalGridPointWithIndex(targetPosition, out gridIndex);

        if (gridRebuildManager != null)
        {
            int gridSizeValue = gridRebuildManager.GetGridSize();

            if (gridIndex.x == 0 || gridIndex.x == gridSizeValue ||
                gridIndex.y == 0 || gridIndex.y == gridSizeValue)
            {
                continue;
            }
        }

        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        probe.name = $"ProbePoint_Parent{parentProbeIndex}_Iteration2_{i}";
        probe.transform.localScale = Vector3.one * probeDots.probeDotSize;
        probe.transform.SetParent(transform);

        Vector3 currentDeformedPosition = gridRebuildManager.GetDeformedGridPoint(gridIndex.y, gridIndex.x);
        currentDeformedPosition.z = centerPosition.z;

        probe.transform.position = currentDeformedPosition;

        Renderer renderer = probe.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.enabled = true;
            renderer.material.color = ProbeColors.Default;
        }

        GridPointData pointData = probe.AddComponent<GridPointData>();
        pointData.isInteractable = true;

        newIteration2Positions[probe] = originalGridPosition;

        newIteration2Probes.Add(probe);

        if (gridRebuildManager != null)
        {
            gridRebuildManager.RegisterProbe(probe, originalGridPosition, 2, gridIndex);
        }
    }

    parentProbeToIteration2Probes[parentProbeIndex] = newIteration2Probes;
    parentProbeToIteration2Positions[parentProbeIndex] = newIteration2Positions;

    probeDots.probes = new List<GameObject>(newIteration2Probes);
    probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>(newIteration2Positions);
    probeDots.selectedProbeIndex = -1;
    
    if (gridRebuildManager != null)
    {
        gridRebuildManager.ForceRebuild();
    }
}

    private Vector3 SnapToOriginalGridPointWithIndex(Vector3 targetPosition, out Vector2Int gridIndex)
    {
        gridIndex = Vector2Int.zero;
        
        if (gridRebuildManager == null)
            return targetPosition;

        int gridSizeValue = gridRebuildManager.GetGridSize();
        float minDistance = float.MaxValue;
        Vector3 closestPoint = targetPosition;

        for (int row = 0; row <= gridSizeValue; row++)
        {
            for (int col = 0; col <= gridSizeValue; col++)
            {
                Vector3 gridPoint = gridRebuildManager.GetOriginalGridPoint(row, col);
                gridPoint.z = targetPosition.z;

                float distance = Vector3.Distance(targetPosition, gridPoint);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = gridPoint;
                    gridIndex = new Vector2Int(col, row);
                }
            }
        }

        return closestPoint;
    }

    private Vector3 SnapToNearestGridPoint(Vector3 targetPosition)
    {
        Vector2Int index;
        return SnapToNearestGridPointWithIndex(targetPosition, out index);
    }

    private Vector3 SnapToNearestGridPointWithIndex(Vector3 targetPosition, out Vector2Int gridIndex)
    {
        gridIndex = Vector2Int.zero;
        
        if (gridRebuildManager == null)
            return targetPosition;

        int gridSizeValue = gridRebuildManager.GetGridSize();
        float minDistance = float.MaxValue;
        Vector3 closestPoint = targetPosition;

        for (int row = 0; row <= gridSizeValue; row++)
        {
            for (int col = 0; col <= gridSizeValue; col++)
            {
                Vector3 gridPoint = gridRebuildManager.GetDeformedGridPoint(row, col);

                gridPoint.z = targetPosition.z;

                float distance = Vector3.Distance(targetPosition, gridPoint);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = gridPoint;
                    gridIndex = new Vector2Int(col, row);
                }
            }
        }

        return closestPoint;
    }

    private void HideIteration1Probes()
    {
        if (iterationProbes.ContainsKey(1))
        {
            foreach (GameObject probe in iterationProbes[1])
            {
                if (probe != null)
                {
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }

                    probe.SetActive(false);
                }
            }
        }
    }

    private void HideAllIteration2Probes()
    {
        foreach (var kvp in parentProbeToIteration2Probes)
        {
            foreach (GameObject probe in kvp.Value)
            {
                if (probe != null)
                {
                    probe.SetActive(false);
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
            }
        }
    }

    private void ReturnToIteration1()
    {
        HideAllIteration2Probes();

        if (iterationProbes.ContainsKey(1))
        {
            foreach (GameObject probe in iterationProbes[1])
            {
                if (probe != null)
                {
                    probe.SetActive(true);
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }

        if (iterationFixationPoints.ContainsKey(1))
        {
            GameObject fixation = iterationFixationPoints[1];
            if (fixation != null)
            {
                fixation.SetActive(true);
                Renderer renderer = fixation.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        probeDots.probes = new List<GameObject>(iterationProbes[1]);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>();

        foreach (GameObject probe in iterationProbes[1])
        {
            if (probe != null && gridRebuildManager != null)
            {
                if (gridRebuildManager.probeOriginalPositions.ContainsKey(probe))
                {
                    probeDots.probeInitialPositions[probe] = gridRebuildManager.probeOriginalPositions[probe];
                }
            }
        }

        probeDots.selectedProbeIndex = -1;

        currentIteration = 1;
        currentParentProbeIndex = -1;
    }

    public int CurrentIteration => currentIteration;
    public bool IsInIteration2 => currentIteration == 2;
    public int CurrentParentProbeIndex => currentParentProbeIndex;

    public bool HasIteration2ForProbe(int probeIndex)
    {
        return parentProbeToIteration2Probes.ContainsKey(probeIndex) && 
               parentProbeToIteration2Probes[probeIndex].Count > 0;
    }

    private Vector2Int GetGridIndexFromPosition(Vector3 position)
    {
        if (mainGrid == null)
            return Vector2Int.zero;

        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        int col = Mathf.RoundToInt((position.x - originX) / cellSize);
        int row = Mathf.RoundToInt((position.y - originY) / cellSize);

        col = Mathf.Clamp(col, 0, gridSize);
        row = Mathf.Clamp(row, 0, gridSize);

        return new Vector2Int(col, row);
    }
}