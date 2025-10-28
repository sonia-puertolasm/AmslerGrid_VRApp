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
    public float minNeighborDistance = 0.2f; // Minimum distance between neighbors (should match probe dot size)

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

    // FUNCTION: Constrain position based on the neighboring probes
    // Constrains movement within the area bounded by neighboring probe initial positions
    private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, Vector3 currentPos, Vector3 initialPos)
    {
        if (neighbors == null || neighbors.Count == 0) // If there is no neighbors, no constraints needed
            return proposedPos;

        Vector3 constrainedPos = proposedPos; // Start with the proposed position

        // Determine movement direction (axis-locked movement)
        float movementDeltaX = Mathf.Abs(proposedPos.x - currentPos.x);
        float movementDeltaY = Mathf.Abs(proposedPos.y - currentPos.y);
        bool isMovingHorizontally = movementDeltaX > movementDeltaY;

        // Threshold for considering positions on the same grid line
        float gridLineThreshold = 0.2f;

        // Find the closest neighbor in each cardinal direction from the INITIAL position
        // These neighbors define the fixed movement boundaries
        float? leftBound = null;   // Max X (cannot go further left than this)
        float? rightBound = null;  // Min X (cannot go further right than this)
        float? downBound = null;   // Max Y (cannot go further down than this)
        float? upBound = null;     // Min Y (cannot go further up than this)

        foreach (Vector3 neighbor in neighbors) // Iterate through each neighbor to determine directional bounds
        {
            if (isMovingHorizontally)
            {
                // For horizontal movement, only consider neighbors on the same horizontal grid line (same Y as initial position)
                if (Mathf.Abs(neighbor.y - initialPos.y) <= gridLineThreshold)
                {
                    // Check if neighbor is to the LEFT of initial position
                    if (neighbor.x < initialPos.x)
                    {
                        if (!leftBound.HasValue || neighbor.x > leftBound.Value)
                            leftBound = neighbor.x;
                    }
                    // Check if neighbor is to the RIGHT of initial position
                    else if (neighbor.x > initialPos.x)
                    {
                        if (!rightBound.HasValue || neighbor.x < rightBound.Value)
                            rightBound = neighbor.x;
                    }
                }
            }
            else
            {
                // For vertical movement, only consider neighbors on the same vertical grid line (same X as initial position)
                if (Mathf.Abs(neighbor.x - initialPos.x) <= gridLineThreshold)
                {
                    // Check if neighbor is BELOW initial position
                    if (neighbor.y < initialPos.y)
                    {
                        if (!downBound.HasValue || neighbor.y > downBound.Value)
                            downBound = neighbor.y;
                    }
                    // Check if neighbor is ABOVE initial position
                    else if (neighbor.y > initialPos.y)
                    {
                        if (!upBound.HasValue || neighbor.y < upBound.Value)
                            upBound = neighbor.y;
                    }
                }
            }
        }

        // Apply directional constraints with minimum distance
        // If a bound exists, constrain movement between neighbors
        // If no bound exists, allow movement to grid edges (handled by ConstrainToBoundary)
        if (leftBound.HasValue)
            constrainedPos.x = Mathf.Max(constrainedPos.x, leftBound.Value + minNeighborDistance);

        if (rightBound.HasValue)
            constrainedPos.x = Mathf.Min(constrainedPos.x, rightBound.Value - minNeighborDistance);

        if (downBound.HasValue)
            constrainedPos.y = Mathf.Max(constrainedPos.y, downBound.Value + minNeighborDistance);

        if (upBound.HasValue)
            constrainedPos.y = Mathf.Min(constrainedPos.y, upBound.Value - minNeighborDistance);

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

        foreach (Vector3 neighbor in neighbors) // Check distance to each neighbor
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y); // 2D representation of neighbor position
            float distance = Vector2.Distance(pos2D, neighbor2D); // Calculate distance

            if (distance < minNeighborDistance)
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