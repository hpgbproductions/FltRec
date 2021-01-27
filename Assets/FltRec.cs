using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public class FltRec : MonoBehaviour
{
    [SerializeField]
    private LineRenderer PreviewLine;

    [SerializeField]
    private Transform StartPoint;

    [SerializeField]
    private Transform PlayerPoint;

    enum FRModes { Inactive, Record, Playback }
    FRModes FRMode = FRModes.Inactive;

    // Temporary values for recording and playback calculation
    Vector3 StartPointCache = Vector3.zero;
    Vector3 OffsetCache = Vector3.zero;
    float StartTime;            // Used to convert between TimeSinceStart and Time.timeSinceLevelLoad (not saved in file)
    float PlaybackTime;
    int DataPointCache = 0;
    bool[] PreviousPlaneAgs;    // PlaneAgs of the previous action frame

    // Storage for recording and playback
    int DataPoints;              // Array length of loaded information
    float[] TimeSinceStart;
    Vector3[] PlanePositions;
    Vector3[] PlaneRotations;
    float[] PlanePitch;
    float[] PlaneRoll;
    float[] PlaneYaw;
    float[] PlaneThrottle;
    float[] PlaneBrake;
    float[] PlaneTrim;
    float[] PlaneVTOL;
    bool[] PlaneGearDown;
    bool[] PlaneGuns;
    bool[] PlaneWeapons;
    bool[] PlaneCountermeasures;
    bool[,] PlaneAgs;            // Store activation group values as a two-dimensional array

    // Recording mode variables
    int RecDataCount = 0;
    int RecFrameCount = 0;

    // Playback information associated with loaded data
    bool PlayMovement = true;
    bool PlayStandard = true;
    bool PlayExtra = false;
    bool PlayAdvanced = false;

    // File directories and paths
    string DirNachSave;
    string DirRecordings;
    string SettingsPath;

    // Filename regex
    static readonly Regex FilenameCheck = new Regex(@"^[0-9A-Za-z_]+$");

    // BEGIN SETTINGS

    // Recorder settings
    int RecordingMaxSize = 3000;    // maximum number of recorded frames
    int RecordingInterval = 10;      // record every number of frames
    bool RecordMovement = true;     // position, rotation - 24 bytes
    bool RecordStandard = true;     // axes, throttle, brake - 20 bytes
    bool RecordExtra = false;       // sliders, gear - 9 bytes
    bool RecordAdvanced = false;    // activation groups, weapons - 11 bytes

    // Player settings
    bool PositionRelative = false;    // Start at current position (true) or saved start position (false)
    bool RotationRelative = false;

    // Line settings
    float ColorR = 0f;
    float ColorG = 1f;
    float ColorB = 0f;
    float ColorA = 0.5f;
    float LineWidth = 0.5f;
    float LineTimeInterval = 0.2f;
    int LineCornerVertices = 0;

    // END SETTINGS
    
    private void Awake()
    {
        // Standard action commands
        ServiceProvider.Instance.DevConsole.RegisterCommand("FltRec_Record", FltRec_Record);
        ServiceProvider.Instance.DevConsole.RegisterCommand("FltRec_Play", FltRec_Play);
        ServiceProvider.Instance.DevConsole.RegisterCommand("FltRec_Stop", FltRec_Stop);
        ServiceProvider.Instance.DevConsole.RegisterCommand<string>("FltRec_Save", FltRec_Save);
        ServiceProvider.Instance.DevConsole.RegisterCommand<string>("FltRec_Load", FltRec_Load);
        ServiceProvider.Instance.DevConsole.RegisterCommand("FltRec_Preview", FltRec_Preview);
        ServiceProvider.Instance.DevConsole.RegisterCommand("FltRec_TogglePreview", FltRec_TogglePreview);
        ServiceProvider.Instance.DevConsole.RegisterCommand<char>("FltRec_ClearSet", FltRec_ClearSet);

        // Settings commands
        ServiceProvider.Instance.DevConsole.RegisterCommand<int, int>("FltRSet_RecordQuality", FltRSet_RecordQuality);
        ServiceProvider.Instance.DevConsole.RegisterCommand<bool, bool, bool, bool>("FltRSet_RecordControls", FltRSet_RecordControls);
        ServiceProvider.Instance.DevConsole.RegisterCommand<bool, bool>("FltRSet_PlaySpace", FltRSet_PlaySpace);
        ServiceProvider.Instance.DevConsole.RegisterCommand<float, float, float, float>("FltRSet_PreviewColor", FltRSet_PreviewColor);
        ServiceProvider.Instance.DevConsole.RegisterCommand<float, float, int>("FltRSet_PreviewQuality", FltRSet_PreviewQuality);

        // Set directory strings
        DirNachSave = Application.persistentDataPath + "/NACHSAVE/";
        DirRecordings = DirNachSave + "/FLTREC/";
        SettingsPath = DirNachSave + "/FLTRSET.DAT";

        // Create directories (does nothing if they already exist)
        Directory.CreateDirectory(DirNachSave);
        Directory.CreateDirectory(DirRecordings);

        // Check for settings file
        if (File.Exists(SettingsPath))
        {
            Debug.Log("Flight Recorder settings file detected. Loading...");

            // Load the settings file
            using (BinaryReader reader = new BinaryReader(File.Open(SettingsPath, FileMode.Open)))
            {
                reader.ReadString();

                RecordingMaxSize = reader.ReadInt32();
                RecordingInterval = reader.ReadInt32();
                RecordMovement = reader.ReadBoolean();
                RecordStandard = reader.ReadBoolean();
                RecordExtra = reader.ReadBoolean();
                RecordAdvanced = reader.ReadBoolean();

                PositionRelative = reader.ReadBoolean();
                RotationRelative = reader.ReadBoolean();

                ColorR = reader.ReadSingle();
                ColorG = reader.ReadSingle();
                ColorB = reader.ReadSingle();
                ColorA = reader.ReadSingle();
                LineWidth = reader.ReadSingle();
                LineTimeInterval = reader.ReadSingle();
                LineCornerVertices = reader.ReadInt32();
            }

            Debug.Log("Flight Recorder settings loaded successfully.");
        }
        else
        {
            Debug.LogWarning("No Flight Recorder settings file detected. Initialized with default values.");
        }
    }

    private void Start()
    {
        FltRSet_PreviewColor(ColorR, ColorG, ColorB, ColorA);
        PreviewLine.enabled = false;
    }

    private void FixedUpdate()
    {
        gameObject.transform.position = -ServiceProvider.Instance.GameWorld.FloatingOriginOffset;

        if (!ServiceProvider.Instance.GameState.IsInLevel || ServiceProvider.Instance.GameState.IsInDesigner)
        {
            PreviewLine.enabled = false;

            if (FRMode != FRModes.Inactive)
            {
                FltRec_Stop();
            }
        }

        if (ServiceProvider.Instance.GameState.IsPaused | FRMode == FRModes.Inactive)
        {
            // return;
        }
        else if (FRMode == FRModes.Record)
        {
            ServiceProvider.Instance.GameWorld.ShowStatusMessage(string.Format("Recording: {0:F1} ({1}/{2})", Time.timeSinceLevelLoad - StartTime, RecDataCount, RecordingMaxSize));

            // Check recording interval
            RecFrameCount++;
            if (RecFrameCount < RecordingInterval)
            {
                // Don't do anything if it's not time for recording the next frame
                return;
            }

            // Reset frame counter
            RecFrameCount = 0;

            // BEGIN record frame data

            if (RecDataCount == 0)
            {
                // Getting information specific to first data point
                // and preparing for other data points

                StartTime = Time.timeSinceLevelLoad;
                TimeSinceStart[0] = 0f;

                PlanePositions[0] = ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition + ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
                PlaneRotations[0] = ServiceProvider.Instance.PlayerAircraft.MainCockpitRotation;

                StartPoint.position = ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition - ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
                StartPoint.eulerAngles = ServiceProvider.Instance.PlayerAircraft.MainCockpitRotation;

                StartPointCache = ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition;
                OffsetCache = ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
            }
            else
            {
                TimeSinceStart[RecDataCount] = Time.timeSinceLevelLoad - StartTime;

                StartPoint.position = StartPointCache - ServiceProvider.Instance.GameWorld.FloatingOriginOffset;

                PlayerPoint.position = ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition - OffsetCache;
                PlayerPoint.eulerAngles = ServiceProvider.Instance.PlayerAircraft.MainCockpitRotation;

                PlanePositions[RecDataCount] = PlayerPoint.localPosition;
                PlaneRotations[RecDataCount] = PlayerPoint.localEulerAngles;
            }

            PlanePitch[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Pitch;
            PlaneRoll[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Roll;
            PlaneYaw[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Yaw;
            PlaneThrottle[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Throttle;
            PlaneBrake[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Brake;
            PlaneTrim[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Trim;
            PlaneVTOL[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.Vtol;
            PlaneGearDown[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.LandingGearDown;
            PlaneGuns[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.FireGuns;
            PlaneWeapons[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.FireWeapons;
            PlaneCountermeasures[RecDataCount] = ServiceProvider.Instance.PlayerAircraft.Controls.LaunchCountermeasures;
            for (int i = 0; i < 8; i++)
            {
                PlaneAgs[RecDataCount, i] = ServiceProvider.Instance.PlayerAircraft.Controls.GetActivationGroupState(i + 1);
            }

            // END record frame data

            RecDataCount++;
            DataPoints = RecDataCount;

            // Exit recording mode if maximum size is reached
            if (RecDataCount == RecordingMaxSize)
            {
                ServiceProvider.Instance.GameWorld.ShowStatusMessage("Maximum recording size reached. Recording stopped. Data sets " + LogDataSets(RecordMovement, RecordStandard, RecordExtra, RecordAdvanced));
                FRMode = FRModes.Inactive;
            }
        }
        else if (FRMode == FRModes.Playback)
        {
            StartPoint.position = StartPointCache - ServiceProvider.Instance.GameWorld.FloatingOriginOffset;

            // Get the theoretical floating point index dpf
            PlaybackTime = Time.timeSinceLevelLoad - StartTime;
            float dpf = SeekDataPoint(TimeSinceStart, PlaybackTime, DataPointCache);
            DataPointCache = Mathf.FloorToInt(dpf);

            // End of data stream
            if (DataPointCache >= DataPoints - 1)
            {
                FltRec_Stop();
            }

            if (PlayMovement)
            {
                PlayerPoint.localPosition = DataAtPoint(PlanePositions, Mathf.Max(dpf, 1f), false);
                PlayerPoint.localEulerAngles = DataAtPoint(PlaneRotations, Mathf.Max(dpf, 1f), true);

                ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition = PlayerPoint.position;
                ServiceProvider.Instance.PlayerAircraft.MainCockpitRotation = PlayerPoint.eulerAngles;
            }

            if (PlayStandard)
            {
                ServiceProvider.Instance.PlayerAircraft.Controls.Pitch = DataAtPoint(PlanePitch, dpf);
                ServiceProvider.Instance.PlayerAircraft.Controls.Roll = DataAtPoint(PlaneRoll, dpf);
                ServiceProvider.Instance.PlayerAircraft.Controls.Yaw = DataAtPoint(PlaneYaw, dpf);
                ServiceProvider.Instance.PlayerAircraft.Controls.Throttle = DataAtPoint(PlaneThrottle, dpf);
                ServiceProvider.Instance.PlayerAircraft.Controls.Brake = DataAtPoint(PlaneBrake, dpf);
            }

            if (PlayExtra)
            {
                ServiceProvider.Instance.PlayerAircraft.Controls.Trim = DataAtPoint(PlaneTrim, dpf);
                ServiceProvider.Instance.PlayerAircraft.Controls.Vtol = DataAtPoint(PlaneVTOL, dpf);
                ServiceProvider.Instance.PlayerAircraft.Controls.LandingGearDown = PlaneGearDown[DataPointCache];
            }

            if (PlayAdvanced)
            {
                ServiceProvider.Instance.PlayerAircraft.Controls.FireGuns = PlaneGuns[DataPointCache];
                ServiceProvider.Instance.PlayerAircraft.Controls.FireWeapons = PlaneWeapons[DataPointCache];
                ServiceProvider.Instance.PlayerAircraft.Controls.LaunchCountermeasures = PlaneCountermeasures[DataPointCache];
                for (int i = 0; i < 8; i++)
                {
                    if (PlaneAgs[DataPointCache, i] != PreviousPlaneAgs[i])
                    {
                        ServiceProvider.Instance.PlayerAircraft.Controls.ToggleActivationGroup(i + 1);
                        PreviousPlaneAgs[i] = !PreviousPlaneAgs[i];
                    }
                }
            }
        }
    }

    private void FltRec_Record()
    {
        if (!ServiceProvider.Instance.GameState.IsInLevel || ServiceProvider.Instance.GameState.IsInDesigner)
        {
            Debug.LogError("Can only record in map");
        }
        else if (FRMode == FRModes.Playback)
        {
            Debug.LogError("Cannot record when playing a recording");
        }

        // Set up recording system
        ResetBuffer(RecordingMaxSize);
        RecDataCount = 0;
        RecFrameCount = 0;
        FRMode = FRModes.Record;

        PlayMovement = RecordMovement;
        PlayStandard = RecordStandard;
        PlayExtra = RecordExtra;
        PlayAdvanced = RecordAdvanced;

        Debug.Log("Recording started");
    }

    private void FltRec_Play()
    {
        if (!ServiceProvider.Instance.GameState.IsInLevel || ServiceProvider.Instance.GameState.IsInDesigner)
        {
            Debug.LogError("Can only play recordings in map");
        }
        else if (FRMode == FRModes.Record)
        {
            Debug.LogError("Cannot start playback when recording (stop recording first)");
        }

        // Set up playback system
        StartTime = Time.timeSinceLevelLoad;
        DataPointCache = 0;
        PreviousPlaneAgs = new bool[8];
        FRMode = FRModes.Playback;

        if (PositionRelative)
        {
            StartPointCache = ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition + ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
            OffsetCache = ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
        }
        else
        {
            StartPointCache = PlanePositions[0];
            OffsetCache = ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
        }

        if (RotationRelative)
        {
            StartPoint.eulerAngles = ServiceProvider.Instance.PlayerAircraft.MainCockpitRotation;
        }
        else
        {
            StartPoint.eulerAngles = PlaneRotations[0];
        }

        Debug.Log("Playback started");
    }

    private void FltRec_Stop()
    {
        if (FRMode == FRModes.Inactive)
        {
            Debug.LogWarning("No function is currently active");
        }
        else if (FRMode == FRModes.Playback)
        {
            Debug.Log("Playback stopped");
        }
        else if (FRMode == FRModes.Record)
        {
            Debug.LogFormat("Recording stopped ({0} data points)", RecDataCount);
            ServiceProvider.Instance.GameWorld.ShowStatusMessage(string.Format("Recording stopped ({0} data points). Run 'FltRec_Save' to save recording. Data sets {1}", RecDataCount, LogDataSets(RecordMovement, RecordStandard, RecordExtra, RecordAdvanced)));
        }

        FRMode = FRModes.Inactive;
    }
    
    private void FltRec_Save(string filename)
    {
        if (!FilenameCheck.IsMatch(filename))
        {
            Debug.LogError("Invalid filename! Only use alphanumerics or underscore.");
            return;
        }
        else if (File.Exists(DirRecordings + filename + ".FRC"))
        {
            Debug.LogError("A file with the same name already exists in the recordings folder. Try again with a different file name.");
            return;
        }

        using (BinaryWriter writer = new BinaryWriter(File.Open(DirRecordings + filename + ".FRC", FileMode.Create)))
        {
            writer.Write("NACHSAVEFLTREC");
            writer.Write(DataPoints);
            writer.Write(RecordMovement);
            writer.Write(RecordStandard);
            writer.Write(RecordExtra);
            writer.Write(RecordAdvanced);

            for (int i = 0; i < DataPoints; i++)
            {
                writer.Write(TimeSinceStart[i]);

                if (RecordMovement)
                {
                    writer.Write(PlanePositions[i].x);
                    writer.Write(PlanePositions[i].y);
                    writer.Write(PlanePositions[i].z);
                    writer.Write(PlaneRotations[i].x);
                    writer.Write(PlaneRotations[i].y);
                    writer.Write(PlaneRotations[i].z);
                }

                if (RecordStandard)
                {
                    writer.Write(PlanePitch[i]);
                    writer.Write(PlaneRoll[i]);
                    writer.Write(PlaneYaw[i]);
                    writer.Write(PlaneThrottle[i]);
                    writer.Write(PlaneBrake[i]);
                }

                if (RecordExtra)
                {
                    writer.Write(PlaneTrim[i]);
                    writer.Write(PlaneVTOL[i]);
                    writer.Write(PlaneGearDown[i]);
                }

                if (RecordAdvanced)
                {
                    writer.Write(PlaneGuns[i]);
                    writer.Write(PlaneWeapons[i]);
                    writer.Write(PlaneCountermeasures[i]);
                    for (int j = 0; j < 8; j++)
                    {
                        writer.Write(PlaneAgs[i, j]);
                    }
                }
            }
        }

        Debug.Log("Recording saved as '" + filename + ".FRC' with data sets " + LogDataSets(RecordMovement, RecordStandard, RecordExtra, RecordAdvanced));
    }

    private void FltRec_Load(string filename)
    {
        if (!FilenameCheck.IsMatch(filename))
        {
            Debug.LogError("Invalid filename! Only use alphanumerics or underscore.");
            return;
        }
        else if (!File.Exists(DirRecordings + filename + ".FRC"))
        {
            Debug.LogError("The defined file does not exist.");
            return;
        }

        try
        {
            using (BinaryReader reader = new BinaryReader(File.Open(DirRecordings + filename + ".FRC", FileMode.Open)))
            {
                reader.ReadString();
                DataPoints = reader.ReadInt32();
                ResetBuffer(DataPoints);

                PlayMovement = reader.ReadBoolean();
                PlayStandard = reader.ReadBoolean();
                PlayExtra = reader.ReadBoolean();
                PlayAdvanced = reader.ReadBoolean();

                for (int i = 0; i < DataPoints; i++)
                {
                    TimeSinceStart[i] = reader.ReadSingle();

                    if (PlayMovement)
                    {
                        float px = reader.ReadSingle();
                        float py = reader.ReadSingle();
                        float pz = reader.ReadSingle();
                        PlanePositions[i] = new Vector3(px, py, pz);

                        float rx = reader.ReadSingle();
                        float ry = reader.ReadSingle();
                        float rz = reader.ReadSingle();
                        PlaneRotations[i] = new Vector3(rx, ry, rz);
                    }

                    if (PlayStandard)
                    {
                        PlanePitch[i] = reader.ReadSingle();
                        PlaneRoll[i] = reader.ReadSingle();
                        PlaneYaw[i] = reader.ReadSingle();
                        PlaneThrottle[i] = reader.ReadSingle();
                        PlaneBrake[i] = reader.ReadSingle();
                    }

                    if (PlayExtra)
                    {
                        PlaneTrim[i] = reader.ReadSingle();
                        PlaneVTOL[i] = reader.ReadSingle();
                        PlaneGearDown[i] = reader.ReadBoolean();
                    }

                    if (PlayAdvanced)
                    {
                        PlaneGuns[i] = reader.ReadBoolean();
                        PlaneWeapons[i] = reader.ReadBoolean();
                        PlaneCountermeasures[i] = reader.ReadBoolean();
                        for (int j = 0; j < 8; j++)
                        {
                            PlaneAgs[i, j] = reader.ReadBoolean();
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError(ex);
            ResetBuffer(0);
            return;
        }

        Debug.Log("Loaded '" + filename + ".FRC' with data sets " + LogDataSets(PlayMovement, PlayStandard, PlayExtra, PlayAdvanced));
    }

    private void FltRec_Preview()
    {
        if (DataPoints == 0 || !PlayMovement)
        {
            Debug.LogError("No movement information to preview.");
            return;
        }

        int LinePointIndex = 1;
        int VectorIndex = 0;
        float SimTime = 0;
        float EndTime = TimeSinceStart[DataPoints - 1];
        PreviewLine.positionCount = Mathf.CeilToInt(EndTime / LineTimeInterval);

        // Set the start point of the preview using playback settings
        if (PositionRelative)
        {
            PreviewLine.gameObject.transform.position = ServiceProvider.Instance.PlayerAircraft.MainCockpitPosition;
        }
        else
        {
            PreviewLine.gameObject.transform.position = PlanePositions[0] - ServiceProvider.Instance.GameWorld.FloatingOriginOffset;
        }

        if (RotationRelative)
        {
            PreviewLine.gameObject.transform.eulerAngles = ServiceProvider.Instance.PlayerAircraft.MainCockpitRotation;
        }
        else
        {
            PreviewLine.gameObject.transform.eulerAngles = PlaneRotations[0];
        }

        while (LinePointIndex < PreviewLine.positionCount)
        {
            SimTime += LineTimeInterval;

            float dpf = Mathf.Max(SeekDataPoint(TimeSinceStart, SimTime, VectorIndex), 1f);
            VectorIndex = Mathf.FloorToInt(dpf);

            PreviewLine.SetPosition(LinePointIndex, DataAtPoint(PlanePositions, dpf, false));

            LinePointIndex++;
        }

        PreviewLine.enabled = true;
        Debug.LogFormat("Successfully generated preview ({0} points)", PreviewLine.positionCount);
    }

    private void FltRec_TogglePreview()
    {
        if (ServiceProvider.Instance.GameState.IsInLevel)
        {
            PreviewLine.enabled = !PreviewLine.enabled;

            if (PreviewLine.enabled)
            {
                Debug.Log("Preview shown");
            }
            else
            {
                Debug.Log("Preview hidden");
            }
        }
        else
        {
            Debug.LogWarning("Can only show preview in map");
        }
    }

    private void FltRec_ClearSet(char set)
    {
        switch (set)
        {
            case 'M':
            case 'm':
                {
                    PlayMovement = false;
                    Debug.LogWarning("Deleted data set M (Movement)");
                    break;
                }
            case 'S':
            case 's':
                {
                    PlayStandard = false;
                    Debug.LogWarning("Deleted data set S (Standard)");
                    break;
                }
            case 'E':
            case 'e':
                {
                    PlayExtra = false;
                    Debug.LogWarning("Deleted data set E (Extra)");
                    break;
                }
            case 'A':
            case 'a':
                {
                    PlayAdvanced = false;
                    Debug.LogWarning("Deleted data set A (Advanced)");
                    break;
                }
            default:
                {
                    Debug.LogError("The entered character is invalid and does not correspond to any data set");
                    break;
                }
        }
    }

    private void FltRSet_RecordQuality(int size, int interval)
    {
        if (FRMode == FRModes.Record)
        {
            Debug.LogError("Cannot change recorder settings when recording.");
            return;
        }

        RecordingMaxSize = Math.Max(size, 1);
        RecordingInterval = Math.Max(interval, 1);

        float est = Time.fixedDeltaTime * RecordingMaxSize * RecordingInterval;

        Debug.LogFormat("Recorder quality settings applied (estimated maximum length {0:F0} seconds)", est);
    }

    private void FltRSet_RecordControls(bool m, bool s, bool e, bool a)
    {
        if (FRMode == FRModes.Record)
        {
            Debug.LogError("Cannot change recorder settings when recording.");
            return;
        }

        RecordMovement = m;
        RecordStandard = s;
        RecordExtra = e;
        RecordAdvanced = a;

        int est = 4 + (m ? 24 : 0) + (s ? 20 : 0) + (e ? 9 : 0) + (a ? 11 : 0);

        Debug.Log(string.Format("Recorder information settings applied ({0} bytes per recorded frame)", est));
    }

    private void FltRSet_PlaySpace(bool pos, bool rot)
    {
        if (FRMode == FRModes.Playback)
        {
            Debug.LogError("Cannot change playback settings when in playback mode.");
            return;
        }

        PositionRelative = pos;
        RotationRelative = rot;

        Debug.Log("Playback space settings applied");
    }

    private void FltRSet_PreviewColor(float r, float g, float b, float a)
    {
        ColorR = r;
        ColorG = g;
        ColorB = b;
        ColorA = a;

        PreviewLine.startColor = new Color(r, g, b, a);
        PreviewLine.endColor = new Color(r, g, b, a);
        Debug.Log("Preview color settings applied");
    }

    private void FltRSet_PreviewQuality(float width, float interval, int cvert)
    {
        LineWidth = width;
        LineTimeInterval = interval;
        LineCornerVertices = Math.Max(cvert, 0);

        PreviewLine.widthMultiplier = LineWidth;
        PreviewLine.numCornerVertices = LineCornerVertices;
        Debug.Log("Preview quality settings applied");
    }

    // Returns the theoretical floating point index of 'times' that corresponds to 'current'
    private float SeekDataPoint(float[] times, float current, int cache = 0)
    {
        int index = 0;
        float progress;

        // Look for the two values of 'times' surrounding 'current'
        for (int i = cache; i < times.Length - 1; i++)
        {
            if (times[i] <= current && current < times[i+1])
            {
                index = i;
                break;
            }
        }

        progress = Mathf.InverseLerp(times[index], times[index + 1], current);

        return index + progress;
    }

    private Vector3 DataAtPoint(Vector3[] data, float indexf, bool anglemode)
    {
        float lerpamount = Mathf.Repeat(indexf, 1);

        if (anglemode)
        {
            float rx = Mathf.LerpAngle(data[Mathf.FloorToInt(indexf)].x, data[Mathf.CeilToInt(indexf)].x, lerpamount);
            float ry = Mathf.LerpAngle(data[Mathf.FloorToInt(indexf)].y, data[Mathf.CeilToInt(indexf)].y, lerpamount);
            float rz = Mathf.LerpAngle(data[Mathf.FloorToInt(indexf)].z, data[Mathf.CeilToInt(indexf)].z, lerpamount);
            return new Vector3(rx, ry, rz);
        }
        else
        {
            return Vector3.Lerp(data[Mathf.FloorToInt(indexf)], data[Mathf.CeilToInt(indexf)], lerpamount);
        }
    }

    private float DataAtPoint(float[] data, float indexf)
    {
        return Mathf.Lerp(data[Mathf.FloorToInt(indexf)], data[Mathf.CeilToInt(indexf)], Mathf.Repeat(indexf, 1));
    }

    private string LogDataSets(bool m, bool s, bool e, bool a)
    {
        int SetCount = 0;
        string SetString = string.Empty;

        if (m)
        {
            SetCount++;
            SetString += "M";
        }

        if (s)
        {
            if (SetCount > 0)
            {
                SetString += ", ";
            }
            SetCount++;
            SetString += "S";
        }

        if (e)
        {
            if (SetCount > 0)
            {
                SetString += ", ";
            }
            SetCount++;
            SetString += "E";
        }

        if (a)
        {
            if (SetCount > 0)
            {
                SetString += ", ";
            }
            SetString += "A";
        }

        return SetString;
    }

    // Resets variables and prepares arrays
    private void ResetBuffer(int size)
    {
        DataPoints = size;
        StartTime = Time.timeSinceLevelLoad;

        TimeSinceStart = new float[size];
        PlanePositions = new Vector3[size];
        PlaneRotations = new Vector3[size];
        PlanePitch = new float[size];
        PlaneRoll = new float[size];
        PlaneYaw = new float[size];
        PlaneThrottle = new float[size];
        PlaneBrake = new float[size];
        PlaneTrim = new float[size];
        PlaneVTOL = new float[size];
        PlaneGearDown = new bool[size];
        PlaneGuns = new bool[size];
        PlaneWeapons = new bool[size];
        PlaneCountermeasures = new bool[size];
        PlaneAgs = new bool[size, 8];
    }

    private void OnApplicationQuit()
    {
        // Save settings file
        using (BinaryWriter writer = new BinaryWriter(File.Open(SettingsPath, FileMode.Create)))
        {
            writer.Write("NACHSAVEFLTRSET");

            writer.Write(RecordingMaxSize);
            writer.Write(RecordingInterval);
            writer.Write(RecordMovement);
            writer.Write(RecordStandard);
            writer.Write(RecordExtra);
            writer.Write(RecordAdvanced);

            writer.Write(PositionRelative);
            writer.Write(RotationRelative);

            writer.Write(ColorR);
            writer.Write(ColorG);
            writer.Write(ColorB);
            writer.Write(ColorA);
            writer.Write(LineWidth);
            writer.Write(LineTimeInterval);
            writer.Write(LineCornerVertices);
        }
    }
}
