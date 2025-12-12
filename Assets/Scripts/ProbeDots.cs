using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Input method for the dropdown in the Unity inspector

public enum ProbeInputMethod
{
    Keyboard,
    ViveTrackpad
}

// Manages probe dots within the application
public class ProbeDots : MonoBehaviour
{
    // Definition of references to main grid
    private MainGrid mainGrid;
    private ProbeDotConstraints constraints;
    private FocusSystem focusSystem;
    private IterationManager iterationManager;
    private VRInputHandler vrInputHandler;

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
    private bool isMoving = false;

    // VR displacement settings
    [SerializeField] private ProbeInputMethod inputMethod = ProbeInputMethod.Keyboard;
    [SerializeField] private float vrMovementSpeed = 1.5f;
    [SerializeField] private float vrSensitivity = 1.0f;

    // Initialization of all probe dot functionalities
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();

        // Define GO in reference to mainGrid
        if (mainGrid != null)
        {
            Vector3 alignedPosition = mainGrid.GridCenterPosition;
            transform.position = alignedPosition;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
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

        // Initialize VR INPUT handler if required
        InitializeVRInput();

        // Start coroutine to wait for grid points to be ready
        StartCoroutine(InitializeProbes());
    }

    private void InitializeVRInput()
    {
        if (inputMethod == ProbeInputMethod.ViveTrackpad)
        {
            vrInputHandler = FindObjectOfType<VRInputHandler>();

            if (vrInputHandler == null)
            {
                GameObject vrInputObj = new GameObject("VRInputHandler");
                vrInputHandler = vrInputObj.AddComponent<VRInputHandler>();
            }
        }
    }

    private IEnumerator InitializeProbes()
    {
        // Wait until grid points are created by MainGrid
        while (mainGrid.GridPoints == null)
        {
            yield return null; // Wait one frame
        }

        CreateProbes(); // Execute method for probe creation after grid points are ready
        IdentifyProbeNeighbors(); // Identify which probes are neighbors of each other
    }

    // Update is called once per frame
    void Update()
    {
        // Numpad selection works in BOTH modes
        HandleKeyboardProbeSelection();

        if (inputMethod == ProbeInputMethod.ViveTrackpad)
        {
            // VR MODE: Controller input for completion and movement
            HandleVRTriggerComplete(); // Handle VR trigger for marking probe complete
            HandleProbeMovement();
        }
        else
        {
            // KEYBOARD MODE: Keyboard movement only
            HandleProbeMovement();
        }
    }

    // METHOD: Creation of probes
    private void CreateProbes()
    {
        // Get the grid points from the MainGrid
        GameObject[,] gridPoints = mainGrid.GridPoints;

        if (gridPoints == null) // Safety: Check on whether the gridPoints are available (or not)
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
                renderer.material.renderQueue = 3100; // Ensure probes render on top without z offsets
            }

            // Set the scale of the grid points to probe dot size
            gridPoint.transform.localScale = Vector3.one * probeDotSize;

            Vector3 adjustedPos = gridPoint.transform.position;

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

    // METHOD: Handle probe selection via numerical pad keyboard (1-9)
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

    // HELPER METHOD: Map keyboard position (0-8) to probe index based on iteration
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

    // METHOD: Handle VR trigger for marking probe complete (same as Space bar)
    private void HandleVRTriggerComplete()
    {
        if (inputMethod == ProbeInputMethod.ViveTrackpad)
        {
            if (vrInputHandler != null && vrInputHandler.TriggerPressed)
            {
                // Mark probe as complete (same logic as Space bar)
                if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
                {
                    GameObject selectedProbe = probes[selectedProbeIndex];
                    Renderer renderer = selectedProbe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.material.color = ProbeColors.Completed; // Turn probe GREEN
                    }
                    selectedProbeIndex = -1; // Deselect the probe

                    // Exit focus mode if active
                    if (focusSystem != null)
                    {
                        focusSystem.ExitFocusMode();
                    }

                    Debug.Log("VR Trigger: Marked probe as complete");
                }
            }
        }
    }

    // METHOD: Handle probe movement based on selected input method
    private void HandleProbeMovement()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            GameObject selectedProbe = probes[selectedProbeIndex];
            Vector3 currentPosition = selectedProbe.transform.position;
            Vector3 proposedPosition = currentPosition;

            bool hasInput = false;
            Vector3 inputDirection = Vector3.zero;

            // Vive Trackpad displacement method

            if (inputMethod == ProbeInputMethod.ViveTrackpad)
            {
                if (vrInputHandler != null && vrInputHandler.IsControllerAvailable()) // Verification of the INPUT system being ready
                {
                    if (vrInputHandler.IsTrackpadPressed)
                    {
                        inputDirection = vrInputHandler.GetMovementDirection(vrSensitivity);
                        hasInput = inputDirection.magnitude > 0.01f;

                        if (hasInput)
                        {
                            float speed = vrMovementSpeed * Time.deltaTime;
                            proposedPosition += inputDirection * speed;
                        }
                    }
                }
            }
            
            // Keyboard displacement method

            else if (inputMethod == ProbeInputMethod.Keyboard)
            {
                hasInput = GetKeyboardDisplacementInput(ref inputDirection, ref proposedPosition, currentPosition);
            }

            if (hasInput)
            {
                if (!isMoving)
                {
                    isMoving = true;
                }

                proposedPosition.z = currentPosition.z;
                
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
                isMoving = false;
            }
        }
    }

    // METHOD: Obtain keyboard INPUT for displacement via arrow keys
    private bool GetKeyboardDisplacementInput(ref Vector3 inputDirection, ref Vector3 proposedPosition, Vector3 currentPosition)
    {
        bool hasInput = false;
        float speed = moveSpeed * Time.deltaTime;

        inputDirection = Vector3.zero; // FIXED: Reset inputDirection at the start

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
            if (inputDirection.magnitude > 1f)
            {
                inputDirection.Normalize();
            }

            proposedPosition += inputDirection * speed;
        }

        return hasInput;
    }

    // METHOD: Select a probe by index
    private void SelectProbe(int index)
    {
        if (index >= probes.Count) return; // Safety: If the index of the probe is out of range, exit.

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

    //METHOD: Identify which probes are neighbors of each other based on their grid layout
    private void IdentifyProbeNeighbors()
    {
        probeNeighbors.Clear();

        // Define neighbor relationships

        probeNeighbors[0] = new List<int> { 1, 2, 3, 5 };           
        probeNeighbors[1] = new List<int> { 0, 2, 3, 4, 6 };        
        probeNeighbors[2] = new List<int> { 0, 1, 4, 7 };           
        probeNeighbors[3] = new List<int> { 0, 1, 5, 6 };           
        probeNeighbors[4] = new List<int> { 1, 2, 6, 7 };          
        probeNeighbors[5] = new List<int> { 0, 3, 6 };              
        probeNeighbors[6] = new List<int> { 1, 3, 4, 5, 7 };        
        probeNeighbors[7] = new List<int> { 2, 4, 6 };              
    }

    // METHOD: Get the current world positions of all neighbors for a given probe
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
                    // Use current position to prevent overlap with displaced neighbors
                    neighborPositions.Add(neighborProbe.transform.position);
                }
            }
        }

        // Add central fixation point as a constraint
        if (mainGrid != null)
        {
            Vector3 centerPosition = mainGrid.GridCenterPosition;
            if (probes.Count > 0)
            {
                centerPosition.z = probes[0].transform.position.z;
            }
            neighborPositions.Add(centerPosition);
        }

        return neighborPositions;
    }

    // METHOD: Change INPUT method at runtime
    public void SetInputMethod(ProbeInputMethod method)
    {
        inputMethod = method; // FIXED: Changed from ProbeInputMethod = method

        if (method == ProbeInputMethod.ViveTrackpad && vrInputHandler == null)
        {
            InitializeVRInput();
        }
    }

    // METHOD: Get current input method
    public ProbeInputMethod GetInputMethod()
    {
        return inputMethod;
    }
}