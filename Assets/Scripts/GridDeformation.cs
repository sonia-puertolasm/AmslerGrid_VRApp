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

        // Update grid line deformations based on probe positions
        UpdateGridDeformation();
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
                originalEnd = end
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
}

// Helper class to store LineRenderer with its original positions
public class LineRendererInfo
{
    public LineRenderer lineRenderer;
    public Vector3 originalStart;
    public Vector3 originalEnd;
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
