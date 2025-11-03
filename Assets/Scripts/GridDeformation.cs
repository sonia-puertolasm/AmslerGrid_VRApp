using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages dynamic deformation of grid lines based on probe dot movements
public class GridDeformation : MonoBehaviour
{
    // Reference to the main grid and probe dots
    private MainGrid mainGrid;
    private ProbeDots probeDots;

    // Store references to all grid lines organized by type and index with original positions
    public Dictionary<int, List<LineRendererInfo>> horizontalLines = new Dictionary<int, List<LineRendererInfo>>();
    public Dictionary<int, List<LineRendererInfo>> verticalLines = new Dictionary<int, List<LineRendererInfo>>();

    // Maps each probe dot to its corresponding grid lines
    private Dictionary<GameObject, GridLineInfo> probeGridLineMap = new Dictionary<GameObject, GridLineInfo>();

    // Enable/disable deformation
    private bool enableDeformation = true;

    // Movement threshold - only deform if probe moved more than this distance
    private float movementThreshold = 0.01f;

    // Displacement mode tracking
    private bool isInDisplacementMode = false;
    private GameObject activeProbe = null;
    private Vector3 activeProbeStartPosition;
    private Vector3 lastMovementDirection = Vector3.zero; // Track movement direction for dynamic deformation

    // x4 2-line displacement lines definition and storage
    private List<LineRenderer> displacementLines = new List<LineRenderer>(); // List for storage of the drawing of the x4 2-line displacement lines definition
    private Dictionary<LineRenderer, DisplacementLineInfo> displacementLineData = new Dictionary<LineRenderer, DisplacementLineInfo>(); // Dictionary for creating a relationship between each lr and it's associated 2-line displacement line

    // Reference + definition of grid line elements that should be temporarily hidden/invisible
    private List<LineRendererInfo> hiddenSegments = new List<LineRendererInfo>();

    // Dynamic deformation directions (calculated based on movement)
    private Vector3[] deformationDirections = new Vector3[4];

    // Initialization of Amsler Grid's deformation process
    void Start()
    {
        // Find required components that have been previously defined
        mainGrid = FindObjectOfType<MainGrid>();
        probeDots = FindObjectOfType<ProbeDots>();

        if (mainGrid == null || probeDots == null) // Avoid initiation of deformation if the main grid or probe dots are missing
        {
            enabled = false;
            return;
        }

        // Start coroutine to wait for grid initialization
        StartCoroutine(InitializeDeformation());
    }

    private IEnumerator InitializeDeformation() // Coroutine to initialize deformation after grid is created -> allows method to pause and resume
    {
        // Wait until grid is fully created
        yield return new WaitForSeconds(0.1f);

        // Organize all grid lines
        OrganizeGridLines();

        // Map probes to their grid lines
        MapProbesToGridLines();
    }
    void LateUpdate() // Ensure that probe positions are updated first
    {
        if (!enableDeformation) // Skip if the deformation has been disabled
            return;

        UpdateGridDeformation(); // Ensure that the grid visually responds to the user's interactions with the probe dots

        // Check if we should enter or update displacement mode
        if (isInDisplacementMode)
        {
            // Update the 4 displacement lines (these replace the hidden grid segments)
            UpdateDisplacementLines();
        }
    }

    // FUNCTION: Organize all grid lines into horizontal and vertical dictionaries
    private void OrganizeGridLines()
    {
        Transform gridLinesParent = mainGrid.transform.Find("GridLines"); // Find grid lines parent
        if (gridLinesParent == null) // If there are no grid lines, exit
        {
            return;
        }

        // Re-define previously defined variables
        int gridSize = mainGrid.GridSize; 
        float cellSize = mainGrid.CellSize;
        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        // Calculate grid origin -> we use the bottom-left corner as the reference point for all calculations
        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        // Initialize dictionaries for each grid line index
        for (int i = 0; i <= gridSize; i++)
        {
            horizontalLines[i] = new List<LineRendererInfo>(); // Horizontal axis
            verticalLines[i] = new List<LineRendererInfo>(); // Vertical axis
        }

        // Iterate through all line renderers and categorize them
        foreach (Transform lineTransform in gridLinesParent)
        {
            LineRenderer lr = lineTransform.GetComponent<LineRenderer>(); // Obtain LineRenderer component
            if (lr == null)
            {
                continue; // If there is no LineRenderer, skip
            } 

            // Get and store the original start and end positions for each line renderer
            Vector3 start = lr.GetPosition(0);
            Vector3 end = lr.GetPosition(1);

            // Create info object to store line data
            LineRendererInfo lineInfo = new LineRendererInfo
            {
                lineRenderer = lr,
                originalStart = start,     // Store deformation-updated start position
                originalEnd = end,         // Store deformation-updated end position
                initialStart = start,      // Store truly original start position
                initialEnd = end           // Store truly original end position
            };

            // Determine if this is a horizontal or vertical line
            bool isHorizontal = Mathf.Abs(start.y - end.y) < 0.01f;
            bool isVertical = Mathf.Abs(start.x - end.x) < 0.01f;

            if (isHorizontal) // Calculate which grid line (X-coordinates) is this
            {
                // Calculate which horizontal line index this is
                int lineIndex = Mathf.RoundToInt((start.y - originY) / cellSize); // How many cells is this specific line away from the origin?
                if (lineIndex >= 0 && lineIndex <= gridSize) // Add the information to the dictionary if the values are within valid ranges
                {
                    horizontalLines[lineIndex].Add(lineInfo);
                }
            }
            else if (isVertical) // Calculate which grid "column" (Y-coordinates) is this -> same approach as for X-axis
            {
                // Calculate which vertical line index this is
                int lineIndex = Mathf.RoundToInt((start.x - originX) / cellSize); // How many cells in this specific line away from the origin?
                if (lineIndex >= 0 && lineIndex <= gridSize) // Add the information to the dictionary if the values are within valid ranges
                {
                    verticalLines[lineIndex].Add(lineInfo);
                }
            }
        }
    }

    // FUNCTION: Map each probe to its corresponding horizontal and vertical grid lines
    private void MapProbesToGridLines()
    {
        GameObject[,] gridPoints = mainGrid.GridPoints; // Creates a 2D array of all the grid points from MainGrid
        if (gridPoints == null) 
            return; // Avoids errors in case the grid points are not defined (it means that the grid wasn't built)

        // Reading grid layout metrics
        float cellSize = mainGrid.CellSize;
        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        // Computes the "origin" point of the grid -> bottom-left edge -> will serve as a reference
        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        // Iterate through all probes under the parent that contains them
        Transform probeDotTransform = probeDots.transform; 
        foreach (Transform probeTransform in probeDotTransform) // Iterate over each probe
        {
            GameObject probe = probeTransform.gameObject; // We obtain the probe-associated GO to use later
            Vector3 probePos = probe.transform.position; // We determine the world-space position of this probe

            // Find which grid lines this probe sits on (based on its initial position)
            int verticalLineIndex = Mathf.RoundToInt((probePos.x - originX) / cellSize);
            int horizontalLineIndex = Mathf.RoundToInt((probePos.y - originY) / cellSize);

            // Store the mapping of the probe point to its grid lines
            GridLineInfo info = new GridLineInfo
            {
                verticalLineIndex = verticalLineIndex,
                horizontalLineIndex = horizontalLineIndex,
                originalPosition = probePos
            };

            probeGridLineMap[probe] = info; // Actual storage of the mapping in the dictionary
        }
    }

    // FUNCTION: Update grid line deformations based on current probe positions
    private void UpdateGridDeformation()
    {
        // Creation of fresh deformation dictionaries that will store the line indexes and the deformation points
        Dictionary<int, List<DeformationPoint>> horizontalDeformations = new Dictionary<int, List<DeformationPoint>>();
        Dictionary<int, List<DeformationPoint>> verticalDeformations = new Dictionary<int, List<DeformationPoint>>();

        // Iterate over every probe and its grid-line indices
        foreach (var kvp in probeGridLineMap)
        {
            GameObject probe = kvp.Key; // Retrieves the probe object reference
            GridLineInfo info = kvp.Value; // Fetches the location information (line indices + original position)

            if (probe == null || !probe.activeInHierarchy) continue; // Skip if the probe is missing or inactive

            Vector3 currentPos = probe.transform.position; // Get transform of the probe position
            Vector3 displacement = currentPos - info.originalPosition; // Vector that points from where the probe started to where it is now

            if (displacement.magnitude > movementThreshold) // Avoids small movements that don't require deformation
            {
                // Add deformation point for horizontal line
                if (!horizontalDeformations.ContainsKey(info.horizontalLineIndex))
                {
                    horizontalDeformations[info.horizontalLineIndex] = new List<DeformationPoint>(); // Incorporate in the dictionary the deformation points
                }
                horizontalDeformations[info.horizontalLineIndex].Add(new DeformationPoint // Create and add a new DeformationPoint to track how a probe's movement affects a specific horizontal line
                {
                    position = currentPos, // Full 3D position
                    xCoord = currentPos.x, // X-axis position
                    displacement = displacement // How far has it moved?
                });

                // Add deformation point for vertical line
                if (!verticalDeformations.ContainsKey(info.verticalLineIndex)) 
                {
                    verticalDeformations[info.verticalLineIndex] = new List<DeformationPoint>(); // Incorporate in the dictionary the deformation points
                }
                verticalDeformations[info.verticalLineIndex].Add(new DeformationPoint // Create and add a new DeformationPoint to track how a probe's movement affects a specific vertical line
                {
                    position = currentPos, // Full 3D position
                    yCoord = currentPos.y, // Y-axis position
                    displacement = displacement // How far has it moved?
                });
            }
        }

        // Update horizontal lines
        foreach (var kvp in horizontalLines) // Iteration over all organised horizontal lines
        {
            int lineIndex = kvp.Key; // Which horizontal line are we looking at?
            List<LineRendererInfo> lines = kvp.Value; // List of lr segments that conform this line

            if (horizontalDeformations.ContainsKey(lineIndex)) // If the line has deformation points
            {
                List<DeformationPoint> deformPoints = horizontalDeformations[lineIndex]; // Get the list of deformation points
                UpdateHorizontalLine(lines, lineIndex, deformPoints); // Update the line segments accordingly to "bend" the line
            }
            else
            {
                // Reset to straight line
                ResetLineRenderers(lines);
            }
        }

        // Update vertical lines
        foreach (var kvp in verticalLines) // Iteration over all organised vertical lines
        {
            int lineIndex = kvp.Key; // Which vertical line are we looking at?
            List<LineRendererInfo> lines = kvp.Value; // List of lr segments that conform this line

            if (verticalDeformations.ContainsKey(lineIndex)) // If the line has deformation points
            {
                List<DeformationPoint> deformPoints = verticalDeformations[lineIndex]; // Get the list of deformation points
                UpdateVerticalLine(lines, lineIndex, deformPoints); // Update the line segments accordingly to "bend" the line
            }
            else
            {
                // Reset to straight line
                ResetLineRenderers(lines);
            }
        }
    }

    // FUNCTION: Update horizontal line segments to pass through deformation points
    private void UpdateHorizontalLine(List<LineRendererInfo> lineSegments, int lineIndex, List<DeformationPoint> deformPoints)
    {
        // Sort deformation points by x coordinate
        deformPoints.Sort((a, b) => a.xCoord.CompareTo(b.xCoord));

        // Iterate through each line segment that makes up this horizontal line
        foreach (LineRendererInfo lineInfo in lineSegments)
        {
            LineRenderer lr = lineInfo.lineRenderer; // Retrieve line renderer component
            Vector3 originalStart = lineInfo.originalStart; // Un-deformed start point
            Vector3 originalEnd = lineInfo.originalEnd; // Un-deformed end point

            // Store deformation points within this line segment
            List<DeformationPoint> relevantPoints = new List<DeformationPoint>();

            foreach (var dp in deformPoints) // Iterate over every deformed point
            {
                if (dp.xCoord >= Mathf.Min(originalStart.x, originalEnd.x) &&
                    dp.xCoord <= Mathf.Max(originalStart.x, originalEnd.x)) // Comparison with x start/end point -> if within interval = relevant point
                {
                    relevantPoints.Add(dp);
                }
            }

            if (relevantPoints.Count > 0) // If there are deformation points within this segment
            {
                // Deform this line segment
                lr.positionCount = relevantPoints.Count + 2; // Introduce all relevant points + start/end point (2 points in total)
                lr.SetPosition(0, originalStart); // Locking of the 1st vertex of the lr to the original start point -> gives a stable anchor to the line (avoids weird deformations)

                for (int i = 0; i < relevantPoints.Count; i++) // Loop through all relevant deformation points and set them as positions in the line renderer
                {
                    lr.SetPosition(i + 1, relevantPoints[i].position); // The lr bends over every position until reaching the amount of points
                }
                lr.SetPosition(relevantPoints.Count + 1, originalEnd); // Locking of the last verteix of the lr to the original end point
            }
            else
            {
                // No deformation needed, keep as 2-point line -> original start point to original end point
                lr.positionCount = 2;
                lr.SetPosition(0, originalStart);
                lr.SetPosition(1, originalEnd);
            }
        }
    }

    // FUNCTION: Update vertical line segments to pass through deformation points
    private void UpdateVerticalLine(List<LineRendererInfo> lineSegments, int lineIndex, List<DeformationPoint> deformPoints)
    {
        // Sort deformation points by y coordinate
        deformPoints.Sort((a, b) => a.yCoord.CompareTo(b.yCoord));

        // Iterate through each line segment that makes up this vertical line
        foreach (LineRendererInfo lineInfo in lineSegments)
        {
            LineRenderer lr = lineInfo.lineRenderer; // Retrieve line renderer component
            
            Vector3 originalStart = lineInfo.originalStart; // Un-deformed start point
            Vector3 originalEnd = lineInfo.originalEnd; // Un-deformed end point

            // Store deformation points within this line segment
            List<DeformationPoint> relevantPoints = new List<DeformationPoint>(); // Iterate over every deformed point

            foreach (var dp in deformPoints)
            {
                if (dp.yCoord >= Mathf.Min(originalStart.y, originalEnd.y) &&
                    dp.yCoord <= Mathf.Max(originalStart.y, originalEnd.y)) // Comparison with y start/end point -> if within interval = relevant point
                {
                    relevantPoints.Add(dp);
                }
            }

            if (relevantPoints.Count > 0) // If there are deformation points within this segment
            {
                // Deform this line segment
                lr.positionCount = relevantPoints.Count + 2; // Introduce all relevant points + start/end point (2 points in total)
                lr.SetPosition(0, originalStart); // Locking of the 1st vertex of the lr to the original start point -> gives a stable anchor to the line (avoids weird deformations)

                for (int i = 0; i < relevantPoints.Count; i++) // Loop through all relevant deformation points and set them as positions in the line renderer
                {
                    lr.SetPosition(i + 1, relevantPoints[i].position); // The lr bends over every position until reaching the amount of points
                }

                lr.SetPosition(relevantPoints.Count + 1, originalEnd); // Locking of the last verteix of the lr to the original end point
            }
            else
            {
                // No deformation needed, keep as 2-point line
                lr.positionCount = 2;
                lr.SetPosition(0, originalStart);
                lr.SetPosition(1, originalEnd);
            }
        }
    }

    // FUNCTION: Reset line renderers to their original 2-point straight configuration
    private void ResetLineRenderers(List<LineRendererInfo> lineRenderers)
    {
        foreach (LineRendererInfo lineInfo in lineRenderers) // Iteration through each segment
        {
            LineRenderer lr = lineInfo.lineRenderer; // Obtain line renderer information
            if (lr.positionCount != 2) // In case the position count is different from 2, reset
            {
                lr.positionCount = 2;
            }
            // Always reset to original positions
            lr.SetPosition(0, lineInfo.originalStart);
            lr.SetPosition(1, lineInfo.originalEnd);
        }
    }

    // FUNCTION: Calculate 4 deformation directions based on movement direction -> Creates perpendicular and parallel directions for the 4-line deformation pattern
    private void CalculateDeformationDirections(Vector3 movementDir)
    {
        // Normalize the movement direction
        if (movementDir.magnitude < 0.01f)
        {
            // Default to cardinal directions if there is no significant movement
            deformationDirections[0] = Vector3.up;
            deformationDirections[1] = Vector3.down;
            deformationDirections[2] = Vector3.left;
            deformationDirections[3] = Vector3.right;
            return;
        }

        movementDir.Normalize(); // Normalises the direction while maintaining the direction

        // Calculate perpendicular direction (rotate 90 degrees in 2D)
        Vector3 perpendicular = new Vector3(-movementDir.y, movementDir.x, 0f);

        // Create 4 directions: forward, backward, left-perpendicular, right-perpendicular
        deformationDirections[0] = movementDir;          // Forward (along movement)
        deformationDirections[1] = -movementDir;         // Backward (opposite movement)
        deformationDirections[2] = perpendicular;        // Left perpendicular
        deformationDirections[3] = -perpendicular;       // Right perpendicular
    }

    // FUNCTION: Enter displacement mode when probe starts moving
    public void EnterDisplacementMode(GameObject probe)
    {
        if (probe == null || isInDisplacementMode) // Safety: Ensure that the probe is existing and is in "displacement" mode
            return;

        // Safety: Ensure all lines are visible (but keep their deformed state)
        EnsureAllLinesVisible();

        activeProbe = probe; // Set current probe as active probe
        activeProbeStartPosition = probe.transform.position; //Get position of the probe of interest

        // Define movement-related variables
        isInDisplacementMode = true;
        lastMovementDirection = Vector3.zero;

        // Initialize with default cardinal directions
        CalculateDeformationDirections(Vector3.zero);

        // Generate the 4 continuous displacement lines
        GenerateDisplacementLines(probe);
    }

    // FUNCTION: Reset all line segments to their original 2-point configuration
    private void ResetAllLinesToOriginal()
    {
        foreach (var kvp in horizontalLines)
        {
            ResetLineRenderers(kvp.Value);
        }
        foreach (var kvp in verticalLines)
        {
            ResetLineRenderers(kvp.Value);
        }
    }

    // FUNCTION: Exit displacement mode and make deformations
    public void ExitDisplacementMode()
    {
        if (!isInDisplacementMode) // Safety: to ensure that it the probe is in displacement mode
            return;

        // Show the hidden segments again (so we can modify them)
        ShowHiddenSegments();

        // Safety: to ensure visibility of ALL grid lines
        EnsureAllLinesVisible();

        // Apply the deformation to the visible segments
        ExtendDeformationToSegments();

        // Clean up displacement lines
        foreach (LineRenderer lr in displacementLines)
        {
            if (lr != null)
                Destroy(lr.gameObject);
        }
        displacementLines.Clear();
        displacementLineData.Clear();

        // Safety: Double-check that ALL segments are visible and have valid positions
        ValidateAllSegments();

        // Reset state
        isInDisplacementMode = false;
        activeProbe = null;
    }

    // FUNCTION: Validate that all segments are properly visible with valid geometry
    private void ValidateAllSegments()
    {
        foreach (var kvp in horizontalLines) // Iterate over every horizontal segment
        {
            foreach (LineRendererInfo lineInfo in kvp.Value) // Obtain information for every line renderer object
            {
                if (lineInfo.lineRenderer != null) // Safety: Proceed as long as there's lr-associated information
                {
                    // Ensure that line renderer is enabled
                    if (!lineInfo.lineRenderer.enabled)
                    {
                        lineInfo.lineRenderer.enabled = true;
                    }

                    // Ensure that line renderer (or the rendered line) has at least 2 points -> original start point + original end point
                    if (lineInfo.lineRenderer.positionCount < 2)
                    {
                        lineInfo.lineRenderer.positionCount = 2;
                        lineInfo.lineRenderer.SetPosition(0, lineInfo.originalStart);
                        lineInfo.lineRenderer.SetPosition(1, lineInfo.originalEnd);
                    }
                }
            }
        }

        foreach (var kvp in verticalLines) // Iterate over every vertical segment
        {
            foreach (LineRendererInfo lineInfo in kvp.Value) // Obtain information for every line renderer object
            {
                if (lineInfo.lineRenderer != null) // Safety: Proceed as long as there's lr-associated information
                {
                    // Ensure that line renderer is enabled
                    if (!lineInfo.lineRenderer.enabled)
                    {
                        lineInfo.lineRenderer.enabled = true;
                    }

                    // Ensure that line renderer (or the rendered line) has at least 2 points -> original start point + original end point
                    if (lineInfo.lineRenderer.positionCount < 2)
                    {
                        lineInfo.lineRenderer.positionCount = 2;
                        lineInfo.lineRenderer.SetPosition(0, lineInfo.originalStart);
                        lineInfo.lineRenderer.SetPosition(1, lineInfo.originalEnd);
                    }
                }
            }
        }
    }

    // FUNCTION: Generate 4 continuous lines for displacement mode
    private void GenerateDisplacementLines(GameObject probe)
    {
        if (probe == null) // Safety: check if the grid exists. if not, don't perform deformation
            return;

        Vector3 probePos = probe.transform.position; // Find location for the specific probe
        // Generate 4 lines in the calculated deformation directions
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = deformationDirections[i]; // Create 3D vector specific for the probe direction

            // Find the endpoint by detecting the object up to grid border (or if it does, finding the nearest probe)
            Vector3 endPoint = FindEndpointInDirection(probePos, direction, out GameObject endProbe);
            bool endIsProbe = endProbe != null;

            // Create a new LineRenderer for this direction
            GameObject lineObj = new GameObject($"DisplacementLine_{i}");
            lineObj.transform.SetParent(transform);
            LineRenderer lr = lineObj.AddComponent<LineRenderer>();

            // Configure the line renderer (match grid line appearance)
            lr.startWidth = 0.15f;
            lr.endWidth = 0.15f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = Color.white;
            lr.endColor = Color.white;
            lr.positionCount = 2;
            lr.sortingOrder = 1; // Draw above grid lines

            // Set initial positions
            lr.SetPosition(0, probePos);
            lr.SetPosition(1, endPoint);

            // Store line information
            DisplacementLineInfo info = new DisplacementLineInfo
            {
                startPoint = probePos,
                endPoint = endPoint,
                startIsProbe = true,
                endIsProbe = endIsProbe,
                endProbeObject = endProbe,
                directionVector = direction
            };

            displacementLines.Add(lr);
            displacementLineData[lr] = info;
        }
    }

    // FUNCTION: Find endpoint in a given direction (grid border or nearest probe)
    private Vector3 FindEndpointInDirection(Vector3 startPos, Vector3 direction, out GameObject hitProbe)
    {
        hitProbe = null;

        // Define grid-specific parameters
        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        // Calculate grid boundaries
        float leftBound = gridCenter.x - halfWidth;
        float rightBound = gridCenter.x + halfWidth;
        float bottomBound = gridCenter.y - halfWidth;
        float topBound = gridCenter.y + halfWidth;

        // Find closer option from probe position in the given direction to find endpoint
        float maxDistance = mainGrid.TotalGridWidth * 1.5f;

        // Check for probe intersections along the direction
        float closestProbeDistance = maxDistance;
        GameObject closestProbe = null;

        // Iterate over every other probe in the group of probes
        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null || !probe.activeInHierarchy) // Safety: if the probe doesn't exist or is selected in hierarchy, ignore
                continue;

            Vector3 probePos = probe.transform.position; // Obtain the probe's position
            Vector3 toProbe = probePos - startPos; // Calculate the probe's displacement

            // Skip if it's the same probe or very close to us
            if (toProbe.magnitude < 0.1f)
                continue;

            float dotProduct = Vector3.Dot(toProbe.normalized, direction);

            if (dotProduct > 0.94f)
            {
                float distance = toProbe.magnitude;
                if (distance < closestProbeDistance)
                {
                    closestProbeDistance = distance;
                    closestProbe = probe;
                }
            }
        }

        if (closestProbe != null) // In case there is no closest probe
        {
            hitProbe = closestProbe; 
            return closestProbe.transform.position;
        }

        // No probe found, calculate intersection with grid border
        Vector3 endPoint = startPos + direction * maxDistance;

        // Constraint to grid boundaries
        float t = maxDistance;

        // Check intersection with each boundary
        if (direction.x > 0.01f)
        {
            float tRight = (rightBound - startPos.x) / direction.x;
            if (tRight > 0 && tRight < t) t = tRight;
        }
        else if (direction.x < -0.01f)
        {
            float tLeft = (leftBound - startPos.x) / direction.x;
            if (tLeft > 0 && tLeft < t) t = tLeft;
        }
        if (direction.y > 0.01f)
        {
            float tTop = (topBound - startPos.y) / direction.y;
            if (tTop > 0 && tTop < t) t = tTop;
        }
        else if (direction.y < -0.01f)
        {
            float tBottom = (bottomBound - startPos.y) / direction.y;
            if (tBottom > 0 && tBottom < t) t = tBottom;
        }

        endPoint = startPos + direction * t; // Define updated end-point
        endPoint.z = startPos.z; // Keep same Z

        return endPoint;
    }

    // FUNCTION: Update displacement lines in real-time as probe moves
    private void UpdateDisplacementLines()
    {
        if (activeProbe == null) // Safety: exit in case there is no active probe dot
            return;

        Vector3 currentProbePos = activeProbe.transform.position; // Obtain location of selected probe dot
        Vector3 movementDir = currentProbePos - activeProbeStartPosition; // Calculate movement direction

        // Update deformation directions based on current movement ONCE when movement starts
        if (movementDir.magnitude > 0.5f && lastMovementDirection.magnitude < 0.1f)
        {
            // Recalculate deformation directions based on initial movement
            CalculateDeformationDirections(movementDir);
            lastMovementDirection = movementDir;

            // Regenerate displacement lines with new directions
            RegenerateDisplacementLines();

            // NOW hide segments along the displacement line paths (not the grid lines being deformed)
            UpdateHiddenSegments();
        }

        // Update existing line positions (always update, don't regenerate)
        foreach (LineRenderer lr in displacementLines)
        {
            if (!displacementLineData.ContainsKey(lr)) // Safety: exit in case there is no displacement line data with lr-associated information
                continue;

            DisplacementLineInfo info = displacementLineData[lr];

            // Update start position (probe moves)
            lr.SetPosition(0, currentProbePos);

            // Recalculate endpoint based on current direction
            Vector3 endPoint = FindEndpointInDirection(currentProbePos, info.directionVector, out GameObject endProbe);
            lr.SetPosition(1, endPoint);

            // Update info
            info.endPoint = endPoint; // Storing of coordinates where the line ends
            info.endProbeObject = endProbe; // Defines if the endpoint is associated with a probe or not
            info.endIsProbe = endProbe != null;
        }
    }

    // FUNCTION: Regenerate displacement lines with new directions
    private void RegenerateDisplacementLines()
    {
        if (activeProbe == null) // Safety: exits in case there is no currently active probe
            return;

        // Clean up old lines
        foreach (LineRenderer lr in displacementLines)
        {
            if (lr != null) // In case there is a lr-associated element existing
                Destroy(lr.gameObject);
        }
        displacementLines.Clear();
        displacementLineData.Clear();

        // Generate new lines
        GenerateDisplacementLines(activeProbe);
    }

    // FUNCTION: Update hidden segments when directions change
    private void UpdateHiddenSegments()
    {
        // Show previously hidden segments
        foreach (LineRendererInfo lineInfo in hiddenSegments) // Obtain information for every line segment previously hidden
        {
            if (lineInfo.lineRenderer != null) // If there exists information
            {
                lineInfo.lineRenderer.enabled = true;
            }
        }
        hiddenSegments.Clear();

        // Hide segments for new directions
        if (activeProbe != null)
        {
            HideSegmentsAroundProbe(activeProbe);
        }
    }

    // FUNCTION: Extend deformation from 4 lines back to all segments along the paths
    private void ExtendDeformationToSegments()
    {
        if (activeProbe == null || !probeGridLineMap.ContainsKey(activeProbe)) // Safety: exit in case there is no active probe
            return;

        GridLineInfo probeInfo = probeGridLineMap[activeProbe];
        Vector3 currentProbePos = activeProbe.transform.position;
        Vector3 originalProbePos = probeInfo.originalPosition;

        // For each of the 4 dynamic directions, deform all segments along the path
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = deformationDirections[i];

            // Find endpoint using current probe position
            Vector3 endPoint = FindEndpointInDirection(currentProbePos, direction, out GameObject endProbe);

            // Also calculate the original endpoint (before probe moved)
            Vector3 originalEndPoint = FindEndpointInDirection(originalProbePos, direction, out GameObject originalEndProbe);

            // Apply deformation to all segments along this dynamic path
            ExtendDeformationAlongDynamicPath(currentProbePos, originalProbePos, endPoint, originalEndPoint);
        }
    }

    // FUNCTION: Extend deformation to all segments along a dynamic (potentially diagonal) path
    private void ExtendDeformationAlongDynamicPath(Vector3 probeCurrentPos, Vector3 probeOriginalPos, Vector3 endCurrentPos, Vector3 endOriginalPos)
    {
        // Define corridor width for affected segments
        float corridorWidth = 0.8f; // Wide enough to capture all segments along the path including perpendicular ones

        // Use a HashSet to track which segments we've already deformed (prevent double-deformation)
        HashSet<LineRenderer> deformedSegments = new();

        // Check all horizontal segments
        foreach (var kvp in horizontalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (!deformedSegments.Contains(lineInfo.lineRenderer))
                {
                    if (TryDeformSegmentAlongPath(lineInfo, probeCurrentPos, probeOriginalPos, endCurrentPos, endOriginalPos, corridorWidth))
                    {
                        deformedSegments.Add(lineInfo.lineRenderer);
                    }
                }
            }
        }

        // Check all vertical segments
        foreach (var kvp in verticalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (!deformedSegments.Contains(lineInfo.lineRenderer))
                {
                    if (TryDeformSegmentAlongPath(lineInfo, probeCurrentPos, probeOriginalPos, endCurrentPos, endOriginalPos, corridorWidth))
                    {
                        deformedSegments.Add(lineInfo.lineRenderer);
                    }
                }
            }
        }
    }

    // FUNCTION: Try to deform a segment if it's near the deformation path
    private bool TryDeformSegmentAlongPath(LineRendererInfo lineInfo, Vector3 probeCurrentPos, Vector3 probeOriginalPos, Vector3 endCurrentPos, Vector3 endOriginalPos, float corridorWidth)
    {
        if (lineInfo?.lineRenderer == null)
            return false;

        Vector3 segStart = lineInfo.initialStart;
        Vector3 segEnd = lineInfo.initialEnd;

        // Calculate path projection info once
        PathProjectionInfo pathInfo = CalculatePathProjection(segStart, segEnd, probeOriginalPos, endOriginalPos);

        if (!pathInfo.IsValid)
            return false;

        // Single unified corridor check
        if (!IsSegmentInCorridor(segStart, segEnd, pathInfo, corridorWidth))
            return false;

        // Apply deformation to the segment
        return ApplySegmentDeformation(lineInfo, pathInfo, probeCurrentPos, endCurrentPos);
    }

    // FUNCTION: Calculate projection of segment onto path
    private PathProjectionInfo CalculatePathProjection(Vector3 segStart, Vector3 segEnd, Vector3 pathStart, Vector3 pathEnd)
    {
        Vector3 pathVector = pathEnd - pathStart;
        float pathLength = pathVector.magnitude;

        // Validate path length
        if (pathLength < 0.01f)
            return new PathProjectionInfo { IsValid = false };

        float pathLengthSq = pathLength * pathLength;

        // Calculate projection factors for segment start and end
        Vector3 toStart = segStart - pathStart;
        Vector3 toEnd = segEnd - pathStart;

        float tStart = Vector3.Dot(toStart, pathVector) / pathLengthSq;
        float tEnd = Vector3.Dot(toEnd, pathVector) / pathLengthSq;

        // Check if projections are within reasonable bounds (with margin)
        const float margin = 0.2f;
        if ((tStart < -margin && tEnd < -margin) || (tStart > 1f + margin && tEnd > 1f + margin))
            return new PathProjectionInfo { IsValid = false };

        // Calculate minimum distance from segment to path
        Vector3 segMid = (segStart + segEnd) / 2f;
        Vector3 toMid = segMid - pathStart;
        float tMid = Vector3.Dot(toMid, pathVector) / pathLengthSq;
        Vector3 closestPointOnPath = pathStart + pathVector * Mathf.Clamp01(tMid);
        float minDistance = Vector3.Distance(segMid, closestPointOnPath);

        return new PathProjectionInfo
        {
            IsValid = true,
            PathVector = pathVector,
            PathLength = pathLength,
            TStart = tStart,
            TEnd = tEnd,
            MinDistance = minDistance
        };
    }

    // FUNCTION: Check if segment is within the corridor around the path
    private bool IsSegmentInCorridor(Vector3 segStart, Vector3 segEnd, PathProjectionInfo pathInfo, float corridorWidth)
    {
        // Quick check: if midpoint is near path, segment is in corridor
        if (pathInfo.MinDistance < corridorWidth)
            return true;

        // Check if segment crosses the path corridor
        Vector3 segmentVector = segEnd - segStart;
        float segmentLength = segmentVector.magnitude;

        if (segmentLength < 0.01f)
            return false;

        // Use parametric line-line distance to check crossing
        Vector3 pathStart = Vector3.zero; // Relative calculation
        Vector3 w = segStart - pathStart;

        float a = Vector3.Dot(segmentVector, segmentVector);
        float b = Vector3.Dot(segmentVector, pathInfo.PathVector);
        float c = Vector3.Dot(pathInfo.PathVector, pathInfo.PathVector);
        float d = Vector3.Dot(segmentVector, w);
        float e = Vector3.Dot(pathInfo.PathVector, w);

        float denom = a * c - b * b;
        if (Mathf.Abs(denom) > 0.0001f)
        {
            float sc = (b * e - c * d) / denom;
            float tc = (a * e - b * d) / denom;

            // Check if closest approach is within both line segments
            if (sc >= 0 && sc <= 1 && tc >= 0 && tc <= 1)
            {
                Vector3 pointOnSegment = segStart + segmentVector * sc;
                Vector3 pointOnPath = pathStart + pathInfo.PathVector * tc;
                float closestDistance = Vector3.Distance(pointOnSegment, pointOnPath);

                return closestDistance < corridorWidth;
            }
        }

        return false;
    }

    // FUNCTION: Apply deformation to a segment along the path
    private bool ApplySegmentDeformation(LineRendererInfo lineInfo, PathProjectionInfo pathInfo, Vector3 probeCurrentPos, Vector3 endCurrentPos)
    {
        // Clamp projection factors to valid range
        float tStart = Mathf.Clamp01(pathInfo.TStart);
        float tEnd = Mathf.Clamp01(pathInfo.TEnd);

        // Calculate new positions along the deformed path
        Vector3 newStart = Vector3.Lerp(probeCurrentPos, endCurrentPos, tStart);
        Vector3 newEnd = Vector3.Lerp(probeCurrentPos, endCurrentPos, tEnd);

        // Validate deformation is reasonable
        float newLength = Vector3.Distance(newStart, newEnd);
        float originalLength = Vector3.Distance(lineInfo.initialStart, lineInfo.initialEnd);

        // Prevent extreme stretching or shrinking
        if (originalLength > 0.01f && (newLength > originalLength * 10f || newLength < originalLength * 0.05f))
            return false;

        // Apply the deformation
        LineRenderer lr = lineInfo.lineRenderer;
        lr.positionCount = 2;
        lr.SetPosition(0, newStart);
        lr.SetPosition(1, newEnd);

        // Ensure visible
        if (!lr.enabled)
            lr.enabled = true;

        // Update stored positions for future deformations
        lineInfo.originalStart = newStart;
        lineInfo.originalEnd = newEnd;

        return true;
    }

    // FUNCTION: Hide ONLY segments along the 4 specific paths from probe to neighbors/borders
    private void HideSegmentsAroundProbe(GameObject probe)
    {
        if (!probeGridLineMap.ContainsKey(probe))
            return;

        hiddenSegments.Clear();

        // Use current position for dynamic direction calculation
        Vector3 probePos = probe.transform.position;

        // For each deformation direction, hide segments along the path
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = deformationDirections[i];

            // Find endpoint for this direction
            Vector3 endPoint = FindEndpointInDirection(probePos, direction, out GameObject endProbe);

            // Hide segments along this path
            HideSegmentsAlongDynamicPath(probePos, endPoint);
        }
    }

    // FUNCTION: Hide segments along a dynamic (potentially diagonal) path
    private void HideSegmentsAlongDynamicPath(Vector3 startPos, Vector3 endPos)
    {
        // Calculate path characteristics
        Vector3 pathVector = endPos - startPos;
        float pathLength = pathVector.magnitude;

        if (pathLength < 0.1f)
            return;

        // Define a "corridor" width for hiding segments - match deformation width
        float corridorWidth = 0.8f; // Width of influence around the path (same as deformation)

        // Check all horizontal line segments
        foreach (var kvp in horizontalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (IsSegmentNearPath(lineInfo.initialStart, lineInfo.initialEnd, startPos, endPos, corridorWidth))
                {
                    lineInfo.lineRenderer.enabled = false;
                    if (!hiddenSegments.Contains(lineInfo))
                    {
                        hiddenSegments.Add(lineInfo);
                    }
                }
            }
        }

        // Check all vertical line segments
        foreach (var kvp in verticalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (IsSegmentNearPath(lineInfo.initialStart, lineInfo.initialEnd, startPos, endPos, corridorWidth))
                {
                    lineInfo.lineRenderer.enabled = false;
                    if (!hiddenSegments.Contains(lineInfo))
                    {
                        hiddenSegments.Add(lineInfo);
                    }
                }
            }
        }
    }

    // FUNCTION: Check if a line segment is near a path (within corridor width)
    private bool IsSegmentNearPath(Vector3 segStart, Vector3 segEnd, Vector3 pathStart, Vector3 pathEnd, float corridorWidth)
    {
        // Check if segment midpoint is within the path corridor
        Vector3 segMid = (segStart + segEnd) / 2f;

        // Calculate closest point on path to segment midpoint
        Vector3 pathVector = pathEnd - pathStart;
        float pathLength = pathVector.magnitude;

        if (pathLength < 0.01f)
            return false;

        // Project segment midpoint onto path
        Vector3 toMid = segMid - pathStart;
        float t = Vector3.Dot(toMid, pathVector) / (pathLength * pathLength);

        // Check if projection is within path bounds (with expanded margin)
        if (t < -0.2f || t > 1.2f)
            return false;

        // Calculate closest point on path
        Vector3 closestPointOnPath = pathStart + pathVector * Mathf.Clamp01(t);

        // Check distance from segment midpoint to path
        float distance = Vector3.Distance(segMid, closestPointOnPath);

        return distance < corridorWidth;
    }

    // FUNCTION: Show previously hidden segments
    private void ShowHiddenSegments()
    {
        int shownCount = 0;
        foreach (LineRendererInfo lineInfo in hiddenSegments)
        {
            if (lineInfo.lineRenderer != null)
            {
                lineInfo.lineRenderer.enabled = true;
                shownCount++;
            }
        }
        hiddenSegments.Clear();
    }

    // FUNCTION: Safety check to ensure all grid lines are visible
    private void EnsureAllLinesVisible()
    {
        // Go through all horizontal lines
        foreach (var kvp in horizontalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (lineInfo.lineRenderer != null && !lineInfo.lineRenderer.enabled)
                {
                    lineInfo.lineRenderer.enabled = true;
                }
            }
        }

        // Go through all vertical lines
        foreach (var kvp in verticalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (lineInfo.lineRenderer != null && !lineInfo.lineRenderer.enabled)
                {
                    lineInfo.lineRenderer.enabled = true;
                }
            }
        }
    }
}

// Helper class to store LineRenderer with its original positions
public class LineRendererInfo
{
    public LineRenderer lineRenderer;
    public Vector3 originalStart;          // Current baseline (gets updated after deformations)
    public Vector3 originalEnd;            // Current baseline (gets updated after deformations)
    public Vector3 initialStart;           // TRULY original position (never changes)
    public Vector3 initialEnd;             // TRULY original position (never changes)
}

// Helper class to store grid line information for each probe
public class GridLineInfo
{
    public int verticalLineIndex;
    public int horizontalLineIndex;
    public Vector3 originalPosition;
}

// Helper class to store deformation points along a line
public class DeformationPoint
{
    public Vector3 position;
    public float xCoord; // For horizontal lines, track x position
    public float yCoord; // For vertical lines, track y position
    public Vector3 displacement;
}

// Helper class for displacement line information (4-line mode)
public class DisplacementLineInfo
{
    public Vector3 startPoint;        // Fixed endpoint (probe or border)
    public Vector3 endPoint;          // Fixed endpoint (neighbor probe or border)
    public bool startIsProbe;         // Is start point the active probe?
    public bool endIsProbe;           // Is end point a probe?
    public GameObject endProbeObject; // Reference if end is a probe
    public Vector3 directionVector;   // Dynamic direction vector
}

// Helper struct to consolidate path projection calculations
public struct PathProjectionInfo
{
    public bool IsValid;              // Is the projection valid?
    public Vector3 PathVector;        // Vector along the path
    public float PathLength;          // Length of the path
    public float TStart;              // Projection factor for segment start point
    public float TEnd;                // Projection factor for segment end point
    public float MinDistance;         // Minimum distance from segment to path
}
