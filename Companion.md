# FLTSCR Reference

Information related to use of the Flight Scripter companion program.

Beginner's guide below main reference.

## Enumerations

`Vector3Controls { Position, Rotation }`
- A list of controls that use the Vector3 type, simulated by `float[3]` in this program.

`FloatControls { Pitch, Roll, Yaw, Throttle, Brake, Trim, VTOL }`
- A list of controls that use the floating-point type.

`BoolControls { GearDown, Guns, Weapons, Countermeasures }`
- A list of controls that use the Boolean type.

## Method Groups

`void SelectDataSets(bool m = false, bool s = true, bool e = true, bool a = true)`
- Choose which data sets will be exported. It must be called before the save function.
- `m`: Export the Movement set.
- `s`: Export the Standard set.
- `e`: Export the Extra set.
- `a`: Export the Advanced set.

`void SetBufferSize(int size)`
- Initializes the arrays, setting a maximum value of frames allowed by the program.
- `size`: Number of frames. It should be equal to the number of NextFrame calls.

`void WriteValue(Vector3Controls control, float x, float y, float z)`
`void WriteValue(FloatControls control, float value)`
`void WriteValue(BoolControls control, bool value)`
- Set the selected information at the current frame.
- `control`: Name of the information.

`void WriteActivationGroup(int ag, bool value)`
- Set an activation group value at the current frame.
- `ag`: Activation group number.
- `value`: Activation state.

`void NextFrame(float delay = 1f)`
- Write the values defined before it to a data point, and advances to the next.
- `delay`: Time to the next data point. The first `NextFrame` call should have this set to 0.

`void Save(string filename)`
- Export the script into a file readable by the Flight Recorder mod.
- `filename`: File name.

# FLTSCR Guide

## Preparation

1. Download and install [Microsoft Visual Studio 2019](https://visualstudio.microsoft.com/downloads/).
2. Download and unzip [FltScr.zip](https://github.com/hpgbproductions/FltRec/blob/main/FltScr.zip)
3. Open `FltScr.sln`.
4. Open `NewRecording.cs`. You will only ever need to modify `NewRecordingData()`. Currently, an example script is included there.
5. Build using Ctrl+B or the green arrow at the top. This will generate a FLTREC `EXAMPLE.FRC` file on your desktop.
6. Copy the file to your FLTREC recordings folder at `\NACHSAVE\FLTREC\`.
7. Load it in-game and watch your plane automatically activate some activation groups and take off.

Tip: If you run `FltRSet_PlayDebug true`, you can see the current data point and timer during playback.

## Implementation

It is important to know how FLTREC recordings are stored and interpreted, in order to program them.

FLTREC .FRC File Structure:
- Header: An identification string (currently serves no purpose). Number of data points. Whether each data set (M, S, E, A) is present.
- Data Point: Corresponding time since start, followed by movement and input information according to which data sets are enabled.
- All I/O is handled using C# BinaryReader and BinaryWriter classes.

FLTREC Playback System:
- When playback is started, the start time (UnityEngine.Time.timeSinceLevelLoad) is recorded. The time is checked against the start time every FixedUpdate to get a relative time, which is then used to find the corresponding theorhetical floating-point index of the time array.
- This index is used to calculate inputs at any given time.
- Floating-point and Vector3 values are linearly interpolated (lerped) if the floating-point index falls between two integers.
- Boolean values take the previous data point.
- Note: The first data point (index 0) corresponds to the starting values.
- Note: The first data point (index 0) for movement is in world space. The remaining movement data points are relative to the first point. As such, it will be very difficult to write movement data using FLTSCR.
- Note: Movement between the first (index 0) and second (index 1) data points are ignored. The actual starting point is in the second data point. This is not a problem in mod recordings as the interval is small, but must be considered in FLTSCR.
- Tip: To increase a value instantaneously, write them with zero or very small NextFrame delays.

## Program Checklist

- `SelectDataSets` called at the beginning with values (not required, but a good practice)
- `SetBufferSize` called at the beginning with a value equal to the number of `NextFrame` calls
- `Save` called at the end with a valid filename - alphanumeric characters and underscores only
- No `NextFrame` calls before `SetBufferSize`
- No functions or statements after `Save`
