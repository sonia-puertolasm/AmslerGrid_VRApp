using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IterationManager : MonoBehaviour
{
    private ProbeDots probeDots;
    private MainGrid mainGrid;
    private GridRebuildManager gridRebuildManager;
    private FocusSystem focusSystem;

    private int currentIteration = 1;
    private int currentParentProbeIndex = -1;

    private Dictionary<int, List<GameObject>> iterationProbes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, GameObject> iterationFixationPoints = new Dictionary<int, GameObject>();

    private Dictionary<int, List<GameObject>> parentProbeToIteration2Probes = new Dictionary<int, List<GameObject>>();
    private Dictionary<int, Dictionary<GameObject, Vector3>> parentProbeToIteration2Positions = new Dictionary<int, Dictionary<GameObject, Vector3>>();

    private Vector3 gridCenter;

    void Start()
    {
        probeDots = FindObjectOfType<ProbeDots>();
        mainGrid = FindObjectOfType<MainGrid>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();
        focusSystem = FindObjectOfType<FocusSystem>();

        if (mainGrid != null)
        {
            gridCenter = mainGrid.GridCenterPosition;
        }

        StartCoroutine(InitializeIterationSystem());
    }

    private IEnumerator InitializeIterationSystem()
    {
        yield return new WaitForSeconds(0.2f);

        if (probeDots != null && probeDots.probes != null && probeDots.probes.Count > 0)
        {
            iterationProbes[1] = new List<GameObject>(probeDots.probes);

            GameObject centerFixation = GameObject.Find("CenterFixationPoint");
            if (centerFixation != null)
            {
                iterationFixationPoints[1] = centerFixation;
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            HandleEnterKey();
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            HandleBackspaceKey();
        }
    }

    private void HandleEnterKey()
    {
        if (currentIteration == 1 && probeDots != null && probeDots.selectedProbeIndex >= 0)
        {
            int selectedIndex = probeDots.selectedProbeIndex;

            if (parentProbeToIteration2Probes.ContainsKey(selectedIndex) && 
                parentProbeToIteration2Probes[selectedIndex].Count > 0)
            {
                ReturnToIteration2(selectedIndex);
            }
            else
            {
                AdvanceToIteration2(selectedIndex);
            }
        }
    }

    private void ReturnToIteration2(int parentProbeIndex)
    {
        HideIteration1Probes();

        if (focusSystem != null && focusSystem.focusSystemEnabled)
        {
            focusSystem.ExitFocusMode();
        }

        GameObject parentProbe = iterationProbes[1][parentProbeIndex];
        if (parentProbe == null || gridRebuildManager == null)
        {
            return;
        }

        if (!gridRebuildManager.probeGridIndices.ContainsKey(parentProbe))
        {
            return;
        }

        Vector2Int parentGridIndex = gridRebuildManager.probeGridIndices[parentProbe];
        int parentRow = parentGridIndex.y;
        int parentCol = parentGridIndex.x;

        List<GameObject> iteration2Probes = parentProbeToIteration2Probes[parentProbeIndex];
        Dictionary<GameObject, Vector3> iteration2Positions = parentProbeToIteration2Positions[parentProbeIndex];

        foreach (GameObject probe in iteration2Probes)
        {
            if (probe != null)
            {
                if (gridRebuildManager.probeGridIndices.ContainsKey(probe))
                {
                    Vector2Int probeGridIndex = gridRebuildManager.probeGridIndices[probe];
                    int probeRow = probeGridIndex.y;
                    int probeCol = probeGridIndex.x;

                    Vector3 deformedPosition = gridRebuildManager.GetDeformedGridPoint(probeRow, probeCol);
                    deformedPosition.z = gridCenter.z - 0.15f;

                    probe.transform.position = deformedPosition;
                }

                probe.SetActive(true);
                Renderer renderer = probe.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        probeDots.probes = new List<GameObject>(iteration2Probes);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>(iteration2Positions);
        probeDots.selectedProbeIndex = -1;

        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;
    }

    private void HandleBackspaceKey()
    {
        if (currentIteration > 1 && probeDots != null && probeDots.selectedProbeIndex == -1)
        {
            if (currentIteration == 2)
            {
                ReturnToIteration1();
            }
        }
    }

    private void AdvanceToIteration2(int parentProbeIndex)
    {
        if (parentProbeIndex < 0 || parentProbeIndex >= iterationProbes[1].Count)
        {
            return;
        }

        GameObject selectedProbe = iterationProbes[1][parentProbeIndex];
        if (selectedProbe == null)
        {
            return;
        }

        Vector3 newCenterPosition = selectedProbe.transform.position;

        HideIteration1Probes();

        if (focusSystem != null && focusSystem.focusSystemEnabled)
        {
            focusSystem.ExitFocusMode();
        }

        if (iterationFixationPoints.ContainsKey(1))
        {
            GameObject fixation = iterationFixationPoints[1];
            if (fixation != null)
            {
                fixation.SetActive(true);
                Renderer renderer = fixation.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        SpawnIteration2Probes(newCenterPosition, parentProbeIndex);

        currentIteration = 2;
        currentParentProbeIndex = parentProbeIndex;

        probeDots.selectedProbeIndex = -1;
    }

    private void SpawnIteration2Probes(Vector3 centerPosition, int parentProbeIndex)
    {
        List<GameObject> newIteration2Probes = new List<GameObject>();
        Dictionary<GameObject, Vector3> newIteration2Positions = new Dictionary<GameObject, Vector3>();

        if (gridRebuildManager == null)
        {
            return;
        }

        GameObject selectedProbe = iterationProbes[1][parentProbeIndex];
        if (selectedProbe == null)
        {
            return;
        }

        if (!gridRebuildManager.probeGridIndices.ContainsKey(selectedProbe))
        {
            return;
        }

        Vector2Int parentGridIndex = gridRebuildManager.probeGridIndices[selectedProbe];
        int parentRow = parentGridIndex.y;
        int parentCol = parentGridIndex.x;

        int gridSizeValue = gridRebuildManager.GetGridSize();

        Vector2Int[] neighborOffsets = new Vector2Int[]
        {
            new Vector2Int(-1, -1),
            new Vector2Int(0, -1),  
            new Vector2Int(1, -1),  
            new Vector2Int(-1, 0),  
            new Vector2Int(1, 0),   
            new Vector2Int(-1, 1),  
            new Vector2Int(0, 1),   
            new Vector2Int(1, 1)   
        };

        int probeCounter = 0;

        foreach (Vector2Int offset in neighborOffsets)
        {
            int newRow = parentRow + offset.y;
            int newCol = parentCol + offset.x;

            if (newRow <= 0 || newRow >= gridSizeValue || 
                newCol <= 0 || newCol >= gridSizeValue)
            {
                continue;
            }

            Vector3 gridPosition = gridRebuildManager.GetDeformedGridPoint(newRow, newCol);
            
            gridPosition.z = gridCenter.z - 0.15f;

            GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            probe.name = $"ProbePoint_Parent{parentProbeIndex}_Iteration2_{probeCounter}";
            probe.transform.position = gridPosition;
            probe.transform.localScale = Vector3.one * probeDots.probeDotSize;
            probe.transform.SetParent(transform);

            Renderer renderer = probe.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = true;
                renderer.material.color = ProbeColors.Default;
            }

            GridPointData pointData = probe.AddComponent<GridPointData>();
            pointData.isInteractable = true;

            Vector3 originalPosition = gridRebuildManager.GetOriginalGridPoint(newRow, newCol);
            originalPosition.z = gridCenter.z - 0.15f;
            
            newIteration2Positions[probe] = originalPosition;
            newIteration2Probes.Add(probe);

            Vector2Int gridIndex = new Vector2Int(newCol, newRow);
            gridRebuildManager.RegisterProbe(probe, originalPosition, 2, gridIndex);

            probeCounter++;
        }

        parentProbeToIteration2Probes[parentProbeIndex] = newIteration2Probes;
        parentProbeToIteration2Positions[parentProbeIndex] = newIteration2Positions;

        probeDots.probes = new List<GameObject>(newIteration2Probes);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>(newIteration2Positions);
        probeDots.selectedProbeIndex = -1;
    }

    private void HideIteration1Probes()
    {
        if (iterationProbes.ContainsKey(1))
        {
            foreach (GameObject probe in iterationProbes[1])
            {
                if (probe != null)
                {
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }

                    probe.SetActive(false);
                }
            }
        }
    }

    private void HideAllIteration2Probes()
    {
        foreach (var kvp in parentProbeToIteration2Probes)
        {
            foreach (GameObject probe in kvp.Value)
            {
                if (probe != null)
                {
                    probe.SetActive(false);
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = false;
                    }
                }
            }
        }
    }

    private void ReturnToIteration1()
    {
        HideAllIteration2Probes();

        if (iterationProbes.ContainsKey(1))
        {
            foreach (GameObject probe in iterationProbes[1])
            {
                if (probe != null)
                {
                    probe.SetActive(true);
                    Renderer renderer = probe.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        renderer.enabled = true;
                    }
                }
            }
        }

        if (iterationFixationPoints.ContainsKey(1))
        {
            GameObject fixation = iterationFixationPoints[1];
            if (fixation != null)
            {
                fixation.SetActive(true);
                Renderer renderer = fixation.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }
        }

        probeDots.probes = new List<GameObject>(iterationProbes[1]);
        probeDots.probeInitialPositions = new Dictionary<GameObject, Vector3>();

        foreach (GameObject probe in iterationProbes[1])
        {
            if (probe != null && gridRebuildManager != null)
            {
                if (gridRebuildManager.probeOriginalPositions.ContainsKey(probe))
                {
                    probeDots.probeInitialPositions[probe] = gridRebuildManager.probeOriginalPositions[probe];
                }
            }
        }

        probeDots.selectedProbeIndex = -1;

        currentIteration = 1;
        currentParentProbeIndex = -1;
    }

    public int CurrentIteration => currentIteration;
    public bool IsInIteration2 => currentIteration == 2;
    public int CurrentParentProbeIndex => currentParentProbeIndex;

    public bool HasIteration2ForProbe(int probeIndex)
    {
        return parentProbeToIteration2Probes.ContainsKey(probeIndex) && 
               parentProbeToIteration2Probes[probeIndex].Count > 0;
    }
}