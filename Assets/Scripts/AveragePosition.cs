using UnityEngine;
using System.Collections.Generic;

public class AveragePosition : MonoBehaviour
{
    public LogManager logManager;

    void Update()
    {
        Vector3 totalPosition = Vector3.zero;
        Vector3 totalRotation = Vector3.zero;

        // Sum up the positions of all gameobjects in the list
        foreach (GameObject obj in logManager.loggedObjects)
        {
            totalPosition += obj.transform.position;
            totalRotation += obj.transform.forward;
        }

        // Divide the total position by the number of gameobjects to get the average position
        Vector3 averagePosition = totalPosition / logManager.loggedObjects.Count;
        Vector3 averageRotation = totalRotation.normalized;

        transform.position = averagePosition;
        transform.LookAt(transform.position + averageRotation);
    }
}
