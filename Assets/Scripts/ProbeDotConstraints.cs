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
    public float boundaryPadding = 0.2f; // Padding from grid edges to prevent deformation from extending beyond boundaries
    private float cellSize; // Size of one grid cell - used to keep probes one cell away from neighbors
    public float constraintDamping = 0.15f; // Damping zone before constraint (in world units) - probe stops this far from boundaries

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
        cellSize = mainGrid.CellSize;

        minX = center.x - halfSize + cellSize + boundaryPadding;
        maxX = center.x + halfSize - cellSize - boundaryPadding;
        minY = center.y - halfSize + cellSize + boundaryPadding;
        maxY = center.y + halfSize - cellSize - boundaryPadding;

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
        // Return the clamping value (where the probe should not be allowed to go further)
        return Mathf.Clamp(value, min + stopDistance, max - stopDistance);
    }

    // FUNCTION: Constrain position within defined boundaries
    private Vector3 ConstrainToBoundary(Vector3 position, Vector3 currentPosition)
    {
        // Define independent axis movement - constrain every axis separately
        float x = position.x;
        float y = position.y;

        // X-axis constrain

        if (x < minX + constraintDamping)
            x = minX + constraintDamping;

        else if (x > maxX - constraintDamping)
            x = maxX - constraintDamping;

        // Y-axis constrain

        if (y < minY + constraintDamping)
            y = minY + constraintDamping;

        else if (y > maxY - constraintDamping)
            y = maxY - constraintDamping;

        // Return position vector

        return new Vector3(x, y, position.z);
    }

    // FUNCTION: Constrain position based on the neighboring probes and grid boundaries
    // Creates a rectangular boundary based on the nearest CARDINAL neighbors in each direction
    private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, Vector3 currentPos, Vector3 initialPos)
    {
        if (neighbors == null || neighbors.Count == 0)
            return proposedPos;

        // Start with grid boundaries as default constraints
        float halfSize = mainGrid.TotalGridWidth / 2f;
        Vector3 center = mainGrid.GridCenterPosition;

        float minX = center.x - halfSize + cellSize + boundaryPadding;
        float maxX = center.x + halfSize - cellSize - boundaryPadding;
        float minY = center.y - halfSize + cellSize + boundaryPadding;
        float maxY = center.y + halfSize - cellSize - boundaryPadding;

        // Find the NEAREST cardinal neighbor in each direction (ignore diagonals)
        Vector3? nearestLeft = null;
        Vector3? nearestRight = null;
        Vector3? nearestUp = null;
        Vector3? nearestDown = null;

        float closestLeftDist = float.MaxValue;
        float closestRightDist = float.MaxValue;
        float closestUpDist = float.MaxValue;
        float closestDownDist = float.MaxValue;

        foreach (Vector3 neighbor in neighbors)
        {
            float deltaX = neighbor.x - initialPos.x;
            float deltaY = neighbor.y - initialPos.y;

            // Check if this is primarily a horizontal neighbor (ignore if mostly diagonal)
            if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY) + 0.1f)
            {
                // Neighbor is primarily to the left or right
                float distance = Mathf.Abs(deltaX);

                if (deltaX < -0.1f && distance < closestLeftDist)
                {
                    // Neighbor is to the left
                    closestLeftDist = distance;
                    nearestLeft = neighbor;
                }
                else if (deltaX > 0.1f && distance < closestRightDist)
                {
                    // Neighbor is to the right
                    closestRightDist = distance;
                    nearestRight = neighbor;
                }
            }
            // Check if this is primarily a vertical neighbor
            else if (Mathf.Abs(deltaY) > Mathf.Abs(deltaX) + 0.1f)
            {
                // Neighbor is primarily above or below
                float distance = Mathf.Abs(deltaY);

                if (deltaY < -0.1f && distance < closestDownDist)
                {
                    // Neighbor is below
                    closestDownDist = distance;
                    nearestDown = neighbor;
                }
                else if (deltaY > 0.1f && distance < closestUpDist)
                {
                    // Neighbor is above
                    closestUpDist = distance;
                    nearestUp = neighbor;
                }
            }
        }

        // Apply constraints based on nearest cardinal neighbors only
        // If no neighbor exists in a direction, keep the grid boundary

        if (nearestLeft.HasValue)
        {
            float midX = (initialPos.x + nearestLeft.Value.x) / 2f;
            minX = Mathf.Max(minX, midX);
        }

        if (nearestRight.HasValue)
        {
            float midX = (initialPos.x + nearestRight.Value.x) / 2f;
            maxX = Mathf.Min(maxX, midX);
        }

        if (nearestDown.HasValue)
        {
            float midY = (initialPos.y + nearestDown.Value.y) / 2f;
            minY = Mathf.Max(minY, midY);
        }

        if (nearestUp.HasValue)
        {
            float midY = (initialPos.y + nearestUp.Value.y) / 2f;
            maxY = Mathf.Min(maxY, midY);
        }

        // Hard clamp the proposed position to the calculated boundaries
        Vector3 constrainedPos = proposedPos;
        constrainedPos.x = Mathf.Clamp(proposedPos.x, minX, maxX);
        constrainedPos.y = Mathf.Clamp(proposedPos.y, minY, maxY);

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