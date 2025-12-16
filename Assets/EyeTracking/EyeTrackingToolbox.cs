using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using System;
using System.Text;
using System.Threading;
using System.IO;

#if USE_VIVE
using ViveSR.anipal.Eye;
#endif

public class EyeTrackingToolbox : MonoBehaviour
{
    public static EyeTrackingToolbox Instance { get; private set; }
    private static float unityTimestamp; // Each thread has access to static variables, so that the timestamp Time.time can be written to the variable unityTimestamp and we can use this as a timestamp even if the function is called in another thread.
    public enum ETProvider
    {
        Dummy,
        HTCViveSRanipal,
        Varjo,
        PupilNeon,
        MetaQuest,
        XTAL
    }
    [Header("Eye Tracking Settings")]

    public ETProvider etprovider = ETProvider.HTCViveSRanipal;
    private IEyeTracker eyeTracker;
    public KeyCode calibrateKey = KeyCode.C;

    private GazeData currentGazeData; // for gaze sample of the current frame
    public Queue<GazeData> gazeTrackingQueue { get; private set; } // TODO: Can it be used publicly? Or should it be private?

    // Enum to define different options for GameObject Tracking
    public enum TrackingOptions
    {
        localTransform,
        globalTransform,
    }

    // Define a class to hold the dropdown option and associated GameObject
    [Serializable]
    public class TrackedObjectOptions
    {
        public TrackingOptions trackingOptions;
        public GameObject gameObject;
    }

    public bool saveRaycastHitpoint = false; // check for raycast intersection with objects during runtime
    public string OutputFolder   { get; private set; } // folder for the recording output files

    [Header("Object Tracking Settings")]
    // List to hold the variables with dropdown options and associated GameObjects
    [SerializeField] private List<TrackedObjectOptions> trackedObjectList = new List<TrackedObjectOptions>();


    private string objectTrackingFile; // output file for object tracking (bound to framerate)
    private string gazeTrackingFile; // output file for eye tracking data (bound to eye tracking frequency)
    Queue trackingDataQueue = new Queue();
    static string msgBuffer = "";

    public bool isObjectRecording = false;
    private bool isRecording = false;
    private Thread savingThread; // background thread for writing to files

    void Awake()
    {
        // set US culture for number formatting in strings
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");
        
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Debug.Log("Singleton instance already existed.");
            Destroy(gameObject);
            return;
        }

        gazeTrackingQueue = new Queue<GazeData>();

        switch (etprovider)
        {
             case ETProvider.Dummy:
                eyeTracker = gameObject.AddComponent<DummyEyeTracker>(); // add DummyEyeTracker as component so that Update/Coroutine is called
                eyeTracker.Initialize();
                break;
#if USE_VIVE
            case ETProvider.HTCViveSRanipal:
                // start SRanipal // todo, should be part of the interface I guess ???
                //Instance.AddComponent<SRanipal_Eye_Framework>();
       
                eyeTracker = new ViveEyeTracker();
                eyeTracker.Initialize();
                Debug.Log("HTC Vive SRanipal initialized.");
                break;
#endif
#if USE_VARJO
            case ETProvider.Varjo:
                eyeTracker = new VarjoEyeTracker();
                eyeTracker.Initialize();
                break;
#endif
#if USE_NEON
            case ETProvider.PupilNeon:
                eyeTracker = new NeonEyeTracker();
                break;
#endif
#if USE_QUEST
            case ETProvider.MetaQuest:
                eyeTracker = new QuestEyeTracker();
                eyeTracker.Initialize();
                Debug.Log("Meta Quest initialized.");
                break;
#endif
#if USE_XTAL
            case ETProvider.XTAL:
                eyeTracker = new XtalEyeTracker();
                Debug.Log("XTAL eye tracker");
                eyeTracker.Initialize();
                break;
#endif
            default:
                Debug.Log("No Eye Tracker selected. Using dummy eye tracker.");
                eyeTracker = gameObject.AddComponent<DummyEyeTracker>(); // add DummyEyeTracker as component so that Update/Coroutine is called
                eyeTracker.Initialize();
                break;
        }

        // event handler for gaze data
        EyeTrackingEvent.OnDataAvailable += HandleData; // subscribe to event

        // test Datetime accuracy
        DateTime t1 = DateTime.Now;
        DateTime t2;
        while ((t2 = DateTime.Now) == t1)
        {
            Debug.Log("DateTime accuracy: " + (t2 - t1).TotalMilliseconds + "ms");
        }

        // set US culture for number formatting in strings
        System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("en-US");
        System.Threading.Thread.CurrentThread.CurrentUICulture = new System.Globalization.CultureInfo("en-US");

        // set default output folder
        OutputFolder = null;
    }

    public void SetOutputFolder(string folder)
    {
        OutputFolder = folder;
    }

    private void Start()
    {
        eyeTracker.StartListening(); // start the background event system
    }

    void Update()
    {
        unityTimestamp = Time.time;
        // TODO change to get-function
        //currentGazeData = eyeTracker.GetGazeData();
        if (Input.GetKeyDown(calibrateKey))
        {
            Debug.Log("Calibration");
            Calibrate();
        }

        if (isRecording && isObjectRecording)
        {
            QueueTrackingData(trackingDataQueue);
        }
    }

    public GazeData GetGazeData()
    {
        currentGazeData.leftRayWorld = new Ray(Camera.main.transform.TransformPoint(currentGazeData.leftRayLocal.origin), Camera.main.transform.TransformDirection(currentGazeData.leftRayLocal.direction));
        currentGazeData.rightRayWorld = new Ray(Camera.main.transform.TransformPoint(currentGazeData.rightRayLocal.origin), Camera.main.transform.TransformDirection(currentGazeData.rightRayLocal.direction));
        currentGazeData.combinedRayWorld = new Ray(Camera.main.transform.TransformPoint(currentGazeData.combinedRayLocal.origin), Camera.main.transform.TransformDirection(currentGazeData.combinedRayLocal.direction));
        return currentGazeData;
    }

    public void Calibrate()
    {
        Debug.Log("Starting eye tracking calibration");
        eyeTracker.Calibrate();
    }

    // start with queueing tracking data
    public void StartRecording(string outputFileName)
    {
        if (!isRecording)
        {
            gazeTrackingQueue.Clear(); // hier oder in stop tracking
            trackingDataQueue.Clear();
            if (OutputFolder == null)
            {
                Debug.LogError("Output folder not set. Please set the output folder with SetOutputFolder(string folder) before starting the recording.");
            }
            if (Path.HasExtension(outputFileName))
            {
                objectTrackingFile = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(outputFileName) + "_head.csv");
                gazeTrackingFile = Path.Combine(OutputFolder, Path.GetFileNameWithoutExtension(outputFileName) + "_gaze.csv");
            }
            else
            {
                objectTrackingFile = Path.Combine(OutputFolder, outputFileName + "_head.csv");
                gazeTrackingFile = Path.Combine(OutputFolder, outputFileName + "_gaze.csv");
            }
            Debug.Log("Object tracking file " + objectTrackingFile);
            Debug.Log("Gaze tracking file " + gazeTrackingFile);
            
            isRecording = true;

            Debug.Log("Start recording: " + eyeTracker);
            
            // check if csv file already exists and change filename if necessary (e.g. _01...)
            int counter = 0;
            while (File.Exists(objectTrackingFile) || File.Exists(gazeTrackingFile))
            {
                counter++;
                objectTrackingFile = Path.Combine(Application.dataPath, OutputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_" + counter.ToString("D2") + "_head.csv");
                Debug.Log("Object tracking file already exists. Changing filename to " + objectTrackingFile);
                gazeTrackingFile = Path.Combine(Application.dataPath, OutputFolder, outputFileName.Substring(0, outputFileName.Length - 4) + "_" + counter.ToString("D2") + "_gaze.csv");
            }
            WriteHeader();
            InvokeRepeating("Save", 0.0f, 1.0f); // save data to file every second
        }
    }
    
    // handle data of the event invoked by the eye tracker
    private void HandleData(GazeData gazeData)
    {
        // gazeDate.unityTimestamp = Time.time; // set Unity timestamp for the current frame
        gazeData.unityTimestamp = unityTimestamp; // If the static event is called from another thread, it appears that this function is called from this thread and does not have access to Time.time, so we use the static variable here.
        currentGazeData = gazeData;
        
        if (isRecording)
        {
            gazeTrackingQueue.Enqueue(gazeData);
        }
    }

    // stop the background saving of the tracking data
    public void StopRecording()
    {
        isRecording = false;
    }

    private void OnDisable()
    {
        WriteTrackingData();
        if (eyeTracker != null)
        {
            eyeTracker.StopListening(); // stop the background gaze data sampling
        }
        if (EyeTrackingEvent.HasSubscribers()) // chek if EyeTrackingEvent.OnDataAvailable is != null, only then we can unsubscribe
        {
            EyeTrackingEvent.OnDataAvailable -= HandleData;
        }
    }
    
    // Write header for tracking files
    private void WriteHeader()
    {
        // check if output folder exists
        if (!Directory.Exists(Path.Combine(Application.dataPath, OutputFolder)))
        {
            Directory.CreateDirectory(Path.Combine(Application.dataPath, OutputFolder));
        }
        StreamWriter sw = new StreamWriter(objectTrackingFile);

        // header for object tracking file
        string header = "timestamp,";
        header += "eye_timestamp,";

        foreach (TrackedObjectOptions trackedObject in trackedObjectList)
        {
            switch (trackedObject.trackingOptions)
            {
                case TrackingOptions.localTransform:
                    header += trackedObject.gameObject.name + "_localPosition.x,";
                    header += trackedObject.gameObject.name + "_localPosition.y,";
                    header += trackedObject.gameObject.name + "_localPosition.z,";
                    header += trackedObject.gameObject.name + "_localRotation.x,";
                    header += trackedObject.gameObject.name + "_localRotation.y,";
                    header += trackedObject.gameObject.name + "_localRotation.z,";
                    header += trackedObject.gameObject.name + "_localRotation.w,";
                    break;
                case TrackingOptions.globalTransform:
                    header += trackedObject.gameObject.name + "_position.x,";
                    header += trackedObject.gameObject.name + "_position.y,";
                    header += trackedObject.gameObject.name + "_position.z,";
                    header += trackedObject.gameObject.name + "_rotation.x,";
                    header += trackedObject.gameObject.name + "_rotation.y,";
                    header += trackedObject.gameObject.name + "_rotation.z,";
                    header += trackedObject.gameObject.name + "_rotation.w,";
                    break;
                default:
                    Debug.LogError("Unknown option selected for " + trackedObject.gameObject.name);
                    break;
            }
        }

        if (saveRaycastHitpoint)
        {
            header += "hit_object,";
            header += "hit_point.x,hit_point.y,hit_point.z,";
        }

        header += "messages,";
        sw.WriteLine(header);
        sw.Close();

        // header for gaze tracking file
        sw = new StreamWriter(gazeTrackingFile);
        header = "eye_timestamp,";
        header += "left_validata,";
        header += "left_eye_openness,";
        header += "left_eye_pupil_diameter,";
        header += "left_eye_origin.x,left_eye_origin.y,left_eye_origin.z,";
        header += "left_eye_gaze.x,left_eye_gaze.y,left_eye_gaze.z,";
        //header += "left_pupil_position.x,left_pupil_position.y,";
        header += "right_validata,";
        header += "right_eye_openness,";
        header += "right_eye_pupil_diameter,";
        header += "right_eye_origin.x,right_eye_origin.y,right_eye_origin.z,";
        header += "right_eye_gaze.x,right_eye_gaze.y,right_eye_gaze.z,";
        //header += "right_pupil_position.x,right_pupil_position.y,";
        header += "combined_eye_origin.x,combined_eye_origin.y,combined_eye_origin.z,";
        header += "combined_eye_gaze.x,combined_eye_gaze.y,combined_eye_gaze.z,";
        header += "gaze_distance,";

        sw.WriteLine(header);
        sw.Close();
    }

    public void WriteMessage(string msg)
    {
        msgBuffer = msg;
    }

    private string GazeDataString(GazeData gazeDataSample)
    {
        StringBuilder datasetLine = new StringBuilder(350); // adjust capacity to your needs

        datasetLine.Append(gazeDataSample.deviceTimestamp.ToString() + ",");

        // left eye
        datasetLine.Append(gazeDataSample.leftValidity.ToString() + ",");
        datasetLine.Append(gazeDataSample.leftEyeOpenness.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.leftPupilDiameter.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.leftRayLocal.origin.x.ToString("F10") + "," + gazeDataSample.leftRayLocal.origin.y.ToString("F10") + "," + gazeDataSample.leftRayLocal.origin.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.leftRayLocal.direction.x.ToString("F10") + "," + gazeDataSample.leftRayLocal.direction.y.ToString("F10") + "," + gazeDataSample.leftRayLocal.direction.z.ToString("F10") + ",");
        //datasetLine.Append(gazeDataSample.leftPupilPosition.x.ToString("F10") + "," + gazeDataSample.leftPupilPosition.y.ToString("F10") + ",");

        // right eye
        datasetLine.Append(gazeDataSample.rightValidity.ToString() + ",");
        datasetLine.Append(gazeDataSample.leftEyeOpenness.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.rightPupilDiameter.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.rightRayLocal.origin.x.ToString("F10") + "," + gazeDataSample.rightRayLocal.origin.y.ToString("F10") + "," + gazeDataSample.rightRayLocal.origin.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.rightRayLocal.direction.x.ToString("F10") + "," + gazeDataSample.rightRayLocal.direction.y.ToString("F10") + "," + gazeDataSample.rightRayLocal.direction.z.ToString("F10") + ",");
        //datasetLine.Append(gazeDataSample.rightPupilPosition.x.ToString("F10") + "," + gazeDataSample.rightPupilPosition.y.ToString("F10") + ",");

        // combined eye
        datasetLine.Append(gazeDataSample.combinedRayLocal.origin.x.ToString("F10") + "," + gazeDataSample.combinedRayLocal.origin.y.ToString("F10") + "," + gazeDataSample.combinedRayLocal.origin.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.combinedRayLocal.direction.x.ToString("F10") + "," + gazeDataSample.combinedRayLocal.direction.y.ToString("F10") + "," + gazeDataSample.combinedRayLocal.direction.z.ToString("F10") + ",");
        datasetLine.Append(gazeDataSample.gazeDistance.ToString("F10") + ",");
        return (datasetLine.ToString());
    }

    private void QueueTrackingData(Queue queue)
    {
        // StringBuilder should be quite effiction: https://stackoverflow.com/questions/21078/most-efficient-way-to-concatenate-strings
        StringBuilder datasetLine = new StringBuilder(700); // adjust capacity to your needs

        // timestamp: use time at beginning of frame
        datasetLine.Append(Time.time.ToString("F10") + ",");

        // eye tracking timestampe
        datasetLine.Append(currentGazeData.deviceTimestamp.ToString() + ",");

        foreach (TrackedObjectOptions trackedObject in trackedObjectList)
        {
            switch (trackedObject.trackingOptions)
            {
                case TrackingOptions.localTransform:
                    datasetLine.Append(trackedObject.gameObject.transform.localPosition.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localPosition.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localPosition.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.localRotation.w.ToString("F10") + ",");
                    break;
                case TrackingOptions.globalTransform:
                    datasetLine.Append(trackedObject.gameObject.transform.position.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.position.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.position.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.x.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.y.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.z.ToString("F10") + ",");
                    datasetLine.Append(trackedObject.gameObject.transform.rotation.w.ToString("F10") + ",");
                    break;
                default:
                    Debug.LogError("Unknown option selected for " + trackedObject.gameObject.name);
                    break;
            }
        }

        if (saveRaycastHitpoint)
        {
            datasetLine.Append(GazeRaycast());
        }

        // buffered message
        if (!String.IsNullOrEmpty(msgBuffer))
        {
            datasetLine.Append(msgBuffer + ",");
            msgBuffer = "";
        }
        queue.Enqueue(datasetLine.ToString());
    }

    public string GazeRaycast()
    {
        RaycastHit hit;
        Vector3 rayOrigin = Camera.main.transform.position + Camera.main.transform.rotation * currentGazeData.combinedRayLocal.origin;
        Vector3 rayDirection = Camera.main.transform.rotation * currentGazeData.combinedRayLocal.direction;

        if (Physics.Raycast(rayOrigin, rayDirection, out hit))
        {

            return hit.transform.name + "," + hit.point.x.ToString("F10") + "," + hit.point.y.ToString("F10") + "," + hit.point.z.ToString("F10") + ",";
        }
        else
        {
            return "NA,,,,";    
        }
    }

    private void Save()
    {
        if (savingThread != null && savingThread.IsAlive)
        {
            Debug.Log("Previous saving thread is still running");
            return;
        }
        savingThread = new Thread(WriteTrackingData);
        savingThread.Start();
    }

    private void WriteTrackingData()
    {
        int counter = 0;
        StreamWriter sw;
        string datasetLine;

        if (isObjectRecording)
        {
            try
            {
                sw = new StreamWriter(objectTrackingFile, true); //true for append

                // dequeue trackingDataQueue until empty
                while (trackingDataQueue.Count > 0)
                {
                    datasetLine = trackingDataQueue.Dequeue().ToString();
                    counter++;
                    sw.WriteLine(datasetLine); // write to file
                }
                sw.Close(); // close file
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log it, display a message to the user, etc.)
                Console.WriteLine("An error occurred while writing to the file: " + ex.Message);
                // Optionally, you can log more detailed information about the exception:
                Console.WriteLine(ex.ToString());
            }
            //Debug.Log("Writing " + counter.ToString() + " lines of object tracking data");
        }

        if (isRecording)
        {
            try
            {
                sw = new StreamWriter(gazeTrackingFile, true); //true for append
                // dequeue gazeTrackingQueue until empty
                counter = 0;
                while (gazeTrackingQueue.Count > 0)
                {
                    datasetLine = GazeDataString(gazeTrackingQueue.Dequeue());
                    sw.WriteLine(datasetLine); // write to file
                    counter++;
                }
                sw.Close(); // close file
            }
            catch (Exception ex)
            {
                // Handle the exception (e.g., log it, display a message to the user, etc.)
                Console.WriteLine("An error occurred while writing to the file: " + ex.Message);
                // Optionally, you can log more detailed information about the exception:
                Console.WriteLine(ex.ToString());
            }
            //Debug.Log("Writing " + counter.ToString() + " lines of gaze data");
        }
    }
}