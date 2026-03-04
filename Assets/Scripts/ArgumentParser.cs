using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mono.Options;

public static class ArgumentParser
{
    public class CommandLineOptions
    {
        public int cameraResolution;        // Camera Sensor Resolution
        public int episodeSteps;            // Number of steps per episode.
        public int cameraFrequency;         // Number of steps for frame captures
        public int fov;                     // Agent FoV (0-180)
        public int timescale;               // How fast to run the Unity environment
        public string runID;                // Run Identifier
        public bool randomAgent;            // Run a Random Agent
        public bool testMode;               // Running the model in test mode
        public bool noStimulus;             // Running the model in test mode
        public bool enableAgentInfoChannel; // Enable Agent Info Channel
        public bool performanceMode;        // Whether to use Performance Mode
        public bool borderlessMode;         // Whether to use Borderless Mode
        public bool phyiscalMode;           // Whether to use Performance Mode
        public int displays;                // Display Sets (1 = Single Display, 2 = Opposite Displays, 4 = All)
    }

    private static CommandLineOptions options;
    public static CommandLineOptions Options
    {
        get
        {
            // Parse command line when this property is accessed for the first time.
            if (options == null) ParseCommandLineArgs();
            return options;
        }
    }

    private static void ParseCommandLineArgs()
    {
        var args = System.Environment.GetCommandLineArgs();

        var parser = new OptionSet() {
            // Required Options
            {"steps=", "number of steps per episode.",
                (int v) => options.episodeSteps = v},
            {"resolution=", "camera resolution from the camera sensor.",
                (int v) => options.cameraResolution = v},
            {"cam-frequency=", "number of steps between recording a frame.",
                (int v) => options.cameraFrequency = v},
            {"displays=", "number of displays to activate.",
                (int v) => options.displays = v},
            {"fov=", "number of displays to activate.",
                (int v) => options.fov = v},
            {"timescale=", "number of displays to activate.",
                (int v) => options.timescale = v},
            {"id=", "Run Identifier.",
                (string v) => options.runID = v},

            // Optional Agent Type Constraints
            {"random", "enable Random Agent mode.",
                v => options.randomAgent = v != null},
            {"nostim", "disable any Experimental Object Stimuli (Environment Only).",
                v => options.noStimulus = v != null},
            {"test", "run in Agent Testing mode.",
                v => options.testMode = v != null},
            {"performance", "run in Performance mode.",
                v => options.performanceMode = v != null},
            {"physical", "run in Physical mode.",
                v => options.phyiscalMode = v != null},
            {"borderless", "run in Borderless mode.",
                v => options.borderlessMode = v != null},

            // Python Process
            {"enable-info", "enable agentInfoChannel in ChickAgent Script.",
                v => options.enableAgentInfoChannel = v != null},

        };

        options = new CommandLineOptions();
        try
        {
            parser.Parse(args);
        }
        catch (OptionException e)
        {
            Debug.Log("OptionException: " + e.ToString());
        }
    }
}
