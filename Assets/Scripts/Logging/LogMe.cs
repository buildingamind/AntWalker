using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LogMe : MonoBehaviour
{
    public LogManager logManager;

    // Start is called before the first frame update
    void Start()
    {
        if (!logManager.loggedObjects.Contains(this.gameObject))
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

    void OnDestroy()
    {
        if (logManager.loggedObjects.Contains(this.gameObject))
        {
            logManager.loggedObjects.Remove(this.gameObject);
        }
    }
}
