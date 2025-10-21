using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class IAGIteration : MonoBehaviour
{
    private MainGrid mainGrid;
    private IAGFocusSystem focusSystem;
    private ProbeDotConstraints constraintManager;
    private DisplacementTracker displacementTracker;

    private int currentIteration = 1;
    private const int iteration1_probecount = 8;
    private const int iterationhigher_probecount = 8;

    private float probeDotSize = 0.2f;
    private float moveSpeed = 4f;

    private List<GameObject> iteration1Probes = new List<GameObject>();
    private List<GameObject> iterationcurrentProbes = new List<GameObject>();
    private Dictionary<int, List<GameObject>> iterationProbes = new Dictionary<int, List<GameObject>>();

    private class ProbeState
    {
        public Vector3 position;
        public bool isCompleted;
    }
    
    private class IterationState
    {
        public int iteration;
        public List<GameObject> probes;
        public GameObject selectedProbe;
        public HashSet<int> completedIndices;
        public int completedCount;
        public Dictionary<int, ProbeState> probePositions;
    }

    private Stack<IterationState> iterationHistory = new Stack<IterationState>();

    private HashSet<int> completedProbeIndices = new HashSet<int>();

    private int selectedProbeIndex = -1;
    private int completedProbeCount = 0;

    private GameObject selectedIteration1Probe = null;
    private GameObject currentCenterProbe = null;

    private bool isSelectingRegion = false;
    private bool canSelectCenter = false;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid == null)
        {
            return;
        }

        focusSystem = FindObjectOfType<IAGFocusSystem>();
        if (focusSystem == null)
        {
            GameObject focusManagerObj = new GameObject("FocusManager");
            focusSystem = focusManagerObj.AddComponent<IAGFocusSystem>();
        }

        constraintManager = FindObjectOfType<ProbeDotConstraints>();
        if (constraintManager == null)
        {
            GameObject constraintObj = new GameObject("ConstraintManager");
            constraintManager = constraintObj.AddComponent<ProbeDotConstraints>();
        }

        displacementTracker = FindObjectOfType<DisplacementTracker>();
        if (displacementTracker == null)
        {
            GameObject displacementObj = new GameObject("DisplacementTracker");
            displacementTracker = displacementObj.AddComponent<DisplacementTracker>();
        }

        SetupFocusSystem();
        StartIteration1();
    }

    private void SetupFocusSystem()
    {
        if (focusSystem != null && mainGrid != null)
        {
            focusSystem.SetGridParent(mainGrid.transform);
            focusSystem.SetProbeParent(this.transform);
        }
    }

    void Update()
    {
        if (isSelectingRegion)
        {
            HandleRegionSelection();
        }
        else
        {
            HandleProbeSelection();
            HandleProbeMovement();
        }

        HandleKeys();
    }

    private void StartIteration1()
    {
        currentIteration = 1;
        completedProbeCount = 0;
        completedProbeIndices.Clear();
        isSelectingRegion = false;
        selectedProbeIndex = -1;
        canSelectCenter = false;

        CreateIteration1Probes();
        iterationcurrentProbes = iteration1Probes;
    }

    private void CreateIteration1Probes()
    {
        ClearProbes(iteration1Probes);
        iteration1Probes.Clear();

        float cellSize = mainGrid.CellSize;
        int gridSize = mainGrid.GridSize;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        int spacing = gridSize / 3;

        List<Vector2Int> probePositions = new List<Vector2Int>
        {
            new Vector2Int(spacing,spacing),
            new Vector2Int(spacing * 2,spacing),
            new Vector2Int(spacing * 3,spacing),
            new Vector2Int(spacing,spacing * 2),
            new Vector2Int(spacing * 3,spacing * 2),
            new Vector2Int(spacing,spacing * 3),
            new Vector2Int(spacing * 2,spacing * 3),
            new Vector2Int(spacing * 3,spacing * 3)
        };

        float totalGridWidth = mainGrid.TotalGridWidth;

        foreach (var pos in probePositions)
        {
            float worldX = (pos.x * cellSize - totalGridWidth / 2f) + gridCenter.x;
            float worldY = (pos.y * cellSize - totalGridWidth / 2f) + gridCenter.y;

            GameObject probe = CreateProbe(worldX, worldY, gridCenter.z - 0.15f, iteration1Probes.Count);
            iteration1Probes.Add(probe);
        }
    }

    private void StartHigherIteration(GameObject centerProbe)
{
    Dictionary<int, ProbeState> currentProbePositions = new Dictionary<int, ProbeState>();

    for (int i = 0; i < iterationcurrentProbes.Count; i++)
    {
        GameObject probe = iterationcurrentProbes[i];
        currentProbePositions[i] = new ProbeState
        {
            position = probe.transform.position,
            isCompleted = completedProbeIndices.Contains(i)
        };
    }

    IterationState currentState = new IterationState
    {
        iteration = currentIteration,
        probes = new List<GameObject>(iterationcurrentProbes),
        selectedProbe = centerProbe,
        completedIndices = new HashSet<int>(completedProbeIndices),
        completedCount = completedProbeCount,
        probePositions = currentProbePositions // Save positions
    };

    iterationHistory.Push(currentState);

    currentIteration++;
    completedProbeCount = 0;
    completedProbeIndices.Clear();
    isSelectingRegion = false;
    selectedProbeIndex = -1;

    if (currentIteration == 2)
    {
        selectedIteration1Probe = centerProbe;
    }

    foreach (var probe in iterationcurrentProbes)
    {
        probe.SetActive(false);
    }

    centerProbe.SetActive(true);
    centerProbe.GetComponent<Renderer>().material.color = ProbeColors.CenterHigherIt;

    currentCenterProbe = centerProbe;

    // Check if probes for this iteration already exist
    if (iterationProbes.ContainsKey(currentIteration))
    {
        // Restore existing probes
        RestoreExistingIterationProbes(currentIteration);
    }
    else
    {
        // Create new probes and store them
        CreateHigherIterationProbes(centerProbe.transform.position);
        iterationProbes[currentIteration] = new List<GameObject>(iterationcurrentProbes);
    }
}

    private void RestoreExistingIterationProbes(int iteration)
    {
        // Get the existing probes for this iteration
        iterationcurrentProbes = iterationProbes[iteration];

        // Reactivate all probes with their preserved positions and colors
        foreach (var probe in iterationcurrentProbes)
        {
            probe.SetActive(true);
            // Probes retain their displaced positions because they're the same GameObjects
        }
    }

    private void CreateHigherIterationProbes(Vector3 centerPosition)
    {
        iterationcurrentProbes = new List<GameObject>();

        float regionSize = mainGrid.TotalGridWidth / Mathf.Pow(3f, currentIteration - 1);
        float step = regionSize / 3f;

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                float offsetX = (x - 1) * step;
                float offsetY = (y - 1) * step;

                Vector3 probePos = centerPosition + new Vector3(offsetX, offsetY, 0);

                int probeIndex = y * 3 + x;
                if (probeIndex == 4)
                {
                    continue;
                }

                GameObject probe = CreateProbe(probePos.x, probePos.y, probePos.z, iterationcurrentProbes.Count);
                iterationcurrentProbes.Add(probe);
            }
        }
    }

    private GameObject CreateProbe(float x, float y, float z, int index)
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        probe.name = $"Probe_Iter{currentIteration}_#{index}";
        probe.transform.SetParent(transform);
        probe.transform.localScale = Vector3.one * probeDotSize;
        probe.transform.position = new Vector3(x, y, z);

        Color initialColor = currentIteration > 1 ? ProbeColors.InactiveHigherIt : ProbeColors.Default;
        probe.GetComponent<Renderer>().material.color = initialColor;

        return probe;
    }

    private void HandleProbeSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (currentCenterProbe != null && hit.collider.gameObject == currentCenterProbe && canSelectCenter)
                {
                    GoForward1Iteration();
                    return;
                }

                for (int i = 0; i < iterationcurrentProbes.Count; i++)
                {
                    if (hit.collider.gameObject == iterationcurrentProbes[i])
                    {
                        SelectProbe(i);
                        break;
                    }
                }
            }
        }
    }

    private void SelectProbe(int index)
    {
        if (index >= iterationcurrentProbes.Count) return;

        if (selectedProbeIndex >= 0 && selectedProbeIndex < iterationcurrentProbes.Count)
        {
            Renderer prevRenderer = iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>();

            if (completedProbeIndices.Contains(selectedProbeIndex))
            {
                prevRenderer.material.color = ProbeColors.Completed;
            }
            else
            {
                Color inactiveColor = currentIteration > 1 ? ProbeColors.InactiveHigherIt : ProbeColors.Default;
                prevRenderer.material.color = inactiveColor;
            }
        }

        selectedProbeIndex = index;

        iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Selected;

        if (focusSystem != null)
        {
            focusSystem.EnterFocusMode(iterationcurrentProbes[selectedProbeIndex]);
        }
    }

    private void HandleProbeMovement()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < iterationcurrentProbes.Count)
        {
            GameObject selectedProbe = iterationcurrentProbes[selectedProbeIndex];
            float speed = moveSpeed * Time.deltaTime;
            Vector3 previousPosition = selectedProbe.transform.position;
            Vector3 proposedPosition = previousPosition;

            if (Input.GetKey(KeyCode.UpArrow))
                proposedPosition += Vector3.up * speed;
            if (Input.GetKey(KeyCode.DownArrow))
                proposedPosition += Vector3.down * speed;
            if (Input.GetKey(KeyCode.RightArrow))
                proposedPosition += Vector3.right * speed;
            if (Input.GetKey(KeyCode.LeftArrow))
                proposedPosition += Vector3.left * speed;

            if (Vector3.Distance(previousPosition, proposedPosition) > 0.001f && constraintManager != null)
            {
                List<Vector3> neighbors = GetNeighborPositions(selectedProbeIndex);
                float maxDistance = constraintManager.GetMaxNeighborDistance(currentIteration);
                Vector3 constrainedPosition = constraintManager.ApplyConstraints(proposedPosition, neighbors, maxDistance, currentIteration);
                
                selectedProbe.transform.position = Vector3.Lerp(previousPosition, constrainedPosition, 0.1f);

                if (focusSystem != null && focusSystem.IsFocused())
                {
                    focusSystem.UpdateFocusPosition(constrainedPosition);
                }
            }
            else if (Vector3.Distance(previousPosition, proposedPosition) > 0.001f)
            {
                selectedProbe.transform.position = proposedPosition;
                
                if (focusSystem != null && focusSystem.IsFocused())
                {
                    focusSystem.UpdateFocusPosition(proposedPosition);
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                CompleteCurrentProbe();
            }
        }
    }

    private List<Vector3> GetNeighborPositions(int currentIndex)
    {
        List<Vector3> neighbors = new List<Vector3>();
        
        if (currentIteration == 1)
        {
            Vector3 currentPos = iterationcurrentProbes[currentIndex].transform.position;
            
            foreach (var probe in iterationcurrentProbes)
            {
                if (probe == iterationcurrentProbes[currentIndex])
                    continue;
                    
                Vector3 probePos = probe.transform.position;
                float distance = Vector2.Distance(
                    new Vector2(currentPos.x, currentPos.y),
                    new Vector2(probePos.x, probePos.y)
                );
                
                float cellSize = mainGrid.CellSize;
                float maxNeighborDist = cellSize * 3.5f;
                
                if (distance < maxNeighborDist)
                {
                    neighbors.Add(probePos);
                }
            }
        }
        else
        {
            int gridIndex = currentIndex < 4 ? currentIndex : currentIndex + 1;
            int row = gridIndex / 3;
            int col = gridIndex % 3;
            
            int[] deltaRow = {-1, -1, -1,  0,  0,  1, 1, 1};
            int[] deltaCol = {-1,  0,  1, -1,  1, -1, 0, 1};
            
            for (int i = 0; i < 8; i++)
            {
                int newRow = row + deltaRow[i];
                int newCol = col + deltaCol[i];
                
                if (newRow >= 0 && newRow < 3 && newCol >= 0 && newCol < 3)
                {
                    int neighborGridIndex = newRow * 3 + newCol;
                    
                    if (neighborGridIndex == 4)
                    {
                        if (currentCenterProbe != null && currentCenterProbe.activeSelf)
                        {
                            neighbors.Add(currentCenterProbe.transform.position);
                        }
                    }
                    else
                    {
                        int neighborProbeIndex = neighborGridIndex < 4 ? neighborGridIndex : neighborGridIndex - 1;
                        
                        if (neighborProbeIndex >= 0 && neighborProbeIndex < iterationcurrentProbes.Count)
                        {
                            neighbors.Add(iterationcurrentProbes[neighborProbeIndex].transform.position);
                        }
                    }
                }
            }
        }
        
        return neighbors;
    }

    private void CompleteCurrentProbe()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < iterationcurrentProbes.Count)
        {
            GameObject currentProbe = iterationcurrentProbes[selectedProbeIndex];
            Renderer probeRenderer = currentProbe.GetComponent<Renderer>();

            bool wasAlreadyCompleted = completedProbeIndices.Contains(selectedProbeIndex);

            probeRenderer.material.color = ProbeColors.Completed;

            if (!wasAlreadyCompleted)
            {
                completedProbeIndices.Add(selectedProbeIndex);
                completedProbeCount++;
            }

            if (focusSystem != null)
            {
                focusSystem.ExitFocusMode();
            }

            selectedProbeIndex = -1;
        }
    }

    private void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (focusSystem != null && focusSystem.IsFocused())
            {
                focusSystem.ExitFocusMode();
            }

            if (selectedProbeIndex >= 0)
            {
                Color inactiveColor = currentIteration > 1 ? ProbeColors.InactiveHigherIt : ProbeColors.Default;
                iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>().material.color = inactiveColor;
                selectedProbeIndex = -1;
            }

            if (isSelectingRegion)
            {
                CancelRegionSelection();
            }
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (focusSystem != null && focusSystem.IsFocused())
            {
                return;
            }

            int expectedProbeCount = currentIteration == 1 ? iteration1_probecount : iterationhigher_probecount;

            if (completedProbeCount >= expectedProbeCount && !isSelectingRegion)
            {
                OnIterationComplete();
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (currentIteration > 1 && !isSelectingRegion && !focusSystem.IsFocused())
            {
                GoBack1Iteration();
            }
        }
    }

    private void OnIterationComplete()
    {
        EnterRegionSelectionMode();
    }

    private void EnterRegionSelectionMode()
    {
        isSelectingRegion = true;

        if (currentIteration == 1)
        {
            foreach (var probe in iteration1Probes)
            {
                probe.GetComponent<Renderer>().material.color = ProbeColors.Completed;
                probe.SetActive(true);
            }
        }
        else
        {
            if (selectedIteration1Probe != null)
            {
                selectedIteration1Probe.SetActive(false);
            }

            foreach (var probe in iterationcurrentProbes)
            {
                probe.GetComponent<Renderer>().material.color = ProbeColors.Completed;
                probe.SetActive(true);
            }

            if (currentCenterProbe != null)
            {
                currentCenterProbe.SetActive(true);
                currentCenterProbe.GetComponent<Renderer>().material.color = ProbeColors.Completed;
            }
        }
    }

    private void HandleRegionSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (currentIteration == 1)
                {
                    for (int i = 0; i < iteration1Probes.Count; i++)
                    {
                        if (hit.collider.gameObject == iteration1Probes[i])
                        {
                            OnRegionSelected(iteration1Probes[i]);
                            return;
                        }
                    }
                }
                else
                {
                    if (currentCenterProbe != null && hit.collider.gameObject == currentCenterProbe)
                    {
                        OnRegionSelected(currentCenterProbe);
                        return;
                    }

                    for (int i = 0; i < iterationcurrentProbes.Count; i++)
                    {
                        if (hit.collider.gameObject == iterationcurrentProbes[i])
                        {
                            OnRegionSelected(iterationcurrentProbes[i]);
                            return;
                        }
                    }
                }
            }
        }
    }

    private void OnRegionSelected(GameObject selectedProbe)
    {
        StartHigherIteration(selectedProbe);
    }

    private void CancelRegionSelection()
    {
        isSelectingRegion = false;

        if (currentIteration == 1)
        {
            foreach (var probe in iteration1Probes)
            {
                probe.GetComponent<Renderer>().material.color = ProbeColors.Completed;
                probe.SetActive(true);
            }
        }
        else
        {
            foreach (var probe in iteration1Probes)
            {
                probe.SetActive(false);
            }

            foreach (var probe in iterationcurrentProbes)
            {
                probe.SetActive(true);
            }
        }
    }

    private void ClearProbes(List<GameObject> probeList)
    {
        foreach (var probe in probeList)
        {
            if (probe != null)
            {
                Destroy(probe);
            }
        }
    }

    private void GoBack1Iteration()
    {
        if (iterationHistory.Count == 0)
        {
            return;
        }

        foreach (var probe in iterationcurrentProbes)
        {
            probe.SetActive(false);
        }

        IterationState previousState = iterationHistory.Pop();

        currentIteration = previousState.iteration;
        iterationcurrentProbes = previousState.probes;
        completedProbeIndices = previousState.completedIndices;
        completedProbeCount = previousState.completedCount;
        currentCenterProbe = previousState.selectedProbe;
        canSelectCenter = true;
        selectedProbeIndex = -1;
        isSelectingRegion = true;

        if (previousState.probePositions != null)
        {
            foreach (var entry in previousState.probePositions)
            {
                int index = entry.Key;
                ProbeState state = entry.Value;

                if (index < iterationcurrentProbes.Count)
                {
                    GameObject probe = iterationcurrentProbes[index];
                    probe.transform.position = state.position;

                    probe.GetComponent<Renderer>().material.color = ProbeColors.Completed;

                    if (state.isCompleted && !completedProbeIndices.Contains(index))
                    {
                        completedProbeIndices.Add(index);  
                    }
                }
            }
        }

        foreach (var probe in iterationcurrentProbes)
        {
            probe.SetActive(true);
            probe.GetComponent<Renderer>().material.color = ProbeColors.Completed;
        }

        if (currentCenterProbe != null)
        {
            currentCenterProbe.SetActive(true);
            currentCenterProbe.GetComponent<Renderer>().material.color = ProbeColors.CenterHigherIt;
        }
    }

    private void GoForward1Iteration()
    {
        StartHigherIteration(currentCenterProbe);
    }
}