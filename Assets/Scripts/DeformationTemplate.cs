using System.Collections.Generic;
using UnityEngine;

// Defines different types of metamorphopsia patterns for simulation
[System.Serializable]
public enum TemplateType
{
    LocalDeformationTopLeft,
    LocalDeformationTopRight,
    LocalDeformationBottomLeft,
    LocalDeformationBottomRight
}

[System.Serializable]
public class DeformationTemplate
{
    public TemplateType templateType;
    [Range(0f, 2f)] public float distortionMagnitude = 0.5f;  // Strength of distortion
    [Range(0.1f, 3f)] public float distortionRadius = 1.5f;   // Spread of distortion effect
    
    // Calculated displacement field for this template
    private Dictionary<Vector2Int, Vector3> displacementField = new Dictionary<Vector2Int, Vector3>();
    
    // Generate displacement field for a given grid configuration
    public void GenerateDisplacementField(int gridSize, float cellSize, Vector3 gridCenter)
    {
        displacementField.Clear();
        
        int pointCount = gridSize + 1;
        float halfWidth = (gridSize * cellSize) / 2f;
        
        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                // Calculate normalized position (-1 to 1)
                float normalizedX = (col / (float)gridSize) * 2f - 1f;
                float normalizedY = (row / (float)gridSize) * 2f - 1f;
                
                Vector3 displacement = CalculateDisplacement(normalizedX, normalizedY, row, col, gridSize);
                
                // Scale displacement by cell size
                displacement *= cellSize;
                
                displacementField[new Vector2Int(col, row)] = displacement;
            }
        }
    }
    
    // Calculate displacement for a specific point based on template type
    private Vector3 CalculateDisplacement(float normX, float normY, int row, int col, int gridSize)
    {
        Vector3 displacement = Vector3.zero;
        
        switch (templateType)
        {
            case TemplateType.LocalDeformationTopLeft:
                displacement = CalculateLocalDistortion(normX, normY, -0.5f, 0.5f);
                break;
                
            case TemplateType.LocalDeformationTopRight:
                displacement = CalculateLocalDistortion(normX, normY, 0.5f, 0.5f);
                break;
                
            case TemplateType.LocalDeformationBottomLeft:
                displacement = CalculateLocalDistortion(normX, normY, -0.5f, -0.5f);
                break;
                
            case TemplateType.LocalDeformationBottomRight:
                displacement = CalculateLocalDistortion(normX, normY, 0.5f, -0.5f);
                break;

            default:
                displacement = Vector3.zero;
                break;
        }
        
        return displacement * distortionMagnitude;
    }
    
    // Local distortion: Gaussian-weighted displacement around a specific region
    private Vector3 CalculateLocalDistortion(float normX, float normY, float centerX, float centerY)
    {
        float dx = normX - centerX;
        float dy = normY - centerY;
        float distanceFromCenter = Mathf.Sqrt(dx * dx + dy * dy);

        // Gaussian falloff
        float sigma = 0.4f / distortionRadius;
        float weight = Mathf.Exp(-(distanceFromCenter * distanceFromCenter) / (2f * sigma * sigma));

        // Create local bulge effect
        float angle = Mathf.Atan2(dy, dx);
        float pushMagnitude = weight * distortionRadius;

        return new Vector3(
            Mathf.Cos(angle) * pushMagnitude,
            Mathf.Sin(angle) * pushMagnitude,
            0f
        );
    }
    
    // Get displacement for a specific grid point
    public Vector3 GetDisplacement(int col, int row)
    {
        Vector2Int key = new Vector2Int(col, row);
        if (displacementField.ContainsKey(key))
        {
            return displacementField[key];
        }
        return Vector3.zero;
    }
    
    // Get displacement for a specific grid point (Vector2Int version)
    public Vector3 GetDisplacement(Vector2Int gridPos)
    {
        if (displacementField.ContainsKey(gridPos))
        {
            return displacementField[gridPos];
        }
        return Vector3.zero;
    }
    
    // Check if template has any distortion
    public bool HasDistortion()
    {
        return distortionMagnitude > 0.001f;
    }
    
    // Get all displacement data for export/analysis
    public Dictionary<Vector2Int, Vector3> GetFullDisplacementField()
    {
        return new Dictionary<Vector2Int, Vector3>(displacementField);
    }
}