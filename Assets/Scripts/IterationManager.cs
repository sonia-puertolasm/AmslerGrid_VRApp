using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages the higher-iteration system
public class IterationManager : MonoBehaviour
{
    // Retrieval of required parameters 
    private ProbeDots probeDots;
    private MainGrid mainGrid;
    private GridRebuildManager gridRebuildManager;
    private DisplacementTracker displacementTracker;
    private FocusSystem focusSystem;
    private VRInputHandler vrInputHandler;

    // Definition of iteration configuration parameters and dictionaries
    private int currentIteration = 1;
    private int currentParentProbeIndex = -1;
    private Dictionary<int, List<GameObject>> iterationProbes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, GameObject> iterationFixationPoints = new Dictionary<int, GameObject>();
    private Dictionary<int, List<GameObject>> parentProbeToIteration2Probes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, Dictionary<GameObject, Vector3>> parentProbeToIteration2Positions = new Dictionary<int, Dictionary<GameObject, Vector3>>();
    public int CurrentIteration => currentIteration;
    public bool IsInIteration2 => currentIteration == 2;
    public int CurrentParentProbeIndex => currentParentProbeIndex;

    // Public accessors for iteration probe data
    public Dictionary<int, List<GameObject>> GetIterationProbes() => iterationProbes;
    public Dictionary<int, List<GameObject>> GetParentProbeToIteration2Probes() => parentProbeToIteration2Probes;
    public Dictionary<int, Dictionary<GameObject, Vector3>> GetParentProbeToIteration2Positions() => parentProbeToIteration2Positions;

    // Retrieval of specific grid parameters
    private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    // Initializations of all iteration-related methods
    void Start()
    {
        // Find required objects
        probeDots = FindObjectOfType<ProbeDots>(); 
        mainGrid = FindObjectOfType<MainGrid>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        focusSystem = FindObjectOfType<FocusSystem>();
        vrInputHandler = FindObjectOfType<VRInputHandler>();

        if (mainGrid != null) // Safety: Check if the main grid element exists before proceeding
        {
            gridSize = mainGrid.GridSize;
            cellSize = mainGrid.CellSize;
            gridCenter = mainGrid.GridCenterPosition;
            halfWidth = mainGrid.TotalGridWidth / 2f;

            AlignToGridCenter();
        }

        // Start coroutine to wait for iteration system to be ready
        StartCoroutine(InitializeIterationSystem());
    }

    // METHOD: Delays startup and snapshots the initial status of the grid
    private IEnumerator InitializeIterationSystem()
    {
        yield return new WaitForSeconds(0.2f); // Delay all setup work for 0.2 seconds to start other routines and populate references

        if (probeDots != null && probeDots.probes != null && probeDots.probes.Count > 0) // Safety: Check if the probe dots exist correctly before performing the function
        {
            iterationProbes[1] = new List<GameObject>(probeDots.probes); // Save the current probe list into the second slot of the list

            GameObject centerFixation = GameObject.Find("CenterFixationPoint");

            if (centerFixation != null) // Safety: Ensure that the center fixation point exists
            {
                iterationFixationPoints[1] = centerFixation; // Save the center fixation point 
            }
        }
    }

    // HELPER METHOD: Align the iteration logic to the center of the grid
    private void AlignToGridCenter()
    {
        if (mainGrid == null) // Safety: Avoids task if the main grid doesn't exist
        {
            return;
        }

        transform.position = mainGrid.GridCenterPosition;
        transform.rotation = Quaternion.identity;
    }


    void Update() // Update is called once per frame
    {
        bool isVRMode = (probeDots != null && probeDots.GetInputMethod() == ProbeInputMethod.ViveTrackpad); // Verifies in which mode are we (VR Controller or Keyboard)

        if (isVRMode)
        {
            HandleVRControllerInput();
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                HandleEnterKey();
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                HandleSpaceBar();
            }

            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                HandleBackspaceKey();
            }
        }
    }


    // METHOD: Handle VR controller input for iteration navigation
    private void HandleVRControllerInput()
    {
        if (vrInputHandler == null || !vrInputHandler.IsControllerAvailable())
            return;

        if (vrInputHandler.TriggerPressed)
        {
            if (currentIteration == 1)
            {
                HandleEnterKey();
            }
            else if (currentIteration == 2)
            {
                HandleBackspaceKey();
            }
        }

        if (vrInputHandler.TrackpadCenterClicked)
        {
            HandleBackspaceKey();
        }
    }
    // HELPER METHOD: Manages the interaction given enter key engagement
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

    // HELPER METHOD: Manages the interaction with the space bar
    private void HandleSpaceBar()
    {
        if (probeDots == null) // Safety: Exit if probe dots don't exist
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
                    renderer.material.color = ProbeColors.Completed; // Turn probe GREEN
                }
                probeDots.selectedProbeIndex = -1; // Deselect the probe

                // Exit focus mode if active
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
                    renderer.material.color = ProbeColors.Completed; // Turn probe GREEN
                }
                probeDots.selectedProbeIndex = -1; // Deselect the probe

                // Exit focus mode if active
                if (focusSystem != null)
                {
                    focusSystem.ExitFocusMode();
                }
            }
        }
    }

    // HELPER METHOD: Manages the interaction with the backspace key
    private void HandleBackspaceKey()
    {
        if (currentIteration > 1 && probeDots != null && probeDots.selectedProbeIndex == -1) // Safety: Ensures the travelling to a previous iteration is possible and the existance of probe dots
        {
            if (currentIteration == 2) // Travelling back to iteration 1 after pressing backspace in iteration 2 ONLY
            {
                ReturnToIteration1();
            }
        }
    }

    // METHOD: Retrieves higher-iteration setup for already existing IT2 probe dots
    private void ReturnToIteration2(int parentProbeIndex)
    {
        GameObject parentProbe = iterationProbes[1][parentProbeIndex];
        if (parentProbe == null || gridRebuildManager == null) // Safety: Checks if the references exist before proceeding
        {
            return;
        }

        if (!gridRebuildManager.probeGridIndices.ContainsKey(parentProbe)) // Safety: Checks if the parent probe exists
        {
            return;
        }

        Vector2Int parentGridIndex = gridRebuildManager.probeGridIndices[parentProbe]; // Indexing of the parent probe in rows (x) and columns (y)
        int parentRow = parentGridIndex.y;
        int parentCol = parentGridIndex.x;

        HideIteration1ProbesExcept(parentProbe);

        UpdateProbeInfluenceRadius(parentProbe, 1);

        List<GameObject> iteration2Probes = parentProbeToIteration2Probes[parentProbeIndex]; // Defines a list for the probe dots for IT2 given the parent probe dot
        Dictionary<GameObject, Vector3> iteration2Positions = parentProbeToIteration2Positions[parentProbeIndex]; // Defines a dictionary for the positions for IT2 given the parent probe dot

        Vector3 parentDeformedPosition = gridRebuildManager.GetDeformedGridPoint(parentRow, parentCol); // Extraction of deformed position of parent probe dot
        parentDeformedPosition.z = gridCenter.z; // Define z-coordinate as center of the grid
        parentProbe.transform.position = parentDeformedPosition;

        parentProbe.SetActive(true); 
        Renderer parentRenderer = parentProbe.GetComponent<Renderer>();
        if (parentRenderer != null) // Safety: Ensures performance only if the parent renderer exists
        {
            parentRenderer.enabled = true;
        }

        foreach (GameObject probe in iteration2Probes) // Iteration over probe dots for IT2
        {
            if (probe != null) // Safety: Proceeds as long as the probe dots exist
            {
                if (gridRebuildManager.probeGridIndices.ContainsKey(probe))
                {
                    Vector2Int probeGridIndex = gridRebuildManager.probeGridIndices[probe];
                    int probeRow = probeGridIndex.y;
                    int probeCol = probeGridIndex.x;

                    Vector3 deformedPosition = gridRebuildManager.GetDeformedGridPoint(probeRow, probeCol);
                    deformedPosition.z = gridCenter.z;

                    probe.transform.position = deformedPosition;
                }

                probe.SetActive(true);
                Renderer renderer = probe.GetComponent<Renderer>();
                if (renderer != null) // Safety: Ensures performance only if the parent renderer exists
                {
                    renderer.enabled = true;
                }
            }
        }

        List<GameObject> allIteration2Probes = new List<GameObject>(iteration2Probes); // Creates a list for storing over time all iteration 2 probes
        allIteration2Probes.Add(parentProbe); // Appends the parent probe to the stored ones

        Dictionary<GameObject, Vector3> allIteration2Positions = new Dictionary<GameObject, Vector3>(iteration2Positions); // Clone iteration 2 map to be able to extend it without impact on the original
        if (gridRebuildManager.probeOriginalPositions.ContainsKey(parentProbe))
        {
            allIteration2Positions[parentProbe] = gridRebuildManager.probeOriginalPositions[parentProbe];
        }

        probeDots.probes = allIteration2Probes;
        probeDots.probeInitialPositions = allIteration2Positions;
        probeDots.selectedProbeIndex = -1;

        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;
    }

    // METHOD: Progress to iteration 2 after enter interaction
    private void AdvanceToIteration2(int parentProbeIndex)
    {
        if (parentProbeIndex < 0 || parentProbeIndex >= iterationProbes[1].Count) // Safety: Avoids execution in case of invalid probe dot indexing
        {
            return;
        }

        GameObject selectedProbe = iterationProbes[1][parentProbeIndex];

        if (selectedProbe == null) // Safety: Avoids execution in case the probe dots don't exist
        {
            return;
        }

        Vector3 newCenterPosition = selectedProbe.transform.position;

        HideIteration1ProbesExcept(selectedProbe); 

        UpdateProbeInfluenceRadius(selectedProbe, 1);

        if (iterationFixationPoints.ContainsKey(1))
        {
            GameObject fixation = iterationFixationPoints[1];
            if (fixation != null) // Safety: Proceeds ONLY if the center fixation point exists
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

    // METHOD: Spawns the probe dots for higher iterations
    private void SpawnIteration2Probes(Vector3 centerPosition, int parentProbeIndex)
    {
        List<GameObject> newIteration2Probes = new List<GameObject>();
        Dictionary<GameObject, Vector3> newIteration2Positions = new Dictionary<GameObject, Vector3>();

        if (gridRebuildManager == null) // Safety: Avoids execution if the deformation system doesn't exist
        {
            return;
        }

        GameObject selectedProbe = iterationProbes[1][parentProbeIndex];

        if (selectedProbe == null) // Safety: Avoids execution if the selected probe doesn't exist
        {
            return;
        }

        if (!gridRebuildManager.probeGridIndices.ContainsKey(selectedProbe)) // Safety: Avoids execution if the probe dot is invalid
        {
            return;
        }

        Vector2Int parentGridIndex = gridRebuildManager.probeGridIndices[selectedProbe];
        int parentRow = parentGridIndex.y;
        int parentCol = parentGridIndex.x;

        int gridSizeValue = gridRebuildManager.GetGridSize();

        Vector2Int[] neighborOffsets = new Vector2Int[]
        {
            new Vector2Int(-1, -1), 
            new Vector2Int(0, -1),  
            new Vector2Int(1, -1),  
            new Vector2Int(-1, 0),  
            new Vector2Int(1, 0),   
            new Vector2Int(-1, 1),  
            new Vector2Int(0, 1),   
            new Vector2Int(1, 1)    
        };

        int probeCounter = 0;

        foreach (Vector2Int offset in neighborOffsets) // Iterates each offset to convert into a candidate cell 
        {
            int newRow = parentRow + offset.y;
            int newCol = parentCol + offset.x;

            if (newRow <= 0 || newRow >= gridSizeValue || 
                newCol <= 0 || newCol >= gridSizeValue) // Safety: Discard offsets that fall outside the valid range of the grid 
            {
                continue;
            }

            Vector3 gridPosition = gridRebuildManager.GetDeformedGridPoint(newRow, newCol);
            gridPosition.z = gridCenter.z; // Align deformed position z-coordinate with grid center

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
                renderer.material.renderQueue = 3100;
            }

            GridPointData pointData = probe.AddComponent<GridPointData>(); // Sets the new probe dots as interactable
            pointData.isInteractable = true;

            Vector3 originalPosition = gridRebuildManager.GetOriginalGridPoint(newRow, newCol);
            originalPosition.z = gridCenter.z;
            
            newIteration2Positions[probe] = originalPosition;
            newIteration2Probes.Add(probe);

            Vector2Int gridIndex = new Vector2Int(newCol, newRow);
            gridRebuildManager.RegisterProbe(probe, originalPosition, 2, gridIndex);

            probeCounter++;
        }

        parentProbeToIteration2Probes[parentProbeIndex] = newIteration2Probes;
        parentProbeToIteration2Positions[parentProbeIndex] = newIteration2Positions;

        List<GameObject> allIteration2Probes = new List<GameObject>(newIteration2Probes);
        allIteration2Probes.Add(selectedProbe);

        Dictionary<GameObject, Vector3> allIteration2Positions = new Dictionary<GameObject, Vector3>(newIteration2Positions);
        if (gridRebuildManager.probeOriginalPositions.ContainsKey(selectedProbe))
        {
            allIteration2Positions[selectedProbe] = gridRebuildManager.probeOriginalPositions[selectedProbe];
        }

        probeDots.probes = allIteration2Probes;
        probeDots.probeInitialPositions = allIteration2Positions;
        probeDots.selectedProbeIndex = -1;
    }

    // METHOD: Hides all probe dots of iteration 1 except the selected 1
    private void HideIteration1ProbesExcept(GameObject exceptProbe)
    {
        if (iterationProbes.ContainsKey(1)) // Safety: Checks before proceeding if iteration 1 has been registered
        {
            foreach (GameObject probe in iterationProbes[1])
            {
                if (probe != null && probe != exceptProbe) // Safety: Ignore when probes are null or the exceptProbe
                {
                    Renderer renderer = probe.GetComponent<Renderer>(); // Disable rendering of probes that are not exceptProbe
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }

                    probe.SetActive(false);
                }
            }
        }
    }

    // METHOD: Hides all the iteration 2 probes when travelling back to iteration 1
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

    // METHOD: Returns to iteration 1 after pressing backspace
    private void ReturnToIteration1()
    {
        HideAllIteration2Probes();

        if (iterationProbes.ContainsKey(1)) // Safety: Checks before proceeding if iteration 1 has been registered
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

                    UpdateProbeInfluenceRadius(probe, 2);
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

    // HELPER METHOD: Checks if there is iteration 2 children stored
    public bool HasIteration2ForProbe(int probeIndex)
    {
        return parentProbeToIteration2Probes.ContainsKey(probeIndex) &&
               parentProbeToIteration2Probes[probeIndex].Count > 0;
    }

    // HELPER METHOD: Updates the influence deformation radius
    private void UpdateProbeInfluenceRadius(GameObject probe, int radius)
    {
        if (gridRebuildManager != null && probe != null) // Safety: Proceeds ONLY if the grid rebuild manager and the probe exist
        {
            if (gridRebuildManager.probeInfluenceRadius.ContainsKey(probe))
            {
                gridRebuildManager.probeInfluenceRadius[probe] = radius;
            }
        }
    }
}