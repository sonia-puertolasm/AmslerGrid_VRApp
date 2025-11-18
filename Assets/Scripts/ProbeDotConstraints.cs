using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;

public class ProbeDotConstraints : MonoBehaviour
{
    private MainGrid mainGrid;

    public float boundaryPadding = 0.1f;
    private float cellSize;
    public float overlapBuffer = 0.15f;

    private float minX, maxX, minY, maxY;

    public float MinX => minX;
    public float MaxX => maxX;
    public float MinY => minY;
    public float MaxY => maxY;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid == null)
        {
            return;
        }

        CalculateBoundaries();
    }

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

    public Vector3 ApplyConstraints(Vector3 proposedPosition, Vector3 currentPosition, List<Vector3> neighborPositions, Vector3 initialPosition)
    {
        Vector3 constrainedPos = proposedPosition;

        if (neighborPositions != null && neighborPositions.Count > 0)
        {
            constrainedPos = ConstrainToNeighbors(constrainedPos, neighborPositions, currentPosition, initialPosition);
        }

        constrainedPos = ConstrainToBoundary(constrainedPos, currentPosition);

        return constrainedPos;
    }

    public Vector3 ApplyFocusAreaConstraints(Vector3 proposedPosition, Vector3 focusMinBounds, Vector3 focusMaxBounds)
    {
        float x = Mathf.Clamp(proposedPosition.x, focusMinBounds.x + boundaryPadding, focusMaxBounds.x - boundaryPadding);
        float y = Mathf.Clamp(proposedPosition.y, focusMinBounds.y + boundaryPadding, focusMaxBounds.y - boundaryPadding);
        return new Vector3(x, y, proposedPosition.z);
    }

    private Vector3 ConstrainToBoundary(Vector3 position, Vector3 currentPosition)
    {
        float x = Mathf.Clamp(position.x, minX, maxX);
        float y = Mathf.Clamp(position.y, minY, maxY);

        return new Vector3(x, y, position.z);
    }

    private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, Vector3 currentPos, Vector3 initialPos)
    {
        if (neighbors == null || neighbors.Count == 0)
            return proposedPos;

        float halfSize = mainGrid.TotalGridWidth / 2f;
        Vector3 center = mainGrid.GridCenterPosition;

        float constraintMinX = center.x - halfSize + boundaryPadding;
        float constraintMaxX = center.x + halfSize - boundaryPadding;
        float constraintMinY = center.y - halfSize + boundaryPadding;
        float constraintMaxY = center.y + halfSize - boundaryPadding;

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

            if (Mathf.Abs(deltaX) > Mathf.Abs(deltaY) + 0.1f)
            {
                float distance = Mathf.Abs(deltaX);

                if (deltaX < -0.1f && distance < closestLeftDist)
                {
                    closestLeftDist = distance;
                    nearestLeft = neighbor;
                }
                else if (deltaX > 0.1f && distance < closestRightDist)
                {
                    closestRightDist = distance;
                    nearestRight = neighbor;
                }
            }
            else if (Mathf.Abs(deltaY) > Mathf.Abs(deltaX) + 0.1f)
            {
                float distance = Mathf.Abs(deltaY);

                if (deltaY < -0.1f && distance < closestDownDist)
                {
                    closestDownDist = distance;
                    nearestDown = neighbor;
                }
                else if (deltaY > 0.1f && distance < closestUpDist)
                {
                    closestUpDist = distance;
                    nearestUp = neighbor;
                }
            }
        }

        if (nearestLeft.HasValue)
        {
            float neighborLineX = nearestLeft.Value.x;
            constraintMinX = Mathf.Max(constraintMinX, neighborLineX + overlapBuffer);
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
        constrainedPos.x = Mathf.Clamp(proposedPos.x, constraintMinX, constraintMaxX);
        constrainedPos.y = Mathf.Clamp(proposedPos.y, constraintMinY, constraintMaxY);

        return constrainedPos;
    }

    public bool IsWithinBoundaries(Vector3 position)
    {
        return position.x >= minX && position.x <= maxX &&
               position.y >= minY && position.y <= maxY;
    }

    public bool IsValidDistanceFromNeighbors(Vector3 position, List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0)
            return true;

        Vector2 pos2D = new Vector2(position.x, position.y);

        foreach (Vector3 neighbor in neighbors)
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);
            float distance = Vector2.Distance(pos2D, neighbor2D);

            if (distance < overlapBuffer)
                return false;
        }

        return true;
    }

    public float GetDistanceToNearestNeighbor(Vector3 position, List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0)
            return float.MaxValue;

        float minDist = float.MaxValue;
        Vector2 pos2D = new Vector2(position.x, position.y);

        foreach (Vector3 neighbor in neighbors)
        {
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);
            float distance = Vector2.Distance(pos2D, neighbor2D);
            minDist = Mathf.Min(minDist, distance);
        }

        return minDist;
    }

    public Vector4 GetNeighborBounds(List<Vector3> neighbors)
    {
        if (neighbors == null || neighbors.Count == 0)
            return new Vector4(minX, maxX, minY, maxY);

        float minNeighborX = float.MaxValue;
        float maxNeighborX = float.MinValue;
        float minNeighborY = float.MaxValue;
        float maxNeighborY = float.MinValue;

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