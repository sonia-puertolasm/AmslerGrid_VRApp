# Amsler Grid VR App

Interactive Amsler Grid (IAG) application for desktop-based and VR-based use

## Description

This project implements a VR–based prototype of the IAG approach for the assessment of visual distortions associated with age-related macular degeneration (AMD), particularly metamorphopsia. The application translates the original screen-based IAG approach from Ayhan et. Al (2022) into an immersive VR environment using Unity and C#, enabling real-time grid deformation through user interaction with probe dots.

## Getting Started

### Dependencies

- Unity 2022.3.19f1
- Unity XR Plugin Management
- SteamVR Plugin
- Compatible VR headset (Application programmed using HTC Vive)

### Installing

1. Clone the repository to your local machine using: git clone https://github.com/sonia-puertolasm/AmslerGrid_VRApp.git
2. Open Unity Hub and add the cloned project folder (! Make sure that you have the Unity 2022.3.19f1 version installed in your local machine for avoiding compilation errors or others !)
3. Open the project using Unity 2022.3.19f1 version

*For VR use*

- In Package Manager, verify that all XR-related packages are installed
- If using SteamVR, ensure that the plugin is correctly imported and configured

### Executing program

*For desktop mode*

1. Open the main scene of the project under name AmslerGrid 
2. Verify that the camera and grid visualization settings are as desired via the Inspector window for the Main Camera and Main Grid GOs: if necessary, modify the properties under the Projection section for the camera and the Viewing Distance property for the Main Grid for grid visualization
3. Select the input method under the Probe Dots GO as Keyboard
4. Press Play in the Unity Editor to launch the application

*For VR mode*

1. Connect the VR headset and controllers (only one is required) and complete the necessary room and device setup
2. Start SteamVR (if applicable) and confirm that the devices are connected and detected
3. Open the main scene of the project under name AmslerGrid
4. Verify that the camera and grid visualization settings are as desired via the Inspector window for the Main Camera and Main Grid GOs: if necessary, modify the properties under the Projection section for the camera and the Viewing Distance property for the Main Grid for grid visualization
5. Define a gaze threshold angle for the Eye-Tracking mechanism (e.g. 2,5°)
6. Select the input method under the Probe Dots GO as desired: both keyboard and Vive controller-based methods function in VR mode
7. Press Play in the Unity Editor to launch the application
   
## Performance

*For keyboard mode*

| Interaction                  | Action                                   |
|------------------------------|------------------------------------------|
| Numpad 1–9 (except 5)        | First-iteration probe dot selection      |
| Numpad 1–9                   | Higher-iteration probe dot selection     |
| Arrow keys                   | Selected probe dot displacement          |
| Spacebar                     | Probe dot displacement confirmation      |
| Enter (incl. Numpad Enter)   | Navigate to higher iteration (if IT = 1) |
| Backspace                    | Navigate to lower iteration (if IT = 2)  |

*For controller-based mode using trackpad*

| Interaction                | Action                                   |
|----------------------------|------------------------------------------|
| Numpad 1–9 (except 5)      | First-iteration probe dot selection      |
| Numpad 1–9                 | Higher-iteration probe dot selection     |
| Pressed trackpad           | Selected probe dot displacement          |
| Controller trigger button  | Probe dot displacement confirmation      |
| Controller grip button     | Navigate to higher iteration (if IT = 1) |
| Controller menu button     | Navigate to lower iteration (if IT = 2)  |

*For controller-based mode using motion detection*

_WORK IN PROGRESS_

## Help

If you encounter issues related to XR setup, controller input, or scene configuration, ensure that:
- The correct XR plugin is enabled in the Unity project settings.
- SteamVR is running and the headset is properly detected.
- Input mappings for controllers and keyboard are correctly assigned.

If other issues arise and/or persist, contact the project's author

## Authors

Sonia Puértolas Martínez
[@sonia-puertolasm]

## Version History

v.0.1  
  - Initial functional prototype of the VR-based Iterative Amsler Grid (IAG)
  - Implementation of real-time grid deformation
  - Support for keyboard- and controller-based interaction
  - Configurable viewing parameters (FOV, viewing distance, projection)

## License

This project is provided for academic and research purposes.  
No clinical validation has been performed, and the software is not intended for medical diagnosis or treatment.

## Acknowledgments

- Ayhan et al. (2022) for the original Iterative Amsler Grid (IAG) methodology
- Supervisor Dr. Yannick Sauer for guidance and feedback throughout the project
- Unity Technologies for the Unity engine
- Valve for the SteamVR framework
