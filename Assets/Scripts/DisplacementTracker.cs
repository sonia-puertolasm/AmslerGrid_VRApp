using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplacementTracker : MonoBehaviour
{
    // Definition of references to main grid
    private ProbeDots probeDots;
    private MainGrid mainGrid;
    private GridRebuildManager gridRebuildManager;

    // Definition of empty list for displacement data
    private List<IterationDisplacementData> iterationHistory = new List<IterationDisplacementData>();

    private bool isInitialized = false;

    // Definition of iteration displacement data
    public class IterationDisplacementData
    {
        public int iteration; // Define IT number
        public Dictionary<int, ProbeDisplacement> probeDisplacements; // Definition of empty dictionary for storage of probe displacements
    }

    // Definition of probe displacement
    public class ProbeDisplacement 
    {
        public int probeIndex;
        public Vector3 originalPosition;
        public Vector3 currentPosition;
        public Vector2 displacementVector2D;
        public Vector3 displacementVector3D;
        public float displacementMagnitude;

        public ProbeDisplacement(int index, Vector3 origPos, Vector3 currPos)
        {
            probeIndex = index;
            originalPosition = origPos;
            currentPosition = currPos;

            displacementVector3D = currPos - origPos;
            displacementVector2D = new Vector2(displacementVector3D.x, displacementVector3D.y);
            displacementMagnitude = displacementVector3D.magnitude;
        }
    }

    // Initialization of relevant functions
    private void Start()
    {
        StartCoroutine(InitializeTracker());
    }

    // Coroutine to initialize tracking of displacement until after grid is generated  -> allows method to pause and resume
    private IEnumerator InitializeTracker()
    {
        yield return new WaitForEndOfFrame();

        // Retrieve GO elements from other scripts
        probeDots = FindObjectOfType<ProbeDots>();
        mainGrid = FindObjectOfType<MainGrid>();
        gridRebuildManager = FindObjectOfType<GridRebuildManager>();

        if (probeDots == null || probeDots.probes == null || probeDots.probes.Count == 0) // Safety: avoid action in case probe dots are not existing
        {
            yield break;
        }

        if (mainGrid == null) // Safety: avoid action in case grid is not existing
        {
            yield break;
        }

        isInitialized = true;
    }

    // FUNCTION: Retrieve displacement of a probe based on its index
    public ProbeDisplacement GetProbeDisplacement(int probeIndex)
    {
        if (!isInitialized || probeDots == null) // Safety: in case the process is not initialised or there is no probe dots, exit
        {
            return null;
        }

        if (probeIndex < 0 || probeIndex >= probeDots.probes.Count) // Safety: avoids "weird" indexing and counts of probe dots
        {
            return null;
        }

        GameObject probe = probeDots.probes[probeIndex]; // Retrieve specific probe dot from dictionary

        if (!probeDots.probeInitialPositions.ContainsKey(probe)) // Safety: check if the probe dot is contained in the dictionary, if not, exit
        {
            return null;
        }

        Vector3 originalPos = probeDots.probeInitialPositions[probe]; // Obtain probe dot initial position
        Vector3 currentPos = probe.transform.position; // Obtain current location of probe dot

        return new ProbeDisplacement(probeIndex, originalPos, currentPos);
    }

    // FUNCTION: Alternative method ~ Retrieve displacement of a probe based given GO -> calls the first method to calculate the displacement
    public ProbeDisplacement GetProbeDisplacement(GameObject probe)
    {
        if (!isInitialized || probeDots == null) // Safety: in case the process is not initialised or there is no probe dots, exit
        {
            return null;
        }

        int index = probeDots.probes.IndexOf(probe); // Extract index of selected probe

        if (index < 0) // Safety: exits in case the index is non-existing
        {
            return null;
        }

        return GetProbeDisplacement(index); // Executes the obtaining of the displacement of the selected probe
    }

    // FUNCTION: Calculates and returns the displacement data for all probes being tracked
    public Dictionary<int, ProbeDisplacement> GetAllDisplacements()
    {
        if (!isInitialized || probeDots == null) // Safety: in case the process is not initialised or there is no probe dots, return empty dictionary
        {
            return new Dictionary<int, ProbeDisplacement>(); 
        }

        Dictionary<int, ProbeDisplacement> allDisplacements = new Dictionary<int, ProbeDisplacement>(); // Creates new dictionary with all displacements of mutiple probes

        for (int i = 0; i < probeDots.probes.Count; i++) // Iterates over probe count
        {
            ProbeDisplacement displacement = GetProbeDisplacement(i); // Obtaining of displacement of every probe -> if result = null, displacement cannot be calculated

            if (displacement != null) // In case of displacement having been calculated
            {
                allDisplacements[i] = displacement; // Storage of displacement for specific index
            }
        }

        return allDisplacements;
    }

    // HELPER FUNCTION: Determines whether a probe dot has moved (or not)
    public bool HasProbeMoved(int probeIndex, float threshold = 0.01f)
    {
        ProbeDisplacement displacement = GetProbeDisplacement(probeIndex); // Obtain displacement for specific probe dot
        if (displacement == null) return false; // Safety: exit in case of null displacement

        return displacement.displacementMagnitude > threshold; 
    }

    // HELPER FUNCTION: Calculates overall displacement of a probe dot
    public float GetTotalDisplacement()
    {
        if (!isInitialized) return 0f; // Safety: in case of non-initialization, return empty value

        float total = 0f;
        foreach (var displacement in GetAllDisplacements().Values)
        {
            total += displacement.displacementMagnitude;
        }

        return total;
    }

    // HELPER FUNCTION: Get average displacement
    public float GetAverageDisplacement()
    {
        if (!isInitialized || probeDots == null || probeDots.probes.Count == 0) // Safety: in case of non-initialization/non-existance of probe dots, return empty value
            return 0f;

        return GetTotalDisplacement() / probeDots.probes.Count;
    }

    // FUNCTION: Capture and store the current state of all probe displacements at a specific iteration
    public void SaveIterationSnapshot(int iteration)
    {
        if (!isInitialized)
        {
            return;
        }

        IterationDisplacementData snapshot = new IterationDisplacementData(iteration);

        // Capture all current displacements for all probes
        for (int i = 0; i < probeDots.probes.Count; i++)
        {
            ProbeDisplacement displacement = GetProbeDisplacement(i);
            if (displacement != null)
            {
                snapshot.probeDisplacements[i] = displacement;
            }
        }

        iterationHistory.Add(snapshot);
    }

    // HELPER FUNCTION: 
    public IterationDisplacementData GetIteration(int iteration)
    {
        return iterationHistory.Find(iter => iter.iteration == iteration);
    }

    // HELPER FUNCTION:
    public List<IterationDisplacementData> GetAllIterations()
    {
        return new List<IterationDisplacementData>(iterationHistory);
    }

    // HELPER FUNCTION: 
    public IterationDisplacementData GetLatestIteration()
    {
        if (iterationHistory.Count == 0) return null;
        return iterationHistory[iterationHistory.Count - 1];
    }

    // HELPER FUNCTION: 
    public void ClearIterationHistory()
    {
        iterationHistory.Clear();
    }

    // HELPER FUNCTION: 
    public int GetIterationCount()
    {
        return iterationHistory.Count;
    }

    // HELPER FUNCTION: 
    public void ResetTracker()
    {
        ClearIterationHistory();
    }


    public bool IsInitialized => isInitialized;
    public int ProbeCount => probeDots != null ? probeDots.probes.Count : 0;
}
