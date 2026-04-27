# Attentional Transplants Unity Prototype

This Unity project contains the current donor-data collection prototype and visualization scene.

## Main Scenes

- `Assets/Scenes/Playground.unity`: the playable donor collection scene.
- `Assets/Scenes/Visualization.unity`: the scene used to load donor exports and show the current visualization.
- `Assets/Scenes/End.unity`: the scene loaded after the donor reaches the goal.

## Donor Data Collection

When `Playground` starts, runtime recording components are created automatically. The project records:

- player movement and camera pose
- center-of-camera attention hits against `AttentionTarget` objects
- visible and central-cone tracked objects
- zone enter/exit events
- obstacle collisions
- dwell time by target and semantic layer

The current zoning system is generated at runtime by `AttentionGridZoneGenerator`. Both `Playground` and `Visualization` have one `Attention Grid` object configured as a `5 x 5` grid over the playable area. At play time, it creates 25 trigger cells with stable IDs such as `grid_r00_c00` through `grid_r04_c04`.

The older manual `zone1`, `zone2`, and `zone3` objects are left in the scenes but disabled. Do not use them for new zoning work.

## Checking Runtime Zones

1. Open `Assets/Scenes/Playground.unity`.
2. Press Play.
3. Expand `Attention Grid` in the Hierarchy.
4. Confirm there are 25 children named like `Generated Attention Zone grid_r02_c03`.
5. Select a generated child and confirm it has a trigger `BoxCollider` and an `AttentionZone`.

Attention samples now include `playerZoneId`, and trial summaries include `dwellByZoneTarget` for future grid-filtered dwell visualization work.

## Output Files

Donor sessions are written under:

`Application.persistentDataPath/DonorSessions/<sessionId>`

Each session contains `session_meta.json`, `object_catalog.json`, `trial_manifest.json`, and trial-specific samples, events, and summary files.

For detailed run instructions and output-file meanings, see `Docs/Playground_guide.md`.

## Validation

The project includes edit-mode tests for the runtime attention grid in:

`Assets/Scripts/DonorDataCollection/Editor/AttentionGridZoneGeneratorTests.cs`

The fastest compile check is:

```powershell
dotnet build "My project (2).sln" --no-restore -m:1 -v:minimal
```
