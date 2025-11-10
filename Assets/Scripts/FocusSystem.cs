using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FocusSystem : MonoBehaviour
{
    // Definition of grid configuration specific parameters
    private ProbeDots probeDots;
    private GridRebuildManager gridRebuildManager;
    private GameObject centerFixationPoint;
     private int gridSize;
    private float cellSize;
    private Vector3 gridCenter;
    private float halfWidth;

    // Definition of focus mode specific parameters
    private bool isInFocusMode = false;
    public bool focusSystemEnabled = false;
    private const int focusRadius = 2;

    // Definition of dictionary for the visibility of the probes
    private Dictionary<GameObject, bool> originalProbeVisibility = new Dictionary<GameObject, bool>();

    // Definition of dictionary for the line segments to be shown while in focus mode
    private List<FocusLineSegment> focusModeLineSegments = new List<FocusLineSegment>();
    
    // Definition of parameters for the focus mode when updated
    private int currentFocusMinCol = 0;
    private int currentFocusMaxCol = 0;
    private int currentFocusMinRow = 0;
    private int currentFocusMaxRow = 0;
    private int currentFocusProbeIndex = -1;

    // Declaration of the segment of lines to be defined while in focus mode
    private class FocusLineSegment
    {
        // Definition of specific parameters for the definition of the line segments of focus
        public LineRenderer segmentRenderer;
        public LineRenderer sourceRenderer;
        public int startIndex;
        public int endIndex;
        public bool isHorizontal;

        // Constructor for the FocusLineSegment class
        public FocusLineSegment(LineRenderer segment, LineRenderer source, int start, int end, bool horizontal)
        {
            segmentRenderer = segment;
            sourceRenderer = source;
            startIndex = start;
            endIndex = end;
            isHorizontal = horizontal;
        }
    }

    // Initialization of all grid-generation functions
    void Start()
    {
        probeDots = FindObjectOfType<ProbeDots>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        centerFixationPoint = GameObject.Find("CenterFixationPoint");

        MainGrid mainGrid = FindObjectOfType<MainGrid>();

        if (mainGrid != null) // Safety: ensures that the parameters are defined ONLY when the Amsler Grid exists
        {
            gridSize = mainGrid.GridSize;
            cellSize = mainGrid.CellSize;
            gridCenter = mainGrid.GridCenterPosition;
            halfWidth = mainGrid.TotalGridWidth / 2f;
        }
    }

    // Callback method for every frame
    void Update()
    {
        if (!focusSystemEnabled) // Safety: exit in case of focus system not being enabled
        {
            return;
        }

        if (probeDots == null || gridRebuildManager == null) // Safety: exit in case of probe dots and deformation manager not existing
        {
            return;
        }

        int currentSelectedIndex = probeDots.selectedProbeIndex; // Obtaining of current index of selected probe

        if (currentSelectedIndex != -1 && !isInFocusMode) // Safety: avoid un-indexable probe dots and ensure that the user is not already in focus mode
        {
            EnterFocusMode(currentSelectedIndex);
        }

        else if (isInFocusMode && currentSelectedIndex == -1)
        {
            ExitFocusMode();
        }
        
        else if (isInFocusMode && currentSelectedIndex >= 0)
        {
            UpdateFocusModeSegments();
        }
    }

    // FUNCTION: Entering of focus mode
    private void EnterFocusMode(int probeIndex)
    {
        isInFocusMode = true;
        currentFocusProbeIndex = probeIndex;
        HideAllProbesExcept(probeIndex);
        ShowFocusAreaAroundProbe(probeIndex);
    }

    // FUNCTION: Exiting of focus mode
    public void ExitFocusMode()
    {
        if (!isInFocusMode) //Safety: avoids the action of exiting focus mode while already not being in it
        {
            return;
        }

        isInFocusMode = false;
        currentFocusProbeIndex = -1;

        RestoreAllLines();
        RestoreProbeVisibility();
    }

    // FUNCTION: Displaying of area of interest around the selected probe dot
    private void ShowFocusAreaAroundProbe(int probeIndex)
    {
        if (gridRebuildManager == null || probeDots == null || probeDots.probes == null) //Safety: exit in case of the main elements necessary for focus mode not existing
            return;

        if (probeIndex < 0 || probeIndex >= probeDots.probes.Count) // Safety: exit for avoiding invalid probe dot search
            return;

        GameObject selectedProbe = probeDots.probes[probeIndex];

        if (selectedProbe == null) // Safety: exit in case of no selected probe
            return;

        //Definition of probe grid position in x-axis and y-axis    
        Vector2Int probeGridPos = GetProbeGridIndex(selectedProbe);
        int probeRow = probeGridPos.y;
        int probeCol = probeGridPos.x;

        // Iterate over every line-rendered horizontal line to hide them when entering focus mode
        foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool)
        {
            if (lr != null)
                lr.enabled = false;
        }

        // Iterate over every line-rendered vertical line to hide them when entering focus mode
        foreach (LineRenderer lr in gridRebuildManager.verticalLinePool)
        {
            if (lr != null)
                lr.enabled = false;
        }

        // Definition of min and max ranges of column and row for focus mode of probe of interest
        currentFocusMinCol = Mathf.Max(0, probeCol - focusRadius);
        currentFocusMaxCol = Mathf.Min(gridSize, probeCol + focusRadius);
        currentFocusMinRow = Mathf.Max(0, probeRow - focusRadius);
        currentFocusMaxRow = Mathf.Min(gridSize, probeRow + focusRadius);

        // Showing of only vertical and horizontal lines that go through the selected probe dot
        if (probeRow >= 0 && probeRow < gridRebuildManager.horizontalLinePool.Count)
        {
            LineRenderer horizontalLine = gridRebuildManager.horizontalLinePool[probeRow]; // Obtanining of specific horizontal line
            if (horizontalLine != null) // Safety: ensure that the horizontal line exists
            {
                CreateLineSegment(horizontalLine, currentFocusMinCol, currentFocusMaxCol,
                                $"Horizontal_Row{probeRow}", true); // Display specific segment of the original rendered line
            }
        }

        if (probeCol >= 0 && probeCol < gridRebuildManager.verticalLinePool.Count)
        {
            LineRenderer verticalLine = gridRebuildManager.verticalLinePool[probeCol]; // Obtanining of specific vertical line
            if (verticalLine != null) // Safety: ensure that the vertical line exists
            {
                CreateLineSegment(verticalLine, currentFocusMinRow, currentFocusMaxRow,
                                $"Vertical_Col{probeCol}", false); // Display specific segment of the original rendered line
            }
        }

        if (centerFixationPoint != null) // Safety: ensure that the center fixation point exists
        {
            Renderer renderer = centerFixationPoint.GetComponent<Renderer>(); // Display the center fixation point
            if (renderer != null) renderer.enabled = true;
        }
    }

    // HELPER FUNCTION: Create "new" visual segment by extracting a portion of an existing LineRenderer
    private void CreateLineSegment(LineRenderer originalLine, int startIndex, int endIndex, string name, bool isHorizontal)
    {
        GameObject segmentObj = new GameObject(name); // Definition of segment GO
        segmentObj.transform.SetParent(transform);

        LineRenderer segmentLine = segmentObj.AddComponent<LineRenderer>(); // Adding of LineRenderer property to new GO

        // Definition of visual properties as identical to the original line
        segmentLine.startWidth = originalLine.startWidth;
        segmentLine.endWidth = originalLine.endWidth;
        segmentLine.material = new Material(originalLine.material);
        segmentLine.startColor = originalLine.startColor;
        segmentLine.endColor = originalLine.endColor;
        segmentLine.useWorldSpace = true;
        int segmentLength = endIndex - startIndex + 1;
        segmentLine.positionCount = segmentLength;

        // Iterative loop over all indexes to copy every position from the original line
        for (int i = 0; i < segmentLength; i++)
        {
            int originalIndex = startIndex + i;
            if (originalIndex >= 0 && originalIndex < originalLine.positionCount) // Safety: avoids invalid indices
            {
                Vector3 pos = originalLine.GetPosition(originalIndex);
                segmentLine.SetPosition(i, pos);
            }
        }

        focusModeLineSegments.Add(new FocusLineSegment(segmentLine, originalLine, startIndex, endIndex, isHorizontal));
    }

    // FUNCTION: Synchronize the line segments created in focus mode with the original lines
    private void UpdateFocusModeSegments()
    {
        if (gridRebuildManager == null) // Safety: avoids this method taking place when there is no deformation
            return;

        foreach (FocusLineSegment segment in focusModeLineSegments)
        {
            if (segment.segmentRenderer == null || segment.sourceRenderer == null) // Safety: ensure that the segment's renderer and the source renderer exist
                continue;

            int segmentLength = segment.endIndex - segment.startIndex + 1;

            for (int i = 0; i < segmentLength; i++) // Iteration over each segmentLength position
            {
                int sourceIndex = segment.startIndex + i; // i: 0 to segmentLength-1
                if (sourceIndex >= 0 && sourceIndex < segment.sourceRenderer.positionCount) // Safety: ensuring the method only accesses valid positions
                {
                    Vector3 currentPos = segment.sourceRenderer.GetPosition(sourceIndex); // Extract position
                    segment.segmentRenderer.SetPosition(i, currentPos); //Copy position to the segment
                }
            }
        }
    }

    // HELPER FUNCTION: Restoring all lines of the original Amsler Grid
    private void RestoreAllLines()
    {
        if (gridRebuildManager == null) // Safety: ensure that there exists the line-restoring components, if not, exit
            return;

        foreach (FocusLineSegment segment in focusModeLineSegments) // Iterate over each focus mode line segment
        {
            if (segment.segmentRenderer != null && segment.segmentRenderer.gameObject != null) // For each focus-line segment, destroy it
            {
                Destroy(segment.segmentRenderer.gameObject);
            }
        }
        focusModeLineSegments.Clear();

        foreach (LineRenderer lr in gridRebuildManager.horizontalLinePool) // Iterate over ALL horizontal lines
        {
            if (lr != null) // Make visible again all horizontal lines
                lr.enabled = true;
        }

        foreach (LineRenderer lr in gridRebuildManager.verticalLinePool) // Iterate over ALL vertical lines
        {
            if (lr != null) // Make visible again all vertical lines
                lr.enabled = true;
        }

        if (centerFixationPoint != null) // If center fixation point exists, enable the visualization (control measure)
        {
            Renderer renderer = centerFixationPoint.GetComponent<Renderer>();
            if (renderer != null) renderer.enabled = true;
        }
    }

    // HELPER FUNCTION: Hides all probe dots except the selected one
    private void HideAllProbesExcept(int selectedIndex)
    {
        if (probeDots == null || probeDots.probes == null) // Safety: exit in case there is no existing probe dots
            return;

        originalProbeVisibility.Clear(); // Eliminate the visibility of the probe dots

        for (int i = 0; i < probeDots.probes.Count; i++) // Iterate over every probe dot in the system
        {
            GameObject probe = probeDots.probes[i]; // Retrieving of the probe dot of a specific index

            if (probe == null) // Safety: ensures avoiding null probe dots

                continue;

            originalProbeVisibility[probe] = probe.activeSelf; // Establish the selected probe dot as active

            if (i != selectedIndex) // Defines all other probe dots as inactive
            {
                probe.SetActive(false);
            }
        }
    }

    // HELPER FUNCTION: Restore the visibility of all probes
    private void RestoreProbeVisibility()
    {
        foreach (var kvp in originalProbeVisibility) // Iterates over every probe in the original probe set
        {
            if (kvp.Key != null) // Defines all probe dots as active for restoring their visibility
            {
                kvp.Key.SetActive(kvp.Value);
            }
        }
        originalProbeVisibility.Clear(); // Resets dictionary
    }

    // HELPER FUNCTION: Obtaining of the grid index of a specific probe dot
    private Vector2Int GetProbeGridIndex(GameObject probe)
    {
        if (gridRebuildManager == null) // Safety: exit in case of null deformation manager
        {
            return Vector2Int.zero;
        }
        return gridRebuildManager.GetProbeGridCell(probe); // use the deformation manager to obtain the grid cell of the probe dot
    }
}