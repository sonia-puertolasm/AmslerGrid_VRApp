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

    // 4-line displacement mode storage
    private List<LineRenderer> displacementLines = new List<LineRenderer>();
    private Dictionary<LineRenderer, DisplacementLineInfo> displacementLineData = new Dictionary<LineRenderer, DisplacementLineInfo>();

    // Dynamic deformation directions (calculated based on movement)
    private Vector3[] deformationDirections = new Vector3[4];

    // Store which line segments should be hidden during displacement mode
    private List<LineRendererInfo> hiddenSegments = new List<LineRendererInfo>();

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

    void LateUpdate() // Use LateUpdate to ensure that probe positions are updated first
    {
        if (!enableDeformation) // Skip if the deformation has been disabled
            return;

        // Check if we should enter or update displacement mode
        if (isInDisplacementMode)
        {
            // Update the 4 displacement lines (these replace the hidden grid segments)
            UpdateDisplacementLines();
        }
        // NOTE: In selection mode, we don't update deformations continuously
        // The deformations are "baked" when exiting displacement mode and persist until the next displacement
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
            horizontalLines[i] = new List<LineRendererInfo>();
            verticalLines[i] = new List<LineRendererInfo>();
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
                originalStart = start,
                originalEnd = end,
                initialStart = start,      // Store truly original positions
                initialEnd = end           // Store truly original positions
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
                int lineIndex = Mathf.RoundToInt((start.x - originX) / cellSize);
                if (lineIndex >= 0 && lineIndex <= gridSize)
                {
                    verticalLines[lineIndex].Add(lineInfo);
                }
            }
        }
    }

    // FUNCTION: Map each probe to its corresponding horizontal and vertical grid lines
    private void MapProbesToGridLines()
    {
        GameObject[,] gridPoints = mainGrid.GridPoints; // Creates a 2D array of all the grid points
        if (gridPoints == null) return; // Avoids errors in case the grid points are not defined

        float cellSize = mainGrid.CellSize;
        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;

        // Iterate through all probes
        Transform probeDotTransform = probeDots.transform;
        foreach (Transform probeTransform in probeDotTransform)
        {
            GameObject probe = probeTransform.gameObject; // We obtain the probe GameObject to use later
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

        // Collect all probe deformations
        foreach (var kvp in probeGridLineMap)
        {
            GameObject probe = kvp.Key;
            GridLineInfo info = kvp.Value;

            if (probe == null || !probe.activeInHierarchy) continue; // Skip if the probe is missing or inactive

            Vector3 currentPos = probe.transform.position;
            Vector3 displacement = currentPos - info.originalPosition; // Vector that points from where the probe started to where it is now

            if (displacement.magnitude > movementThreshold) // Avoids small movements that don't require deformation
            {
                // Add deformation point for horizontal line
                if (!horizontalDeformations.ContainsKey(info.horizontalLineIndex))
                {
                    horizontalDeformations[info.horizontalLineIndex] = new List<DeformationPoint>();
                }
                horizontalDeformations[info.horizontalLineIndex].Add(new DeformationPoint
                {
                    position = currentPos, // Full 3D position
                    xCoord = currentPos.x, // X-axis position
                    displacement = displacement // How far has it moved?
                });

                // Add deformation point for vertical line
                if (!verticalDeformations.ContainsKey(info.verticalLineIndex))
                {
                    verticalDeformations[info.verticalLineIndex] = new List<DeformationPoint>();
                }
                verticalDeformations[info.verticalLineIndex].Add(new DeformationPoint
                {
                    position = currentPos,
                    yCoord = currentPos.y,
                    displacement = displacement
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
                // This line is affected by probe(s)
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
        foreach (var kvp in verticalLines)
        {
            int lineIndex = kvp.Key;
            List<LineRendererInfo> lines = kvp.Value;

            if (verticalDeformations.ContainsKey(lineIndex))
            {
                // This line is affected by probe(s)
                List<DeformationPoint> deformPoints = verticalDeformations[lineIndex];
                UpdateVerticalLine(lines, lineIndex, deformPoints);
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
            LineRenderer lr = lineInfo.lineRenderer;
            Vector3 originalStart = lineInfo.originalStart;
            Vector3 originalEnd = lineInfo.originalEnd;

            // Find deformation points within this line segment
            List<DeformationPoint> relevantPoints = new List<DeformationPoint>();
            foreach (var dp in deformPoints)
            {
                if (dp.xCoord >= Mathf.Min(originalStart.x, originalEnd.x) &&
                    dp.xCoord <= Mathf.Max(originalStart.x, originalEnd.x))
                {
                    relevantPoints.Add(dp);
                }
            }

            if (relevantPoints.Count > 0) // If there are deformation points within this segment
            {
                // Deform this line segment
                lr.positionCount = relevantPoints.Count + 2;
                lr.SetPosition(0, originalStart);

                for (int i = 0; i < relevantPoints.Count; i++) // Loop through all relevant deformation points and set them as positions in the line renderer
                {
                    lr.SetPosition(i + 1, relevantPoints[i].position);
                }

                lr.SetPosition(relevantPoints.Count + 1, originalEnd);
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

    // FUNCTION: Update vertical line segments to pass through deformation points
    private void UpdateVerticalLine(List<LineRendererInfo> lineSegments, int lineIndex, List<DeformationPoint> deformPoints)
    {
        // Sort deformation points by y coordinate
        deformPoints.Sort((a, b) => a.yCoord.CompareTo(b.yCoord));

        foreach (LineRendererInfo lineInfo in lineSegments)
        {
            LineRenderer lr = lineInfo.lineRenderer;
            Vector3 originalStart = lineInfo.originalStart;
            Vector3 originalEnd = lineInfo.originalEnd;

            // Find deformation points within this line segment
            List<DeformationPoint> relevantPoints = new List<DeformationPoint>();
            foreach (var dp in deformPoints)
            {
                if (dp.yCoord >= Mathf.Min(originalStart.y, originalEnd.y) &&
                    dp.yCoord <= Mathf.Max(originalStart.y, originalEnd.y))
                {
                    relevantPoints.Add(dp);
                }
            }

            if (relevantPoints.Count > 0)
            {
                // Deform this line segment
                lr.positionCount = relevantPoints.Count + 2;
                lr.SetPosition(0, originalStart);

                for (int i = 0; i < relevantPoints.Count; i++)
                {
                    lr.SetPosition(i + 1, relevantPoints[i].position);
                }

                lr.SetPosition(relevantPoints.Count + 1, originalEnd);
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
            LineRenderer lr = lineInfo.lineRenderer;
            if (lr.positionCount != 2)
            {
                lr.positionCount = 2;
            }
            // Always reset to original positions
            lr.SetPosition(0, lineInfo.originalStart);
            lr.SetPosition(1, lineInfo.originalEnd);
        }
    }

    // ============== DISPLACEMENT MODE METHODS ==============

    // FUNCTION: Calculate 4 deformation directions based on movement direction
    // Creates perpendicular and parallel directions for the 4-line deformation pattern
    private void CalculateDeformationDirections(Vector3 movementDir)
    {
        // Normalize the movement direction
        if (movementDir.magnitude < 0.01f)
        {
            // Default to cardinal directions if no significant movement
            deformationDirections[0] = Vector3.up;
            deformationDirections[1] = Vector3.down;
            deformationDirections[2] = Vector3.left;
            deformationDirections[3] = Vector3.right;
            return;
        }

        movementDir.Normalize();

        // Calculate perpendicular direction (rotate 90 degrees in 2D)
        Vector3 perpendicular = new Vector3(-movementDir.y, movementDir.x, 0f);

        // Create 4 directions: forward, backward, left-perpendicular, right-perpendicular
        deformationDirections[0] = movementDir;          // Forward (along movement)
        deformationDirections[1] = -movementDir;         // Backward (opposite movement)
        deformationDirections[2] = perpendicular;        // Left perpendicular
        deformationDirections[3] = -perpendicular;       // Right perpendicular
    }

    // PUBLIC: Enter displacement mode when probe starts moving
    public void EnterDisplacementMode(GameObject probe)
    {
        if (probe == null || isInDisplacementMode)
            return;

        // SAFETY: Ensure all lines are visible (but keep their deformed state)
        EnsureAllLinesVisible();

        activeProbe = probe;
        activeProbeStartPosition = probe.transform.position;
        isInDisplacementMode = true;
        lastMovementDirection = Vector3.zero;

        // Initialize with default cardinal directions
        CalculateDeformationDirections(Vector3.zero);

        // Note: We DON'T hide segments initially - only after we determine movement direction
        // The grid lines need to stay visible to be deformed in real-time

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

    // PUBLIC: Exit displacement mode and bake deformation
    public void ExitDisplacementMode()
    {
        if (!isInDisplacementMode)
            return;

        // FIRST: Show the hidden segments again (so we can modify them)
        ShowHiddenSegments();

        // SAFETY: Ensure all grid lines are visible
        EnsureAllLinesVisible();

        // THEN: Bake the deformation to the now-visible segments
        BakeDeformationToSegments();

        // Clean up displacement lines
        foreach (LineRenderer lr in displacementLines)
        {
            if (lr != null)
                Destroy(lr.gameObject);
        }
        displacementLines.Clear();
        displacementLineData.Clear();

        // CRITICAL: Double-check that ALL segments are visible and have valid positions
        ValidateAllSegments();

        // Reset state
        isInDisplacementMode = false;
        activeProbe = null;
    }

    // FUNCTION: Validate that all segments are properly visible with valid geometry
    private void ValidateAllSegments()
    {
        foreach (var kvp in horizontalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (lineInfo.lineRenderer != null)
                {
                    // Ensure enabled
                    if (!lineInfo.lineRenderer.enabled)
                    {
                        lineInfo.lineRenderer.enabled = true;
                    }

                    // Ensure has at least 2 points
                    if (lineInfo.lineRenderer.positionCount < 2)
                    {
                        lineInfo.lineRenderer.positionCount = 2;
                        lineInfo.lineRenderer.SetPosition(0, lineInfo.originalStart);
                        lineInfo.lineRenderer.SetPosition(1, lineInfo.originalEnd);
                    }
                }
            }
        }

        foreach (var kvp in verticalLines)
        {
            foreach (LineRendererInfo lineInfo in kvp.Value)
            {
                if (lineInfo.lineRenderer != null)
                {
                    // Ensure enabled
                    if (!lineInfo.lineRenderer.enabled)
                    {
                        lineInfo.lineRenderer.enabled = true;
                    }

                    // Ensure has at least 2 points
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

    // FUNCTION: Generate 4 continuous lines for displacement mode (dynamic directions)
    private void GenerateDisplacementLines(GameObject probe)
    {
        if (probe == null)
            return;

        Vector3 probePos = probe.transform.position;

        // Generate 4 lines in the calculated deformation directions
        for (int i = 0; i < 4; i++)
        {
            Vector3 direction = deformationDirections[i];

            // Find the endpoint by raycasting to grid border or finding nearest probe
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

        float halfWidth = mainGrid.TotalGridWidth / 2f;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        // Calculate grid boundaries
        float leftBound = gridCenter.x - halfWidth;
        float rightBound = gridCenter.x + halfWidth;
        float bottomBound = gridCenter.y - halfWidth;
        float topBound = gridCenter.y + halfWidth;

        // Cast a ray from probe position in the given direction to find endpoint
        // Maximum distance is the grid diagonal
        float maxDistance = mainGrid.TotalGridWidth * 1.5f;

        // Check for probe intersections along the direction
        float closestProbeDistance = maxDistance;
        GameObject closestProbe = null;

        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null || !probe.activeInHierarchy)
                continue;

            Vector3 probePos = probe.transform.position;
            Vector3 toProbe = probePos - startPos;

            // Skip if it's the same probe or behind us
            if (toProbe.magnitude < 0.1f)
                continue;

            // Project toProbe onto direction to see if probe is along the ray
            float dotProduct = Vector3.Dot(toProbe.normalized, direction);

            // Probe is roughly in this direction (within 20 degrees) - more strict
            if (dotProduct > 0.94f) // cos(20°) ≈ 0.94 (was 0.866 for 30°)
            {
                float distance = toProbe.magnitude;
                if (distance < closestProbeDistance)
                {
                    closestProbeDistance = distance;
                    closestProbe = probe;
                }
            }
        }

        if (closestProbe != null)
        {
            hitProbe = closestProbe;
            return closestProbe.transform.position;
        }

        // No probe found, calculate intersection with grid border
        Vector3 endPoint = startPos + direction * maxDistance;

        // Clamp to grid boundaries
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

        endPoint = startPos + direction * t;
        endPoint.z = startPos.z; // Keep same Z

        return endPoint;
    }

    // FUNCTION: Update displacement lines in real-time as probe moves
    private void UpdateDisplacementLines()
    {
        if (activeProbe == null)
            return;

        Vector3 currentProbePos = activeProbe.transform.position;
        Vector3 movementDir = currentProbePos - activeProbeStartPosition;

        // Update deformation directions based on current movement ONCE when movement starts
        // Only recalculate on first significant movement, then lock it
        if (movementDir.magnitude > 0.5f && lastMovementDirection.magnitude < 0.1f)
        {
            // Recalculate deformation directions based on initial movement
            CalculateDeformationDirections(movementDir);
            lastMovementDirection = movementDir;

            // Regenerate displacement lines with new directions
            RegenerateDisplacementLines();

            // NOW hide segments along the displacement line paths (not the grid lines being deformed)
            // Only hide segments that are very close to the 4 displacement line paths
            UpdateHiddenSegments();
        }

        // Update existing line positions (always update, don't regenerate)
        foreach (LineRenderer lr in displacementLines)
        {
            if (!displacementLineData.ContainsKey(lr))
                continue;

            DisplacementLineInfo info = displacementLineData[lr];

            // Update start position (probe moves)
            lr.SetPosition(0, currentProbePos);

            // Recalculate endpoint based on current direction
            Vector3 endPoint = FindEndpointInDirection(currentProbePos, info.directionVector, out GameObject endProbe);
            lr.SetPosition(1, endPoint);

            // Update info
            info.endPoint = endPoint;
            info.endProbeObject = endProbe;
            info.endIsProbe = endProbe != null;
        }
    }

    // FUNCTION: Regenerate displacement lines with new directions
    private void RegenerateDisplacementLines()
    {
        if (activeProbe == null)
            return;

        // Clean up old lines
        foreach (LineRenderer lr in displacementLines)
        {
            if (lr != null)
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
        foreach (LineRendererInfo lineInfo in hiddenSegments)
        {
            if (lineInfo.lineRenderer != null)
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

    // FUNCTION: Bake deformation from 4 lines back to all segments along the paths
    private void BakeDeformationToSegments()
    {
        if (activeProbe == null || !probeGridLineMap.ContainsKey(activeProbe))
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
            BakeDeformationAlongDynamicPath(currentProbePos, originalProbePos, endPoint, originalEndPoint);
        }
    }

    // FUNCTION: Bake deformation to all segments along a dynamic (potentially diagonal) path
    private void BakeDeformationAlongDynamicPath(Vector3 probeCurrentPos, Vector3 probeOriginalPos, Vector3 endCurrentPos, Vector3 endOriginalPos)
    {
        // Define corridor width for affected segments - narrower to only affect nearby segments
        float corridorWidth = 0.2f; // Reduced from 0.5f

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
        // Skip if segment is null or currently hidden
        if (lineInfo == null || lineInfo.lineRenderer == null)
            return false;

        // Use initial positions for path detection
        Vector3 segStart = lineInfo.initialStart;
        Vector3 segEnd = lineInfo.initialEnd;
        Vector3 segMid = (segStart + segEnd) / 2f;

        // Calculate the original path vector
        Vector3 originalPathVector = endOriginalPos - probeOriginalPos;
        float originalPathLength = originalPathVector.magnitude;

        if (originalPathLength < 0.01f)
            return false;

        // Check if segment midpoint is near the ORIGINAL path
        Vector3 toMid = segMid - probeOriginalPos;
        float t = Vector3.Dot(toMid, originalPathVector) / (originalPathLength * originalPathLength);

        // Check if projection is within path bounds (with margin)
        if (t < -0.1f || t > 1.1f)
            return false;

        // Calculate closest point on original path
        Vector3 closestPointOnOriginalPath = probeOriginalPos + originalPathVector * Mathf.Clamp01(t);

        // Check distance from segment midpoint to original path
        float distanceToPath = Vector3.Distance(segMid, closestPointOnOriginalPath);

        if (distanceToPath >= corridorWidth)
            return false;

        // Segment is within corridor - deform it
        // Calculate where segment endpoints should be on the deformed path
        Vector3 currentPathVector = endCurrentPos - probeCurrentPos;

        // Calculate projection factors for segment endpoints
        Vector3 toStart = segStart - probeOriginalPos;
        Vector3 toEnd = segEnd - probeOriginalPos;

        float tStart = Vector3.Dot(toStart, originalPathVector) / (originalPathLength * originalPathLength);
        float tEnd = Vector3.Dot(toEnd, originalPathVector) / (originalPathLength * originalPathLength);

        // Clamp to [0, 1]
        tStart = Mathf.Clamp01(tStart);
        tEnd = Mathf.Clamp01(tEnd);

        // Calculate new positions along the current (deformed) path
        Vector3 newStart = Vector3.Lerp(probeCurrentPos, endCurrentPos, tStart);
        Vector3 newEnd = Vector3.Lerp(probeCurrentPos, endCurrentPos, tEnd);

        // Validate the new positions are reasonable
        float newLength = Vector3.Distance(newStart, newEnd);
        float originalLength = Vector3.Distance(segStart, segEnd);

        // Sanity check: don't deform if it would stretch segment more than 3x or shrink less than 0.3x
        if (newLength > originalLength * 3f || newLength < originalLength * 0.3f)
        {
            return false;
        }

        // Apply deformation
        LineRenderer lr = lineInfo.lineRenderer;
        lr.positionCount = 2;
        lr.SetPosition(0, newStart);
        lr.SetPosition(1, newEnd);

        // Ensure visible
        if (!lr.enabled)
        {
            lr.enabled = true;
        }

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

        // Define a "corridor" width for hiding segments - very narrow to avoid hiding too much
        float corridorWidth = 0.15f; // Width of influence around the path

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

        // Check if projection is within path bounds (with some margin)
        if (t < -0.1f || t > 1.1f)
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
