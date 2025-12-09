using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
public class DummyEyeTracker : MonoBehaviour, IEyeTracker
{
    // DummyEyeTracker simulates an eye tracker by generating gaze data based on the camera's transform (and curser position).
    // It can be used for testing purposes when a real eye tracker is not available.
    // It is MonoBehaviour and is added as Compoenent in EyeTrackingToolbox to call Update/Coroutine 
    private GazeData currentGazeData;
    private GameObject cam;
    private Stopwatch stopwatch;
    private float simulatedIpd = 0.064f; // default is 64mm
    private float simulatedNoise = 0.0f; // default is 0.0
    private bool backgroundSampling = false;
    private Coroutine samplingCoroutine;
    public float interval = 0.008f; // sampling interval for generating dummy gaze data


    // Initialize the dummy eye tracker and set simulated gaze properties
    public void Initialize(float ipd,float noise)
    {
        // Find camera object
        cam = GameObject.Find("Main Camera");

        // Initialize and start the stopwatch
        stopwatch = new Stopwatch();
        stopwatch.Start();

        currentGazeData = new GazeData();

        // Set the simulated IPD and noise
        simulatedIpd = ipd;
        simulatedNoise = noise;
        UnityEngine.Debug.Log("Dummy eye tracker initialized with IPD: " + simulatedIpd + " and noise: " + simulatedNoise);
    }

    private void Update()
    {
    }    

    public void StartListening()
    {
        backgroundSampling = true;
        UnityEngine.Debug.Log("Dummy eye tracker started listening");

        if (samplingCoroutine == null)
        {
            samplingCoroutine = StartCoroutine(SamplingCoroutine(interval));
        }
    }

    public void StopListening()
    {
        backgroundSampling = false;

        if (samplingCoroutine != null)
        {
            StopCoroutine(samplingCoroutine);
            samplingCoroutine = null;
        }
    }

    private IEnumerator SamplingCoroutine(float interval)
    {
        while (backgroundSampling)
        {
            currentGazeData = SimulatedGazeData();
            EyeTrackingEvent.TriggerEvent(currentGazeData);
            yield return new WaitForSeconds(interval);
        }
    }
    
    // Initialize the dummy eye tracker with default noise
    public void Initialize(float ipd)
    {
        // Find camera object
        cam = GameObject.Find("Main Camera");

        // Initialize and start the stopwatch
        stopwatch = new Stopwatch();
        stopwatch.Start();

        currentGazeData = new GazeData();

        // Set the simulated IPD
        simulatedIpd = ipd;
        UnityEngine.Debug.Log("Dummy eye tracker initialized with IPD: " + simulatedIpd + " and noise: " + simulatedNoise);

    }
    
    // initialize the default eye tracker with default IPD and noise
    public void Initialize()
    {
        // Find camera object
        cam = GameObject.Find("Main Camera");

        // Initialize and start the stopwatch
        stopwatch = new Stopwatch();
        stopwatch.Start();

        currentGazeData = new GazeData();
        UnityEngine.Debug.Log("Dummy eye tracker initialized with IPD: " + simulatedIpd + " and noise: " + simulatedNoise);

    }

    // Calibrate eye tracking
    public void Calibrate()
    {
        // so far we dont simulate a calibratio
        return;
    }

    // Get the current gaze point
    public GazeData GetGazeData()
    {
        return currentGazeData;
    }   

    private GazeData SimulatedGazeData()
    {
        // Simulate gaze data based ray casting from the camera in the direction of the cursor
        Camera cam = Camera.main;
        RaycastHit hit;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        UnityEngine.Debug.DrawRay(ray.origin, ray.direction * 1000, Color.red);

        Physics.Raycast(ray, out hit);

        Vector3 leftGazeOriginWorld = cam.transform.position - cam.transform.right * simulatedIpd / 2;
        Vector3 rightGazeOriginWorld = cam.transform.position + cam.transform.right * simulatedIpd / 2;
        Vector3 leftGazeDirectionWorld = hit.point - leftGazeOriginWorld;
        Vector3 rightGazeDirectionWorld = hit.point - rightGazeOriginWorld;
        // normalize gaze rayDirection
        leftGazeDirectionWorld.Normalize();
        rightGazeDirectionWorld.Normalize();
        // get the local camera coordinates for the gaze rays
        Vector3 leftGazeOriginLocal = cam.transform.InverseTransformDirection(leftGazeDirectionWorld);
        Vector3 rightGazeOriginLocal = cam.transform.InverseTransformDirection(rightGazeDirectionWorld);
        // simulate gaze rays in local (camera) coordinates
        Ray leftGazeRay = new Ray(new Vector3(- simulatedIpd / 2,0,0), leftGazeOriginLocal);    
        Ray rightGazeRay = new Ray(new Vector3(simulatedIpd / 2,0,0), rightGazeOriginLocal);
        Ray combinedGazeRay = new Ray(new Vector3(0,0,0), Vector3.Normalize(leftGazeOriginLocal + rightGazeOriginLocal));
        
        GazeData simulatedGazeData = new GazeData(); 
        simulatedGazeData.deviceTimestamp = stopwatch.ElapsedMilliseconds;
        simulatedGazeData.unityTimestamp = UnityEngine.Time.time;
        
        simulatedGazeData.leftRayLocal = leftGazeRay;
        simulatedGazeData.rightRayLocal = rightGazeRay;
        simulatedGazeData.combinedRayLocal = combinedGazeRay;
        simulatedGazeData.gazeDistance = 1000.0f;
        simulatedGazeData.leftPupilDiameter = 4.0f;
        simulatedGazeData.rightPupilDiameter = 4.0f;
        simulatedGazeData.leftEyeOpenness = 1.0f;
        simulatedGazeData.rightEyeOpenness = 1.0f;
        simulatedGazeData.leftValidity = 1;
        simulatedGazeData.rightValidity = 1;

        // add gaussian noise to the gaze data
        //TODO: implement noise
        return simulatedGazeData;
    }
 }