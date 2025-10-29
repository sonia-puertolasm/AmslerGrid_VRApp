using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Import specific UnityEngine classes for easier access (Vectors in 2D and 3D)
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

// Manages constraints for probe dot movements
public class ProbeDotConstraints : MonoBehaviour
{
    // Reference to the main grid
    private MainGrid mainGrid;

    // Define configuration parameters
    public float boundaryPadding = 0.1f; // Padding from grid edges
    private float cellSize; // Size of one grid cell - used to keep probes one cell away from neighbors

    // Define boundary limits variables
    private float minX, maxX, minY, maxY;

    // Public accessors for boundary information
    public float MinX => minX;
    public float MaxX => maxX;
    public float MinY => minY;
    public float MaxY => maxY;

    // Initialization of constraint functionalities
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid == null)
        {
            return;
        }

        CalculateBoundaries();
    }

    // FUNCTION: Calculate boundary limits based on main grid properties
    private void CalculateBoundaries()
    {
        float halfSize = mainGrid.TotalGridWidth / 2f;
        Vector3 center = mainGrid.GridCenterPosition;

        minX = center.x - halfSize + boundaryPadding;
        maxX = center.x + halfSize - boundaryPadding;
        minY = center.y - halfSize + boundaryPadding;
        maxY = center.y + halfSize - boundaryPadding;

        // Calculate cell size - this will be used as minimum distance to keep probes one cell away from neighbors
        cellSize = mainGrid.CellSize;
    }

    // FUNCTION: Application of constraints to a proposed probe position -> Neighbor and Boundary constraints
    public Vector3 ApplyConstraints(Vector3 proposedPosition, Vector3 currentPosition, List<Vector3> neighborPositions, Vector3 initialPosition)
    {
        Vector3 constrainedPos = proposedPosition; // Start with the proposed position

        // Apply neighbor constraints first (if neighbors exist)
        if (neighborPositions != null && neighborPositions.Count > 0)
        {
            constrainedPos = ConstrainToNeighbors(constrainedPos, neighborPositions, currentPosition, initialPosition);
        }

        // Apply boundary constraints to ensure probe stays within grid limits
        constrainedPos = ConstrainToBoundary(constrainedPos);

        return constrainedPos;
    }

    // FUNCTION: Apply focus area constraints to a proposed probe position
    public Vector3 ApplyFocusAreaConstraints(Vector3 proposedPosition, Vector3 focusMinBounds, Vector3 focusMaxBounds) // Focus area defined by min and max bounds
    {
        float x = Mathf.Clamp(proposedPosition.x, focusMinBounds.x + boundaryPadding, focusMaxBounds.x - boundaryPadding); // Clamp x within focus area with padding
        float y = Mathf.Clamp(proposedPosition.y, focusMinBounds.y + boundaryPadding, focusMaxBounds.y - boundaryPadding); // Clamp y within focus area with padding
        return new Vector3(x, y, proposedPosition.z); // Return constrained position
    }

    // FUNCTION: Constrain position within defined boundaries
    private Vector3 ConstrainToBoundary(Vector3 position) //  Constrain position within defined boundaries
    {
        float x = Mathf.Clamp(position.x, minX, maxX); // Clamp x within minX and maxX
        float y = Mathf.Clamp(position.y, minY, maxY); // Clamp y within minY and maxY
        return new Vector3(x, y, position.z); // Return constrained position
    }

    // FUNCTION: Constrain position based on the neighboring probes and grid boundaries
    // Constrains movement within a fixed rectangular region defined by neighbors relative to initial position
    private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, Vector3 currentPos, Vector3 initialPos)
    {
        Vector3 constrainedPos = proposedPos; // Start with the proposed position

        // Define a fixed rectangular region based on the nearest neighbor in each cardinal direction
        // from the probe's INITIAL position. This creates a "cell" the probe cannot leave.

        // Initialize bounds to grid boundaries (used if no neighbor exists in a direction)
        float leftBound = minX;    // Nearest obstacle to the left
        float rightBound = maxX;   // Nearest obstacle to the right
        float downBound = minY;    // Nearest obstacle below
        float upBound = maxY;      // Nearest obstacle above

        // Track whether each bound is set by a neighbor (true) or grid edge (false)
        bool leftIsNeighbor = false;
        bool rightIsNeighbor = false;
        bool downIsNeighbor = false;
        bool upIsNeighbor = false;

        // Find the nearest neighbor in each cardinal direction from the INITIAL position
        if (neighbors != null && neighbors.Count > 0)
        {
            foreach (Vector3 neighbor in neighbors)
            {
                // Check if neighbor is to the LEFT of initial position (regardless of Y)
                if (neighbor.x < initialPos.x)
                {
                    // Find the CLOSEST neighbor to the left (maximum X among left neighbors)
                    if (neighbor.x > leftBound)
                    {
                        leftBound = neighbor.x;
                        leftIsNeighbor = true;
                    }
                }

                // Check if neighbor is to the RIGHT of initial position (regardless of Y)
                if (neighbor.x > initialPos.x)
                {
                    // Find the CLOSEST neighbor to the right (minimum X among right neighbors)
                    if (neighbor.x < rightBound)
                    {
                        rightBound = neighbor.x;
                        rightIsNeighbor = true;
                    }
                }

                // Check if neighbor is BELOW initial position (regardless of X)
                if (neighbor.y < initialPos.y)
                {
                    // Find the CLOSEST neighbor below (maximum Y among neighbors below)
                    if (neighbor.y > downBound)
                    {
                        downBound = neighbor.y;
                        downIsNeighbor = true;
                    }
                }

                // Check if neighbor is ABOVE initial position (regardless of X)
                if (neighbor.y > initialPos.y)
                {
                    // Find the CLOSEST neighbor above (minimum Y among neighbors above)
                    if (neighbor.y < upBound)
                    {
                        upBound = neighbor.y;
                        upIsNeighbor = true;
                    }
                }
            }
        }

        // Small buffer to stop "right before" a neighbor (approximately half a grid cell)
        float neighborBuffer = cellSize * 0.5f;

        // Apply constraints with appropriate padding based on whether it's a neighbor or grid edge
        float minXConstraint = leftBound + (leftIsNeighbor ? neighborBuffer : 0);
        float maxXConstraint = rightBound - (rightIsNeighbor ? neighborBuffer : 0);
        float minYConstraint = downBound + (downIsNeighbor ? neighborBuffer : 0);
        float maxYConstraint = upBound - (upIsNeighbor ? neighborBuffer : 0);

        constrainedPos.x = Mathf.Clamp(constrainedPos.x, minXConstraint, maxXConstraint);
        constrainedPos.y = Mathf.Clamp(constrainedPos.y, minYConstraint, maxYConstraint);

        return constrainedPos;
    }

    // FUNCTION: Check if a position is within the grid boundaries
    public bool IsWithinBoundaries(Vector3 position)
    {
        return position.x >= minX && position.x <= maxX && // Check X boundaries
               position.y >= minY && position.y <= maxY; // Check Y boundaries
    }

    // FUNCTION: Check if a position respects the minimum distance from all neighbors
    public bool IsValidDistanceFromNeighbors(Vector3 position, List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0) // No neighbors means no distance constraints
            return true;

        Vector2 pos2D = new Vector2(position.x, position.y); // 2D representation of the position
        float neighborBuffer = cellSize * 0.5f; // Same buffer as in ConstrainToNeighbors

        foreach (Vector3 neighbor in neighbors) // Check distance to each neighbor
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y); // 2D representation of neighbor position
            float distance = Vector2.Distance(pos2D, neighbor2D); // Calculate distance

            if (distance < neighborBuffer)
                return false; // Too close to a neighbor
        }

        return true;
    }

    // FUNCTION: Get distance to the nearest neighbor
    public float GetDistanceToNearestNeighbor(Vector3 position, List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0) // No neighbors means no distance
            return float.MaxValue; 

        float minDist = float.MaxValue; // Initialize minimum distance
        Vector2 pos2D = new Vector2(position.x, position.y); // 2D representation of the position

        foreach (Vector3 neighbor in neighbors) // Check distance to each neighbor
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);
            float distance = Vector2.Distance(pos2D, neighbor2D);
            minDist = Mathf.Min(minDist, distance);
        }

        return minDist;
    }

    // FUNCTION: Get the bounding area defined by neighbor positions
    // Returns a Vector4 with (minX, maxX, minY, maxY)
    public Vector4 GetNeighborBounds(List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0)
            return new Vector4(minX, maxX, minY, maxY); // Return grid bounds if no neighbors

        // Initialize min/max values

        float minNeighborX = float.MaxValue; 
        float maxNeighborX = float.MinValue;
        float minNeighborY = float.MaxValue;
        float maxNeighborY = float.MinValue;

        // Iterate through neighbors to find bounds

        foreach (Vector3 neighbor in neighbors)
        {
            minNeighborX = Mathf.Min(minNeighborX, neighbor.x);
            maxNeighborX = Mathf.Max(maxNeighborX, neighbor.x);
            minNeighborY = Mathf.Min(minNeighborY, neighbor.y);
            maxNeighborY = Mathf.Max(maxNeighborY, neighbor.y);
        }

        return new Vector4(minNeighborX, maxNeighborX, minNeighborY, maxNeighborY);
    }

}