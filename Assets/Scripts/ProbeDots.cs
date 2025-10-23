using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProbeDots : MonoBehaviour
{
    private float probeDotSize = 0.2f;
    private int probeSpacing = 2;
    private float moveSpeed = 2f;

    private MainGrid mainGrid;
    private IAGFocusSystem focusSystem;
    private ProbeDotConstraints probeDotConstraints;

    private List<GameObject> probes = new List<GameObject>();
    private Dictionary<GameObject, Vector3> probeInitialPositions = new Dictionary<GameObject, Vector3>();
    private int selectedProbeIndex = -1;

    void Start()
    {
        if (mainGrid == null)
        {
            mainGrid = FindObjectOfType<MainGrid>();
            if (mainGrid == null)
            {
                return;
            }
        }

        if (focusSystem == null)
        {
            GameObject focusManagerObj = new GameObject("FocusManager");
            focusSystem = focusManagerObj.AddComponent<IAGFocusSystem>();
        }

        probeDotConstraints = FindObjectOfType<ProbeDotConstraints>();
        if (probeDotConstraints == null)
        {
            GameObject constraintsObj = new GameObject("ProbeDotConstraints");
            probeDotConstraints = constraintsObj.AddComponent<ProbeDotConstraints>();
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
                Vector3 initialPosition = new Vector3(worldX, worldY, gridCenter.z - 0.15f);
                probe.transform.position = initialPosition;

                probeInitialPositions[probe] = initialPosition;

                probe.GetComponent<Renderer>().material.color = ProbeColors.Default;

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
            Vector3 proposedPosition = selectedProbe.transform.position;

            if (Input.GetKey(KeyCode.UpArrow))
                proposedPosition += Vector3.up * speed;
            if (Input.GetKey(KeyCode.DownArrow))
                proposedPosition += Vector3.down * speed;
            if (Input.GetKey(KeyCode.RightArrow))
                proposedPosition += Vector3.right * speed;
            if (Input.GetKey(KeyCode.LeftArrow))
                proposedPosition += Vector3.left * speed;

            if (focusSystem != null && focusSystem.IsFocused() && probeDotConstraints != null)
            {
                Vector3 focusMinBounds, focusMaxBounds;
                focusSystem.GetFocusedRegionWorldBounds(out focusMinBounds, out focusMaxBounds);
                proposedPosition = probeDotConstraints.ApplyFocusAreaConstraints(proposedPosition, focusMinBounds, focusMaxBounds);
            }

            selectedProbe.transform.position = proposedPosition;
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
            probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Default;
        }

        selectedProbeIndex = index;
        probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Selected;

        if (focusSystem != null)
        {
            GameObject selectedProbe = probes[selectedProbeIndex];
            Vector3? initialPos = probeInitialPositions.ContainsKey(selectedProbe) ? probeInitialPositions[selectedProbe] : (Vector3?)null;
            focusSystem.EnterFocusMode(selectedProbe, initialPos);
        }
    }

    private void DeselectAllProbes()
    {
        if (selectedProbeIndex >= 0 && selectedProbeIndex < probes.Count)
        {
            probes[selectedProbeIndex].GetComponent<Renderer>().material.color = ProbeColors.Completed;
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