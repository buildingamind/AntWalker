using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class TimescaleFix : MonoBehaviour
{
    NavMeshAgent navMeshAgent;

    private void Start()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (Time.timeScale > 1.0f)
        {
            CheckSteeringTargetPosition();
        }
    }

    protected float _distanceStearingTarget;

    protected void CheckSteeringTargetPosition()
    {
        float distanceST = Vector3.Distance(navMeshAgent.transform.position, navMeshAgent.steeringTarget);
        if (distanceST <= 15) //distance to next edge on nav mesh
        {
            if (_distanceStearingTarget < distanceST)
            {
                navMeshAgent.transform.position = navMeshAgent.steeringTarget;
            }
            else
            {
                _distanceStearingTarget = distanceST;
            }
        }
    }
}
