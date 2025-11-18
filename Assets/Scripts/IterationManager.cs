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
    private const int maxIterations = 2;

    private Dictionary<int, List<GameObject>> iterationProbes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, GameObject> iterationFixationPoints = new Dictionary<int, GameObject>();

    private GameObject iteration2CenterFixationPoint;
    private List<GameObject> iteration2Probes = new List<GameObject>();
    private Dictionary<GameObject, Vector3> iteration2InitialPositions = new Dictionary<GameObject, Vector3>();

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
        // Wait for probes to be fully initialized
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
            // Check if iteration 2 already exists (user went back and is now going forward)
            if (iteration2Probes != null && iteration2Probes.Count > 0)
            {
                ReturnToIteration2();
            }
            else
            {
                AdvanceToIteration2();
            }
        }
    }

    private void ReturnToIteration2()
    {
        // Hide iteration 1 probes
        HideIteration1Probes();

        // Exit focus mode if active
        if (focusSystem != null && focusSystem.focusSystemEnabled)
        {
            focusSystem.ExitFocusMode();
        }

        // Show existing iteration 2 probes
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

        // Show iteration 2 fixation point
        if (iteration2CenterFixationPoint != null)
        {
            iteration2CenterFixationPoint.SetActive(true);
            Renderer renderer = iteration2CenterFixationPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
            }
        }

        // Switch active probe list to iteration 2
        probeDots.probes = new List<GameObject>(iteration2Probes);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>(iteration2InitialPositions);
        probeDots.selectedProbeIndex = -1;

        currentIteration = 2;
    }

    private void HandleBackspaceKey()
    {
        // Only allow going back if not in iteration 1 AND no probe is currently selected
        if (currentIteration > 1 && probeDots != null && probeDots.selectedProbeIndex == -1)
        {
            if (currentIteration == 2)
            {
                ReturnToIteration1();
            }
            // Can add more iteration levels here in the future
        }
    }

    private void AdvanceToIteration2()
    {
        if (probeDots.selectedProbeIndex < 0 || probeDots.selectedProbeIndex >= probeDots.probes.Count)
        {
            return;
        }

        GameObject selectedProbe = probeDots.probes[probeDots.selectedProbeIndex];
        if (selectedProbe == null)
        {
            return;
        }

        Vector3 newCenterPosition = selectedProbe.transform.position;

        HideIteration1Probes();

        if (focusSystem != null && focusSystem.focusSystemEnabled)
        {
            focusSystem.ExitFocusMode();
        }

        CreateIteration2CenterFixationPoint();

        SpawnIteration2Probes(newCenterPosition);

        currentIteration = 2;

        probeDots.selectedProbeIndex = -1;

        // Don't call ForceRebuild here - iteration 2 probes start at their "home" positions
        // on the deformed grid with zero displacement, so they don't affect deformation yet.
        // The grid will rebuild automatically in LateUpdate when probes actually move.
    }

    private void CreateIteration2CenterFixationPoint()
    {
        // Get the original iteration 1 center fixation point to match its properties
        GameObject iteration1Center = iterationFixationPoints.ContainsKey(1) ? iterationFixationPoints[1] : null;

        if (iteration1Center == null)
        {
            iteration1Center = GameObject.Find("CenterFixationPoint");
        }

        // Create the center fixation point for iteration 2 at the grid center
        iteration2CenterFixationPoint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        iteration2CenterFixationPoint.name = "CenterFixationPoint_Iteration2";
        iteration2CenterFixationPoint.transform.position = gridCenter;

        // Match the size and appearance of iteration 1's center fixation point
        if (iteration1Center != null)
        {
            iteration2CenterFixationPoint.transform.localScale = iteration1Center.transform.localScale;

            Renderer originalRenderer = iteration1Center.GetComponent<Renderer>();
            Renderer newRenderer = iteration2CenterFixationPoint.GetComponent<Renderer>();
            if (originalRenderer != null && newRenderer != null)
            {
                newRenderer.material.color = originalRenderer.material.color;
            }
        }
        else
        {
            // Fallback to default sizing
            iteration2CenterFixationPoint.transform.localScale = Vector3.one * probeDots.probeDotSize;

            Renderer renderer = iteration2CenterFixationPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = ProbeColors.Default;
            }
        }

        GridPointData pointData = iteration2CenterFixationPoint.AddComponent<GridPointData>();
        pointData.isInteractable = false;
        pointData.isCenterFixation = true;

        iterationFixationPoints[2] = iteration2CenterFixationPoint;
    }

    private void SpawnIteration2Probes(Vector3 centerPosition)
    {
        iteration2Probes.Clear();
        iteration2InitialPositions.Clear();

        float originalSpacing = probeDots.probeSpacing;
        float newSpacing = originalSpacing * spacingScaleFactor;

        Vector3[] relativePositions = new Vector3[]
        {
            new Vector3(-newSpacing, -newSpacing, 0),  // Bottom-left
            new Vector3(0, -newSpacing, 0),            // Bottom-center
            new Vector3(newSpacing, -newSpacing, 0),   // Bottom-right
            new Vector3(-newSpacing, 0, 0),            // Middle-left
            new Vector3(newSpacing, 0, 0),             // Middle-right
            new Vector3(-newSpacing, newSpacing, 0),   // Top-left
            new Vector3(0, newSpacing, 0),             // Top-center
            new Vector3(newSpacing, newSpacing, 0)     // Top-right
        };

        for (int i = 0; i < relativePositions.Length; i++)
        {
            Vector3 targetPosition = centerPosition + relativePositions[i];

            Vector3 snappedPosition = SnapToNearestGridPoint(targetPosition);

            if (gridRebuildManager != null)
            {
                int gridSizeValue = gridRebuildManager.GetGridSize();
                Vector2Int gridIndex = GetGridIndexFromPosition(snappedPosition);
                
                if (gridIndex.x == 0 || gridIndex.x == gridSizeValue || 
                    gridIndex.y == 0 || gridIndex.y == gridSizeValue)
                {
                    continue;
                }
            }

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = $"ProbePoint_Iteration2_{i}";
            probe.transform.position = snappedPosition;
            probe.transform.localScale = Vector3.one * probeDots.probeDotSize;
            probe.transform.SetParent(transform);

            Renderer renderer = probe.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.material.color = ProbeColors.Default;
            }

            GridPointData pointData = probe.AddComponent<GridPointData>();
            pointData.isInteractable = true;

            iteration2InitialPositions[probe] = snappedPosition;

            iteration2Probes.Add(probe);

            if (gridRebuildManager != null)
            {
                gridRebuildManager.RegisterProbe(probe, snappedPosition, 2);
            }
        }

        probeDots.probes = new List<GameObject>(iteration2Probes);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>(iteration2InitialPositions);
        probeDots.selectedProbeIndex = -1;

        iterationProbes[2] = new List<GameObject>(iteration2Probes);
    }

    private Vector3 SnapToNearestGridPoint(Vector3 targetPosition)
    {
        if (gridRebuildManager == null)
            return targetPosition;

        int gridSize = gridRebuildManager.GetGridSize();
        float minDistance = float.MaxValue;
        Vector3 closestPoint = targetPosition;

        for (int row = 0; row <= gridSize; row++)
        {
            for (int col = 0; col <= gridSize; col++)
            {
                // Use DEFORMED grid points for iteration 2+ probes
                // This ensures new probes sit on the already-deformed grid
                Vector3 gridPoint = gridRebuildManager.GetDeformedGridPoint(row, col);

                gridPoint.z = targetPosition.z;

                float distance = Vector3.Distance(targetPosition, gridPoint);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestPoint = gridPoint;
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

        if (iterationFixationPoints.ContainsKey(1))
        {
            GameObject fixation = iterationFixationPoints[1];
            if (fixation != null)
            {

                Renderer renderer = fixation.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = false;
                }

                fixation.SetActive(false);
            }
        }
    }

    private void ReturnToIteration1()
    {
        // HIDE iteration 2 probes - do NOT destroy or unregister
        // This preserves their deformations (truly accumulative)
        foreach (GameObject probe in iteration2Probes)
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

        // HIDE iteration 2 center fixation point - do NOT destroy
        if (iteration2CenterFixationPoint != null)
        {
            iteration2CenterFixationPoint.SetActive(false);
            Renderer renderer = iteration2CenterFixationPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }
        }

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
    }

    public int CurrentIteration => currentIteration;
    public bool IsInIteration2 => currentIteration == 2;

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