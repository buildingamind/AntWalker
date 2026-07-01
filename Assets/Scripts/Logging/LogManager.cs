using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;
using TMPro;

[System.Flags]
public enum AttributeEnum : int
{
    xPosition = 0x01,
    yPosition = 0x02,
    zPosition = 0x04,
    xAngle = 0x08,
    yAngle = 0x10,
    zAngle = 0x20,
}

public class LogManager : MonoBehaviour
{
    [Header("[?] Which Objects are to be Logged?")]
    [Tooltip("GameObjects whose attributes will be logged.")]
    [ReadOnly]
    public List<GameObject> loggedObjects;

    [Header("[?] Which Attributes of these Objects are to be logged?")]
    [Tooltip("Which attributes of the object to log.")]
    public AttributeEnum objectAttributes;

    [Header("[?] How frequently should the Cameras be Sampled? (in Steps)")]
    [Tooltip("Record from the camera every number of frames. 0 = Never. 1 = All frames. 2 = every other frame. 5 = every fifth frame, etc.")]
    public int cameraFrequency = 0;

    [Header("[?] What is the maximum number of images to take from each camera? (0 = All)")]
    [Tooltip("This is meant to prevent data explosion on long runs, yet allow for early camera debugging.")]
    public int maximumCaptures = 100;

    [Header("[?] What is the maximum number of episodes from which to record? (0 = All)")]
    [Tooltip("This allows you to choose a certain number of trails after which the logging will finish")]
    public int maximumEpisodes;

    [Header("[?] What is the maximum number of logs of which to record? (0 = All)")]
    [Tooltip("This allows you to choose a certain number of steps after which the logging will finish")]
    public int maximumEntries;

    [Header("[?] Which Cameras will be Sampled?")]
    [Tooltip("Cameras which will be rendered. These cameras must have Render Textures.")]
    public List<Camera> loggedCameras;

    public GameObject referenceObject;

    // public int objectLogFrequency = 1;

    StreamWriter logWriter;
    string objectPath;
    string framesPath;
    int frame;

    [ReadOnly]
    public int entryCount;
    [ReadOnly]
    public int captureCount;
    [ReadOnly]
    public int currentEpisode;
    [ReadOnly]
    public int currentStep;


    public string runID;
    public string logPath;

    public TextMeshProUGUI episodeMonitor;

    private List<GameObject> headerObjects;

    private void Awake()
    {
        loggedObjects = new();
        headerObjects = new();
    }

    void Start()
    {
        /*if (!Application.isEditor)  // We won't log from the editor
        {*/
            if (runID.Equals(string.Empty))
            {
                runID = ArgumentParser.Options.runID;
            }
            
            /*cameraFrequency = ArgumentParser.Options.cameraFrequency;*/

            // Create Logging Directory
            Directory.CreateDirectory(string.Format("{0}/Logs/{1}", Application.dataPath, runID));
            logPath = string.Format("{0}/Logs/{1}", Application.dataPath, runID);

            if (loggedObjects.Count > 0)
            {
                objectPath = string.Format("{0}/Objects", logPath);
                Directory.CreateDirectory(objectPath);
            }

            if (loggedCameras.Count > 0 && cameraFrequency > 0)
            {

                framesPath = string.Format("{0}/Frames", logPath);
                foreach (Camera cam in loggedCameras)
                {
                    Directory.CreateDirectory(string.Format("{0}/{1}", framesPath, cam.name));
                }
            }
            logWriter = new StreamWriter(string.Format("{0}/{1}.csv", objectPath, runID), false);

            WriteHeaders();
        /*}*/
    }

    void WriteHeaders()
    {
        // Write the headers to the log file

        string header = "episode,step,";
        foreach (GameObject obj in loggedObjects)
        {
            if (obj is not null)
            {
                if (objectAttributes.HasFlag(AttributeEnum.xPosition))  { header += $"{obj.name}.xPosition,"; }
                if (objectAttributes.HasFlag(AttributeEnum.yPosition))  { header += $"{obj.name}.yPosition,"; }
                if (objectAttributes.HasFlag(AttributeEnum.zPosition))  { header += $"{obj.name}.zPosition,"; }
                if (objectAttributes.HasFlag(AttributeEnum.xAngle))     { header += $"{obj.name}.xAngle,"; }
                if (objectAttributes.HasFlag(AttributeEnum.yAngle))     { header += $"{obj.name}.yAngle,"; }
                if (objectAttributes.HasFlag(AttributeEnum.zAngle))     { header += $"{obj.name}.zAngle,"; }
            }
            headerObjects.Add(obj); // We can use this to keep track of the original agents
        }
        Write(header.ToLower());
    }

    public static void Quit()
    {
    #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
    #else
        Application.Quit();
    #endif
    }

    public void AddEntry(int episode, int step)
    {
        if (!this.gameObject.activeSelf)
        {
            return;
        }

        if (episode == currentEpisode && step == currentStep)
        {
            return;
        }

        if (episode > maximumEpisodes && maximumEpisodes > 0)
        {
            Quit();
            return;
        }

        if (entryCount > maximumEntries && maximumEntries > 0)
        {
            Quit();
            return;
        }

        // Prevent the logger from double logging
        currentEpisode = episode;
        currentStep = step;
        if (episodeMonitor is not null)
        {
            episodeMonitor.text = string.Format("Episode: {0} : Step {1}", episode, step);
        }

        // Collect the attribute data. 
        // Include any other attributes you want to collect in this method.
        // Also include the metric in the objectAttribute enum so you can select it.

        string rowLog = string.Format("{0},{1},", episode, step);
        foreach (GameObject obj in loggedObjects)
        {
            if (obj is null || !headerObjects.Contains(obj)) { continue; }

            if (objectAttributes.HasFlag(AttributeEnum.xPosition))
            {
                if (referenceObject is not null)
                {
                    rowLog += (obj.transform.position.x - referenceObject.transform.position.x) + ",";
                }
                else
                {
                    rowLog += obj.transform.position.x + ",";
                }
            }
            if (objectAttributes.HasFlag(AttributeEnum.yPosition))
            {
                if (referenceObject is not null)
                {
                    rowLog += (obj.transform.position.y - referenceObject.transform.position.y) + ",";
                }
                else
                {
                    rowLog += obj.transform.position.y + ",";
                }
            }
            if (objectAttributes.HasFlag(AttributeEnum.zPosition))
            {
                if (referenceObject is not null)
                {
                    rowLog += (obj.transform.position.z - referenceObject.transform.position.z) + ",";
                }
                else
                {
                    rowLog += obj.transform.position.z + ",";
                }
            }
            if (objectAttributes.HasFlag(AttributeEnum.xAngle))
            {
                if (referenceObject is not null)
                {
                    rowLog += (obj.transform.rotation.eulerAngles.x - referenceObject.transform.rotation.eulerAngles.x) % 360 + ",";
                }
                else
                {
                    rowLog += obj.transform.rotation.eulerAngles.x % 360 + ",";
                }
            }
            if (objectAttributes.HasFlag(AttributeEnum.yAngle))
            {
                if (referenceObject is not null)
                {
                    rowLog += (obj.transform.rotation.eulerAngles.y - referenceObject.transform.rotation.eulerAngles.y) % 360 + ",";
                }
                else
                {
                    rowLog += obj.transform.rotation.eulerAngles.y % 360 + ",";
                }
            }
            if (objectAttributes.HasFlag(AttributeEnum.zAngle))
            {
                if (referenceObject is not null)
                {
                    rowLog += (obj.transform.rotation.eulerAngles.z - referenceObject.transform.rotation.eulerAngles.z) % 360 + ",";
                }
                else
                {
                    rowLog += obj.transform.rotation.eulerAngles.z % 360 + ",";
                }
            }
        }
        if (!rowLog.Equals(""))
        {
            Write(rowLog.ToLower());
        }

        if (cameraFrequency > 0 && (frame % cameraFrequency == 0) && (captureCount < maximumCaptures || maximumCaptures <= 0))
        {
            foreach (Camera cam in loggedCameras)
            {
                CaptureCamera(cam, episode, step);
            }
            captureCount++;
        }
        frame++;
        entryCount++;
    }

    void CaptureCamera(Camera cam, int episode, int step)
    {
        if (cam.targetTexture != null)
        {
            RenderTexture currentRT = RenderTexture.active;
            RenderTexture.active = cam.targetTexture;

            cam.Render();
            Texture2D Image = new Texture2D(cam.targetTexture.width, cam.targetTexture.height);
            Image.ReadPixels(new Rect(0, 0, cam.targetTexture.width, cam.targetTexture.height), 0, 0);
            Image.Apply();
            RenderTexture.active = currentRT;

            var Bytes = Image.EncodeToPNG();
            Destroy(Image);

            File.WriteAllBytes(string.Format("{0}/{1}/{2}_{3}.png", framesPath, cam.name, episode.ToString("0000"), step.ToString("0000")), Bytes);
        }
    }

    void Write(string line)
    {
        if (logWriter == null)
        {
            return;
        }
        logWriter.WriteLine(line);
        logWriter.Flush();
    }
}
