using System;
using System.Collections.Generic;
using System.Text;

namespace FltScr
{
    public class NewRecording
    {
        static FSFunctions FSFunction = new FSFunctions();

        // Only change inside this code block
        public void NewRecordingData()
        {
            // Choose the data sets to include in the recording
            FSFunction.SelectDataSets(false, true, true, true);
            // Count the number of NextFrame calls to get the buffer size
            FSFunction.SetBufferSize(7);

            // Starting values can be defined before the starting frame (index 0)
            FSFunction.WriteActivationGroup(6, true);
            FSFunction.NextFrame(0f);

            // Wait 4 seconds
            // By calling the next frame without writing any control values, the state of the previous frame carries over
            FSFunction.NextFrame(4f);

            // Increase Throttle to 1 over 2 seconds
            FSFunction.WriteValue(FSFunctions.FloatControls.Throttle, 1f);
            FSFunction.NextFrame(2f);

            // Wait 4 seconds
            FSFunction.NextFrame(4f);

            // Decrease Pitch to -1 over 2 seconds
            FSFunction.WriteValue(FSFunctions.FloatControls.Pitch, -1f);
            FSFunction.NextFrame(2f);

            // Increase Pitch back to 0, and retract landing gear
            FSFunction.WriteValue(FSFunctions.FloatControls.Pitch, 0f);
            FSFunction.WriteValue(FSFunctions.BoolControls.GearDown, false);
            // Uses the default value, delay = 1
            FSFunction.NextFrame();

            // Activate AG7
            FSFunction.WriteActivationGroup(7, true);
            FSFunction.NextFrame();

            // Notice: The game will report a few more action frames than you write.
            // This is because a few frames are added to the end to ensure that your last written frame will be played.

            // Save by providing a string (only alphanumeric and underscore)
            FSFunction.Save("EXAMPLE");
        }
    }
}
