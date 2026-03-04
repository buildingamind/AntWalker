using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.SideChannels;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;


public class AnimalAgent : Agent
{
    [Tooltip("Distance the agent travels per unit time.")]
    public float MoveSpeed;

    [Tooltip("Angle of rotation per unit time.")]
    public float RotateSpeed;
    public float RotateLerp = 0.1f; 

    [Tooltip("Cost of moving forward.")]
    public float ForwardCost;

    [Tooltip("Cost of moving backward.")]
    public float BackwardCost;

    [Tooltip("Cost of rotation.")]
    public float RotateCost;

    [Tooltip("If true, The agent will always make random actions.")]
    public bool RandomAgent;


    public bool ForceForwardAction;
    public bool RestrainMove;
    public bool RestrainTurn;
    public bool RestrainDive;
    // public bool RestrainLean;

    public Transform headRoot;

    /*int HeadLeanValue = 0;
    int headTiltValue = 0;
    int headLiftValue = 0;*/

    public int maxHeadTilt = 20;
    public int maxHeadLean = 15;
    public int maxHeadLift = 3;
    public float maxRise = 85; // in degrees


    [ReadOnly]
    public float currentRise = 0f;

    [ReadOnly]
    public int ActionDimensions; // 3D or 2D actions

    public Transform environmentCenter;
    public float SpawnRange;
    public float respawnRotation = 360;

    public LogManager logger;
    FloatPropertiesChannel agentInfoChannel;

    int episode, step;
    Rigidbody rbody;
    private Vector3 startPosition;

    public bool RandomSpawn = true; // Otherwise they will face each other
    public float separationDistance = 6f;

    private BehaviorParameters behaviorParams;

    private int forwardSteps, leftTurns, rightTurns, backwardSteps;

    private float rotateDir;
    public bool rewardFlag = false;

    private void Awake()
    {
        // Let's set the behavior name equal to the agent's gameObject name.
        behaviorParams = GetComponent<BehaviorParameters>();
        behaviorParams.BehaviorName = this.gameObject.name;
    }

    public override void Initialize()
    {
        if (environmentCenter == null){
            // Create an Empty GameObject to use as the environment center at the Position of the Agent
            environmentCenter = new GameObject("EnvironmentCenter").transform;
            environmentCenter.position = transform.position;
            environmentCenter.rotation = transform.rotation;
        }

        SetMaxStep();
        SetCameraResolution();

        startPosition = transform.position;

        // Register a side channel to communicate auxiliary agent information.
        agentInfoChannel = new FloatPropertiesChannel();
        if (ArgumentParser.Options.enableAgentInfoChannel)
        {
            SideChannelManager.RegisterSideChannel(agentInfoChannel);
        }
        if (!Application.isEditor)
        {
            RandomAgent = ArgumentParser.Options.randomAgent || RandomAgent;
        }

        rbody = GetComponent<Rigidbody>();
        if (RandomAgent)
        {
            Debug.LogWarning("NOTE: " + this.gameObject.name + " is in RANDOM AGENT mode. Keep this in mind while collecting data.");
        }

        if (RestrainDive)
        {
            rbody.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX;
        }
    }

    private void SetMaxStep()
    {
        int maxStep = ArgumentParser.Options.episodeSteps;
        if (maxStep > 0)
        {
            MaxStep = maxStep;
        }
        step = 0;
        episode = 0;
    }

    private void SetCameraResolution()
    {
        // Set input resolution before CameraSensors are initialized in Agent.OnEnable.
        int resolution = ArgumentParser.Options.cameraResolution;
        foreach (CameraSensorComponent camSensor in gameObject.GetComponents<CameraSensorComponent>())
        {
            if (resolution > 0)
            {
                camSensor.Camera.fieldOfView = ArgumentParser.Options.fov;
                camSensor.Height = resolution;
                camSensor.Width = resolution;
            }
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        step++;
        if (transform.position.y < environmentCenter.transform.position.y - 10f)
        {
            transform.position = environmentCenter.transform.position;
            EndEpisode();
        }

        // Note: This works because it happens to be the case, but if another dimension was added
        ActionDimensions = Mathf.Min(3, actions.DiscreteActions.Length);
        ActionDimensions = RestrainDive ? 2 : ActionDimensions;

        //var rotateDir = 0f;
        var diveDir = 0f;

        int move = 0;
        int rotate = 0;
        int dive = 0;

        if (actions.DiscreteActions.Length > 0)
        {
            move = ForceForwardAction ? 1 : Mathf.FloorToInt(actions.DiscreteActions[0]);
            move = RestrainMove ? 0 : move;
        }
        if (actions.DiscreteActions.Length > 1)
        {
            rotate = RestrainTurn ? 0 : Mathf.FloorToInt(actions.DiscreteActions[1]);
        }
        if (actions.DiscreteActions.Length > 2)
        {
            dive = RestrainDive ? 0 : Mathf.FloorToInt(actions.DiscreteActions[2]);
        }
        // var look = Mathf.FloorToInt(actions.DiscreteActions[2]);

        if (RandomAgent)
        {
            if (actions.DiscreteActions.Length > 0)
            {
                move = RestrainMove ? 0 : Random.Range(0, behaviorParams.BrainParameters.ActionSpec.BranchSizes[0]);  // With 3 Actions, we want (0, 1, or 2)
            }
            if (actions.DiscreteActions.Length > 1)
            {
                rotate = RestrainTurn ? 0 : Random.Range(0, behaviorParams.BrainParameters.ActionSpec.BranchSizes[1]); // With 3 Actions, we want (0, 1, or 2)
            }
            if (actions.DiscreteActions.Length > 2)
            {
                dive = RestrainDive ? 0 : Random.Range(0, behaviorParams.BrainParameters.ActionSpec.BranchSizes[2]); // With 3 Actions, we want (0, 1, or 2)
            }
            // look = Random.Range(0, 7); // With 7 Actions, we want (0, 1, 2, 3, 4, 5, 6, or 7)
        }

        //previousActions3.Add(dive);

        switch (move)
        {
            case 0: // Do nothing
                break;
            case 1: // W: Move forward
                rbody.MovePosition(transform.position + MoveSpeed * Time.deltaTime * transform.forward);
                AddReward(ForwardCost);
                forwardSteps++;
                break;
            case 2: // S: Move backward (Leave this out of random if you don't want it to move randomly)
                rbody.MovePosition(transform.position - MoveSpeed * Time.deltaTime * transform.forward);
                AddReward(BackwardCost);
                backwardSteps++;
                break;
            default:
                break;
        }

        switch (rotate)
        {
            case 0:
                break;
            case 1: // D: Turn right
                rotateDir += RotateSpeed * Time.deltaTime;
                // rotateDir = RotateSpeed * Time.deltaTime;
                AddReward(RotateCost);
                rightTurns++;
                break;
            case 2: // A: Turn left
                rotateDir -= RotateSpeed * Time.deltaTime;
                // rotateDir = -RotateSpeed * Time.deltaTime;
                AddReward(RotateCost);
                leftTurns++;
                break;
            default:
                break;
        }

        switch (dive)
        {
            case 0:
                break;
            case 1: // Up: Rise Up
                if (-maxRise < currentRise)
                {
                    diveDir = -RotateSpeed * Time.deltaTime;
                }
                AddReward(RotateCost);
                break;
            case 2: // Down: Dive Down
                if (currentRise < maxRise)
                {
                    diveDir = RotateSpeed * Time.deltaTime;
                }
                AddReward(RotateCost);
                break;
            default:
                break;
        }

        currentRise += diveDir;
        transform.rotation = Quaternion.Lerp(transform.rotation, Quaternion.Euler(new Vector3(transform.rotation.x, rotateDir, transform.rotation.z)), RotateLerp);

        if (logger != null)
        {
            logger.AddEntry(episode, step);
        }
    }

    public void Respawn()
    {
        rewardFlag = false;
        forwardSteps = 0;
        backwardSteps = 0;
        leftTurns = 0;
        rightTurns = 0;

        Vector3 candidateSpawn;
        if (RandomSpawn)
        {
            candidateSpawn = environmentCenter.transform.position + new Vector3(Random.Range(-SpawnRange, SpawnRange),
            Random.Range(0, SpawnRange),
            Random.Range(-SpawnRange, SpawnRange));
            candidateSpawn.y = ActionDimensions == 2 || RestrainDive ? startPosition.y : candidateSpawn.y;
            transform.rotation = Quaternion.Euler(0, Random.Range(-respawnRotation / 2, respawnRotation / 2), 0);
        }
        else // They are facing each other
        {
            int facingDirection = Random.Range(0, 2);
            facingDirection = facingDirection == 0 ? -1 : 1;
            candidateSpawn = new Vector3(environmentCenter.transform.position.x + (separationDistance * facingDirection), startPosition.y, environmentCenter.transform.position.z);
            Quaternion candidateDirection = Quaternion.Euler(new Vector3(0, 90 * facingDirection * -1, 0));
            transform.rotation = candidateDirection;
        }
        transform.position = candidateSpawn;
        rbody.linearVelocity = Vector3.zero;
        currentRise = 0f;
    }

    public override void OnEpisodeBegin()
    {
        Respawn();
        episode++;
        step = 0;
        // Randomize position and rotation.
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<int> discreteActionsOut = actionsOut.DiscreteActions;
        if (actionsOut.DiscreteActions.Length > 0)
        {
            // MOVE /////////////////////
            if (Input.GetKey(KeyCode.W))
            {
                discreteActionsOut[0] = 1;
            }
            else if (Input.GetKey(KeyCode.S))
            {
                discreteActionsOut[0] = 2;
            }
            else
            {
                discreteActionsOut[0] = 0;
            }
        }

        if (actionsOut.DiscreteActions.Length > 1)
        {
            // ROTATE ///////////////////
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
            {
                discreteActionsOut[1] = 1;
            }
            else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
            {
                discreteActionsOut[1] = 2;
            }
            else
            {
                discreteActionsOut[1] = 0;
            }
        }

        // DIVE ///////////////////
        if (actionsOut.DiscreteActions.Length > 2)
        {
            if (Input.GetKey(KeyCode.UpArrow))
            {
                discreteActionsOut[2] = 1;
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                discreteActionsOut[2] = 2;
            }
            else
            {
                discreteActionsOut[2] = 0;
            }
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            RandomAgent = !RandomAgent;
        }
    }
}
