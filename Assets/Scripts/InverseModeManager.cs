using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Manages the inverse simulation mode where grid starts distorted and subject corrects it
public class InverseModeManager : MonoBehaviour
{
    // References to core systems
    private MainGrid mainGrid;
    private GridRebuildManager gridRebuildManager;
    private ProbeDots probeDots;
    private IterationManager iterationManager;
    
    // Inverse mode configuration
    public bool inverseModeEnabled = false;
    
    public TemplateType templateType;
    [Range(0f, 2f)] public float distortionMagnitude = 0.5f;
    [Range(0.1f, 3f)] public float distortionRadius = 1.5f;

    // Internal template object
    private DeformationTemplate deformationTemplate;
    
    // Template-related data
    private Dictionary<GameObject, Vector3> templateDisplacements = new Dictionary<GameObject, Vector3>();
    private Dictionary<Vector2Int, Vector3> gridPointTemplateDisplacements = new Dictionary<Vector2Int, Vector3>();
    
    // Grid configuration
    private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    
    // Public accessors
    public bool InverseModeEnabled => inverseModeEnabled;
    public DeformationTemplate Template => deformationTemplate;
    
    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        probeDots = FindObjectOfType<ProbeDots>();
        iterationManager = FindObjectOfType<IterationManager>();
        
        if (mainGrid != null)
        {
            gridSize = mainGrid.GridSize;
            cellSize = mainGrid.CellSize;
            gridCenter = mainGrid.GridCenterPosition;
            
            transform.position = gridCenter;
            transform.rotation = Quaternion.identity;
        }
        
        // Create template from public inspector fields
        CreateTemplateFromInspectorSettings();
        
        if (inverseModeEnabled)
        {
            StartCoroutine(InitializeInverseMode());
        }
    }
    
    // Create the DeformationTemplate object from Inspector settings
    private void CreateTemplateFromInspectorSettings()
    {
        deformationTemplate = new DeformationTemplate();
        deformationTemplate.templateType = templateType;
        deformationTemplate.distortionMagnitude = distortionMagnitude;
        deformationTemplate.distortionRadius = distortionRadius;
    }
    
    // Initialize the inverse mode with template distortion
    private IEnumerator InitializeInverseMode()
    {
        // Wait for other systems to initialize
        yield return new WaitForSeconds(0.3f);
        
        if (!deformationTemplate.HasDistortion())
        {
            yield break;
        }
        
        // Generate the displacement field for the template
        deformationTemplate.GenerateDisplacementField(gridSize, cellSize, gridCenter);
        
        // Wait for probes to be created
        while (probeDots == null || probeDots.probes == null || probeDots.probes.Count == 0)
        {
            yield return null;
        }
        
        // Apply template distortion to grid and probes
        ApplyTemplateDistortion();
    }
    
    // Apply the template distortion to grid points and probes
    private void ApplyTemplateDistortion()
    {
        if (gridRebuildManager == null || probeDots == null) return;
        
        // Store template displacements for all grid points
        int pointCount = gridSize + 1;
        for (int row = 0; row < pointCount; row++)
        {
            for (int col = 0; col < pointCount; col++)
            {
                Vector3 displacement = deformationTemplate.GetDisplacement(col, row);
                gridPointTemplateDisplacements[new Vector2Int(col, row)] = displacement;
            }
        }
        
        // Apply template distortion to accumulated displacement array in GridRebuildManager
        if (gridRebuildManager.accumulatedDisplacement != null)
        {
            for (int row = 0; row < pointCount; row++)
            {
                for (int col = 0; col < pointCount; col++)
                {
                    Vector3 templateDisp = deformationTemplate.GetDisplacement(col, row);
                    gridRebuildManager.accumulatedDisplacement[row, col] = templateDisp;
                }
            }
        }
        
        // Apply template distortion to probe initial positions
        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null) continue;
            
            // Get probe's grid position
            Vector2Int probeGridPos = gridRebuildManager.GetProbeGridCell(probe);
            
            // Get template displacement for this position
            Vector3 templateDisp = deformationTemplate.GetDisplacement(probeGridPos);
            
            // Store the template displacement for this probe
            templateDisplacements[probe] = templateDisp;
            
            // Apply displacement to probe position
            Vector3 originalPos = probeDots.probeInitialPositions[probe];
            Vector3 distortedPos = originalPos + templateDisp;
            
            probe.transform.position = distortedPos;
            
            // Update the probe's recorded initial position to the distorted position
            // This is important so the constraint system works correctly
            probeDots.probeInitialPositions[probe] = distortedPos;
        }
        
        // Force grid rebuild to show distorted grid
        if (gridRebuildManager != null)
        {
            // Trigger rebuild by disabling and re-enabling
            bool wasEnabled = gridRebuildManager.enableDeformation;
            gridRebuildManager.enableDeformation = false;
            gridRebuildManager.enableDeformation = wasEnabled;
        }
    }
    
    // Get template displacement for a grid position
    public Vector3 GetTemplateDisplacement(Vector2Int gridPos)
    {
        if (gridPointTemplateDisplacements.ContainsKey(gridPos))
        {
            return gridPointTemplateDisplacements[gridPos];
        }
        return Vector3.zero;
    }
    
    // Get the true original (un-distorted) position for a grid cell
    private Vector3 GetTrueOriginalPosition(Vector2Int gridCell)
    {
        float halfWidth = (gridSize * cellSize) / 2f;
        float originX = gridCenter.x - halfWidth;
        float originY = gridCenter.y - halfWidth;
        
        float x = originX + gridCell.x * cellSize;
        float y = originY + gridCell.y * cellSize;
        float z = gridCenter.z;
        
        return new Vector3(x, y, z);
    }
    
    // Reset to original template distortion
    public void ResetToTemplateDistortion()
    {
        if (!inverseModeEnabled) return;
        
        foreach (GameObject probe in probeDots.probes)
        {
            if (probe == null || !templateDisplacements.ContainsKey(probe)) continue;
            
            Vector2Int probeGridPos = gridRebuildManager.GetProbeGridCell(probe);
            Vector3 trueOriginalPos = GetTrueOriginalPosition(probeGridPos);
            Vector3 templateDisp = templateDisplacements[probe];
            
            probe.transform.position = trueOriginalPos + templateDisp;
        }
    }
    
    // Enable/disable inverse mode at runtime
    public void SetInverseMode(bool enabled)
    {
        if (enabled && !inverseModeEnabled)
        {
            inverseModeEnabled = true;
            CreateTemplateFromInspectorSettings();
            StartCoroutine(InitializeInverseMode());
        }
        else if (!enabled && inverseModeEnabled)
        {
            inverseModeEnabled = false;
        }
    }
    
    // Set a new template and reinitialize
    public void SetTemplate(TemplateType type, float magnitude = 0.5f, float radius = 1.5f)
    {
        templateType = type;
        distortionMagnitude = magnitude;
        distortionRadius = radius;
        
        CreateTemplateFromInspectorSettings();
        
        if (inverseModeEnabled)
        {
            StartCoroutine(InitializeInverseMode());
        }
    }
}