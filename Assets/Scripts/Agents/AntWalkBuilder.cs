using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Drives an ant agent through a configurable playlist of *predefined* learning-walk
/// patterns (the deterministic counterpart to <see cref="AntRandomWalk"/>).
///
/// Each <see cref="PathDefinition"/> describes one segment of the playlist. Segments are
/// followed in order; once a segment has been performed <c>repeats</c> times the playlist
/// advances to the next one, optionally looping back to the start.
///
/// Pirouettes (spin-in-place) and voltes (small circular detour) fire at an independent
/// per-step probability and only *temporarily* detour the path. Because a pirouette is a
/// full 360 deg spin and a volte is a full circle, the agent returns to exactly where it
/// left the path, then resumes from the same point (the path progress is frozen during a
/// detour, so the net displacement adds to zero).
/// </summary>
public class AntWalkBuilder : MonoBehaviour
{
    public enum WalkType
    {
        StraightOutAndBack, // Out along a direction for `distance`, then straight back.
        RandomTeleport,     // Hard-teleport to random points within a disk tangent to the nest (no smooth motion, no detours).
        FullLoop,           // A single full loop in front of the nest, returning to it.
        HalfLoop,           // Out along half the loop's arc, then back the same arc, returning to it.
        Spiral              // Spiral out from the nest to loopDiameter/2 radius, then return per spiralReturnMode.
    }

    public enum LoopDirection
    {
        Clockwise,
        CounterClockwise,
        Random
    }

    public enum SpiralReturnMode
    {
        ReverseReturn,  // Spiral back inward along the same coiled path it went out on.
        StraightReturn, // Straight line from the outermost point directly back to the nest.
        TeleportReturn  // Hard-teleport back to the nest once the outermost point is reached.
    }

    [System.Serializable]
    public class PathDefinition
    {
        public WalkType type = WalkType.StraightOutAndBack;

        [Tooltip("How many times to perform this walk before advancing to the next segment. For RandomTeleport this is the number of teleport hops.")]
        public int repeats = 1;

        [Tooltip("[All types] Heading in degrees (yaw) the walk points away from the nest.")]
        public float directionAngle = 0f;

        [Header("Straight Out & Back")]
        [Tooltip("[Straight] Distance out from the nest before turning back.")]
        public float distance = 10f;

        [Header("Full Loop / Half Loop / Spiral")]
        [Tooltip("[Loop/Half Loop] Diameter of the loop performed in front of the nest. [Spiral] Diameter of the outermost coil, centred on the nest.")]
        public float loopDiameter = 5f;
        [Tooltip("[Loop/Half Loop/Spiral] Direction of travel around the loop.")]
        public LoopDirection loopDirection = LoopDirection.Random;

        [Header("Spiral")]
        [Tooltip("[Spiral] Number of coils the spiral winds through going out (and again coming back, if the return mode retraces it). Higher values coil tighter for the same diameter.")]
        public float spiralTurns = 3f;
        [Tooltip("[Spiral] How the ant gets back to the nest after reaching the spiral's outermost point.")]
        public SpiralReturnMode spiralReturnMode = SpiralReturnMode.ReverseReturn;

        [Header("Random Teleport")]
        [Tooltip("[Teleport] Diameter of the region (tangent to the nest, same placement as Full Loop) to teleport within.")]
        public float teleportDiameter = 40f;
        [Tooltip("[Teleport] How many FixedUpdates to hold each teleport position before the next hop.")]
        public int holdDuration = 10;
    }

    [Header("Randomness")]
    [Tooltip("Seeds Unity's Random state on Start, so loop direction, teleport spots, and pirouette/volte timing & direction are reproducible.")]
    public int seed = 0;

    [Header("Walk Playlist")]
    [Tooltip("Ordered list of walk segments. Joined together like a playlist.")]
    public List<PathDefinition> playlist = new List<PathDefinition>();

    [Tooltip("When the playlist finishes, start it over from the beginning.")]
    public bool loopPlaylist = true;

    [Header("Playlist State [ReadOnly]")]
    [ReadOnly] public int segmentIndex = 0;
    [ReadOnly] public int repeatCount = 0;
    [ReadOnly] public WalkType currentType;

    [Header("Movement")]
    [Tooltip("Speed (units/second) the ant follows the path.")]
    public float moveSpeed = 1.0f;

    [Header("Pirouette (spin in place)")]
    [Tooltip("Per-step probability (0-1) of starting a pirouette.")]
    public float pirouetteChance = 0.005f;
    [Tooltip("Number of steps a full 360 deg pirouette is spread across.")]
    public int pirouetteSize = 18;
    [ReadOnly] public bool isPirouetting = false;

    [Header("Volte (small circular detour)")]
    [Tooltip("Per-step probability (0-1) of starting a volte.")]
    public float volteChance = 0.005f;
    [Tooltip("Radius of the volte circle.")]
    public float volteSize = 1.0f;
    [Tooltip("Number of steps a full volte circle is spread across.")]
    public int volteSteps = 36;
    [ReadOnly] public bool isVolting = false;

    [Header("References")]
    private LogManager logManager;
    private Collider homeCollider;

    [Header("Counters [ReadOnly]")]
    [ReadOnly] public int step = 0;
    [Tooltip("Number of completed playlist passes (episodes).")]
    [ReadOnly] public int returns = 0;

    [Header("Editor Gizmos")]
    [Tooltip("Draw the planned target trajectory in the Scene view.")]
    public bool drawTrajectory = true;

    // --- private runtime state ---
    private Vector3 home;
    private float pathDistance = 0f;     // arc length travelled along the current path instance
    private float currentPathLength = 0f; // total arc length of the current path instance
    private bool randomLoopCW = false;   // resolved loop direction for the current repeat

    // Once facing within this many degrees of the path tangent, movement is allowed to resume.
    private const float TurnFacingToleranceDegrees = 3f;

    // teleport state
    private bool teleportPlaced = false;
    private int teleportHoldCounter = 0;

    // pirouette state
    private int pirouetteStep = 0;
    private int pirouetteDirection = 1;

    // volte state
    private int volteStep = 0;
    private int volteDirection = 1;
    private Vector3 detourAnchor;
    private Vector3 volteStartForward;

    void Start()
    {
        Random.InitState(seed);
        home = GetHome();
    }

    Vector3 GetHome()
    {
        return homeCollider != null ? homeCollider.transform.position : transform.position;
    }

    void FixedUpdate()
    {
        HandleTimescaleKeys();

        if (playlist == null || playlist.Count == 0)
        {
            return;
        }

        // Playlist exhausted (only possible when loopPlaylist is false).
        if (segmentIndex >= playlist.Count)
        {
            return;
        }

        PathDefinition seg = playlist[segmentIndex];
        currentType = seg.type;

        if (seg.type == WalkType.RandomTeleport)
        {
            // No smooth motion, and no pirouettes or voltes during teleport segments.
            UpdateTeleport(seg);
        }
        else
        {
            // Detours take priority and only fire when not already detouring.
            if (!isPirouetting && !isVolting)
            {
                if (Random.value < pirouetteChance)
                {
                    StartPirouette();
                }
                else if (Random.value < volteChance)
                {
                    StartVolte();
                }
            }

            if (isPirouetting)
            {
                UpdatePirouette();
            }
            else if (isVolting)
            {
                UpdateVolte();
            }
            else
            {
                AdvancePath(seg);
            }
        }

        if (logManager != null && logManager.enabled)
        {
            logManager.AddEntry(returns, step);
        }
        step++;
    }

    // ------------------------------------------------------------------
    // Path following
    // ------------------------------------------------------------------

    void BeginPath(PathDefinition seg)
    {
        pathDistance = 0f;
        currentPathLength = SegmentLength(seg);

        if (seg.type == WalkType.FullLoop || seg.type == WalkType.HalfLoop || seg.type == WalkType.Spiral)
        {
            randomLoopCW = seg.loopDirection == LoopDirection.Clockwise ? true :
                           seg.loopDirection == LoopDirection.CounterClockwise ? false :
                           Random.value < 0.5f;
        }
    }

    void AdvancePath(PathDefinition seg)
    {
        if (currentPathLength <= 0f)
        {
            BeginPath(seg);
        }

        // Turn on the spot to face the upcoming direction before moving, so a sharp
        // turn (e.g. the out-and-back turnaround) doesn't slide the agent sideways
        // or backwards while it's still rotating.
        if (!FaceTangent(PathTangent(seg, pathDistance)))
        {
            return;
        }

        pathDistance += moveSpeed * Time.deltaTime;

        if (pathDistance >= currentPathLength)
        {
            // Snap exactly to the end of the path (back at the nest) and complete a repeat.
            SetPathPosition(seg, currentPathLength);

            if (seg.type == WalkType.Spiral && seg.spiralReturnMode == SpiralReturnMode.TeleportReturn)
            {
                // The path only covers the outbound leg; hop straight back to the nest
                // instead of walking, the same way RandomTeleport moves between hops.
                transform.position = new Vector3(home.x, transform.position.y, home.z);
            }

            repeatCount++;
            currentPathLength = 0f;
            pathDistance = 0f;

            if (repeatCount >= Mathf.Max(1, seg.repeats))
            {
                AdvanceSegment();
            }
            return;
        }

        SetPathPosition(seg, pathDistance);
    }

    /// <summary>Tangent direction (nest-local XZ plane) of the path at arc length <paramref name="s"/>.</summary>
    Vector3 PathTangent(PathDefinition seg, float s)
    {
        Vector3 off = PathOffset(seg, s, randomLoopCW);
        Vector3 offNext = PathOffset(seg, Mathf.Min(s + 0.05f, currentPathLength), randomLoopCW);
        Vector3 tangent = offNext - off;
        tangent.y = 0f;
        return tangent;
    }

    void SetPathPosition(PathDefinition seg, float s)
    {
        Vector3 off = PathOffset(seg, s, randomLoopCW);
        transform.position = new Vector3(home.x + off.x, transform.position.y, home.z + off.z);
    }

    /// <summary>Eases the rotation toward <paramref name="tangent"/>. Returns true once facing it closely enough to move.</summary>
    bool FaceTangent(Vector3 tangent)
    {
        if (tangent.sqrMagnitude <= 1e-6f)
        {
            return true;
        }

        Quaternion target = Quaternion.LookRotation(tangent.normalized);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 0.2f);
        return Quaternion.Angle(transform.rotation, target) < TurnFacingToleranceDegrees;
    }

    /// <summary>Offset (in the nest-local XZ plane, y = 0) of the path at arc length <paramref name="s"/>.</summary>
    Vector3 PathOffset(PathDefinition seg, float s, bool cw)
    {
        switch (seg.type)
        {
            case WalkType.StraightOutAndBack:
            {
                Vector3 dir = HeadingVector(seg.directionAngle);
                float d = Mathf.Max(0.0001f, seg.distance);
                return s <= d ? dir * s : dir * (2f * d - s);
            }
            case WalkType.FullLoop:
            {
                float r = Mathf.Max(0.0001f, seg.loopDiameter * 0.5f);
                float theta = s / r; // arc length -> angle (radians)
                return LoopOffset(seg, theta, cw);
            }
            case WalkType.HalfLoop:
            {
                // Out along half the loop's arc (180 deg), then back the same arc, mirroring
                // StraightOutAndBack but curved.
                float r = Mathf.Max(0.0001f, seg.loopDiameter * 0.5f);
                float halfArcLength = Mathf.PI * r;
                float theta = s <= halfArcLength ? (s / r) : ((2f * halfArcLength - s) / r);
                return LoopOffset(seg, theta, cw);
            }
            case WalkType.Spiral:
                return SpiralOffset(seg, s, cw);
            default:
                return Vector3.zero;
        }
    }

    /// <summary>A circle of radius loopDiameter/2 tangent to the nest, centred in front of it. Origin at theta = 0 and theta = 2*PI.</summary>
    Vector3 LoopOffset(PathDefinition seg, float theta, bool cw)
    {
        Vector3 front = HeadingVector(seg.directionAngle);
        float r = Mathf.Max(0.0001f, seg.loopDiameter * 0.5f);
        Vector3 center = front * r;
        Vector3 startRel = -front; // unit vector from centre back to the nest
        float sign = cw ? -1f : 1f;
        Vector3 rel = Quaternion.AngleAxis(sign * theta * Mathf.Rad2Deg, Vector3.up) * startRel;
        return center + r * rel;
    }

    /// <summary>
    /// Spirals out from the nest to loopDiameter/2, coiling spiralTurns times, then returns to the
    /// nest via <see cref="PathDefinition.spiralReturnMode"/>. TeleportReturn's return leg isn't part
    /// of this offset function at all -- AdvancePath snaps the position directly once the outbound
    /// leg completes, the same way RandomTeleport does.
    /// </summary>
    Vector3 SpiralOffset(PathDefinition seg, float s, bool cw)
    {
        float maxR = Mathf.Max(0.0001f, seg.loopDiameter * 0.5f);
        float outLength = SpiralLegLength(seg);

        switch (seg.spiralReturnMode)
        {
            case SpiralReturnMode.StraightReturn:
            {
                if (s <= outLength)
                {
                    return SpiralPoint(seg, Mathf.Clamp01(s / outLength), cw);
                }
                Vector3 outEnd = SpiralPoint(seg, 1f, cw);
                float returnT = Mathf.Clamp01((s - outLength) / maxR);
                return Vector3.Lerp(outEnd, Vector3.zero, returnT);
            }
            case SpiralReturnMode.TeleportReturn:
                return SpiralPoint(seg, Mathf.Clamp01(s / outLength), cw);
            case SpiralReturnMode.ReverseReturn:
            default:
            {
                float t = s <= outLength ? s / outLength : (2f * outLength - s) / outLength;
                return SpiralPoint(seg, Mathf.Clamp01(t), cw);
            }
        }
    }

    /// <summary>Position along the spiral's outbound leg at progress <paramref name="t"/> (0 = nest, 1 = outermost point).
    /// Radius and angle both scale linearly with t, so it isn't a true constant-speed arc-length
    /// parametrisation, but it's smooth and simple.</summary>
    Vector3 SpiralPoint(PathDefinition seg, float t, bool cw)
    {
        float maxR = Mathf.Max(0.0001f, seg.loopDiameter * 0.5f);
        float totalTheta = Mathf.Max(0.0001f, seg.spiralTurns) * 2f * Mathf.PI;
        float theta = t * totalTheta;
        float r = t * maxR;
        float sign = cw ? -1f : 1f;
        Vector3 dir = Quaternion.AngleAxis(sign * theta * Mathf.Rad2Deg, Vector3.up) * HeadingVector(seg.directionAngle);
        return dir * r;
    }

    /// <summary>Arc length of one spiral leg (nest to outermost point), found by sampling since the
    /// spiral's radius/angle relationship isn't a closed form we need to keep readable.</summary>
    static float SpiralLegLength(PathDefinition seg)
    {
        float maxR = Mathf.Max(0.0001f, seg.loopDiameter * 0.5f);
        float totalTheta = Mathf.Max(0.0001f, seg.spiralTurns) * 2f * Mathf.PI;

        const int samples = 32;
        float length = 0f;
        Vector3 prev = Vector3.zero;
        for (int i = 1; i <= samples; i++)
        {
            float t = i / (float)samples;
            float theta = t * totalTheta;
            float r = t * maxR;
            Vector3 cur = new Vector3(Mathf.Cos(theta), 0f, Mathf.Sin(theta)) * r;
            length += Vector3.Distance(prev, cur);
            prev = cur;
        }
        return length;
    }

    float SegmentLength(PathDefinition seg)
    {
        switch (seg.type)
        {
            case WalkType.StraightOutAndBack:
                return 2f * Mathf.Max(0.0001f, seg.distance);
            case WalkType.FullLoop:
                return Mathf.PI * Mathf.Max(0.0001f, seg.loopDiameter);
            case WalkType.HalfLoop:
                return Mathf.PI * Mathf.Max(0.0001f, seg.loopDiameter); // half the arc, out and back
            case WalkType.Spiral:
                return SpiralSegmentLength(seg);
            default:
                return 0f;
        }
    }

    static float SpiralSegmentLength(PathDefinition seg)
    {
        float outLength = SpiralLegLength(seg);
        switch (seg.spiralReturnMode)
        {
            case SpiralReturnMode.StraightReturn:
                return outLength + Mathf.Max(0.0001f, seg.loopDiameter * 0.5f); // + straight line back
            case SpiralReturnMode.TeleportReturn:
                return outLength; // return leg is an instant snap, not part of the path
            case SpiralReturnMode.ReverseReturn:
            default:
                return 2f * outLength; // out, then back the same way
        }
    }

    static Vector3 HeadingVector(float angleDegrees)
    {
        return Quaternion.Euler(0f, angleDegrees, 0f) * Vector3.forward;
    }

    void AdvanceSegment()
    {
        repeatCount = 0;
        currentPathLength = 0f;
        pathDistance = 0f;
        teleportPlaced = false;
        teleportHoldCounter = 0;
        segmentIndex++;

        if (segmentIndex >= playlist.Count)
        {
            // A full pass through every segment in the playlist is one episode.
            returns++;

            if (loopPlaylist)
            {
                segmentIndex = 0;
            }
        }
    }

    // ------------------------------------------------------------------
    // Random teleport
    // ------------------------------------------------------------------

    void UpdateTeleport(PathDefinition seg)
    {
        if (!teleportPlaced)
        {
            // Same tangent-to-the-nest placement as Full Loop: a disk of teleportDiameter
            // centred directionAngle-forward, touching the nest at its near edge.
            float radius = Mathf.Max(0.0001f, seg.teleportDiameter * 0.5f);
            Vector3 center = HeadingVector(seg.directionAngle) * radius;
            Vector2 offset = Random.insideUnitCircle * radius;
            Vector3 point = center + new Vector3(offset.x, 0f, offset.y);

            transform.position = new Vector3(home.x + point.x, transform.position.y, home.z + point.z);
            teleportHoldCounter = 0;
            teleportPlaced = true;
        }

        teleportHoldCounter++;

        if (teleportHoldCounter >= Mathf.Max(1, seg.holdDuration))
        {
            repeatCount++;
            teleportPlaced = false;

            if (repeatCount >= Mathf.Max(1, seg.repeats))
            {
                AdvanceSegment();
            }
        }
    }

    // ------------------------------------------------------------------
    // Pirouette (spin in place)
    // ------------------------------------------------------------------

    void StartPirouette()
    {
        isPirouetting = true;
        pirouetteStep = 0;
        pirouetteDirection = Random.Range(0, 2) == 0 ? -1 : 1;
    }

    void UpdatePirouette()
    {
        if (pirouetteStep < pirouetteSize)
        {
            // Position is pinned; only the heading rotates, so the net displacement is zero.
            transform.Rotate(0f, (360f / Mathf.Max(1, pirouetteSize)) * pirouetteDirection, 0f);
            pirouetteStep++;
        }
        else
        {
            isPirouetting = false;
            pirouetteStep = 0;
        }
    }

    // ------------------------------------------------------------------
    // Volte (small circular detour that returns to the path)
    // ------------------------------------------------------------------

    void StartVolte()
    {
        isVolting = true;
        volteStep = 0;
        volteDirection = Random.Range(0, 2) == 0 ? -1 : 1;
        detourAnchor = transform.position;
        volteStartForward = transform.forward;
    }

    void UpdateVolte()
    {
        int steps = Mathf.Max(1, volteSteps);
        if (volteStep < steps)
        {
            float theta = 2f * Mathf.PI * (volteStep / (float)steps);

            // Circle tangent to the anchor: offset is zero at theta 0 and theta 2*PI.
            Vector3 side = Vector3.Cross(Vector3.up, volteStartForward).normalized * volteDirection;
            Vector3 center = side * volteSize;
            Vector3 startRel = -side * volteSize; // from centre back to the anchor
            Vector3 rel = Quaternion.AngleAxis(volteDirection * theta * Mathf.Rad2Deg, Vector3.up) * startRel;
            Vector3 off = center + rel;

            transform.position = new Vector3(detourAnchor.x + off.x, transform.position.y, detourAnchor.z + off.z);

            Vector3 tangent = Vector3.Cross(Vector3.up, rel) * volteDirection;
            tangent.y = 0f;
            if (tangent.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(tangent.normalized);
            }

            volteStep++;
        }
        else
        {
            // Restore exactly to where the detour began, then resume the (frozen) path.
            isVolting = false;
            volteStep = 0;
            transform.position = new Vector3(detourAnchor.x, transform.position.y, detourAnchor.z);
        }
    }

    // ------------------------------------------------------------------
    // Timescale hotkeys (F1-F12 -> Time.timeScale 1-12)
    // ------------------------------------------------------------------

    void HandleTimescaleKeys()
    {
        for (int i = 1; i <= 12; i++)
        {
            if (Input.GetKeyDown(KeyCode.F1 + (i - 1)))
            {
                Time.timeScale = i;
            }
        }
    }

    // ------------------------------------------------------------------
    // Status overlay (Game view)
    // ------------------------------------------------------------------

    [Header("Status Overlay")]
    [Tooltip("Position/size of the draggable status window.")]
    public Rect statusWindowRect = new Rect(10, 10, 280, 220);

    private GUIStyle smallLabelStyle;
    private GUIStyle walkLabelStyle;

    void OnGUI()
    {
        statusWindowRect = GUILayout.Window(GetInstanceID(), statusWindowRect, DrawStatusWindow, "Ant Walk Builder");
    }

    void DrawStatusWindow(int windowId)
    {
        float dist = Vector3.Distance(transform.position, Application.isPlaying ? home : GetHome());

        if (smallLabelStyle == null)
        {
            smallLabelStyle = new GUIStyle(GUI.skin.label) { fontSize = 10, richText = true };
        }
        if (walkLabelStyle == null)
        {
            walkLabelStyle = new GUIStyle(smallLabelStyle) { fontStyle = FontStyle.Bold };
        }
        walkLabelStyle.normal.textColor = GizmoColorFor(currentType);

        GUILayout.Label($"Walk: {currentType}", walkLabelStyle);
        GUILayout.Label($"<b>Segment:</b> {segmentIndex + 1} / {(playlist != null ? playlist.Count : 0)}", smallLabelStyle);
        GUILayout.Label($"<b>Repeat:</b> {repeatCount}", smallLabelStyle);
        GUILayout.Label($"<b>Step:</b> {step}", smallLabelStyle);
        GUILayout.Label($"<b>Returns:</b> {returns}", smallLabelStyle);
        GUILayout.Label($"<b>Distance to nest:</b> {Mathf.Round(dist)}", smallLabelStyle);
        GUILayout.Label($"<b>Pirouetting:</b> {isPirouetting}", smallLabelStyle);
        GUILayout.Label($"<b>Volting:</b> {isVolting}", smallLabelStyle);
        GUILayout.Label($"<b>(X, Z):</b> ({Mathf.Round(transform.position.x)}, {Mathf.Round(transform.position.z)})", smallLabelStyle);
        GUILayout.Label($"<b>Timescale:</b> {Time.timeScale}", smallLabelStyle);

        GUI.DragWindow();
    }

    // ------------------------------------------------------------------
    // Trajectory gizmos (Scene view)
    // ------------------------------------------------------------------

    // Bright versions of the same hues used to tint each walk type in the Inspector.
    static readonly Color StraightGizmoColor = new Color(0.3f, 0.55f, 1f);
    static readonly Color LoopGizmoColor = new Color(0.3f, 1f, 0.3f);
    static readonly Color TeleportGizmoColor = new Color(0.9f, 0.4f, 1f);
    static readonly Color HalfLoopGizmoColor = new Color(1f, 0.85f, 0.2f);
    static readonly Color SpiralGizmoColor = new Color(1f, 0.45f, 0.15f);
    static readonly Color HomeMarkerColor = new Color(0.6f, 0.1f, 0.1f);

    static Color GizmoColorFor(WalkType type)
    {
        switch (type)
        {
            case WalkType.StraightOutAndBack: return StraightGizmoColor;
            case WalkType.FullLoop: return LoopGizmoColor;
            case WalkType.RandomTeleport: return TeleportGizmoColor;
            case WalkType.HalfLoop: return HalfLoopGizmoColor;
            case WalkType.Spiral: return SpiralGizmoColor;
            default: return Color.white;
        }
    }

    void OnDrawGizmos()
    {
        if (!drawTrajectory || playlist == null)
        {
            return;
        }

        // Anchor to the frozen start-of-play position, not the ant's current (moving) transform.
        Vector3 h = Application.isPlaying ? home : GetHome();

        DrawStartMarker(h);

        for (int i = 0; i < playlist.Count; i++)
        {
            PathDefinition seg = playlist[i];
            bool active = Application.isPlaying && i == segmentIndex;
            Color color = GizmoColorFor(seg.type);
            // Highlight the active segment by blending toward white rather than losing its hue.
            Gizmos.color = active ? Color.Lerp(color, Color.white, 0.5f) : color;
            DrawSegmentGizmo(seg, h);
        }
    }

    static void DrawStartMarker(Vector3 h)
    {
#if UNITY_EDITOR
        Color prevColor = UnityEditor.Handles.color;
        UnityEditor.Handles.color = HomeMarkerColor;
        UnityEditor.Handles.DrawSolidDisc(h, Vector3.up, 0.3f);
        UnityEditor.Handles.color = prevColor;
#else
        Gizmos.color = HomeMarkerColor;
        Gizmos.DrawSphere(h, 0.3f);
#endif
    }

    void DrawSegmentGizmo(PathDefinition seg, Vector3 h)
    {
        switch (seg.type)
        {
            case WalkType.StraightOutAndBack:
            {
                Vector3 outPoint = h + HeadingVector(seg.directionAngle) * seg.distance;
                outPoint.y = h.y;
                Gizmos.DrawLine(h, outPoint);
                Gizmos.DrawWireSphere(outPoint, 0.25f);
                break;
            }
            case WalkType.FullLoop:
            case WalkType.HalfLoop:
            case WalkType.Spiral:
            {
                // All three shapes are just PathOffset(s) sampled along the segment's length, so a
                // series of short line segments is enough -- no need for a dedicated arc drawer.
                // Spirals get more samples so tightly-coiled turns don't render as faceted polygons.
                bool cw = seg.loopDirection == LoopDirection.Clockwise;
                int samples = seg.type == WalkType.Spiral ? Mathf.RoundToInt(48 * Mathf.Max(1f, seg.spiralTurns)) : 48;
                float len = SegmentLength(seg);
                Vector3 prev = h + Flatten(PathOffset(seg, 0f, cw));
                for (int k = 1; k <= samples; k++)
                {
                    float s = len * (k / (float)samples);
                    Vector3 cur = h + Flatten(PathOffset(seg, s, cw));
                    cur.y = h.y;
                    Gizmos.DrawLine(prev, cur);
                    prev = cur;
                }
                if (seg.type == WalkType.Spiral && seg.spiralReturnMode == SpiralReturnMode.TeleportReturn)
                {
                    // No line is drawn back to the nest for this mode -- mark the jump-off point instead.
                    Gizmos.DrawWireSphere(prev, 0.25f);
                }
                break;
            }
            case WalkType.RandomTeleport:
            {
                // Draw the teleport region as a ring tangent to the nest, matching Full Loop's placement.
                int samples = 48;
                float radius = Mathf.Max(0.0001f, seg.teleportDiameter * 0.5f);
                Vector3 center = h + HeadingVector(seg.directionAngle) * radius;
                Vector3 prev = center + new Vector3(radius, 0f, 0f);
                for (int k = 1; k <= samples; k++)
                {
                    float a = 2f * Mathf.PI * (k / (float)samples);
                    Vector3 cur = center + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                    Gizmos.DrawLine(prev, cur);
                    prev = cur;
                }
                break;
            }
        }
    }

    static Vector3 Flatten(Vector3 v)
    {
        return new Vector3(v.x, 0f, v.z);
    }
}
