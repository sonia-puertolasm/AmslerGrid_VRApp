using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IAGIteration : MonoBehaviour
{
    private MainGrid mainGrid;
    private IAGFocusSystem focusSystem;

    private int currentIteration = 1;
    private const int iteration1_probecount = 8;
    private const int iterationhigher_probecount = 9;

    private float probeDotSize = 0.2f;
    private Color probeDefaultColor = Color.black;
    private Color probeSelectedColor = Color.yellow;
    private Color probeIt1Color = Color.blue;
    private Color probeHigherItColor = Color.green;

    private float moveSpeed = 2f;

    private List<GameObject> iteration1Probes = new List<GameObject>();
    private List<GameObject> iterationcurrentProbes = new List<GameObject>();

    private class IterationState
    {
        public int iteration;
        public List<GameObject> probes;
        public GameObject selectedProbe;
        public HashSet<int> completedIndices;
        public int completedCount;
    }

    private Stack<IterationState> iterationHistory = new Stack<IterationState>();

    private HashSet<int> completedProbeIndices = new HashSet<int>();

    private int selectedProbeIndex = -1;
    private int completedProbeCount = 0;

    private GameObject selectedIteration1Probe = null;
    private GameObject currentCenterProbe = null;

    private bool isSelectingRegion = false;

    void Start()
    {
        mainGrid = FindObjectOfType<MainGrid>();
        if (mainGrid == null)
        {
            return;
        }

        focusSystem = FindObjectOfType<IAGFocusSystem>();
        if (focusSystem == null)
        {
            GameObject focusManagerObj = new GameObject("FocusManager");
            focusSystem = focusManagerObj.AddComponent<IAGFocusSystem>();
        }

        SetupFocusSystem();
        StartIteration1();
    }

    private void SetupFocusSystem()
    {
        if (focusSystem != null && mainGrid != null)
        {
            focusSystem.SetGridParent(mainGrid.transform);
            focusSystem.SetProbeParent(this.transform);
        }
    }

    void Update()
    {
        if (isSelectingRegion)
        {
            HandleRegionSelection();
        }
        else
        {
            HandleProbeSelection();
            HandleProbeMovement();
        }

        HandleKeys();
    }

    private void StartIteration1()
    {
        currentIteration = 1;
        completedProbeCount = 0;
        completedProbeIndices.Clear();
        isSelectingRegion = false;
        selectedProbeIndex = -1;

        CreateIteration1Probes();
        iterationcurrentProbes = iteration1Probes;
    }

    private void CreateIteration1Probes()
    {
        ClearProbes(iteration1Probes);
        iteration1Probes.Clear();

        float cellSize = mainGrid.CellSize;
        int gridSize = mainGrid.GridSize;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        int spacing = gridSize / 3;

        List<Vector2Int> probePositions = new List<Vector2Int>
        {
            new Vector2Int(spacing,spacing),
            new Vector2Int(spacing * 2,spacing),
            new Vector2Int(spacing * 3,spacing),
            new Vector2Int(spacing,spacing * 2),
            new Vector2Int(spacing * 3,spacing * 2),
            new Vector2Int(spacing,spacing * 3),
            new Vector2Int(spacing * 2,spacing * 3),
            new Vector2Int(spacing * 3,spacing * 3)
        };

        float totalGridWidth = mainGrid.TotalGridWidth;

        foreach (var pos in probePositions)
        {
            float worldX = (pos.x * cellSize - totalGridWidth / 2f) + gridCenter.x;
            float worldY = (pos.y * cellSize - totalGridWidth / 2f) + gridCenter.y;

            GameObject probe = CreateProbe(worldX, worldY, gridCenter.z - 0.15f, iteration1Probes.Count);
            iteration1Probes.Add(probe);
        }
    }

    private void StartHigherIteration(GameObject centerProbe)
    {
        IterationState currentState = new IterationState
        {
            iteration = currentIteration,
            probes = new List<GameObject>(iterationcurrentProbes),
            selectedProbe = centerProbe,
            completedIndices = new HashSet<int>(completedProbeIndices),
            completedCount = completedProbeCount
        };
        iterationHistory.Push(currentState);
        
        currentIteration++;
        completedProbeCount = 0;
        completedProbeIndices.Clear();
        isSelectingRegion = false;
        selectedProbeIndex = -1;

        if (currentIteration == 2)
        {
            selectedIteration1Probe = centerProbe;
        }
            

        foreach (var probe in iterationcurrentProbes)
        {
            if (probe == centerProbe)
            {
                probe.GetComponent<Renderer>().material.color = Color.black;
                probe.SetActive(true);
            }
            else
            {
                probe.SetActive(false);
            }
        }

        currentCenterProbe = centerProbe;
       
        CreateHigherIterationProbes(centerProbe.transform.position);
    }

    private void CreateHigherIterationProbes(Vector3 centerPosition)
    {
        iterationcurrentProbes = new List<GameObject>();
        
        float regionSize = mainGrid.TotalGridWidth / Mathf.Pow(3f, currentIteration - 1);
        float step = regionSize / 3f;

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                float offsetX = (x - 1) * step;
                float offsetY = (y - 1) * step;

                Vector3 probePos = centerPosition + new Vector3(offsetX, offsetY, 0);

                int probeIndex = y * 3 + x;
                if (probeIndex == 4)
                {
                    continue;
                }

                GameObject probe = CreateProbe(probePos.x, probePos.y, probePos.z, iterationcurrentProbes.Count);
                iterationcurrentProbes.Add(probe);
            }
        }
    }

    private GameObject CreateProbe(float x, float y, float z, int index)
    {
        GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        probe.name = $"Probe_Iter{currentIteration}_#{index}";
        probe.transform.SetParent(transform);
        probe.transform.localScale = Vector3.one * probeDotSize;
        probe.transform.position = new Vector3(x, y, z);
        probe.GetComponent<Renderer>().material.color = probeDefaultColor;

        return probe;
    }

    private void HandleProbeSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (currentCenterProbe != null && hit.collider.gameObject == currentCenterProbe)
                {
                    GoForward1Iteration();
                    return;
                }

                for (int i = 0; i < iterationcurrentProbes.Count; i++)
                {
                    if (hit.collider.gameObject == iterationcurrentProbes[i])
                    {
                        SelectProbe(i);
                        break;
                    }
                }
            }
        }
    }

    private void SelectProbe(int index)
    {
        if (index >= iterationcurrentProbes.Count) return;

        if (selectedProbeIndex >= 0 && selectedProbeIndex < iterationcurrentProbes.Count)
        {
            Renderer prevRenderer = iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>();
            Color completedColor = currentIteration == 1 ? probeIt1Color : probeHigherItColor;

            if (completedProbeIndices.Contains(selectedProbeIndex))
            {
                prevRenderer.material.color = completedColor;
            }
            else
            {
                prevRenderer.material.color = probeDefaultColor;
            }
        }

        selectedProbeIndex = index;

        iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeSelectedColor;

        if (focusSystem != null)
        {
            focusSystem.EnterFocusMode(iterationcurrentProbes[selectedProbeIndex]);
        }
    }

    private void HandleProbeMovement()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < iterationcurrentProbes.Count)
        {
            GameObject selectedProbe = iterationcurrentProbes[selectedProbeIndex];
            float speed = moveSpeed * Time.deltaTime;
            Vector3 previousPosition = selectedProbe.transform.position;

            if (Input.GetKey(KeyCode.UpArrow))
                selectedProbe.transform.position += Vector3.up * speed;
            if (Input.GetKey(KeyCode.DownArrow))
                selectedProbe.transform.position += Vector3.down * speed;
            if (Input.GetKey(KeyCode.RightArrow))
                selectedProbe.transform.position += Vector3.right * speed;
            if (Input.GetKey(KeyCode.LeftArrow))
                selectedProbe.transform.position += Vector3.left * speed;

            if (focusSystem != null && focusSystem.IsFocused())
            {
                if (Vector3.Distance(previousPosition, selectedProbe.transform.position) > 0.001f)
                {
                    focusSystem.UpdateFocusPosition(selectedProbe.transform.position);
                }
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                CompleteCurrentProbe();
            }
        }
    }

    private void CompleteCurrentProbe()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < iterationcurrentProbes.Count)
        {
            GameObject currentProbe = iterationcurrentProbes[selectedProbeIndex];
            Renderer probeRenderer = currentProbe.GetComponent<Renderer>();

            Color completedColor = currentIteration == 1 ? probeIt1Color : probeHigherItColor;

            bool wasAlreadyCompleted = completedProbeIndices.Contains(selectedProbeIndex);

            probeRenderer.material.color = completedColor;

            if (!wasAlreadyCompleted)
            {
                completedProbeIndices.Add(selectedProbeIndex);
                completedProbeCount++;
            }

            if (focusSystem != null)
            {
                focusSystem.ExitFocusMode();
            }

            selectedProbeIndex = -1;

            int expectedProbeCount = currentIteration == 1 ? iteration1_probecount : iterationhigher_probecount;
        }
    }

    private void HandleKeys()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (focusSystem != null && focusSystem.IsFocused())
            {
                focusSystem.ExitFocusMode();
            }

            if (selectedProbeIndex >= 0)
            {
                iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeDefaultColor;
                selectedProbeIndex = -1;
            }

            if (isSelectingRegion)
            {
                CancelRegionSelection();
            }
        }

        if (Input.GetKeyDown(KeyCode.Return))
        {
            int expectedProbeCount = currentIteration == 1 ? iteration1_probecount : iterationhigher_probecount;

            if (completedProbeCount >= expectedProbeCount && !isSelectingRegion)
            {
                OnIterationComplete();
            }
        }

        if (Input.GetKeyDown(KeyCode.Backspace))
        {
            if (currentIteration > 1 && !isSelectingRegion)
            {
                GoBack1Iteration();
            }
            else
            {
                return;
            }
        }
    }

    private void OnIterationComplete()
    {
        EnterRegionSelectionMode();
    }

    private void EnterRegionSelectionMode()
    {
        isSelectingRegion = true;

        if (currentIteration == 1)
        {
            foreach (var probe in iteration1Probes)
            {
                probe.GetComponent<Renderer>().material.color = Color.cyan;
                probe.SetActive(true);
            }
        }

        else
        {
            foreach (var probe in iterationcurrentProbes)
            {
                probe.GetComponent<Renderer>().material.color = Color.cyan;
                probe.SetActive(true);
            }

            if (selectedIteration1Probe != null)
            {
                selectedIteration1Probe.SetActive(true);
            }
        }
    }

    private void HandleRegionSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                if (currentIteration == 1)
                {
                    for (int i = 0; i < iteration1Probes.Count; i++)
                    {
                        if (hit.collider.gameObject == iteration1Probes[i])
                        {
                            OnRegionSelected(iteration1Probes[i]);
                            return;
                        }
                    }
                }

                else
                {
                    for (int i = 0; i < iterationcurrentProbes.Count; i++)
                    {
                        if (hit.collider.gameObject == iterationcurrentProbes[i])
                        {
                            OnRegionSelected(iterationcurrentProbes[i]);
                            return;
                        }
                    }
                }
            }
        }
    }

    private void OnRegionSelected(GameObject selectedProbe)
    {
        StartHigherIteration(selectedProbe);
    }

    private void CancelRegionSelection()
    {
        isSelectingRegion = false;

        Color completedColor = currentIteration == 1 ? probeIt1Color : probeHigherItColor;

        if (currentIteration == 1)
        {
            foreach (var probe in iteration1Probes)
            {
                probe.GetComponent<Renderer>().material.color = completedColor;
                probe.SetActive(true);
            }
        }

        else
        {
            foreach (var probe in iteration1Probes)
            {
                probe.SetActive(false);
            }

            foreach (var probe in iterationcurrentProbes)
            {
                probe.SetActive(true);
            }
        }
    }

    private void ClearProbes(List<GameObject> probeList)
    {
        foreach (var probe in probeList)
        {
            if (probe != null)
            {
                Destroy(probe);
            }
        }
    }

    private void GoBack1Iteration()
    {
        if (iterationHistory.Count == 0)
        {
            return;
        }

        foreach (var probe in iterationcurrentProbes)
        {
            probe.SetActive(false);
        }

        IterationState previousState = iterationHistory.Pop();

        currentIteration = previousState.iteration;
        iterationcurrentProbes = previousState.probes;
        completedProbeIndices = previousState.completedIndices;
        completedProbeCount = previousState.completedCount;
        currentCenterProbe = previousState.selectedProbe;
        selectedProbeIndex = -1;
        isSelectingRegion = false;

        foreach (var probe in iterationcurrentProbes)
        {
            probe.SetActive(true);
        }

        if (currentCenterProbe != null)
        {
            currentCenterProbe.SetActive(true);
            currentCenterProbe.GetComponent<Renderer>().material.color = Color.magenta;
        }
        
    }

    private void GoForward1Iteration()
    {
        EnterRegionSelectionMode();
    }
}
