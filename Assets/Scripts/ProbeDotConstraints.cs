using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class ProbeDotConstraints : MonoBehaviour
{
    // Retrieval of grid configuration specific parameters
    private MainGrid mainGrid;
    private float cellSize;

    // Definition of boundary-specific parameters
    internal float boundaryPadding = 0.1f;
    internal float overlapBuffer = 0.25f;
    internal float maxDisplacementFactor = 0.8f;
    private float minX, maxX, minY, maxY;
    private float MinX => minX;
    private float MaxX => maxX;
    private float MinY => minY;
    private float MaxY => maxY;

    // METHOD: Initialization of all probe dot constraint methods
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid == null)
        {
            return;
        }

        CalculateBoundaries();
    }

    // HELPER METHOD: Precalculate the movement limits for probe by considering the centered extents of the grid with the padding
    private void CalculateBoundaries()
    {
        float halfSize = mainGrid.TotalGridWidth / 2f;
        Vector3 center = mainGrid.GridCenterPosition;
        cellSize = mainGrid.CellSize;

        minX = center.x - halfSize + boundaryPadding;
        maxX = center.x + halfSize - boundaryPadding;
        minY = center.y - halfSize + boundaryPadding;
        maxY = center.y + halfSize - boundaryPadding;
    }

    // METHOD: Keeps probe dot in a safe reason
    public Vector3 ApplyConstraints(Vector3 proposedPosition, Vector3 currentPosition, List<Vector3> neighborPositions, Vector3 initialPosition)
    {
        Vector3 constrainedPos = proposedPosition;

        constrainedPos = ConstrainToMaxDisplacement(constrainedPos, initialPosition); // Limits movement to the allowed displacement radius

        if (neighborPositions != null && neighborPositions.Count > 0) // Safety: Apply neighbor checks only when the neighbors exist
        {
            constrainedPos = ConstrainToNeighbors(constrainedPos, neighborPositions, currentPosition, initialPosition); // Adjusts the positioning to respect the spacing between neighbors
        }

        constrainedPos = ConstrainToBoundary(constrainedPos, currentPosition); // Clamps within the external grid boundaries

        return constrainedPos;
    }

    // HELPER METHOD: Limits how far a probe can displace from its original location
    private Vector3 ConstrainToMaxDisplacement(Vector3 proposedPos, Vector3 initialPos)
    {
        float maxDisplacement = cellSize * maxDisplacementFactor;
        
        Vector3 displacement = proposedPos - initialPos; // Calculate offset between proposed and initial positions
        
        if (displacement.magnitude > maxDisplacement) // Check if the offset exceeds the allowed radius
        {
            displacement = displacement.normalized * maxDisplacement;  // Rescale the displacement to the permitted distance while preserving the direction
            return initialPos + displacement; // Return the clamped position at the maximum radius
        }
        
        return proposedPos;
    }

    // HELPER METHOD: Snap the proposed probe dot position back into the focus window
    public Vector3 ApplyFocusAreaConstraints(Vector3 proposedPosition, Vector3 focusMinBounds, Vector3 focusMaxBounds)
    {
        float x = Mathf.Clamp(proposedPosition.x, focusMinBounds.x + boundaryPadding, focusMaxBounds.x - boundaryPadding);
        float y = Mathf.Clamp(proposedPosition.y, focusMinBounds.y + boundaryPadding, focusMaxBounds.y - boundaryPadding);
        return new Vector3(x, y, proposedPosition.z);
    }

    // HELPER METHOD: Ensure a probe dot stays inside the pre-defined grid box
    private Vector3 ConstrainToBoundary(Vector3 position, Vector3 currentPosition)
    {
        float x = Mathf.Clamp(position.x, minX, maxX);
        float y = Mathf.Clamp(position.y, minY, maxY);
        return new Vector3(x, y, position.z);
    }

    // HELPER METHOD: Ensures a probe dot stays within the neighboring probe dots
    private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, Vector3 currentPos, Vector3 initialPos)
    {
        if (neighbors == null || neighbors.Count == 0) // Safety: Returns early if there is no neighbors to consider
            return proposedPos;

        float halfSize = mainGrid.TotalGridWidth / 2f;
        Vector3 center = mainGrid.GridCenterPosition;

        float constraintMinX = center.x - halfSize + boundaryPadding;
        float constraintMaxX = center.x + halfSize - boundaryPadding;
        float constraintMinY = center.y - halfSize + boundaryPadding;
        float constraintMaxY = center.y + halfSize - boundaryPadding;

        // Placeholders for the closest neighbors in all cardinal directions
        Vector3? nearestLeft = null;
        Vector3? nearestRight = null;
        Vector3? nearestUp = null;
        Vector3? nearestDown = null;

        // Tracking of the smallest distances in all cardinal directions
        float closestLeftDist = float.MaxValue;
        float closestRightDist = float.MaxValue;
        float closestUpDist = float.MaxValue;
        float closestDownDist = float.MaxValue;

        // Inspect over each neighbor position
        foreach (Vector3 neighbor in neighbors)
        {
            float deltaX = neighbor.x - initialPos.x;
            float deltaY = neighbor.y - initialPos.y;

            if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY) + 0.1f) // Classification of horizontal (x-axis) neighbors
            {
                float distance = Mathf.Abs(deltaX); // Compute horizontal separation

                if (deltaX < -0.1f && distance < closestLeftDist) // Checks for closer neighbor to the left
                {
                    closestLeftDist = distance;
                    nearestLeft = neighbor; // Record neighbor as the closest neighbor
                }
                else if (deltaX > 0.1f && distance < closestRightDist) // Checks for closer neighbor to the right
                {
                    closestRightDist = distance;
                    nearestRight = neighbor;
                }
            }
            else if (Mathf.Abs(deltaY) > Mathf.Abs(deltaX) + 0.1f) // Classification of vertical (y-axis) neighbors
            {
                float distance = Mathf.Abs(deltaY); // Compute vertical separation

                if (deltaY < -0.1f && distance < closestDownDist) // Checks for closer neighbor below
                {
                    closestDownDist = distance;
                    nearestDown = neighbor;
                }
                else if (deltaY > 0.1f && distance < closestUpDist) // Checks for closer neighbor above
                {
                    closestUpDist = distance;
                    nearestUp = neighbor;
                }
            }
        }

        if (nearestLeft.HasValue) // Tightens boundaries when a neighbor exists to the left
        {
            float neighborLineX = nearestLeft.Value.x; // Read's the coordinate x of the neighbor
            constraintMinX = Mathf.Max(constraintMinX, neighborLineX + overlapBuffer); // Pushes the left bound to mantain spacing between probe dots
        }

        if (nearestRight.HasValue)
        {
            float neighborLineX = nearestRight.Value.x;
            constraintMaxX = Mathf.Min(constraintMaxX, neighborLineX - overlapBuffer);
        }

        if (nearestDown.HasValue)
        {
            float neighborLineY = nearestDown.Value.y;
            constraintMinY = Mathf.Max(constraintMinY, neighborLineY + overlapBuffer);
        }

        if (nearestUp.HasValue)
        {
            float neighborLineY = nearestUp.Value.y;
            constraintMaxY = Mathf.Min(constraintMaxY, neighborLineY - overlapBuffer);
        }

        Vector3 constrainedPos = proposedPos;
        constrainedPos.x = Mathf.Clamp(proposedPos.x, constraintMinX, constraintMaxX); // Clamps the x-coordinate within the neighbor's adjusted bounds
        constrainedPos.y = Mathf.Clamp(proposedPos.y, constraintMinY, constraintMaxY); // Clamps the y-coordinate within the neighbor's adjusted bounds

        return constrainedPos;
    }

    // HELPER METHOD: Performs a check of whereas the probe dot being within boundaries or not
    public bool IsWithinBoundaries(Vector3 position) 
    {
        return position.x >= minX && position.x <= maxX &&
               position.y >= minY && position.y <= maxY;
    }

    // HELPER METHOD: Verifies that a probe dot stays far enough from its neighbors
    public bool IsValidDistanceFromNeighbors(Vector3 position, List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0) // Safety: Checks for null/empty neighbor, accepting the position if there is no neighbors
            return true;

        Vector2 pos2D = new Vector2(position.x, position.y); // Project the proposed position into a 2D plane

        foreach (Vector3 neighbor in neighbors) // Iterates over the position of every neighbor
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);
            float distance = Vector2.Distance(pos2D, neighbor2D); // Measures the distance between the selected and the neighboring probe dots

            if (distance < overlapBuffer) // Tests if the neighbor is too close. If it is, reject the proposed position because it is too close.
                return false;
        }

        return true;
    }

    // HELPER METHOD: Reports how close is the probe dot to the nearest neighbor
    public float GetDistanceToNearestNeighbor(Vector3 position, List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0) // Safety: Avoids normal procedure in case there is no neighbors or they are null
            return float.MaxValue;

        float minDist = float.MaxValue; // Initializes the running minimum distance
        Vector2 pos2D = new Vector2(position.x, position.y); // Projects the probe position to 2D

        foreach (Vector3 neighbor in neighbors) // Iterates over every neighbor
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);
            float distance = Vector2.Distance(pos2D, neighbor2D); // Computes the distance from the selected to the neighboring probe dot
            minDist = Mathf.Min(minDist, distance); // Keeps the smallest distance so far
        }

        return minDist;
    }

    // HELPER METHOD: Determine the tightest bounds enclosing the provided neighbors
    public Vector4 GetNeighborBounds(List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0) // Safety: If no neighbor if found or it is null, return to the precomputed grid bounds
            return new Vector4(minX, maxX, minY, maxY);

        // Initialization of standardized values
        float minNeighborX = float.MaxValue;
        float maxNeighborX = float.MinValue;
        float minNeighborY = float.MaxValue;
        float maxNeighborY = float.MinValue;

        foreach (Vector3 neighbor in neighbors) // Iteration over every neighbor for updating each value depending on the findings
        {
            minNeighborX = Mathf.Min(minNeighborX, neighbor.x);
            maxNeighborX = Mathf.Max(maxNeighborX, neighbor.x);
            minNeighborY = Mathf.Min(minNeighborY, neighbor.y);
            maxNeighborY = Mathf.Max(maxNeighborY, neighbor.y);
        }

        return new Vector4(minNeighborX, maxNeighborX, minNeighborY, maxNeighborY);
    }
}