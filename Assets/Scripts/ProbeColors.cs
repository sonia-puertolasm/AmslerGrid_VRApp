using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEditor;
using UnityEngine;

public class ProbeColors : MonoBehaviour
{

    // Core colors for definition of the probe dots and initial status
    public static readonly Color Default = Color.gray;
    public static readonly Color Selected = Color.yellow;
    public static readonly Color Completed = Color.green;

    // Iteration-specific colors

    public static readonly Color InactiveHigherIt = Color.blue;
    public static readonly Color CenterHigherIt = Color.magenta;
}

    
