# AntWalker — An Ant Learning-Walk Simulator

AntWalker is a Unity project that simulates the **learning walks** of ants: the short, structured
exploratory trips that naïve ants perform around the nest entrance before they begin foraging. A
forward-facing camera on the ant agent captures egocentric (first-person) views as the agent walks,
and those image streams — together with the agent's ground-truth position — are logged to disk.

The purpose of the simulator is to generate the visual and positional data used to study how
navigation and **cognitive maps** can emerge from experience. It is the data-collection engine behind
the study *"Transformers with Predictive Coding Learn Cognitive Maps Through the Eyes of Ants"*
(Pandey, McGraw, Wood & Wood), in which transformers trained with a predictive-coding objective on
simulated learning walks spontaneously develop internal maps that support robust place recognition.

## Research goal

Earlier experiments established that even a handful of learning walks are sufficient to produce
internal maps that support robust place recognition. The **current goal is to control the walks
precisely** so we can systematically map out how *particular walks* lead to *particular place
recognition capacities*.

Concretely, we need to be able to control and vary:

- **Length** of each walk (how far from the nest the agent travels).
- **Direction** of each walk.
- **Spatial spread / shape** of the walks (out-and-back lines, loops, etc.).
- A **teleportation** condition, where the agent jumps from place to place *without* moving along
  spatiotemporally connected paths.
- **Pirouettes** (spin-in-place turns) and **voltes** (small circular detours) — including whether the
  agent performs one, both, or neither, and the rate at which it does them.

The two agent scripts below correspond to two eras of this project: the original *stochastic* walker
used for the initial findings, and the new *controlled* walk builder that fulfils the goals above.

---

## The two ant agents (start here)

The heart of the project is the ant agent's movement behaviour. There are two interchangeable
MonoBehaviours you attach to the ant. **Pick one depending on whether you want emergent/random
exploration or precisely controlled walks.**

### `AntRandomWalk` — stochastic foraging walk

[Assets/Scripts/Agents/AntRandomWalk.cs](Assets/Scripts/Agents/AntRandomWalk.cs)

A **probabilistic** walker. The ant wanders with a per-step chance of moving forward or turning, and
an ever-increasing probability of turning back toward the nest the longer it has been away (a
geometric homing schedule via `returnHomeIncrease` / `returnHomeRatio`). Each time it reaches the
nest it "returns" and resets. It also performs random **pirouettes**, and has an optional **grid mode**
that sends the ant on a space-filling survey of the area around the nest.

**Use it when** you want naturalistic, emergent exploration and don't need to prescribe the exact
path — e.g. reproducing the original dataset where walks grow organically and unpredictably.

Key inspector fields: `initialTurnHomeChance`, `returnHomeRatio`, `moveSpeed`, `maxRotatePerStep`,
`perroutteChance`, `forwardChance`, `minNestDistance`, `GridMode`.

### `AntWalkBuilder` — deterministic, configurable walk playlist

[Assets/Scripts/Agents/AntWalkBuilder.cs](Assets/Scripts/Agents/AntWalkBuilder.cs)

A **prescribed** walker. Instead of wandering, the ant follows an ordered **playlist** of
`PathDefinition` segments, each a specific walk type with its own parameters. This is the script that
implements the precise experimental control described in the Research goal.

Walk types (selected per-segment from an enum dropdown):

| Walk type            | What it does                                                              | Key parameters                          |
|----------------------|--------------------------------------------------------------------------|-----------------------------------------|
| `StraightOutAndBack` | Travels out along a direction, then straight back to the nest.           | `distance`, `directionAngle`            |
| `FullLoop`           | One full loop in front of the nest, returning to it.                     | `loopDiameter`, `loopDirection` (CW/CCW/Random) |
| `HalfLoop`           | Out along half the loop's arc (180°), then back the same arc.            | `loopDiameter`, `loopDirection` (CW/CCW/Random) |
| `Spiral`             | Spirals out from the nest to `loopDiameter`, then returns via `spiralReturnMode`. | `loopDiameter`, `loopDirection` (CW/CCW/Random), `spiralTurns`, `spiralReturnMode` |
| `RandomTeleport`     | Hard-teleports to random points within a disk — **no** smooth motion.    | `teleportDiameter`, `holdDuration`       |

A spiral's outbound leg always coils out from the nest to `loopDiameter`/2. `spiralReturnMode`
controls how it gets back:

| `spiralReturnMode` | What it does                                                                          |
|---------------------|----------------------------------------------------------------------------------------|
| `ReverseReturn`     | Spirals back inward along the same coiled path it went out on (the default).           |
| `StraightReturn`    | Walks a straight line from the outermost point directly back to the nest.              |
| `TeleportReturn`    | Hard-teleports back to the nest the instant the outermost point is reached — **no** smooth return motion (only the outbound leg is walked). |

Additional controls:

- **Playlist**: fill `playlist` with segments and set `repeats` per segment; segments are performed
  in order, then the list optionally restarts (`loopPlaylist`). Segments can be chained like tracks in
  a playlist to build a full multi-walk session.
- **Seed**: `seed` seeds Unity's `Random` state on `Start`, so loop direction, teleport spots, and
  pirouette/volte timing & direction are reproducible run-to-run — handy for comparing conditions
  fairly and for re-running a saved [Walk Preset](#walk-presets) exactly.
- **Pirouettes & voltes**: fire independently of the path at a per-step probability
  (`pirouetteChance`, `volteChance`). Each is a *temporary detour* — a pirouette spins in place; a
  volte traces a small circle of configurable `volteSize` — after which the agent returns to exactly
  where it left the path and resumes. Set the chances to `0` to disable either. Teleport segments
  never pirouette or volte.
- **Trajectory preview**: when `drawTrajectory` is on, the planned target path for every segment is
  drawn in the **Scene view** via gizmos (lines, loop/spiral arcs, and a ring for the teleport region),
  so you can lay out a session visually before pressing Play. A `Spiral` with `TeleportReturn` only
  draws its outbound coil, with a wire sphere marking the jump-off point, since there's no smooth
  return path to draw.
- **Episodes**: `returns` increments once per full pass through the playlist (all segments, all their
  repeats) rather than once per individual out-and-back/loop, and is what's passed to `LogManager` as
  the `episode` column. This keeps `LogManager`'s `maximumEpisodes` cutoff landing on a clean playlist
  boundary instead of quitting mid-segment.
- A live status overlay (steps, returns, current walk, distance, pirouetting/volting, coordinates) is
  drawn in the Game view via `OnGUI()`.

**Use it when** you need to control the length, direction, spread, and shape of walks, run the
teleportation condition, or systematically toggle/vary pirouettes and voltes.

> Both scripts respond to **F1–F12** to set `Time.timeScale` (1×–12×) for fast data collection, and
> both feed the shared logging pipeline described below.

---

## Supporting scripts

- [Assets/Scripts/Logging/LogManager.cs](Assets/Scripts/Logging/LogManager.cs) — the data recorder.
  Writes a CSV of selected object attributes (X/Y/Z position and X/Y/Z angle, chosen via the
  `objectAttributes` flags) each step, optionally relative to a `referenceObject` (e.g. the nest). It
  can also render assigned cameras to PNG frames every `cameraFrequency` steps. Output goes under
  `Assets/Logs/<runID>/` (`Objects/<runID>.csv` and `Frames/<cameraName>/`). Caps via
  `maximumEpisodes`, `maximumEntries`, and `maximumCaptures` stop long runs from exploding.
- [Assets/Scripts/Config/ArgumentParser.cs](Assets/Scripts/Config/ArgumentParser.cs) — parses
  command-line options (via `Mono.Options`) for headless/batch runs. Most relevant here is
  `--id=<runID>`, which names the output folder; also `--timescale`, `--steps`, `--cam-frequency`,
  `--resolution`, `--fov`, etc.
- [Assets/Scripts/Attributes/ReadOnlyAttribute.cs](Assets/Scripts/Attributes/ReadOnlyAttribute.cs) —
  a `[ReadOnly]` inspector attribute used to surface live runtime state (step counts, current walk,
  etc.) without making it editable.
- [Assets/Scripts/Agents/SimpleAntController.cs](Assets/Scripts/Agents/SimpleAntController.cs) —
  manual WASD/arrow control of the ant (R to reset, Space to random-teleport, C to toggle a trail
  line). Handy for eyeballing the environment and camera framing.
- [Assets/Scripts/Helpers/ProceduralAnimator.cs](Assets/Scripts/Helpers/ProceduralAnimator.cs) —
  procedural leg-stepping animation (plant/swing/lift) driven purely by body movement, so any of the
  above locomotion scripts get plausible footfalls without hand-authored walk-cycle clips.
- [Assets/Scripts/Helpers/TimescaleManager.cs](Assets/Scripts/Helpers/TimescaleManager.cs) — drives
  `Time.timeScale` from `ArgumentParser.Options.timescale` in built (non-editor) runs, so headless
  batch jobs can request a fixed speed-up via `--timescale` instead of the F1–F12 hotkeys.

---

## Getting started

### Requirements

- **Unity 6000.3.8f1** (Unity 6.3) — see [ProjectSettings/ProjectVersion.txt](ProjectSettings/ProjectVersion.txt).
  Open the project through Unity Hub; matching the editor version avoids reimport surprises.
- Packages are restored automatically from [Packages/manifest.json](Packages/manifest.json) (AI
  Navigation, Input System, TextMeshPro, etc.).

### Open and run

1. Open the project in Unity and load the scene [Assets/Scenes/AntScene.unity](Assets/Scenes/AntScene.unity).
2. Select the ant agent (from the [Ant_RandomWalkAgent](Assets/Prefabs/Agents/Ant/Ant_RandomWalkAgent.prefab)
   prefab).
3. Choose the behaviour:
   - For emergent exploration, keep/attach **`AntRandomWalk`**.
   - For controlled walks, attach **`AntWalkBuilder`** instead, assign its `homeCollider` (the nest),
     and build a `playlist`.
4. (Optional) Assign a **`LogManager`** and set its `loggedObjects`, `objectAttributes`,
   `loggedCameras`, and a `runID` to record data.
5. Press **Play**. Use **F1–F12** to speed up time.

### Configuring a controlled session (`AntWalkBuilder`)

1. On the agent's `AntWalkBuilder`, set `homeCollider` to the nest collider.
2. Expand `playlist` and add `PathDefinition` entries — e.g.:
   - `StraightOutAndBack`, `distance = 10`, `directionAngle = 0`, `repeats = 2`
   - `FullLoop`, `loopDiameter = 5`, `loopDirection = Clockwise`, `repeats = 1`
   - `HalfLoop`, `loopDiameter = 5`, `loopDirection = Random`, `repeats = 1`
   - `Spiral`, `loopDiameter = 8`, `spiralTurns = 3`, `loopDirection = CounterClockwise`, `spiralReturnMode = StraightReturn`, `repeats = 1`
   - `RandomTeleport`, `teleportDiameter = 20`, `holdDuration = 60`, `repeats = 5`
3. Set `loopPlaylist` if the session should repeat, and `seed` if you want the run reproducible.
4. Set `pirouetteChance` / `volteChance` (and `volteSize`) to include or exclude those movements and
   tune their rate.
5. With the agent selected, confirm the planned trajectory looks right in the **Scene view** before
   pressing Play.
6. Once a session is dialled in, save it as a **Walk Preset** (below) so you can recall or share it
   without rebuilding the playlist by hand.

### Walk Presets

[Assets/Walk Presets/](Assets/Walk%20Presets/)

An experimental *condition* in this project is really just "a fully configured `AntWalkBuilder`" — a
playlist plus `moveSpeed`, `seed`, pirouette/volte rates, and so on. Rebuilding that by hand in the
Inspector every time you want to rerun or tweak a condition is slow and error-prone, and it's easy to
lose a good configuration once you move on to the next experiment. Unity's built-in **Preset** asset
solves this: it snapshots *every* serialized field of a component into a small `.preset` file that can
be applied back to any `AntWalkBuilder`, kept under version control, diffed, and shared between
teammates or machines.

- **Applying a preset**: select the agent, open the gear/preset icon in the top-right of the
  `AntWalkBuilder` inspector header, and choose the preset — or just drag the `.preset` asset onto the
  component. This overwrites the playlist and every other field with the saved values.
- **Creating a preset**: once a playlist and its tuning parameters are configured the way you want,
  use that same preset icon → **Save current to...** and save it into `Assets/Walk Presets/` with a
  descriptive name.
- Preset application is by property path, so it's safe across script changes: fields added to
  `AntWalkBuilder` since a preset was saved just keep their default, and fields the preset has that no
  longer exist are silently ignored.

Two presets currently ship in the folder as starting points:

| Preset                  | Condition                                                                                     |
|--------------------------|------------------------------------------------------------------------------------------------|
| `DemoWalkPlaylist.preset` | A mixed session: two angled out-and-backs, a full loop, then a long random-teleport block — a general-purpose demo touching every walk type. |
| `LoopyLoops.preset`      | An all-`FullLoop` session at increasing diameters (10 → 40) with mixed CW/CCW/Random directions — a loop-only condition for isolating how loop size/direction affects place recognition. |

### Where the data goes

With a `LogManager` present and a `runID` set, each run writes to:

```
Assets/Logs/<runID>/
├── Objects/<runID>.csv        # per-step episode, step, and selected position/angle columns
└── Frames/<cameraName>/       # egocentric PNG frames (if cameras + cameraFrequency are configured)
```

The CSV + frames are the inputs used to train and evaluate the predictive-coding models in the
accompanying study.

---

## Reproducing / extending

- To **reproduce the original-style dataset**, use `AntRandomWalk` with progressively larger walk
  extents across runs (the paper used 28 learning walks that grew in distance and complexity).
- To **run controlled experiments**, use `AntWalkBuilder` to script exact walk lengths, directions,
  shapes, the teleport condition, and pirouette/volte rates, then compare downstream place-recognition
  accuracy across conditions. Save each condition as a [Walk Preset](#walk-presets) so it can be
  rerun (with `seed` set, exactly reproduced) or handed to a collaborator.
- New walk shapes can be added by extending the `WalkType` enum and the `PathOffset` / `SegmentLength`
  methods in `AntWalkBuilder`; the gizmo preview and playlist machinery pick them up automatically.
