# FltRec

**Flight Recorder** (FLTREC) is a tool to save and load aircraft movement and control inputs. Through dev console commands, you can record yourself operating your machine, and replay and save it.

Notice about Download Options:
- The Source Code function is a link to the GitHub repository containing the assets and FLTSCR companion program.

Warning! This mod contains file I/O functionality. To avoid the loss of any data, please do not use the NACHSAVE folder.

####What can you do with FLTREC?

- Record videos without having to operate the aircraft and camera at the same time
- Map and view flight paths
- Run aircraft control macros

####Try It Out

This will help you to familiarize yourself with FLTREC.
1. Install the mod and enter a map.
2. Run `FltRec_Record` and fly around a bit.
3. Run `FltRec_Stop`, then `FltRec_Preview`. Your flight path will appear.
4. Run `FltRec_Play`. Your aircraft will follow the path from the beginning, and the main aircraft controls move accordingly.
5. Run `FltRec_Save` and enter a file name. Only use alphanumeric characters or underscores.
6. Close the game, then re-open it. Try to run `FltRec_Play`. You will notice that there is no replay data to load.
7. Run `FltRec_Load` with the same file name as in step 5, then run `FltRec_Play`. The effect is the same as in step 4.

####Commands Reference

[Actions] `FltRec_ClearSet (char set)`
Delete a data set from the currently loaded data.
`set`: Character corresponding to the data set. May be M, S, E, or A.

[Actions] `FltRec_Load (string filename)`
Load a recording file into internal memory.
`filename`: File name.

[Actions] `FltRec_Play (void)`
Play data currently in memory.

[Actions] `FltRec_Preview (void)`
Generate a preview of the path taken. The data set M must be present. Note that the path is retained even if new data is loaded.

[Actions] `FltRec_Record (void)`
Begin recording aircraft movement and inputs. Be sure to adjust recorder settings first.

[Actions] `FltRec_Save (string filename)`
Save the data currently in memory to a file.
`filename`: File name.

[Actions] `FltRec_Stop (void)`
Manually stop the current recording or playback.

[Actions] `FltRec_TogglePreview (void)`
Toggle visibility of the preview. The preview must be generated first. Note that leaving the sandbox will automatically hide the preview.
