using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// Gaze data struct
public struct GazeData
{
    public long deviceTimestamp; // VR headset/eye tracker-specific timestamp
    public float unityTimestamp; // Unity frame timestamp (Do we want this?)
    
    public Ray leftRayLocal; // headset-relative left eye gaze ray
    public Ray rightRayLocal; // headset-relative right eye gaze ray
    public Ray combinedRayLocal; // headset-relative combined gaze ray

    public Ray leftRayWorld; // left eye gaze ray in world coordinates
    public Ray rightRayWorld; // right eye gaze ray in world coordinates
    public Ray combinedRayWorld; // combined gaze ray in world coordinates

    public float gazeDistance; // distance to the gaze point in meters

    public float leftPupilDiameter; // left eye pupil diameter in mm
    public float rightPupilDiameter; // right eye pupil diameter in mm
    public float leftEyeOpenness; // left eye openness (0-1)
    public float rightEyeOpenness; // right eye openness (0-1)


    public int leftValidity; // TODO, define what and how to use this
    public int rightValidity;
}

public interface IEyeTracker
{    
    // Initialize the eye tracker
    public void Initialize();

    // Calibrate eye tracking
    public void Calibrate();

    // Get the current gaze point
    public GazeData GetGazeData();

    public void StartListening();
    public void StopListening();
}