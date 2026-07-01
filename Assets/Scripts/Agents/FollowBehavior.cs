using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FollowBehavior : MonoBehaviour
{
    public GameObject target; // The target object to follow
    public List<Vector3> path = new List<Vector3>(); // The path to follow, initialized
    public float pathAddFrequency = 0.5f; // Frequency of adding new path points
    private NavMeshAgent agent; // The NavMeshAgent component
    public float randomSpawnRange = 5f; // Range for random spawn position
    public float randomNoise = 1f; // Random noise to add to target position

    // Start is called before the first frame update
    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        // Randomize the Priority of the NavMeshAgent
        agent.avoidancePriority = Random.Range(50, 100);
        // Randomize the Start Position of the NavMeshAgent
        this.transform.position += new Vector3(Random.Range(-randomSpawnRange, randomSpawnRange), 0, Random.Range(-randomSpawnRange, randomSpawnRange));

        if (target == null)
        {
            // "target_01" should target "target_00" and "target_02" should target "target_01"
            string targetName = this.gameObject.name;
            int targetIndex = int.Parse(targetName.Split('_')[1]);
            // Convert the targetIndex to a 2 digit string
            targetName = targetName.Split('_')[0] + "_" + (targetIndex - 1).ToString("00");
            GameObject targetObject = GameObject.Find(targetName); 
            if (targetObject != null)
            {
                target = targetObject;
            }
            else
            {
                Debug.LogWarning("Target not found: " + targetName);
            }
        }

        if (target != null && pathAddFrequency > 0)
        {
            StartCoroutine(RecordPathRoutine());
        }
    }

    IEnumerator RecordPathRoutine()
    {
        while (true)
        {
            if (target != null)
            {
                float randomNoiseX = Random.Range(-randomNoise, randomNoise);
                float randomNoiseZ = Random.Range(-randomNoise, randomNoise);
                // Add the target's position with a small random noise to the path
                path.Add(target.transform.position + new Vector3(randomNoiseX, 0, randomNoiseZ));
            }
            yield return new WaitForSeconds(pathAddFrequency);
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (agent == null || !agent.isOnNavMesh)
        {
            return;
        }

        if (path.Count > 0)
        {
            // Check if the agent is ready for a new destination:
            // - Not currently calculating a path (!agent.pathPending)
            // - AND (EITHER it has no path OR it has reached its current destination)
            if (!agent.pathPending && (!agent.hasPath || agent.remainingDistance <= agent.stoppingDistance))
            {
                agent.SetDestination(path[0]);
                path.RemoveAt(0);
            }
        }
    }
}
