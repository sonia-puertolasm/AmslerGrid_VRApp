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

    // Store iteration 1 probes (the original 8 probes)
    private Dictionary<int, List<GameObject>> iterationProbes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, GameObject> iterationFixationPoints = new Dictionary<int, GameObject>();

    // Store iteration 2 probes for each parent probe
    private Dictionary<int, List<GameObject>> parentProbeToIteration2Probes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, Dictionary<GameObject, Vector3>> parentProbeToIteration2Positions = new Dictionary<int, Dictionary<GameObject, Vector3>>();

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

            transform.position = mainGrid.transform.position;
            transform.rotation = mainGrid.transform.rotation;
            transform.localScale = mainGrid.transform.localScale;
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
        // ENTER KEY: Advance to higher iteration when a probe is selected
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            HandleEnterKey();
        }

        // SPACE BAR: Confirm changes and return to selection screen
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleSpaceBar();
        }

        // BACKSPACE KEY: Two behaviors depending on state
        // 1. If probe selected in IT2: Deselect (return to IT2 selection mode)
        // 2. If in IT2 selection mode (no probe selected): Return to IT1 selection mode
        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            HandleBackspaceKey();
        }
    }

    private void HandleEnterKey()
    {
        // ENTER only works when a probe is selected
        if (probeDots == null || probeDots.selectedProbeIndex < 0)
        {
            return;
        }

        int selectedIndex = probeDots.selectedProbeIndex;

        if (currentIteration == 1)
        {
            // Advance from IT1 to IT2
            if (parentProbeToIteration2Probes.ContainsKey(selectedIndex) &&
                parentProbeToIteration2Probes[selectedIndex].Count > 0)
            {
                // Return to existing iteration 2
                ReturnToIteration2(selectedIndex);
            }
            else
            {
                // Create new iteration 2 for this parent probe
                AdvanceToIteration2(selectedIndex);
            }
        }
        else if (currentIteration == 2)
        {
            // In IT2: ENTER advances to IT3 (not implemented yet)
            // For now, do nothing - IT3 not yet implemented
            Debug.Log("Iteration 3 not yet implemented");
        }
    }

    private void HandleSpaceBar()
    {
        // SPACE BAR: Confirm changes and return to selection screen
        if (probeDots == null)
        {
            return;
        }

        if (currentIteration == 1)
        {
            // In IT1: Mark probe as completed and deselect
            if (probeDots.selectedProbeIndex >= 0 && probeDots.selectedProbeIndex < probeDots.probes.Count)
            {
                GameObject selectedProbe = probeDots.probes[probeDots.selectedProbeIndex];
                Renderer renderer = selectedProbe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ProbeColors.Completed;
                }
                probeDots.selectedProbeIndex = -1;

                // Exit focus mode if available
                if (focusSystem != null)
                {
                    focusSystem.ExitFocusMode();
                }
            }
        }
        else if (currentIteration == 2)
        {
            // In IT2: Mark probe as completed, deselect, and return to IT2 selection screen
            if (probeDots.selectedProbeIndex >= 0 && probeDots.selectedProbeIndex < probeDots.probes.Count)
            {
                GameObject selectedProbe = probeDots.probes[probeDots.selectedProbeIndex];
                Renderer renderer = selectedProbe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ProbeColors.Completed;
                }
                probeDots.selectedProbeIndex = -1;

                // Exit focus mode if available
                if (focusSystem != null)
                {
                    focusSystem.ExitFocusMode();
                }
            }
        }
    }

    private void HandleBackspaceKey()
    {
        if (probeDots == null)
        {
            return;
        }

        if (currentIteration == 2)
        {
            if (probeDots.selectedProbeIndex >= 0)
            {
                // Probe is selected in IT2: Deselect it (return to IT2 selection mode)
                GameObject selectedProbe = probeDots.probes[probeDots.selectedProbeIndex];
                Renderer renderer = selectedProbe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ProbeColors.Default;
                }
                probeDots.selectedProbeIndex = -1;
            }
            else
            {
                // No probe selected in IT2: Return to IT1 selection mode
                ReturnToIteration1();
            }
        }
        else if (currentIteration == 1)
        {
            // In IT1, BACKSPACE just deselects if a probe is selected
            if (probeDots.selectedProbeIndex >= 0)
            {
                GameObject selectedProbe = probeDots.probes[probeDots.selectedProbeIndex];
                Renderer renderer = selectedProbe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.material.color = ProbeColors.Default;
                }
                probeDots.selectedProbeIndex = -1;
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

        // Hide all iteration 1 probes except the selected one
        HideIteration1ProbesExcept(selectedProbe);

        // Ensure the parent probe is visible
        selectedProbe.SetActive(true);
        Renderer parentRenderer = selectedProbe.GetComponent<Renderer>();
        if (parentRenderer != null)
        {
            parentRenderer.enabled = true;
            // Set to default color (not selected anymore)
            parentRenderer.material.color = ProbeColors.Default;
        }

        // Set the selected probe's influence radius to 1 for iteration 2
        UpdateProbeInfluenceRadius(selectedProbe, 1);

        // Ensure center fixation point is visible
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

        // Spawn iteration 2 probes around the selected probe
        SpawnIteration2Probes(selectedProbe.transform.position, parentProbeIndex);

        // Update state
        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;
        probeDots.selectedProbeIndex = -1;
    }

    private void ReturnToIteration2(int parentProbeIndex)
    {
        GameObject parentProbe = iterationProbes[1][parentProbeIndex];
        if (parentProbe == null || gridRebuildManager == null)
        {
            return;
        }

        if (!gridRebuildManager.probeGridIndices.ContainsKey(parentProbe))
        {
            return;
        }

        // Get parent probe's grid position
        Vector2Int parentGridIndex = gridRebuildManager.probeGridIndices[parentProbe];
        int parentRow = parentGridIndex.y;
        int parentCol = parentGridIndex.x;

        // Hide all iteration 1 probes except parent
        HideIteration1ProbesExcept(parentProbe);

        // Update parent probe influence radius to 1 for iteration 2
        UpdateProbeInfluenceRadius(parentProbe, 1);

        // Get existing iteration 2 probes and their positions
        List<GameObject> iteration2Probes = parentProbeToIteration2Probes[parentProbeIndex];
        Dictionary<GameObject, Vector3> iteration2Positions = parentProbeToIteration2Positions[parentProbeIndex];

        // Position parent probe at its current deformed position
        Vector3 parentDeformedLocalPos = gridRebuildManager.GetDeformedGridPoint(parentRow, parentCol);
        parentDeformedLocalPos.z = -0.15f;
        parentProbe.transform.position = gridRebuildManager.transform.TransformPoint(parentDeformedLocalPos);

        // Make parent probe visible
        parentProbe.SetActive(true);
        Renderer parentRenderer = parentProbe.GetComponent<Renderer>();
        if (parentRenderer != null)
        {
            parentRenderer.enabled = true;
        }

        // Restore iteration 2 probes at their current deformed positions
        foreach (GameObject probe in iteration2Probes)
        {
            if (probe != null && gridRebuildManager.probeGridIndices.ContainsKey(probe))
            {
                Vector2Int probeGridIndex = gridRebuildManager.probeGridIndices[probe];
                int probeRow = probeGridIndex.y;
                int probeCol = probeGridIndex.x;

                // Get deformed position in local space and convert to world space
                Vector3 deformedLocalPos = gridRebuildManager.GetDeformedGridPoint(probeRow, probeCol);
                deformedLocalPos.z = -0.15f;
                probe.transform.position = gridRebuildManager.transform.TransformPoint(deformedLocalPos);

                probe.SetActive(true);
                Renderer renderer = probe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        // Build the combined probe list: iteration 2 probes + parent probe
        List<GameObject> allIteration2Probes = new List<GameObject>(iteration2Probes);
        allIteration2Probes.Add(parentProbe);

        // Build the combined positions dictionary
        Dictionary<GameObject, Vector3> allIteration2Positions = new Dictionary<GameObject, Vector3>(iteration2Positions);
        if (gridRebuildManager.probeOriginalPositions.ContainsKey(parentProbe))
        {
            allIteration2Positions[parentProbe] = gridRebuildManager.probeOriginalPositions[parentProbe];
        }

        // Update ProbeDots to work with iteration 2 probes
        probeDots.probes = allIteration2Probes;
        probeDots.probeInitialPositions = allIteration2Positions;
        probeDots.selectedProbeIndex = -1;

        // Force grid rebuild to ensure IT2 probes are visible
        if (gridRebuildManager != null)
        {
            gridRebuildManager.ForceRebuild();
        }

        // Update state
        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;
    }

    private void SpawnIteration2Probes(Vector3 centerPosition, int parentProbeIndex)
    {
        List<GameObject> newIteration2Probes = new List<GameObject>();
        Dictionary<GameObject, Vector3> newIteration2Positions = new Dictionary<GameObject, Vector3>();

        if (gridRebuildManager == null)
        {
            return;
        }

        GameObject selectedProbe = iterationProbes[1][parentProbeIndex];
        if (selectedProbe == null)
        {
            return;
        }

        if (!gridRebuildManager.probeGridIndices.ContainsKey(selectedProbe))
        {
            Debug.LogError($"Parent probe not found in probeGridIndices!");
            return;
        }

        // Get parent probe's grid position
        Vector2Int parentGridIndex = gridRebuildManager.probeGridIndices[selectedProbe];
        int parentRow = parentGridIndex.y;
        int parentCol = parentGridIndex.x;

        int gridSizeValue = gridRebuildManager.GetGridSize();

        // Define the 8 neighboring positions around the parent (3x3 grid minus center)
        Vector2Int[] neighborOffsets = new Vector2Int[]
        {
            new Vector2Int(-1, -1),  // Bottom-left
            new Vector2Int(0, -1),   // Bottom-center
            new Vector2Int(1, -1),   // Bottom-right
            new Vector2Int(-1, 0),   // Middle-left
            new Vector2Int(1, 0),    // Middle-right
            new Vector2Int(-1, 1),   // Top-left
            new Vector2Int(0, 1),    // Top-center
            new Vector2Int(1, 1)     // Top-right
        };

        int probeCounter = 0;

        foreach (Vector2Int offset in neighborOffsets)
        {
            int newRow = parentRow + offset.y;
            int newCol = parentCol + offset.x;

            // Skip if out of bounds (excluding grid borders)
            if (newRow <= 0 || newRow >= gridSizeValue || 
                newCol <= 0 || newCol >= gridSizeValue)
            {
                continue;
            }

            // Get the current deformed position for this grid point
            Vector3 gridLocalPos = gridRebuildManager.GetDeformedGridPoint(newRow, newCol);
            gridLocalPos.z = -0.15f; // Probe Z offset in local space
            Vector3 gridPosition = gridRebuildManager.transform.TransformPoint(gridLocalPos);

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = $"ProbePoint_Parent{parentProbeIndex}_Iteration2_{probeCounter}";
            probe.transform.position = gridPosition;
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

            // Store the current deformed position as the "initial" position for IT2
            // This is the position the probe starts from in iteration 2
            newIteration2Positions[probe] = gridPosition;
            newIteration2Probes.Add(probe);

            // Register with GridRebuildManager (iteration level = 2, influence radius = 1)
            // Use the deformed position as the "original" so displacement starts from here
            Vector2Int gridIndex = new Vector2Int(newCol, newRow);
            gridRebuildManager.RegisterProbe(probe, gridPosition, 2, gridIndex);


            probeCounter++;
        }

        // Store iteration 2 data
        parentProbeToIteration2Probes[parentProbeIndex] = newIteration2Probes;
        parentProbeToIteration2Positions[parentProbeIndex] = newIteration2Positions;

        // Build complete probe list: iteration 2 probes + parent probe
        List<GameObject> allIteration2Probes = new List<GameObject>(newIteration2Probes);
        allIteration2Probes.Add(selectedProbe);

        // Build complete positions dictionary
        Dictionary<GameObject, Vector3> allIteration2Positions = new Dictionary<GameObject, Vector3>(newIteration2Positions);
        if (gridRebuildManager.probeOriginalPositions.ContainsKey(selectedProbe))
        {
            allIteration2Positions[selectedProbe] = gridRebuildManager.probeOriginalPositions[selectedProbe];
        }

        // Update ProbeDots to work with iteration 2 probes
        probeDots.probes = allIteration2Probes;
        probeDots.probeInitialPositions = allIteration2Positions;
        probeDots.selectedProbeIndex = -1;

        // Force grid rebuild to ensure IT2 probes are visible
        if (gridRebuildManager != null)
        {
            gridRebuildManager.ForceRebuild();
        }
    }

    private void ReturnToIteration1()
    {
        // Hide all iteration 2 probes
        HideAllIteration2Probes();

        // Restore all iteration 1 probes
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

                    // Restore influence radius to 2 for iteration 1
                    UpdateProbeInfluenceRadius(probe, 2);
                }
            }
        }

        // Ensure center fixation point is visible
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

        // Restore ProbeDots to iteration 1 state
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

        // Update state
        currentIteration = 1;
        currentParentProbeIndex = -1;
    }

    private void HideIteration1ProbesExcept(GameObject exceptProbe)
    {
        if (iterationProbes.ContainsKey(1))
        {
            foreach (GameObject probe in iterationProbes[1])
            {
                if (probe != null && probe != exceptProbe)
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

    private void UpdateProbeInfluenceRadius(GameObject probe, int radius)
    {
        if (gridRebuildManager != null && probe != null)
        {
            if (gridRebuildManager.probeInfluenceRadius.ContainsKey(probe))
            {
                gridRebuildManager.probeInfluenceRadius[probe] = radius;
            }
        }
    }

    // Public accessors
    public int CurrentIteration => currentIteration;
    public bool IsInIteration2 => currentIteration == 2;
    public int CurrentParentProbeIndex => currentParentProbeIndex;

    public bool HasIteration2ForProbe(int probeIndex)
    {
        return parentProbeToIteration2Probes.ContainsKey(probeIndex) &&
               parentProbeToIteration2Probes[probeIndex].Count > 0;
    }
}