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

        public IterationDisplacementData(int iterationNumber) // Initialize the IterationDisplacementData object
        {
            iteration = iterationNumber; // Set-up iteration number
            probeDisplacements = new Dictionary<int, ProbeDisplacement>(); // Generation of an empty dictionary
        }
    }

    // Definition of probe displacement
    public class ProbeDisplacement
    {
        // Definition of fields that will describe the probe state and movements
        public int probeIndex;
        public Vector3 originalPosition; // Original position of the probe dot in 3D space
        public Vector3 currentPosition; // Current position of the probe dot in 3D space
        public Vector2 displacementVector2D;
        public Vector3 displacementVector3D;
        public float displacementMagnitude;

        public ProbeDisplacement(int index, Vector3 origPos, Vector3 currPos)
        {
            probeIndex = index; // Probe's index
            originalPosition = origPos; // Original position of the probe dot in 3D space
            currentPosition = currPos; // Current position of the probe dot in 3D space

            displacementVector3D = currPos - origPos; // Calculation of the displacement vector in 3D space
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

    // FUNCTION: Capture and store the current state of all probe displacements at a specific iteration
    public void SaveIterationSnapshot(int iteration)
    {
        if (!isInitialized) // Safety: exit in case there has been no initialization of the process
        {
            return;
        }

        IterationDisplacementData snapshot = new IterationDisplacementData(iteration);

        // Capture all current displacements for all probes
        for (int i = 0; i < probeDots.probes.Count; i++)
        {
            ProbeDisplacement displacement = GetProbeDisplacement(i);
            if (displacement != null) // In case there is displacement, incorporate in the snapchot according to the probe dot index
            {
                snapshot.probeDisplacements[i] = displacement;
            }
        }

        iterationHistory.Add(snapshot);
    }

    // HELPER FUNCTION: Retrieve a stored snapchot matching a specific iteration number
    public IterationDisplacementData GetIteration(int iteration)
    {
        return iterationHistory.Find(iter => iter.iteration == iteration);
    }

    // HELPER FUNCTION: Retrieves a copy of the entire iteration history
    public List<IterationDisplacementData> GetAllIterations()
    {
        return new List<IterationDisplacementData>(iterationHistory);
    }

    // HELPER FUNCTION: Returns the most recent snapshot
    public IterationDisplacementData GetLatestIteration()
    {
        if (iterationHistory.Count == 0) return null; // Safety: don't return any snapshot in case they don't exist
        return iterationHistory[iterationHistory.Count - 1];
    }

    // HELPER FUNCTION: Clears iteration history
    public void ClearIterationHistory()
    {
        iterationHistory.Clear();
    }

    // HELPER FUNCTION: Obtain iteration count from the history of probe dots
    public int GetIterationCount()
    {
        return iterationHistory.Count;
    }

    // HELPER FUNCTION: Resets the tracker and iteration history
    public void ResetTracker()
    {
        ClearIterationHistory();
    }

    // Definition of variables for further use
    public bool IsInitialized => isInitialized; // Initialization status
    public int ProbeCount => probeDots != null ? probeDots.probes.Count : 0; // Returns how many probe dots is there as long as there is any number except 0
}