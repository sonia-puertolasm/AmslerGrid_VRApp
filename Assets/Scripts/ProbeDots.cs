using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages probe dots within the application
public class ProbeDots : MonoBehaviour
{
    // Definition of references to main grid
    private MainGrid mainGrid;
    private ProbeDotConstraints constraints;
    private FocusSystem focusSystem;
    private IterationManager iterationManager;

    // Definition of configuration parameters for probe dots (they were previously generated)
    internal float probeDotSize = 0.2f; // Size of each probe dot
    internal float moveSpeed = 2f; // Speed of probe movement
    internal float probeSpacing = 0f; // Spacing between probes

    internal List<GameObject> probes = new List<GameObject>(); // List of probe GameObjects
    internal Dictionary<GameObject, Vector3> probeInitialPositions = new Dictionary<GameObject, Vector3>(); // Initial positions of probes
    internal int selectedProbeIndex = -1; // Index of the currently selected probe

    // Neighbor tracking for constraint application
    private Dictionary<int, List<int>> probeNeighbors = new Dictionary<int, List<int>>(); // Maps probe index to its neighbor indices

    // Variables for movement tracking
    private bool isMoving = false; // Tracking about movement of probe

    // Initialization of all probe dot functionalities
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();

        // Define GO in reference to mainGrid
        if (mainGrid != null)
        {
            transform.position = mainGrid.transform.position;
            transform.rotation = mainGrid.transform.rotation;
            transform.localScale = mainGrid.transform.localScale;
        }

        // Initialize or find the constraints component
        constraints = GetComponent<ProbeDotConstraints>();
        if (constraints == null)
        {
            constraints = gameObject.AddComponent<ProbeDotConstraints>();
        }

        // Initialize focus system reference
        focusSystem = FindObjectOfType<FocusSystem>();

        // Initialize iteration manager reference
        iterationManager = FindObjectOfType<IterationManager>();

        // Start coroutine to wait for grid points to be ready
        StartCoroutine(InitializeProbes());
    }

    private IEnumerator InitializeProbes()
    {
        // Wait until grid points are created by MainGrid
        while (mainGrid.GridPoints == null)
        {
            yield return null; // Wait one frame
        }

        CreateProbes(); // Execute function for probe creation after grid points are ready
        IdentifyProbeNeighbors(); // Identify which probes are neighbors of each other
    }

    void Update() // Update is called once per frame
    {
        HandleKeyboardProbeSelection();
        HandleProbeMovement();
        HandleKeys();
    }

    // FUNCTION: Creation of probes
    private void CreateProbes()
    {
        // Get the grid points from the MainGrid
        GameObject[,] gridPoints = mainGrid.GridPoints;

        if (gridPoints == null) // Safety check on whether the gridPoints are available (or not)
        {
            return;
        }

        int gridSize = mainGrid.GridSize; // Re-definition of the size of the grid
        int spacing = gridSize / 3; // For 8x8 grid: spacing = 2 (grid divisions at indices 2, 4, 6)
        float cellSize = mainGrid.CellSize; // Size of each cell in the grid
        probeSpacing = spacing * cellSize; // Actual spacing in world units

        // Define the 8 probe positions (corners and edges, excluding center)
        List<Vector2Int> probeGridPositions = new List<Vector2Int> // Grid indices for probe placement
        {
            new Vector2Int(spacing, spacing),           // Bottom-left
            new Vector2Int(spacing * 2, spacing),       // Bottom-center
            new Vector2Int(spacing * 3, spacing),       // Bottom-right
            new Vector2Int(spacing, spacing * 2),       // Middle-left
            new Vector2Int(spacing * 3, spacing * 2),   // Middle-right
            new Vector2Int(spacing, spacing * 3),       // Top-left
            new Vector2Int(spacing * 2, spacing * 3),   // Top-center
            new Vector2Int(spacing * 3, spacing * 3)    // Top-right
        };

        foreach (var pos in probeGridPositions) // Loop through each defined probe position
        {
            int row = pos.y; // Y corresponds to the row index
            int col = pos.x; // X corresponds to the column index

            GameObject gridPoint = gridPoints[row, col]; // Get the corresponding grid point

            if (gridPoint == null) // If the grid point is null, skip to next
                continue;

            // Parent the probe dot to this script's GameObject
            gridPoint.transform.SetParent(transform);

            // Enable the renderer to make the grid point visible
            Renderer renderer = gridPoint.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.material.color = ProbeColors.Default;
            }

            // Set the scale of the grid points to probe dot size
            gridPoint.transform.localScale = Vector3.one * probeDotSize;

            // Adjust Z position slightly to be in front of grid lines
            Vector3 currentPos = gridPoint.transform.position;
            Vector3 adjustedPos = new Vector3(currentPos.x, currentPos.y, currentPos.z - 0.15f);
            gridPoint.transform.position = adjustedPos;

            // Store initial position
            probeInitialPositions[gridPoint] = adjustedPos;

            // Mark as interactable in GridPointData
            GridPointData pointData = gridPoint.GetComponent<GridPointData>();
            if (pointData != null)
            {
                pointData.isInteractable = true;
            }

            // Rename for clarity
            gridPoint.name = $"ProbePoint_r{row}_c{col}";

            // Add to probes list
            probes.Add(gridPoint);    
        }
    }

    // FUNCTION: Handle probe selection via numerical pad keyboard (1-9)
    private void HandleKeyboardProbeSelection()
    {
        KeyCode[] numpadKeys = new KeyCode[]
        {
            KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3,
            KeyCode.Keypad4, KeyCode.Keypad5, KeyCode.Keypad6,
            KeyCode.Keypad7, KeyCode.Keypad8, KeyCode.Keypad9
        };

        KeyCode[] alphaKeys = new KeyCode[]
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3,
            KeyCode.Alpha4, KeyCode.Alpha5, KeyCode.Alpha6,
            KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9
        };

        bool isIteration2 = (iterationManager != null && iterationManager.CurrentIteration == 2);

        for (int keyIndex = 0; keyIndex < 9; keyIndex++)
        {
            bool keyPressed = Input.GetKeyDown(numpadKeys[keyIndex]) || Input.GetKeyDown(alphaKeys[keyIndex]);

            if (keyPressed)
            {
                if (!isIteration2 && keyIndex == 4)
                {
                    continue;
                }

                int probeIndex = GetProbeIndexFromKeyPosition(keyIndex, isIteration2);

                if (probeIndex >= 0 && probeIndex < probes.Count)
                {
                    SelectProbe(probeIndex);
                    return;
                }
            }
        }
    }

    // HELPER FUNCTION: Map keyboard position (0-8) to probe index based on iteration
    private int GetProbeIndexFromKeyPosition(int keyPosition, bool isIteration2)
    {
        if (isIteration2)
        {
            int[] keyToProbeMap = { 0, 1, 2, 3, 8, 4, 5, 6, 7 };
            return keyToProbeMap[keyPosition];
        }
        else
        {
            int[] keyToProbeMap = { 0, 1, 2, 3, -1, 4, 5, 6, 7 };
            return keyToProbeMap[keyPosition];
        }
    }

    // FUNCTION: Handle probe movement based on arrow key input
    private void HandleProbeMovement()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
    {
        GameObject selectedProbe = probes[selectedProbeIndex];
        Vector3 currentPosition = selectedProbe.transform.position;
        Vector3 proposedPosition = currentPosition;

        // Check if any arrow key is pressed
        bool hasInput = false;
        float speed = moveSpeed * Time.deltaTime;

        Vector3 inputDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            inputDirection += Vector3.up;
            hasInput = true;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            inputDirection += Vector3.down;
            hasInput = true;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            inputDirection += Vector3.right;
            hasInput = true;
        }
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            inputDirection += Vector3.left;
            hasInput = true;
        }

        if (hasInput)
        {
            // Start movement tracking if not already moving
            if (!isMoving)
            {
                isMoving = true;
            }

            // Normalize input direction to prevent faster diagonal movement
            if (inputDirection.magnitude > 1f)
            {
                inputDirection.Normalize();
            }

            // Apply free 2D movement
            proposedPosition += inputDirection * speed;

            // Keep Z position consistent
            proposedPosition.z = currentPosition.z;

            // Apply constraints if the constraints component is available
            // (This will handle both boundary and neighbor constraints)
            if (constraints != null)
            {
                List<Vector3> neighborPositions = GetNeighborPositions(selectedProbeIndex);
                Vector3 initialPosition = probeInitialPositions[selectedProbe];
                proposedPosition = constraints.ApplyConstraints(proposedPosition, currentPosition, neighborPositions, initialPosition);
            }

            selectedProbe.transform.position = proposedPosition;
        }
        else
        {
            // Reset movement tracking when no input
            isMoving = false;
        }
        }
    }

    // FUNCTION: Select a probe by index
    private void SelectProbe(int index)
    {
        if (index >= probes.Count) return; // Safety check. If the index of the probe is out of range, exit.

        // Deselect previous probe and restore to 'initial' state coloring
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Default;
        }

        // Select new probe and change to 'selected' state coloring
        selectedProbeIndex = index;
        probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Selected;
    }

    private void DeselectAllProbes()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Completed;
        }
        selectedProbeIndex = -1;
    }

    private void HandleKeys()
    {
        // Space key: Complete/mark current probe as done and exit focus mode
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count) // Ensure that a probe is selected for completing the movement process
            {
                GameObject selectedProbe = probes[selectedProbeIndex];

                // Reset movement state
                isMoving = false;

                selectedProbe.GetComponent<Renderer>().material.color = ProbeColors.Completed; // Change color to 'completed' state
                selectedProbeIndex = -1; // Deselect probe

                // Exit focus mode
                if (focusSystem != null)
                {
                    focusSystem.ExitFocusMode();
                }
            }
        }
    }

    // FUNCTION: Identify which probes are neighbors of each other based on their grid layout
    private void IdentifyProbeNeighbors()
    {
        // Probe layout (8 probes in 3x3 grid excluding center):
        // Grid positions in (col, row):
        // 5(2,6)    6(4,6)    7(6,6)
        // 3(2,4)    [CENTER]  4(6,4)
        // 0(2,2)    1(4,2)    2(6,2)

        probeNeighbors.Clear();

        // Define neighbor relationships based on adjacency and close diagonals

        probeNeighbors[0] = new List<int> { 1, 2, 3, 5 };           // Bottom-left: right(2.5), far-right(5.0), up(2.5), far-up(5.0)
        probeNeighbors[1] = new List<int> { 0, 2, 3, 4, 6 };        // Bottom-center: left(2.5), right(2.5), diag-up-left(3.5), diag-up-right(3.5), far-up(5.0)
        probeNeighbors[2] = new List<int> { 0, 1, 4, 7 };           // Bottom-right: far-left(5.0), left(2.5), up(2.5), far-up(5.0)
        probeNeighbors[3] = new List<int> { 0, 1, 5, 6 };           // Middle-left: down(2.5), diag-down-right(3.5), up(2.5), diag-up-right(3.5)
        probeNeighbors[4] = new List<int> { 1, 2, 6, 7 };           // Middle-right: diag-down-left(3.5), down(2.5), diag-up-left(3.5), up(2.5)
        probeNeighbors[5] = new List<int> { 0, 3, 6 };              // Top-left: far-down(5.0), down(2.5), right(2.5)
        probeNeighbors[6] = new List<int> { 1, 3, 4, 5, 7 };        // Top-center: far-down(5.0), diag-down-left(3.5), diag-down-right(3.5), left(2.5), right(2.5)
        probeNeighbors[7] = new List<int> { 2, 4, 6 };              // Top-right: far-down(5.0), down(2.5), left(2.5)
    }

    // FUNCTION: Get the CURRENT world positions of all neighbors for a given probe
    private List<Vector3> GetNeighborPositions(int probeIndex)
    {
        List<Vector3> neighborPositions = new List<Vector3>();

        if (probeNeighbors.ContainsKey(probeIndex))
        {
            foreach (int neighborIndex in probeNeighbors[probeIndex])
            {
                if (neighborIndex >= 0 && neighborIndex < probes.Count)
                {
                    GameObject neighborProbe = probes[neighborIndex];
                    // Use CURRENT position to prevent overlap with displaced neighbors
                    neighborPositions.Add(neighborProbe.transform.position);
                }
            }
        }

        // Add central fixation point as a constraint
        if (mainGrid != null)
        {
            Vector3 centerPosition = mainGrid.GridCenterPosition;
            // Adjust Z to match probe depth
            if (probes.Count > 0)
            {
                centerPosition.z = probes[0].transform.position.z;
            }
            neighborPositions.Add(centerPosition);
        }

        return neighborPositions;
    }
}