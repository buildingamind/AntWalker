using UnityEngine;

public class ProceduralAnimator : MonoBehaviour
{
    private class FootState
    {
        public Vector3 startLocalPos;      // rest offset, relative to this.transform
        public Quaternion startLocalRot;   // rest rotation, relative to this.transform
        public Vector3 plantedWorldPos;    // committed target the foot is at (or swinging toward)
        public Quaternion plantedWorldRot;
        public Vector3 bodyPosAtStepStart; // body position when this foot last started a step

        public bool isStepping;
        public float stepElapsed;
        public Vector3 stepFromPos;
        public Quaternion stepFromRot;
    }

    public GameObject leftFoot;
    public GameObject rightFoot;

    public float maxDistance = 0.5f;
    public float maxRotation = 30f;
    [Tooltip("How far ahead (fraction of the body's movement since this foot's last step) to place the new step, to compensate for the body continuing to move during the foot's stance phase.")]
    public float stepExtrapolationFactor = 0.1f;

    [Header("Step Animation")]
    [Tooltip("How long (seconds) a foot takes to swing from its old planted position to the new one.")]
    public float stepDuration = 0.15f;
    [Tooltip("How high the foot lifts off the ground at the midpoint of a step.")]
    public float stepLiftHeight = 0.1f;

    private bool isLeftFootTurnToStep = true;

    // Beyond this multiple of maxDistance, the target jumped too far to swing to
    // (e.g. the body itself hard-teleported) -- snap instantly instead of stretching the leg there.
    private const float TeleportDistanceMultiplier = 10f;

    private readonly FootState leftState = new FootState();
    private readonly FootState rightState = new FootState();

    void Start()
    {
        if (leftFoot != null)
        {
            InitFootState(leftFoot, leftState);
        }
        if (rightFoot != null)
        {
            InitFootState(rightFoot, rightState);
        }
    }

    void InitFootState(GameObject foot, FootState state)
    {
        state.startLocalPos = transform.InverseTransformPoint(foot.transform.position);
        state.startLocalRot = Quaternion.Inverse(transform.rotation) * foot.transform.rotation;
        state.plantedWorldPos = foot.transform.position;
        state.plantedWorldRot = foot.transform.rotation;
        state.bodyPosAtStepStart = transform.position;
    }

    void Update()
    {
        bool leftWantsToStep = leftFoot != null && CheckIfFootNeedsToStep(leftState);
        bool rightWantsToStep = rightFoot != null && CheckIfFootNeedsToStep(rightState);

        bool steppingLeft = false;
        bool steppingRight = false;

        if (isLeftFootTurnToStep)
        {
            if (leftWantsToStep && leftFoot != null)
            {
                steppingLeft = true;
            }
        }
        else
        {
            if (rightWantsToStep && rightFoot != null)
            {
                steppingRight = true;
            }
        }

        // If the designated foot didn't step, but the other one wants to, let it step.
        // This makes the character more responsive if one foot is stuck but the other can move.
        if (isLeftFootTurnToStep && !steppingLeft && rightWantsToStep && rightFoot != null)
        {
            steppingRight = true;
        }
        else if (!isLeftFootTurnToStep && !steppingRight && leftWantsToStep && leftFoot != null)
        {
            steppingLeft = true;
        }

        if (leftFoot != null)
        {
            UpdateFoot(leftFoot, leftState, steppingLeft, true);
        }
        if (rightFoot != null)
        {
            UpdateFoot(rightFoot, rightState, steppingRight, false);
        }
    }

    // A foot mid-swing doesn't need a new target yet; it'll re-check once it lands.
    bool CheckIfFootNeedsToStep(FootState state)
    {
        if (state.isStepping)
        {
            return false;
        }

        Vector3 idealStepTargetPos = transform.TransformPoint(state.startLocalPos);
        Quaternion idealStepTargetRot = transform.rotation * state.startLocalRot;
        float distanceToIdealTarget = Vector3.Distance(idealStepTargetPos, state.plantedWorldPos);
        float angleToIdealTarget = Quaternion.Angle(idealStepTargetRot, state.plantedWorldRot);

        return distanceToIdealTarget > maxDistance || angleToIdealTarget > maxRotation;
    }

    // Optional: Visualize the Max Distance of the Joints in the Editor
    void OnDrawGizmos()
    {
        if (leftFoot != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(leftFoot.transform.position, maxDistance);
            DrawStepLiftIndicator(leftFoot.transform.position);
        }
        if (rightFoot != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(rightFoot.transform.position, maxDistance);
            DrawStepLiftIndicator(rightFoot.transform.position);
        }
    }

    // A small green line showing how high stepLiftHeight lifts the foot mid-swing.
    void DrawStepLiftIndicator(Vector3 groundPos)
    {
        Gizmos.color = Color.green;
        Gizmos.DrawLine(groundPos, groundPos + Vector3.up * stepLiftHeight);
    }

    void UpdateFoot(GameObject foot, FootState state, bool shouldInitiateStepThisFrame, bool isThisTheLeftFoot)
    {
        if (shouldInitiateStepThisFrame)
        {
            Vector3 idealStepTargetPos = transform.TransformPoint(state.startLocalPos);
            Quaternion idealStepTargetRot = transform.rotation * state.startLocalRot;

            // Calculate body displacement since this foot last started a step, and lead the
            // placement slightly ahead so it's roughly centred by the time the foot next needs to move.
            Vector3 bodyDisplacement = transform.position - state.bodyPosAtStepStart;
            Vector3 extrapolatedTargetPos = idealStepTargetPos + bodyDisplacement * stepExtrapolationFactor;

            float jumpDistance = Vector3.Distance(foot.transform.position, extrapolatedTargetPos);

            if (jumpDistance > maxDistance * TeleportDistanceMultiplier)
            {
                // Too far to swing to (the body itself hopped, e.g. a random-teleport walk) --
                // skip the animation so the leg doesn't stretch across the gap.
                state.isStepping = false;
            }
            else
            {
                // Begin the swing from wherever the foot currently is, so retriggering mid-swing can't pop it.
                state.stepFromPos = foot.transform.position;
                state.stepFromRot = foot.transform.rotation;
                state.stepElapsed = 0f;
                state.isStepping = true;
            }

            state.plantedWorldPos = extrapolatedTargetPos;
            state.plantedWorldRot = idealStepTargetRot;

            // Update body position reference for *this* foot for the *next* time it steps.
            state.bodyPosAtStepStart = transform.position;

            isLeftFootTurnToStep = !isThisTheLeftFoot; // Flip the turn to the other foot
        }

        if (state.isStepping)
        {
            state.stepElapsed += Time.deltaTime;
            float t = stepDuration > 0f ? Mathf.Clamp01(state.stepElapsed / stepDuration) : 1f;

            Vector3 pos = Vector3.Lerp(state.stepFromPos, state.plantedWorldPos, t);
            pos += Vector3.up * (Mathf.Sin(t * Mathf.PI) * stepLiftHeight);

            foot.transform.position = pos;
            foot.transform.rotation = Quaternion.Slerp(state.stepFromRot, state.plantedWorldRot, t);

            if (t >= 1f)
            {
                state.isStepping = false;
                foot.transform.position = state.plantedWorldPos;
                foot.transform.rotation = state.plantedWorldRot;
            }
        }
        else
        {
            // Stay planted: force the foot to hold its current planted world position and rotation.
            foot.transform.position = state.plantedWorldPos;
            foot.transform.rotation = state.plantedWorldRot;
        }
    }
}
