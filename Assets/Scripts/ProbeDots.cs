using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProbeDots : MonoBehaviour
{
    private float probeDotSize = 0.2f;
    private Color probeDefaultColor = Color.grey;
    private Color probeSelectedColor = Color.green;
    private Color probeCompletedColor = Color.yellow;
    private int probeSpacing = 2;

    private float moveSpeed = 2f;

    private MainGrid mainGrid;
    private IAGFocusSystem focusSystem;

    private List<GameObject> probes = new List<GameObject>();
    private int selectedProbeIndex = -1;

    void Start()
    {
        if (mainGrid == null)
        {
            mainGrid = FindObjectOfType<MainGrid>();
            if (mainGrid == null)
            {
                Debug.LogError("ProbeManager: No existing grid, cannot generate probe dots");
                return;
            }
        }

        if (focusSystem == null)
        {
            Debug.LogError("ProbeDots: IAGFocusSystem is not found in scene. Creating...");
            GameObject focusManagerObj = new GameObject("FocusManager");
            focusSystem = focusManagerObj.AddComponent<IAGFocusSystem>();
        }

        CreateProbes();

        SetupFocusSystem();
    }

    private void SetupFocusSystem()
    {
        if (focusSystem != null && mainGrid != null)
        {
            focusSystem.SetGridParent(mainGrid.transform);
            focusSystem.SetProbeParent(this.transform);

            Debug.Log("Focus system references set up successfully");
        }
    }

    void Update()
    {
        ProbeSelection();
        ProbeMovement();
        HandleEscapeKey();
    }

    private void CreateProbes()
    {
        float cellSize = mainGrid.CellSize;
        int gridSize = mainGrid.GridSize;
        float totalGridWidth = mainGrid.TotalGridWidth;
        Vector3 gridCenter = mainGrid.GridCenterPosition;

        for (int x = probeSpacing; x <= gridSize - probeSpacing; x += probeSpacing)
        {
            for (int y = probeSpacing; y <= gridSize - probeSpacing; y += probeSpacing)
            {
                if (x == gridSize / 2 && y == gridSize / 2)
                    continue;

                float worldX = (x * cellSize - totalGridWidth / 2f) + gridCenter.x;
                float worldY = (y * cellSize - totalGridWidth / 2f) + gridCenter.y;

                GameObject probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                probe.name = "Probe" + probes.Count;
                probe.transform.SetParent(transform);
                probe.transform.localScale = Vector3.one * probeDotSize;
                probe.transform.position = new Vector3(worldX, worldY, gridCenter.z - 0.15f);

                probe.GetComponent<Renderer>().material.color = probeDefaultColor;

                probes.Add(probe);
            }
        }
    }

    private void ProbeSelection()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit))
            {
                for (int i = 0; i < probes.Count; i++)
                {
                    if (hit.collider.gameObject == probes[i])
                    {
                        SelectProbe(i);
                        break;
                    }
                }
            }
        }
    }

    private void ProbeMovement()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            GameObject selectedProbe = probes[selectedProbeIndex];
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
        }
    }

    private void HandleEscapeKey()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (focusSystem != null && focusSystem.IsFocused())
            {
                focusSystem.ExitFocusMode();
            }
            DeselectAllProbes();
        }
    }
    private void SelectProbe(int index)
    {
        if (index >= probes.Count) return;

        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            probes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeDefaultColor;
        }

        selectedProbeIndex = index;
        probes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeSelectedColor;

        if (focusSystem != null)
        {
            focusSystem.EnterFocusMode(probes[selectedProbeIndex]);
        }
    }

    private void DeselectAllProbes()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            probes[selectedProbeIndex].GetComponent<Renderer>().material.color = probeCompletedColor;
        }
        selectedProbeIndex = -1;
    }

    public List<GameObject> GetProbes()
    {
        return new List<GameObject>(probes);
    }

    public GameObject GetSelectedProbe()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
            return probes[selectedProbeIndex];
        return null;

    }
}