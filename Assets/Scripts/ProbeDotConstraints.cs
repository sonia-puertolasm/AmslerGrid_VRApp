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
    public float boundaryPadding = 1.0f; // Padding from grid edges to prevent deformation from extending beyond boundaries
    private float cellSize; // Size of one grid cell - used to keep probes one cell away from neighbors
    public float constraintDamping = 0.5f; // Damping zone before hard constraint (in world units) - probe stops this far from boundaries

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
        constrainedPos = ConstrainToBoundary(constrainedPos, currentPosition);

        return constrainedPos;
    }

    // FUNCTION: Apply focus area constraints to a proposed probe position
    public Vector3 ApplyFocusAreaConstraints(Vector3 proposedPosition, Vector3 focusMinBounds, Vector3 focusMaxBounds) // Focus area defined by min and max bounds
    {
        float x = Mathf.Clamp(proposedPosition.x, focusMinBounds.x + boundaryPadding, focusMaxBounds.x - boundaryPadding); // Clamp x within focus area with padding
        float y = Mathf.Clamp(proposedPosition.y, focusMinBounds.y + boundaryPadding, focusMaxBounds.y - boundaryPadding); // Clamp y within focus area with padding
        return new Vector3(x, y, proposedPosition.z); // Return constrained position
    }

    // FUNCTION: Constrain position with hard stop before boundaries (no clamping, no bouncing)
    // Returns the constrained position, stopping before reaching min/max by the constraintDamping amount
    private float ConstrainValue(float value, float currentValue, float min, float max, float stopDistance)
    {
        // Define the safe zone boundaries (where the probe should stop)
        float safeMin = min + stopDistance;
        float safeMax = max - stopDistance;

        // If already at or beyond safe boundary, don't allow further movement in that direction
        if (currentValue <= safeMin && value < currentValue)
        {
            // Already at/beyond min boundary, don't allow moving further left/down
            return currentValue;
        }

        if (currentValue >= safeMax && value > currentValue)
        {
            // Already at/beyond max boundary, don't allow moving further right/up
            return currentValue;
        }

        // If new position would exceed safe boundaries, clamp to safe boundary
        if (value < safeMin)
        {
            return safeMin;
        }
        if (value > safeMax)
        {
            return safeMax;
        }

        // Within safe zone, allow free movement
        return value;
    }

    // FUNCTION: Constrain position within defined boundaries
    private Vector3 ConstrainToBoundary(Vector3 position, Vector3 currentPosition)
    {
        float x = ConstrainValue(position.x, currentPosition.x, minX, maxX, constraintDamping);
        float y = ConstrainValue(position.y, currentPosition.y, minY, maxY, constraintDamping);
        return new Vector3(x, y, position.z);
    }

    // FUNCTION: Constrain position based on the neighboring probes and grid boundaries
    // Prevents overlapping by maintaining minimum distance from neighbors' current positions
    private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, Vector3 currentPos, Vector3 initialPos)
    {
        Vector3 constrainedPos = proposedPos; // Start with the proposed position

        // Minimum safe distance from neighbors to prevent overlap (increased to avoid deformation conflicts)
        // Using full cellSize ensures probes stay far enough apart to avoid deformation overlap
        float minDistanceFromNeighbor = cellSize * 1.0f;

        // Check if proposed position would be too close to any neighbor
        if (neighbors != null && neighbors.Count > 0)
        {
            foreach (Vector3 neighbor in neighbors)
            {
                // Calculate distance from proposed position to this neighbor's current position
                Vector2 proposedPos2D = new Vector2(constrainedPos.x, constrainedPos.y);
                Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);

                float distanceToNeighbor = Vector2.Distance(proposedPos2D, neighbor2D);

                // If proposed position is too close to a neighbor, reject the movement
                if (distanceToNeighbor < minDistanceFromNeighbor)
                {
                    // Stay at current position to prevent getting too close
                    return currentPos;
                }
            }
        }

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