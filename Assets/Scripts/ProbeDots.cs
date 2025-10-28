using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages probe dots within the application
public class ProbeDots : MonoBehaviour
{
    // Definition of references to main grid
    private MainGrid mainGrid;
    private ProbeDotConstraints constraints;

    // Definition of configuration parameters for probe dots (they were previously generated)
    public float probeDotSize = 0.2f; // Size of each probe dot
    public float moveSpeed = 2f; // Speed of probe movement
    public float probeSpacing = 0f; // Spacing between probes

    private List<GameObject> probes = new List<GameObject>(); // List of probe GameObjects
    private Dictionary<GameObject, Vector3> probeInitialPositions = new Dictionary<GameObject, Vector3>(); // Initial positions of probes
    private int selectedProbeIndex = -1; // Index of the currently selected probe

    // Neighbor tracking for constraint application
    private Dictionary<int, List<int>> probeNeighbors = new Dictionary<int, List<int>>(); // Maps probe index to its neighbor indices

    // Variables for movement tracking
    private Vector3 movementStartPosition; // Position at the beginning of the movement
    private bool isMoving = false; // Tracking about movement of probe
    private Vector3 lockedAxis; // Which axis is locked during movement

    // Grid line tracking for movement constraints
    private List<float> gridLinePositionsX = new List<float>(); // Valid X positions (vertical grid lines)
    private List<float> gridLinePositionsY = new List<float>(); // Valid Y positions (horizontal grid lines)
    private int currentGridLineX = -1; // Current vertical line index
    private int currentGridLineY = -1; // Current horizontal line index

    // Initialization of all probe dot functionalities
    void Start()
    {
        if (mainGrid == null) // Ensure mainGrid reference is set
        {
            mainGrid = FindObjectOfType<MainGrid>();
            if (mainGrid == null) // Try to find the MainGrid component. If not found, exit.
            {
                return;
            }
        }

        // Initialize or find the constraints component
        constraints = GetComponent<ProbeDotConstraints>();
        if (constraints == null)
        {
            constraints = gameObject.AddComponent<ProbeDotConstraints>();
        }

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
        CalculateGridLinePositions(); // Calculate all valid grid line positions for movement
    }

    void Update() // Update is called once per frame
    {
        HandleProbeSelection();
        HandleProbeMovement();
        HandleKeys();
    }

    // FUNCTION: Calculate all valid grid line positions based on the grid structure
    private void CalculateGridLinePositions()
    {
        gridLinePositionsX.Clear();
        gridLinePositionsY.Clear();

        float cellSize = mainGrid.CellSize;
        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        // Calculate origin (bottom-left corner of grid)
        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        int pointsPerDimension = mainGrid.GridSize + 1; // For 8x8 grid, there are 9 lines in each direction

        // Calculate all horizontal and vertical grid line positions
        for (int i = 0; i < pointsPerDimension; i++)
        {
            gridLinePositionsX.Add(originX + i * cellSize); // Vertical lines (X positions)
            gridLinePositionsY.Add(originY + i * cellSize); // Horizontal lines (Y positions)
        }
    }

    // FUNCTION: Find the nearest grid line index for a given position
    private int FindNearestGridLineIndex(float position, List<float> gridLines)
    {
        if (gridLines == null || gridLines.Count == 0)
            return -1;

        int nearestIndex = 0;
        float minDistance = Mathf.Abs(position - gridLines[0]);

        for (int i = 1; i < gridLines.Count; i++)
        {
            float distance = Mathf.Abs(position - gridLines[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    // FUNCTION: Snap a position to the nearest grid line
    private Vector3 SnapToGridLine(Vector3 position, bool isHorizontalMovement)
    {
        Vector3 snappedPosition = position;

        if (isHorizontalMovement)
        {
            // Moving horizontally (left/right), so snap Y to nearest horizontal grid line
            int nearestLineY = FindNearestGridLineIndex(position.y, gridLinePositionsY);
            if (nearestLineY >= 0 && nearestLineY < gridLinePositionsY.Count)
            {
                snappedPosition.y = gridLinePositionsY[nearestLineY];
                currentGridLineY = nearestLineY;
            }
        }
        else
        {
            // Moving vertically (up/down), so snap X to nearest vertical grid line
            int nearestLineX = FindNearestGridLineIndex(position.x, gridLinePositionsX);
            if (nearestLineX >= 0 && nearestLineX < gridLinePositionsX.Count)
            {
                snappedPosition.x = gridLinePositionsX[nearestLineX];
                currentGridLineX = nearestLineX;
            }
        }

        return snappedPosition;
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

    // FUNCTION: Handle probe selection via mouse click
    private void HandleProbeSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                for (int i = 0; i < probes.Count; i++)
                {
                    if (hit.collider.gameObject == probes[i])
                    {
                        SelectProbe(i);
                        return;
                    }
                }
            }
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
                movementStartPosition = currentPosition;
                lockedAxis = Vector3.zero;

                // Determine which axis to lock based on first input
                bool isHorizontalMovement = Mathf.Abs(inputDirection.x) > Mathf.Abs(inputDirection.y);

                if (isHorizontalMovement)
                {
                    lockedAxis = new Vector3(1, 0, 1); // Allow X movement only
                    // Snap to nearest horizontal grid line when starting horizontal movement
                    movementStartPosition = SnapToGridLine(currentPosition, true);
                }
                else
                {
                    lockedAxis = new Vector3(0, 1, 1); // Allow Y movement only
                    // Snap to nearest vertical grid line when starting vertical movement
                    movementStartPosition = SnapToGridLine(currentPosition, false);
                }

                // Update position to snapped position
                selectedProbe.transform.position = movementStartPosition;
                currentPosition = movementStartPosition;
            }

            // Apply movement only on the locked axis
            proposedPosition += inputDirection * speed;

            // Constrain to the locked axis and snap to grid line
            if (lockedAxis.x == 0) // Y-axis movement only (vertical)
            {
                proposedPosition.x = movementStartPosition.x;
                // Keep X locked to the grid line
                proposedPosition = SnapToGridLine(proposedPosition, false);
            }
            else // X-axis movement only (horizontal)
            {
                proposedPosition.y = movementStartPosition.y;
                // Keep Y locked to the grid line
                proposedPosition = SnapToGridLine(proposedPosition, true);
            }

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
        // Space key: Complete/mark current probe as done
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count) // Ensure that a probe is selected for completing the movement process
            {
                probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Completed; // Change color to 'completed' state
                selectedProbeIndex = -1; // Deselect probe
            }
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Deselect all probes
            if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
            {
                probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Completed;
            }
            selectedProbeIndex = -1;
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
        // Only including probes within reasonable distance (≤5.0 units)
        // Cell size = 1.25, so: adjacent = 2.5u, diagonal(√2 cells) = 3.5u, 2 cells away = 5.0u

        probeNeighbors[0] = new List<int> { 1, 2, 3, 5 };           // Bottom-left: right(2.5), far-right(5.0), up(2.5), far-up(5.0)
        probeNeighbors[1] = new List<int> { 0, 2, 3, 4, 6 };        // Bottom-center: left(2.5), right(2.5), diag-up-left(3.5), diag-up-right(3.5), far-up(5.0)
        probeNeighbors[2] = new List<int> { 0, 1, 4, 7 };           // Bottom-right: far-left(5.0), left(2.5), up(2.5), far-up(5.0)
        probeNeighbors[3] = new List<int> { 0, 1, 5, 6 };           // Middle-left: down(2.5), diag-down-right(3.5), up(2.5), diag-up-right(3.5)
        probeNeighbors[4] = new List<int> { 1, 2, 6, 7 };           // Middle-right: diag-down-left(3.5), down(2.5), diag-up-left(3.5), up(2.5)
        probeNeighbors[5] = new List<int> { 0, 3, 6 };              // Top-left: far-down(5.0), down(2.5), right(2.5)
        probeNeighbors[6] = new List<int> { 1, 3, 4, 5, 7 };        // Top-center: far-down(5.0), diag-down-left(3.5), diag-down-right(3.5), left(2.5), right(2.5)
        probeNeighbors[7] = new List<int> { 2, 4, 6 };              // Top-right: far-down(5.0), down(2.5), left(2.5)
    }

    // FUNCTION: Get the INITIAL world positions of all neighbors for a given probe
    // Uses initial positions to create fixed movement boundaries
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
                    // Use INITIAL position instead of current position to create fixed bounds
                    if (probeInitialPositions.ContainsKey(neighborProbe))
                    {
                        neighborPositions.Add(probeInitialPositions[neighborProbe]);
                    }
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