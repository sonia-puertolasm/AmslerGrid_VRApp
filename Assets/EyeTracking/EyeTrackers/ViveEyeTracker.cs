#if USE_VIVE
using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using ViveSR.anipal.Eye;
using Unity.VisualScripting;
using System;

public class ViveEyeTracker : IEyeTracker
{
    //private EyeCallbackHandler _callbackHandler;
    private bool eye_callback_registered = false;
    //private Queue<GazeData> _gazeSamples;
    private static GazeData currentGazeData;

    public void Initialize()
    {
        // check if SRanipal_Eye_Framework is already present in scene
        SRanipal_Eye_Framework sranipal = SRanipal_Eye_Framework.Instance;
        // if not, add it
        if (sranipal == null)
        {
            GameObject go = new GameObject("SRanipal_Eye_Framework");
            sranipal = go.AddComponent<SRanipal_Eye_Framework>();
            // set go to not be destroyed on scene change
            GameObject.DontDestroyOnLoad(go);
        }
        
        // Activate Eye Data Callback
        sranipal.EnableEyeDataCallback = true;
        sranipal.StartFramework();

        Debug.Log(SRanipal_Eye_Framework.Status);
        Debug.Log(SRanipal_Eye_Framework.FrameworkStatus.WORKING);

    }

    public void Calibrate()
    {
        SRanipal_Eye.LaunchEyeCalibration();
    }

    public void StartListening()
    {
        if (SRanipal_Eye_Framework.Instance.EnableEyeDataCallback && eye_callback_registered == false)
        {
            eye_callback_registered = true;
            SRanipal_Eye.WrapperRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye.CallbackBasic)EyeCallback));
            Debug.Log("Registered Eye Data Callback");
            Debug.Log(SRanipal_Eye_Framework.FrameworkStatus.WORKING);
            Debug.Log(SRanipal_Eye_Framework.Status);
        }
    }

    public void StopListening()
    {
        if (eye_callback_registered == true)
        {
            SRanipal_Eye.WrapperUnRegisterEyeDataCallback(Marshal.GetFunctionPointerForDelegate((SRanipal_Eye.CallbackBasic)EyeCallback));
            eye_callback_registered = false;
        }
    }

    internal class MonoPInvokeCallbackAttribute : System.Attribute
    {
        public MonoPInvokeCallbackAttribute() { }
    }

    /// <summary>
    /// Eye tracking data callback thread.
    /// Reports data at ~120hz
    /// MonoPInvokeCallback attribute required for IL2CPP scripting backend
    /// </summary>
    /// <param name="eye_data">Reference to latest eye_data</param>
    [MonoPInvokeCallback]
    public static void EyeCallback(ref EyeData eyeData)
    {
        currentGazeData = EyeData2GazeData(eyeData);
        EyeTrackingEvent.TriggerEvent(currentGazeData);
        
        //_gazeSamples.Enqueue(currentGazeData);
        //if (_gazeSamples.Count > 1000) // keep max queue length at 1000
        //{
        //    _gazeSamples.Dequeue();
        //}
    }

    public GazeData GetGazeData()
    {
        // TODO: decide if we want to return gaze data also without callback, then we have to actually get new data from SRanipal here
        return currentGazeData;
    }

    // transform SRAnipal eyeData to general GazeData struct
    // this also converts coordinate system direction and mm to m
    private static GazeData EyeData2GazeData(EyeData eyeData)
    {
        GazeData gazeData = new GazeData();

        // ET timestamp
        gazeData.deviceTimestamp = eyeData.timestamp;
        
        // validity
        eyeData.verbose_data.left.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY);
        eyeData.verbose_data.right.GetValidity(SingleEyeDataValidity.SINGLE_EYE_DATA_EYE_OPENNESS_VALIDITY);

        // left eye
        gazeData.leftValidity = 1;// TODO eyeData.verbose_data.left.eye_data_validata_bit_mask; // datatype ulong
        Vector3 origin = 0.001f * eyeData.verbose_data.left.gaze_origin_mm; // convert from mm to m
        origin.x = -origin.x; // mirror x-axis
        Vector3 direction = eyeData.verbose_data.left.gaze_direction_normalized;
        direction.x = -direction.x; // mirror x-axis
        gazeData.leftRayLocal = new Ray(origin, direction);
        gazeData.leftEyeOpenness = eyeData.verbose_data.left.eye_openness;
        gazeData.leftPupilDiameter = eyeData.verbose_data.left.pupil_diameter_mm;
        //gazeData.leftPupilPosition = eyeData.verbose_data.left.pupil_position_in_sensor_area;

        // right eye
        gazeData.rightValidity = 1;// TODO eyeData.verbose_data.right.eye_data_validata_bit_mask;
        origin = 0.001f * eyeData.verbose_data.right.gaze_origin_mm;
        origin.x = -origin.x;
        direction = eyeData.verbose_data.right.gaze_direction_normalized;
        direction.x = -direction.x;
        gazeData.rightRayLocal = new Ray(origin, direction);
        gazeData.rightEyeOpenness = eyeData.verbose_data.right.eye_openness;
        gazeData.rightPupilDiameter = eyeData.verbose_data.right.pupil_diameter_mm;
        //gazeData.rightPupilPosition = eyeData.verbose_data.right.pupil_position_in_sensor_area;

        // combined gaze ray
        origin = 0.001f * eyeData.verbose_data.combined.eye_data.gaze_origin_mm;
        origin.x = -origin.x;
        direction = eyeData.verbose_data.combined.eye_data.gaze_direction_normalized;
        direction.x = -direction.x;
        gazeData.combinedRayLocal = new Ray(origin, direction);

        // gaze distance
        gazeData.gazeDistance = eyeData.verbose_data.combined.convergence_distance_mm * 0.001f;

        return gazeData;
    }
}
#endif