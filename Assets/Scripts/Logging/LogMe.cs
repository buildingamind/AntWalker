using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogMe : MonoBehaviour
{
    public LogManager logManager;

    void OnEnable()
    {
        if (logManager != null && !logManager.loggedObjects.Contains(this.gameObject))
        {
            logManager.loggedObjects.Add(this.gameObject);
        }

        // Maybe this is attached to an Animal Agent, in which case we should just grab
        // the logger from the agent itself.
        AnimalAgent animalAgent = GetComponent<AnimalAgent>();
        if (animalAgent is not null)
        {
            // Here we know we have an animal Agent
            if (animalAgent.logger is not null)
            {
                // Just grab the logger from the agent (Agent logger takes priority)
                logManager = animalAgent.logger;
            }
            else
            {
                // But if the agent does not have it assigned, we can set it for the agent
                animalAgent.logger = logManager;
            }
        }
    }

    void OnDisable()
    {
        if (logManager != null && logManager.loggedObjects.Contains(this.gameObject))
        {
            logManager.loggedObjects.Remove(this.gameObject);
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (Application.isPlaying) return;

        if (logManager != null)
        {
            logManager.RefreshLoggedObjects();
        }
        else
        {
            LogManager manager = FindObjectOfType<LogManager>();
            if (manager != null)
            {
                manager.RefreshLoggedObjects();
            }
        }
    }
#endif
}
