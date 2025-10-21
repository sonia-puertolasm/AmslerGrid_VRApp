using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Vector3 = UnityEngine.Vector3;
using Vector2 = UnityEngine.Vector2;

public class ProbeDotConstraints : MonoBehaviour
{
    [Header("Constraint Settings")]
    [SerializeField] private float neighborDistanceMultiplier = 1.5f;
    [SerializeField] private float boundaryPadding = 0.1f;
    
    private MainGrid mainGrid;
    private float minX, maxX, minY, maxY;
    
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
        
        minX = center.x - halfSize + boundaryPadding;
        maxX = center.x + halfSize - boundaryPadding;
        minY = center.y - halfSize + boundaryPadding;
        maxY = center.y + halfSize - boundaryPadding;
        
    }
    

    public Vector3 ApplyConstraints(Vector3 proposedPosition, List<Vector3> neighborPositions, float maxNeighborDistance, int currentIteration)
    {
        Vector3 constrainedPos = proposedPosition;
        
        constrainedPos = ConstrainToBoundary(constrainedPos);
        
        if (neighborPositions != null && neighborPositions.Count > 0)
        {
            constrainedPos = ConstrainToNeighbors(constrainedPos, neighborPositions, maxNeighborDistance, currentIteration);
        }
        
        return constrainedPos;
    }
    

    private Vector3 ConstrainToBoundary(Vector3 position)
    {
        float x = Mathf.Clamp(position.x, minX, maxX);
        float y = Mathf.Clamp(position.y, minY, maxY);
        return new Vector3(x, y, position.z);
    }
    

   private Vector3 ConstrainToNeighbors(Vector3 proposedPos, List<Vector3> neighbors, float maxDistance, int currentIteration)
    {
        Vector3 constrainedPos = proposedPos;

        foreach (Vector3 neighbor in neighbors)
        {
            if (!IsSameIteration(neighbor, currentIteration))
            {
                continue;
            }
        
            Vector2 proposedPos2D = new Vector2(proposedPos.x, proposedPos.y);
            Vector2 neighbor2D = new Vector2(neighbor.x, neighbor.y);
            float distance = Vector2.Distance(proposedPos2D, neighbor2D);

            float minDistance = 0.2f; 

            if (distance < minDistance)
            {
                Vector2 direction = (proposedPos2D - neighbor2D).normalized;
                Vector2 minPos2D = neighbor2D + direction * minDistance;
                constrainedPos = new Vector3(minPos2D.x, minPos2D.y, proposedPos.z);
            }
            else if (distance > maxDistance)
            {
                Vector2 direction = (proposedPos2D - neighbor2D).normalized;
                Vector2 maxPos2D = neighbor2D + direction * maxDistance;
                constrainedPos = new Vector3(maxPos2D.x, maxPos2D.y, proposedPos.z);
            }
        }

        return constrainedPos;
    }
    
    public float GetMaxNeighborDistance(int iteration)
    {
        if (mainGrid == null)
        {
            return 1.5f; 
        }
        
        float regionSize = mainGrid.TotalGridWidth / Mathf.Pow(3f, iteration - 1);
        float normalSpacing = regionSize / 3f;
        return normalSpacing * neighborDistanceMultiplier;
    }

    private bool IsSameIteration(Vector3 neighbor, int currentIteration)
    {
        string neighborName = neighbor.ToString();
        return neighborName.Contains($"Iter{currentIteration}_");
    }
}