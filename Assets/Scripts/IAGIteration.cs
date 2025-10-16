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
    private Color probeDefaultColor = Color.grey;
    private Color probeSelectedColor = Color.green;
    private Color probeCompletedColor = Color.yellow;

    private float moveSpeed = 2f;

    private List<GameObject> iteration1Probes = new List<GameObject>();
    private List<GameObject> iterationcurrentProbes = new List<GameObject>();

    private int selectedProbeIndex = -1;
    private int completedProbeCount = 0;

    private GameObject selectedIteration1Probe = null;

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

        HandleSpaceKey();
    }

    private void StartIteration1()
    {
        currentIteration = 1;
        completedProbeCount = 0;
        isSelectingRegion = false;

        CreateIteration1Probes();
        iterationcurrentProbes = new List<GameObject>(iteration1Probes);
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
        currentIteration++;
        completedProbeCount = 0;
        selectedIteration1Probe = centerProbe;
        isSelectingRegion = false;

        CreateHigherIterationProbes(centerProbe.transform.position);
    }

    private void CreateHigherIterationProbes(Vector3 centerPosition)
    {
        ClearProbes(iterationcurrentProbes);
        iterationcurrentProbes.Clear();

        float regionSize = mainGrid.TotalGridWidth / 3f;
        float step = regionSize / 3f;

        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 3; x++)
            {
                float offsetX = (x - 1) * step; // -1, 0, 1 to center around probe
                float offsetY = (y - 1) * step;

                Vector3 probePos = centerPosition + new Vector3(offsetX, offsetY, 0);

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
            iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeDefaultColor;
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
            iterationcurrentProbes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeCompletedColor;
            completedProbeCount++;

            if (focusSystem != null)
            {
                focusSystem.ExitFocusMode();
            }

            selectedProbeIndex = -1;

            int expectedProbeCount = currentIteration == 1 ? iteration1_probecount : iterationhigher_probecount;

            if (completedProbeCount >= expectedProbeCount)
            {
                OnIterationComplete();
            }
        }
    }

    private void HandleSpaceKey()
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
    }

    private void OnIterationComplete()
    {
        if (currentIteration == 1)
        {
            EnterRegionSelectionMode();
        }

        else
        {
            EnterRegionSelectionMode();
        }
    }

    private void EnterRegionSelectionMode()
    {
        isSelectingRegion = true;

        foreach (var probe in iteration1Probes)
        {
            probe.GetComponent<Renderer>().material.color = Color.cyan;
            probe.SetActive(true);
        }

        foreach (var probe in iterationcurrentProbes)
        {
            probe.SetActive(false);
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
                for (int i = 0; i < iteration1Probes.Count; i++)
                {
                    if (hit.collider.gameObject == iteration1Probes[i])
                    {
                        OnRegionSelected(iteration1Probes[i]);
                        break;
                    }
                }
            }
        }
    }

    private void OnRegionSelected(GameObject selectedProbe)
    {
        foreach (var probe in iteration1Probes)
        {
            if (probe != selectedProbe)
            {
                probe.SetActive(false);
            }
            else
            {
                probe.GetComponent<Renderer>().material.color = Color.magenta;
            }
        }

        StartHigherIteration(selectedProbe);
    }

    private void CancelRegionSelection()
    {
        isSelectingRegion = false;

        foreach (var probe in iteration1Probes)
        {
            probe.GetComponent<Renderer>().material.color = probeCompletedColor;
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
}
