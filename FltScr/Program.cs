using System;
using System.Collections.Generic;
using System.Text;

namespace FltScr
{
    class Program
    {
        static NewRecording NewRecording = new NewRecording();
        static FSFunctions FSFunction = new FSFunctions();

        static void Main(string[] args)
        {
            Console.WriteLine("Attempting to load recording script...");

            try
            {
                NewRecording.NewRecordingData();
            }
            catch (Exception ex)
            {
                Console.WriteLine("An exception has occured while building: " + ex);
                return;
            }
        }
    }
}
