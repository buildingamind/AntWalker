using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ProceduralAnimator : MonoBehaviour
{
    public GameObject leftFoot;
    private Vector3 leftFootStartPos; // Will be relative to this.transform
    private Quaternion leftFootStartRot; // Will be relative to this.transform
    private Vector3 leftFootPlantedWorldPos;
    private Quaternion leftFootPlantedWorldRot;
    // private bool isLeftFootStepping = false; // Removed
    private Vector3 leftFoot_bodyPosAtStepStart; // Added

    public GameObject rightFoot;
    private Vector3 rightFootStartPos; // Will be relative to this.transform
    private Quaternion rightFootStartRot; // Will be relative to this.transform
    private Vector3 rightFootPlantedWorldPos;
    private Quaternion rightFootPlantedWorldRot;
    // private bool isRightFootStepping = false; // Removed
    private Vector3 rightFoot_bodyPosAtStepStart; // Added

    public float maxDistance = 0.5f;
    public float maxRotation = 30f;
    // public float stepCompletionThresholdPos = 0.01f; // Removed: No longer used
    // public float stepCompletionThresholdRot = 1.0f; // Removed: No longer used
    public float stepExtrapolationFactor = 0.1f; // Added

    private bool isLeftFootTurnToStep = true; // Added: true if left foot's turn, false if right's

    // Start is called before the first frame update
    void Start()
    {
        if (leftFoot != null)
        {
            // Store initial position/rotation relative to this animator's transform
            leftFootStartPos = transform.InverseTransformPoint(leftFoot.transform.position);
            leftFootStartRot = Quaternion.Inverse(transform.rotation) * leftFoot.transform.rotation;
            
            leftFootPlantedWorldPos = leftFoot.transform.position;
            leftFootPlantedWorldRot = leftFoot.transform.rotation;
            leftFoot_bodyPosAtStepStart = transform.position; // Initialize
        }

        if (rightFoot != null)
        {
            // Store initial position/rotation relative to this animator's transform
            rightFootStartPos = transform.InverseTransformPoint(rightFoot.transform.position);
            rightFootStartRot = Quaternion.Inverse(transform.rotation) * rightFoot.transform.rotation;

            rightFootPlantedWorldPos = rightFoot.transform.position;
            rightFootPlantedWorldRot = rightFoot.transform.rotation;
            rightFoot_bodyPosAtStepStart = transform.position; // Initialize
        }
    }

    void Update()
    {
        bool leftFootWantsToStep = false;
        bool rightFootWantsToStep = false;

        if (leftFoot != null)
        {
            leftFootWantsToStep = CheckIfFootNeedsToStep(leftFootStartPos, leftFootStartRot, leftFootPlantedWorldPos, leftFootPlantedWorldRot);
        }
        if (rightFoot != null)
        {
            rightFootWantsToStep = CheckIfFootNeedsToStep(rightFootStartPos, rightFootStartRot, rightFootPlantedWorldPos, rightFootPlantedWorldRot);
        }

        bool actuallySteppingLeft = false;
        bool actuallySteppingRight = false;

        if (isLeftFootTurnToStep)
        {
            if (leftFootWantsToStep && leftFoot != null)
            {
                actuallySteppingLeft = true;
            }
        }
        else // Right foot's turn
        {
            if (rightFootWantsToStep && rightFoot != null)
            {
                actuallySteppingRight = true;
            }
        }
        
        // If the designated foot didn't step, but the other one wants to, let it step.
        // This makes the character more responsive if one foot is stuck but the other can move.
        if (isLeftFootTurnToStep && !actuallySteppingLeft && rightFootWantsToStep && rightFoot != null)
        {
            actuallySteppingRight = true; 
        }
        else if (!isLeftFootTurnToStep && !actuallySteppingRight && leftFootWantsToStep && leftFoot != null)
        {
            actuallySteppingLeft = true;
        }


        if (leftFoot != null)
        {
            UpdateFoot(leftFoot, leftFootStartPos, leftFootStartRot, ref leftFootPlantedWorldPos, ref leftFootPlantedWorldRot, actuallySteppingLeft, true, ref leftFoot_bodyPosAtStepStart);
        }
        if (rightFoot != null)
        {
            UpdateFoot(rightFoot, rightFootStartPos, rightFootStartRot, ref rightFootPlantedWorldPos, ref rightFootPlantedWorldRot, actuallySteppingRight, false, ref rightFoot_bodyPosAtStepStart);
        }
    }

    // New helper function to check step condition without modifying state
    bool CheckIfFootNeedsToStep(Vector3 startLocalPos, Quaternion startLocalRot, 
                                Vector3 plantedWorldPos, Quaternion plantedWorldRot) // Removed isCurrentlyStepping
    {
        // if (isCurrentlyStepping) return false; // Removed: No longer needed as steps are instant

        Vector3 idealStepTargetPos = transform.TransformPoint(startLocalPos);
        Quaternion idealStepTargetRot = transform.rotation * startLocalRot;
        float distanceToIdealTarget = Vector3.Distance(idealStepTargetPos, plantedWorldPos);
        float angleToIdealTarget = Quaternion.Angle(idealStepTargetRot, plantedWorldRot);

        return (distanceToIdealTarget > maxDistance || angleToIdealTarget > maxRotation);
    }

    // On DrawGizmos - Optional: Visualize the Max Distance of the Joints in the Editor
    void OnDrawGizmos()
    {
        if (leftFoot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leftFoot.transform.position, maxDistance);
        }
        if (rightFoot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(rightFoot.transform.position, maxDistance);
        }
    }

    void UpdateFoot(GameObject foot, Vector3 startLocalPos, Quaternion startLocalRot, 
                    ref Vector3 plantedWorldPos, ref Quaternion plantedWorldRot, 
                    bool shouldInitiateStepThisFrame, bool isThisTheLeftFoot, // Changed: no more isStepping ref
                    ref Vector3 bodyPosAtStepStartVariable) 
    {
        Vector3 idealStepTargetPos = transform.TransformPoint(startLocalPos);
        Quaternion idealStepTargetRot = transform.rotation * startLocalRot;

        if (shouldInitiateStepThisFrame) 
        {
            // Calculate body displacement since this foot last started a step (or was initialized)
            Vector3 bodyDisplacement = transform.position - bodyPosAtStepStartVariable;
            Vector3 extrapolatedTargetPos = idealStepTargetPos + bodyDisplacement * stepExtrapolationFactor;

            plantedWorldPos = extrapolatedTargetPos; // Use extrapolated position
            plantedWorldRot = idealStepTargetRot;
            
            // Instantly move to the new planted position/rotation
            foot.transform.position = plantedWorldPos;
            foot.transform.rotation = plantedWorldRot;
            
            // Update body position reference for *this* foot for the *next* time it steps
            bodyPosAtStepStartVariable = transform.position; 
            
            isLeftFootTurnToStep = !isThisTheLeftFoot; // Flip the turn to the other foot
        }
        else // Not initiating a step this frame
        {
            // Stay planted: Force the foot to maintain its current planted world position and rotation
            foot.transform.position = plantedWorldPos;
            foot.transform.rotation = plantedWorldRot;
        }
    }
}
