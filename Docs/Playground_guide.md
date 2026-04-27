# Playground Donor Guide

## Purpose
`Playground` is the current prototype scene for collecting donor navigation and attention data in Unity. A donor participant moves through a small 3D environment, looks around with the mouse, and tries to reach the goal zone. While they do that, the project records where they moved, which generated grid zone they were in, what the center of the camera was pointed at, what tracked objects were visible, and key events such as entering zones, colliding with obstacles, and reaching the goal.

This guide is for collaborators who want to run the prototype and understand the output files without needing to read the code.

## Quick Start
1. Open the Unity project.
2. Open `attentionaltransplant/Assets/Scenes/Playground.unity`.
3. Press Play in the Unity Editor.
4. Use the mouse to look around and the keyboard to move through the corridor.
5. Navigate to the goal zone.
6. When you enter the goal zone, the trial should end and the project should switch to the `End` scene.
7. After the run, find the newest donor-data folder under `Application.persistentDataPath`.

## What Happens During a Run
- The donor trial starts automatically when `Playground` begins.
- The player object is the existing `Capsule`.
- The main viewing camera is the existing `Main Camera`.
- Runtime recording objects are auto-created when the scene starts, so you do not need to place `SessionManager`, `TrialManager`, `AttentionRecorder`, or `VisibilityRecorder` in the hierarchy by hand.
- The `Attention Grid` object generates a `5 x 5` grid of runtime trigger zones when Play mode starts.
- Entering the goal trigger ends the current trial and loads `attentionaltransplant/Assets/Scenes/End.unity`.

## Scene Elements That Matter
- `AttentionTarget`: Attach this to any object you want tracked in donor data, such as signs, walls, benches, columns, or other meaningful scene elements.
- `TrialObjective`: Attach this to the goal trigger. This is what marks the trial as complete.
- `AttentionGridZoneGenerator`: The configured runtime grid on the `Attention Grid` scene object. It creates the active zones automatically.
- `AttentionZone`: The trigger component used by generated zone cells. The old manual `zone1`, `zone2`, and `zone3` objects remain in the scene only as disabled legacy objects.

## Runtime Attention Grid
`Playground` and `Visualization` now use the same runtime-generated grid setup. Each scene has one active root object named `Attention Grid`.

Current default settings:
- columns: `5`
- rows: `5`
- center: `(-20, 3.5, -15)`
- size: `(60, 60)`
- height: `8`
- zone ID prefix: `grid`

When Play mode starts, the generator creates 25 child trigger cells under `Attention Grid`. Their IDs are deterministic:

```text
grid_r00_c00 ... grid_r00_c04
...
grid_r04_c00 ... grid_r04_c04
```

The row/column ID is also written into each attention sample as `playerZoneId`.

### How To Check Generated Zones
1. Open `attentionaltransplant/Assets/Scenes/Playground.unity`.
2. Press Play.
3. Expand `Attention Grid` in the Hierarchy.
4. Confirm there are 25 generated children named like `Generated Attention Zone grid_r02_c03`.
5. Select a generated child and confirm it has a trigger `BoxCollider` plus an `AttentionZone`.
6. Walk through the scene and confirm `trial_001_events.jsonl` contains `zone_enter` and `zone_exit` events with subjects like `grid_r01_c02`.

### Plain-Language Meanings
- `semanticLayer`: The broad category for a tracked object, such as `Environment`, `Obstacle`, or `Signs`.
- `guidanceRole`: The object's functional role, such as `DirectionalSign`, `Goal`, `Landmark`, `Distractor`, or `Structural`.
- `targetId`: A stable ID for a tracked object. This is the name that will appear in the donor data files.

## Where the Donor Output Is Saved
Each donor run creates a session folder under:

`Application.persistentDataPath/DonorSessions/<sessionId>`

The exact OS path depends on the machine and the Unity Editor environment. The easiest way to find it is to look at the Unity Console after pressing Play. The session manager logs the folder location when a session starts.

## Donor Session Folder Anatomy
Each donor session folder contains:

- `session_meta.json`
- `object_catalog.json`
- `trial_manifest.json`
- `trial_001_samples.jsonl`
- `trial_001_events.jsonl`
- `trial_001_summary.json`

If you later add more trials, you should expect additional trial-specific files with matching names.

## What Each File Is For

### `session_meta.json`
This is the session-level overview. It stores information about the session as a whole rather than a single moment in time.

Typical contents include:
- session ID
- timestamp
- scene name and scene path
- Unity version
- application version
- input profile
- attention sampling rate
- visibility sampling rate

Use this file when you want basic context for a donor run.

### `object_catalog.json`
This is the reference list of tracked objects that were present in the scene when the trial started. It is built from objects that have `AttentionTarget`.

Typical contents include:
- `targetId`
- object name
- semantic category
- guidance role
- parent zone ID, if assigned
- Unity layer
- position
- bounds

Use this file when you want to know what the recorded IDs in the other files actually refer to.

### `trial_manifest.json`
This is the high-level list of trials in the session and the filenames connected to each one.

Typical contents include:
- trial ID
- objective ID
- start and end timestamps
- duration
- success flag
- end reason
- spawn pose
- end pose
- names of the samples, events, and summary files for that trial

Use this file when you want to confirm which files belong to which trial.

### `trial_001_samples.jsonl`
This is the time-based sample stream for the trial. It is a `.jsonl` file, which means it is made of one JSON record per line.

Important: this file is a mixed stream. It contains both:
- `attention_sample`
- `visibility_sample`

`attention_sample` records describe moment-by-moment movement and center-of-camera attention, including:
- player position
- player rotation
- camera position
- camera rotation
- camera forward direction
- movement speed
- current generated grid zone as `playerZoneId`
- center-ray hit target, if any
- hit point and distance

`visibility_sample` records describe what tracked objects were visible at that moment, including:
- visible target IDs
- target IDs inside a central viewing cone

Use this file when you want the detailed time series of what the donor was doing and what they could see.

### `trial_001_events.jsonl`
This is the sparse event log for the trial. Like the samples file, it is one JSON record per line, but it only stores meaningful events rather than continuous samples.

Typical event types include:
- `trial_start`
- `trial_end`
- `objective_reached`
- `zone_enter`
- `zone_exit`
- `obstacle_collision`
- `sign_dwell_start`
- `sign_dwell_end`

Use this file when you want the important milestones of the trial in a compact format.

### `trial_001_summary.json`
This is the compact summary of the trial. It is meant to provide a quick overview without forcing you to re-read all sample lines.

Typical contents include:
- total time
- path length
- number of target switches
- percent of time with no tracked center hit
- attention sample count
- visibility sample count
- first attended target
- set of attended target IDs
- set of visible target IDs
- dwell time by target
- dwell time by semantic layer
- dwell time by generated grid zone and target
- first-seen times for signs

Use this file when you want the fastest summary of what happened in the run.

## Glossary
- `AttentionTarget`: A component used to mark a scene object as something the system should track.
- `semanticLayer`: A broad content category for a tracked object, such as `Environment`, `Obstacle`, or `Signs`.
- `guidanceRole`: The tracked object's functional role in the scene, such as `DirectionalSign` or `Distractor`.
- `trial`: One donor run from start to finish.
- `targetId`: The stable name used to identify a tracked object in the data files.
- `jsonl`: "JSON Lines," a format where each line is its own JSON record.

## How To Sanity-Check a Run
After one successful run in `Playground`, check the following:

1. The scene switches from `Playground` to `End` when the player enters the goal zone.
2. A new donor session folder appears.
3. The folder contains all six expected files.
4. `object_catalog.json` contains the tracked scene objects you expected.
5. During Play mode, `Attention Grid` contains 25 generated zone children.
6. `trial_001_events.jsonl` contains `zone_enter` or `zone_exit` events with `grid_rXX_cXX` subject IDs.
7. `trial_001_events.jsonl` contains both `objective_reached` and `trial_end`.
8. `trial_001_samples.jsonl` attention samples include `playerZoneId`.
9. `trial_001_summary.json` has a non-zero path length and at least some attended or visible targets.

## Troubleshooting

### No donor data folder appears
- Make sure you are running `attentionaltransplant/Assets/Scenes/Playground.unity`, not another scene.
- Check the Unity Console for startup errors.
- Check the Console message that reports where the donor session folder is being written.

### The goal is reached, but the scene does not change
- Confirm that the goal object has `TrialObjective`.
- Confirm that the `End` scene is in Build Settings.
- Confirm that the goal collider is a trigger and that the player actually enters it.

### The scene changes, but there is no summary file
- Check the Unity Console for exceptions during trial end.
- Confirm that the trial reached the goal through `TrialObjective`, rather than changing scenes some other way.

### A tracked object is missing from `object_catalog.json`
- Make sure the object has an `AttentionTarget` component.
- Make sure the object was active in the scene when the trial started.
- Make sure the object has a stable `targetId` and is not being created too late.

### No generated grid zones appear
- Make sure the scene has an active `Attention Grid` object.
- Make sure `Attention Grid` has `AttentionGridZoneGenerator`.
- Press Play before checking for generated cells; the 25 child zones are runtime objects.
- Confirm the disabled legacy `zone1`, `zone2`, and `zone3` objects are not being re-enabled by accident.

## Technical Appendix
For orientation only, the current donor output is created by the scripts in `attentionaltransplant/Assets/Scripts/DonorDataCollection`. The most relevant runtime pieces are:

- `attentionaltransplant/Assets/Scripts/DonorDataCollection/SessionManager.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/TrialManager.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/AttentionRecorder.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/VisibilityRecorder.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/TrialObjective.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/AttentionTarget.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/AttentionZone.cs`
- `attentionaltransplant/Assets/Scripts/DonorDataCollection/AttentionGridZoneGenerator.cs`

Collaborators do not need to edit these scripts to run the prototype, but these are the right files to inspect if technical questions come up.
