# Commands Reference

Notice about Data Sets:
This program splits the list of recorded information into four categories:
- M (Movement): Position and rotation.
- S (Standard): Basic control axes, Throttle, and Brake.
- E (Extra): Trim and VTOL sliders, and landing gear.
- A (Advanced): Weapons and activation groups.

Recording settings will determine which data sets are recorded. This setting is saved in each recording. During playback, this information is checked. A recorded data set will be locked from player input during playback.

The following information cannot be recorded:
- Start values of disabled data sets
- Switching of weapons, targets, or combat mode

## Commands

[Actions] `FltRec_ClearSet (char set)`
- Delete a data set from the currently loaded data.
- `set`: Character corresponding to the data set. May be M, S, E, or A.

[Actions] `FltRec_Load (string filename)`
- Load a recording file into internal memory.
- `filename`: File name.

[Actions] `FltRec_Play (void)`
- Play data currently in memory.

[Actions] `FltRec_Preview (void)`
- Generate a preview of the path taken. The data set M must be present. Note that the path is retained even if new data is loaded.

[Actions] `FltRec_Record (void)`
- Begin recording aircraft movement and inputs. Be sure to adjust recorder settings first.

[Actions] `FltRec_Save (string filename)`
- Save the data currently in memory to a file.
- `filename`: File name.

[Actions] `FltRec_Stop (void)`
- Manually stop the current recording or playback.

[Actions] `FltRec_TogglePreview (void)`
- Toggle visibility of the preview. The preview must be generated first. Note that leaving the sandbox will automatically hide the preview.

[Check Settings] `FltRSet_Check (void)` *(added in V1.1)*
- Prints the current settings in the dev console.

[Recording Settings] `FltRSet_RecordControls (bool m, bool s, bool e, bool a)`
- Choose whether certain data sets are recorded.
- `m`: Movement. Default true.
- `s`: Standard. Default true.
- `e`: Extra. Default false.
- `a`: Advanced. Default false.

[Recording Settings] `FltRSet_RecordQuality (int size, int interval)`
- Recording quality.
- `size`: Maximum number of data points that can be recorded before the recording is automatically stopped. Default 3000.
- `interval`: Number of FixedUpdate frames between each recorded data point. Default 10.

[Playback Settings] `FltRSet_PlayDebug (bool active)`
- Show or hide the debug message during data playback. Useful for testing FLTSCR output data.
- `active`: Debug message visibility. Default false.

[Playback Settings] `FltRSet_PlaySpace (bool pos, bool rot)`
- Set the space options for playback. If true, a start position and rotation in recording data is used. If false, the current position and rotation of the aircraft is used.
- `pos`: Position relativity. Default true.
- `rot`: Rotation relativity. Default true.

[Previewer Settings] `FltRSet_PreviewColor (float r, float g, float b, float a)`
- Color of the preview line.
- `r`: Red. Default 0.
- `g`: Green. Default 1.
- `b`: Blue. Default 0.
- `a`: Alpha (opacity). Default 0.5f.

[Previewer Settings] `FltRSet_PreviewQuality (float width, float interval, int cvert)`
- Other appearance factors of the preview line.
- `width`: Line thickness. Default 0.5f.
- `interval`: Recording's simulation time between each point of the preview. Default 0.1f.
- `cvert`: Amount of rounding applied to corners. Default 0.
