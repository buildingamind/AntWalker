using System.Collections;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;

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

    [Header("[?] How frequently should Objects be Sampled? (in Frames)")]
    [Tooltip("Record objects every number of frames. 0 = Driven by Agent. 1 = All frames. 2 = every other frame, etc.")]
    public int objectLogFrequency = 0;
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
    [Tooltip("Append the current date/time to the Run ID, so consecutive runs get their own log folder instead of overwriting each other's.")]
    public bool timestampRunID = false;

    private List<GameObject> headerObjects;

    private void Awake()
    {
        if (loggedObjects == null)
        {
            loggedObjects = new List<GameObject>();
        }
        headerObjects = new();
    }

#if UNITY_EDITOR
    public void RefreshLoggedObjects()
    {
        if (Application.isPlaying) return;

        // Automatically collect LogMe objects in the scene at edit-time
        LogMe[] loggers = FindObjectsOfType<LogMe>();
        
        if (loggedObjects == null)
        {
            loggedObjects = new List<GameObject>();
        }
        else
        {
            loggedObjects.Clear();
        }

        foreach (LogMe logger in loggers)
        {
            if (logger != null && logger.gameObject != null && logger.enabled)
            {
                // Only collect it if it's unassigned or assigned to this specific manager
                if (logger.logManager == this || logger.logManager == null)
                {
                    if (!loggedObjects.Contains(logger.gameObject))
                    {
                        loggedObjects.Add(logger.gameObject);
                    }
                    
                    if (logger.logManager == null)
                    {
                        logger.logManager = this;
                        UnityEditor.EditorUtility.SetDirty(logger);
                    }
                }
            }
        }
    }

    private void OnValidate()
    {
        RefreshLoggedObjects();
    }
#endif

    private void FixedUpdate()
    {
        if (objectLogFrequency > 0 && (Time.frameCount % objectLogFrequency == 0))
        {
            // Auto-increment the step if it's being driven automatically
            AddEntry(currentEpisode, currentStep + 1);
        }
    }

    void Start()
    {
        /*if (!Application.isEditor)  // We won't log from the editor
        {*/
            if (runID.Equals(string.Empty))
            {
                runID = ArgumentParser.Options.runID;
            }

            if (timestampRunID)
            {
                runID += "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            }

            /*cameraFrequency = ArgumentParser.Options.cameraFrequency;*/

            // Create Logging Directory
            if (string.IsNullOrEmpty(logPath))
            {
                logPath = string.Format("{0}/Logs/{1}", Application.dataPath, runID);
            }
            else
            {
                // Ensure the path uses the runID if it doesn't already
                if (!logPath.EndsWith(runID))
                {
                    logPath = string.Format("{0}/{1}", logPath.TrimEnd('/'), runID);
                }
            }
            Directory.CreateDirectory(logPath);

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

        // Collect the attribute data. 
        // Include any other attributes you want to collect in this method.
        // Also include the metric in the objectAttribute enum so you can select it.

        string rowLog = string.Format("{0},{1},", episode, step);
        foreach (GameObject obj in headerObjects)
        {
            LogMe logMe = obj != null ? obj.GetComponent<LogMe>() : null;
            bool isLogging = obj != null && loggedObjects.Contains(obj) && obj.activeInHierarchy && (logMe == null || logMe.enabled);

            if (!isLogging) 
            { 
                // Write empty commas to preserve CSV column alignment
                if (objectAttributes.HasFlag(AttributeEnum.xPosition)) rowLog += ",";
                if (objectAttributes.HasFlag(AttributeEnum.yPosition)) rowLog += ",";
                if (objectAttributes.HasFlag(AttributeEnum.zPosition)) rowLog += ",";
                if (objectAttributes.HasFlag(AttributeEnum.xAngle)) rowLog += ",";
                if (objectAttributes.HasFlag(AttributeEnum.yAngle)) rowLog += ",";
                if (objectAttributes.HasFlag(AttributeEnum.zAngle)) rowLog += ",";
                continue; 
            }

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
