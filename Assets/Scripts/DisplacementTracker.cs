using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DisplacementTracker : MonoBehaviour
{
    private Dictionary<GameObject, Vector3> originalPositions = new Dictionary<GameObject, Vector3>();
    private Dictionary<GameObject, Vector2> currentDisplacements = new Dictionary<GameObject, Vector2>();
    private Dictionary<int, IterationDisplacementData> iterationData = new Dictionary<int, IterationDisplacementData>();

    public class IterationDisplacementData
    {
        public int iteration;
        public Dictionary<int, ProbeDisplacement> probeDisplacements;

        public IterationDisplacementData(int iter)
        {
            iteration = iter;
            probeDisplacements = new Dictionary<int, ProbeDisplacement>();
        }
    }

    public class ProbeDisplacement
    {
        public int probeIndex;
        public Vector3 originalPosition;
        public Vector3 currentPosition;
        public Vector2 displacementVector;

        public ProbeDisplacement(int index, Vector3 origPos, Vector3 currPos)
        {
            probeIndex = index;
            originalPosition = origPos;
            currentPosition = currPos;
            displacementVector = new Vector2(currPos.x - origPos.x, currPos.y - origPos.y);
        }
    }

    public void RegisterProbe(GameObject probe, Vector3 originalPosition)
    {
        if (!originalPositions.ContainsKey(probe))
        {
            originalPositions[probe] = originalPosition;
            currentDisplacements[probe] = Vector3.zero;
        }
    }

    public void UpdateDisplacement(GameObject probe)
    {
        if (!originalPositions.ContainsKey(probe))
        {
            return;
        }

        Vector3 original = originalPositions[probe];
        Vector3 current = probe.transform.position;

        currentDisplacements[probe] = new Vector2(current.x - original.x, current.y - original.y);
    }

    public void SaveIterationData(int iteration, List<GameObject> probes)
    {
        IterationDisplacementData iterData = new IterationDisplacementData(iteration);

        for (int i = 0; i < probes.Count; i++)
        {
            GameObject probe = probes[i];
            if (originalPositions.ContainsKey(probe))
            {
                iterData.probeDisplacements[i] = new ProbeDisplacement(i, originalPositions[probe], probe.transform.position);
            }
        }

        iterationData[iteration] = iterData;
    }
}