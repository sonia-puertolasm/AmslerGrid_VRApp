using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// This class is used to trigger an event when eye tracking data is available
// This is used to decouple the subsciption to the event from the actual eye tracking implementation
// The individual eye tracking implementations will trigger this event when new data is available
public static class EyeTrackingEvent
{
    public static event Action<GazeData> OnDataAvailable;

    // use a trigger method to invoke the event 
    // can be extended to filter/validate the data before invoking the event
    public static void TriggerEvent(GazeData data)
    {
        OnDataAvailable?.Invoke(data);
    }

    // Check if there are any subscribers to the event
    public static bool HasSubscribers()
    {
        return OnDataAvailable != null;
    }
}