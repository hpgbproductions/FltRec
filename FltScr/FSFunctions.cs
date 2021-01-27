using System;
using System.IO;
using System.Text.RegularExpressions;

namespace FltScr
{
    public class FSFunctions
    {
        static string DirRecordings = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

        // Enums are used in function arguments
        public enum Vector3 { x, y, z }
        public enum ControlTypes { Vector3, Floating, Boolean }
        public enum Vector3Controls { Position, Rotation }
        public enum FloatControls { Pitch, Roll, Yaw, Throttle, Brake, Trim, VTOL }
        public enum BoolControls { GearDown, Guns, Weapons, Countermeasures }

        // Flags for different data sets
        bool RecordMovement = false;
        bool RecordStandard = true;
        bool RecordExtra = true;
        bool RecordAdvanced = true;

        // Overall storage
        int DataPoints = 0;          // Array length of loaded information
        float[] TimeSinceStart;
        float[,] PlanePositions;     // Store Vector3 as a two-dimensional array
        float[,] PlaneRotations;
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
        bool[,] PlaneAgs;              // Store activation group values as a two-dimensional array

        // Frame state storage
        float CurrentTime = 0f;
        float[] CurrentPosition = new float[3] { 0f, 0f, 0f };
        float[] CurrentRotation = new float[3] { 0f, 0f, 0f };
        float CurrentPitch = 0f;
        float CurrentRoll = 0f;
        float CurrentYaw = 0f;
        float CurrentThrottle = 0f;
        float CurrentBrake = 0f;
        float CurrentTrim = 0f;
        float CurrentVTOL = 0f;
        bool CurrentGearDown = true;
        bool CurrentGuns = false;
        bool CurrentWeapons = false;
        bool CurrentCountermeasures = false;
        bool[] CurrentAgs = new bool[8] { false, false, false, false, false, false, false, true };

        static string CurrentException = string.Empty;
        static readonly Regex FilenameCheck = new Regex(@"^[0-9A-Za-z_]+$");

        /// <summary>
        /// Choose which data sets will be exported.
        /// </summary>
        /// <param name="m">Movement. Handles position and rotation. Note that the first entry is in world space, and the rest of the entries are relative to the first.</param>
        /// <param name="s">Standard. Handles normal control axes, throttle, and brake.</param>
        /// <param name="e">Extra. Handles Trim, VTOL, and landing gear.</param>
        /// <param name="a">Advanced. Handles weapons and activation groups.</param>
        public void SelectDataSets(bool m = false, bool s = true, bool e = true, bool a = true)
        {
            RecordMovement = m;
            RecordStandard = s;
            RecordExtra = e;
            RecordAdvanced = a;
        }

        /// <summary>
        /// Set the maximum number of recorded frames allowed by the program. This should be exactly the number of frames defined, and never less.
        /// </summary>
        /// <param name="size">Number of frames.</param>
        public void SetBufferSize(int size)
        {
            size += 2;
            TimeSinceStart = new float[size];
            PlanePositions = new float[size, 3];
            PlaneRotations = new float[size, 3];
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

        /// <summary>
        /// Write controls to the current frame.
        /// </summary>
        /// <param name="control">Information to change.</param>
        /// <param name="x">X-component of the Unity Vector3.</param>
        /// <param name="y">Y-component of the Unity Vector3.</param>
        /// <param name="z">Z-component of the Unity Vector3.</param>
        public void WriteValue(Vector3Controls control, float x, float y, float z)
        {
            if (control == Vector3Controls.Position) CurrentPosition = new float[3] { x, y, z };
            else if (control == Vector3Controls.Rotation) CurrentRotation = new float[3] { x, y, z };
        }
        /// <summary>
        /// Write controls to the current frame.
        /// </summary>
        /// <param name="control">Information to change.</param>
        /// <param name="value">The value to change to. Minimum of -1 or 0 depending on variable, maximum of 1 for all variables.</param>
        public void WriteValue(FloatControls control, float value)
        {
            if (control == FloatControls.Pitch) CurrentPitch = value;
            else if (control == FloatControls.Roll) CurrentRoll = value;
            else if (control == FloatControls.Yaw) CurrentYaw = value;
            else if (control == FloatControls.Throttle) CurrentThrottle = value;
            else if (control == FloatControls.Brake) CurrentBrake = value;
            else if (control == FloatControls.Trim) CurrentTrim = value;
            else if (control == FloatControls.VTOL) CurrentVTOL = value;
        }
        /// <summary>
        /// Write controls to the current frame.
        /// </summary>
        /// <param name="control">Information to change.</param>
        /// <param name="value">The value to change to. May be either true or false.</param>
        public void WriteValue(BoolControls control, bool value)
        {
            if (control == BoolControls.GearDown) CurrentGearDown = value;
            else if (control == BoolControls.Guns) CurrentGuns = value;
            else if (control == BoolControls.Weapons) CurrentWeapons = value;
            else if (control == BoolControls.Countermeasures) CurrentCountermeasures = value;
        }

        /// <summary>
        /// Write an activation group value to the current frame.
        /// </summary>
        /// <param name="ag">Activation group from 1 to 8, inclusive.</param>
        /// <param name="value">True to activate, or false to deactivate.</param>
        public void WriteActivationGroup(int ag, bool value)
        {
            if (ag < 1 || ag > 8)
            {
                Console.WriteLine("[WriteActivationGroup] Error: The activation group number specified was out of bounds");
                return;
            }
            CurrentAgs[ag - 1] = value;
        }

        /// <summary>
        /// Advance to the next frame.
        /// </summary>
        /// <param name="delay">Time to the next frame.</param>
        public void NextFrame(float delay = 1f)
        {
            if (delay < 0f)
            {
                delay = 0f;
                Console.WriteLine("[NextFrame] Warning: Delay cannot be negative. Automatically set to zero.");
            }

            int i = DataPoints;

            // Apply values
            TimeSinceStart[i] = CurrentTime;
            PlanePositions[i, 0] = CurrentPosition[0];
            PlanePositions[i, 1] = CurrentPosition[1];
            PlanePositions[i, 2] = CurrentPosition[2];
            PlaneRotations[i, 0] = CurrentRotation[0];
            PlaneRotations[i, 1] = CurrentRotation[1];
            PlaneRotations[i, 2] = CurrentRotation[2];
            PlanePitch[i] = CurrentPitch;
            PlaneRoll[i] = CurrentRoll;
            PlaneYaw[i] = CurrentYaw;
            PlaneThrottle[i] = CurrentThrottle;
            PlaneBrake[i] = CurrentBrake;
            PlaneTrim[i] = CurrentTrim;
            PlaneVTOL[i] = CurrentVTOL;
            PlaneGearDown[i] = CurrentGearDown;
            PlaneGuns[i] = CurrentGuns;
            PlaneWeapons[i] = CurrentWeapons;
            PlaneCountermeasures[i] = CurrentCountermeasures;

            for (int j = 0; j < 8; j++)
            {
                PlaneAgs[i, j] = CurrentAgs[j];
            }

            // Prepare for next frame
            CurrentTime += delay;
            DataPoints++;
        }

        public void Save(string filename)
        {
            if (!FilenameCheck.IsMatch(filename))
            {
                Console.WriteLine("[Save] Error: Invalid filename! Only use alphanumerics or underscore.");
                return;
            }
            else if (File.Exists(Path.Combine(DirRecordings, filename + ".FRC")))
            {
                Console.WriteLine("[Save] Error: A file with the same name already exists in the folder. Try again with a different file name.");
                return;
            }

            // Extra frames are written to allow proper ending of the file
            NextFrame(0.01f);
            NextFrame(0.01f);

            using (BinaryWriter writer = new BinaryWriter(File.Open(Path.Combine(DirRecordings, filename + ".FRC"), FileMode.Create)))
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
                        writer.Write(PlanePositions[i, 0]);
                        writer.Write(PlanePositions[i, 1]);
                        writer.Write(PlanePositions[i, 2]);
                        writer.Write(PlaneRotations[i, 0]);
                        writer.Write(PlaneRotations[i, 1]);
                        writer.Write(PlaneRotations[i, 2]);
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

            Console.WriteLine("Recording saved as '" + filename + ".FRC' with data sets " + LogDataSets(RecordMovement, RecordStandard, RecordExtra, RecordAdvanced));
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
    }
}
