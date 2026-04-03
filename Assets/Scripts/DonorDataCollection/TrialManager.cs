using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [DefaultExecutionOrder(-150)]
    public class TrialManager : MonoBehaviour
    {
        [SerializeField] private bool autoStartDefaultTrial = true;
        [SerializeField] private string defaultTrialId = "trial_001";
        [SerializeField] private string defaultObjectiveId = "exploration_goal";
        [SerializeField] private float defaultTimeLimitSeconds;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Camera attentionCamera;

        public static TrialManager Instance { get; private set; }

        public bool IsTrialActive => activeTrial != null;
        public Transform PlayerRoot => playerRoot;
        public Camera AttentionCamera => attentionCamera;
        public string CurrentTrialId => activeTrial != null ? activeTrial.trialRecord.trialId : string.Empty;

        public event Action<ActiveTrialContext> TrialStarted;
        public event Action<ActiveTrialContext, TrialSummaryRecord> TrialEnded;

        private ActiveTrialContext activeTrial;
        private SessionManager sessionManager;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Start()
        {
            sessionManager = SessionManager.Instance;
            ResolveBindings();

            if (autoStartDefaultTrial)
            {
                BeginTrial(defaultTrialId, defaultObjectiveId, defaultTimeLimitSeconds);
            }
        }

        private void Update()
        {
            if (!IsTrialActive)
            {
                return;
            }

            if (activeTrial.timeLimitSeconds > 0f && GetElapsedTimeSeconds() >= activeTrial.timeLimitSeconds)
            {
                EndTrial(TrialEndReason.Timeout);
            }
        }

        public void BeginTrial(string trialId, string objectiveId, float timeLimitSeconds = 0f)
        {
            sessionManager ??= SessionManager.Instance;
            ResolveBindings();

            if (sessionManager == null || playerRoot == null || attentionCamera == null)
            {
                Debug.LogWarning("TrialManager could not start a trial because the session manager, player, or camera is missing.", this);
                return;
            }

            if (IsTrialActive)
            {
                EndTrial(TrialEndReason.ManualStop);
            }

            sessionManager.WriteObjectCatalog();

            string resolvedTrialId = string.IsNullOrWhiteSpace(trialId) ? "trial_001" : trialId.Trim();
            TrialFileSet fileSet = sessionManager.CreateTrialFiles(resolvedTrialId);

            activeTrial = new ActiveTrialContext
            {
                sessionId = sessionManager.SessionId,
                trialRecord = new DonorTrialRecord
                {
                    trialId = resolvedTrialId,
                    objectiveId = objectiveId,
                    startAtUtc = DateTime.UtcNow.ToString("O"),
                    spawnPose = new PoseData
                    {
                        position = playerRoot.position,
                        rotation = playerRoot.rotation
                    },
                    samplesFile = System.IO.Path.GetFileName(fileSet.SamplesPath),
                    eventsFile = System.IO.Path.GetFileName(fileSet.EventsPath),
                    summaryFile = System.IO.Path.GetFileName(fileSet.SummaryPath)
                },
                fileSet = fileSet,
                startRealtime = Time.realtimeSinceStartupAsDouble,
                timeLimitSeconds = timeLimitSeconds,
                lastPathPosition = playerRoot.position
            };

            sessionManager.UpsertTrialRecord(activeTrial.trialRecord);
            RecordEvent("trial_start", message: $"Started trial '{resolvedTrialId}'.");
            TrialStarted?.Invoke(activeTrial);
        }

        public void CompleteCurrentTrial(string objectiveId)
        {
            if (!IsTrialActive)
            {
                return;
            }

            activeTrial.trialRecord.objectiveId = objectiveId;
            RecordEvent("objective_reached", subjectId: objectiveId, message: $"Objective '{objectiveId}' reached.");
            EndTrial(TrialEndReason.ObjectiveReached);
        }

        public void StopCurrentTrial(string reason = "manual_stop")
        {
            if (!IsTrialActive)
            {
                return;
            }

            RecordEvent(reason, message: "Trial stopped manually.");
            EndTrial(TrialEndReason.ManualStop);
        }

        public void RecordAttentionSample(AttentionSampleLine sampleLine)
        {
            if (!IsTrialActive)
            {
                return;
            }

            JsonFileUtility.AppendJsonLine(activeTrial.fileSet.SamplesPath, sampleLine);

            activeTrial.summaryState.attentionSampleCount++;
            float deltaTime = sampleLine.timestampSeconds - activeTrial.lastAttentionTimestampSeconds;
            if (deltaTime < 0f)
            {
                deltaTime = 0f;
            }

            activeTrial.summaryState.totalNoHitTimeSeconds += sampleLine.hadRelevantHit ? 0f : deltaTime;
            activeTrial.summaryState.pathLengthMeters += Vector3.Distance(activeTrial.lastPathPosition, sampleLine.playerPosition);
            activeTrial.lastPathPosition = sampleLine.playerPosition;

            string currentTargetId = sampleLine.hadRelevantHit ? sampleLine.hitTargetId : string.Empty;
            if (!string.IsNullOrEmpty(currentTargetId))
            {
                activeTrial.summaryState.attendedTargetIds.Add(currentTargetId);
                if (string.IsNullOrEmpty(activeTrial.summaryState.firstAttendedTargetId))
                {
                    activeTrial.summaryState.firstAttendedTargetId = currentTargetId;
                }
            }

            if (!string.IsNullOrEmpty(activeTrial.lastAttentionTargetId) &&
                !string.IsNullOrEmpty(currentTargetId) &&
                !string.Equals(activeTrial.lastAttentionTargetId, currentTargetId, StringComparison.Ordinal))
            {
                activeTrial.summaryState.targetSwitchCount++;
            }

            if (!string.IsNullOrEmpty(currentTargetId) && deltaTime > 0f)
            {
                AddDuration(activeTrial.summaryState.dwellByTarget, currentTargetId, deltaTime);
                AddDuration(activeTrial.summaryState.dwellBySemanticLayer, sampleLine.hitSemanticLayer, deltaTime);
            }

            activeTrial.lastAttentionTargetId = currentTargetId;
            activeTrial.lastAttentionTimestampSeconds = sampleLine.timestampSeconds;
        }

        public void RecordVisibilitySample(VisibilitySampleLine sampleLine, IReadOnlyList<AttentionTarget> visibleTargets)
        {
            if (!IsTrialActive)
            {
                return;
            }

            JsonFileUtility.AppendJsonLine(activeTrial.fileSet.SamplesPath, sampleLine);
            activeTrial.summaryState.visibilitySampleCount++;

            foreach (AttentionTarget target in visibleTargets)
            {
                activeTrial.summaryState.visibleTargetIds.Add(target.ResolvedTargetId);

                if (!target.IsSignLike || activeTrial.summaryState.firstSeenSigns.ContainsKey(target.ResolvedTargetId))
                {
                    continue;
                }

                activeTrial.summaryState.firstSeenSigns[target.ResolvedTargetId] = sampleLine.timestampSeconds;
            }
        }

        public void RecordZoneEvent(string zoneId, bool entered)
        {
            if (!IsTrialActive)
            {
                return;
            }

            string eventType = entered ? "zone_enter" : "zone_exit";
            RecordEvent(eventType, subjectId: zoneId, message: $"{eventType} '{zoneId}'.");
        }

        public void RecordObstacleCollision(AttentionTarget target, Vector3 hitPoint)
        {
            if (!IsTrialActive)
            {
                return;
            }

            CollisionPayload payload = new()
            {
                hitPoint = hitPoint
            };

            RecordEvent(
                "obstacle_collision",
                subjectId: target != null ? target.ResolvedTargetId : string.Empty,
                semanticLayer: target != null ? target.ResolvedSemanticLayer.ToString() : AttentionSemanticLayer.Obstacle.ToString(),
                guidanceRole: target != null ? target.GuidanceRole.ToString() : GuidanceRole.Distractor.ToString(),
                message: "Player collided with an obstacle.",
                payloadJson: JsonUtility.ToJson(payload));
        }

        public void RecordCustomEvent(string eventType, string subjectId = "", string message = "", string payloadJson = "")
        {
            if (!IsTrialActive)
            {
                return;
            }

            RecordEvent(eventType, subjectId, string.Empty, string.Empty, message, payloadJson);
        }

        public float GetElapsedTimeSeconds()
        {
            return !IsTrialActive ? 0f : (float)(Time.realtimeSinceStartupAsDouble - activeTrial.startRealtime);
        }

        private void EndTrial(TrialEndReason endReason)
        {
            if (!IsTrialActive)
            {
                return;
            }

            activeTrial.trialRecord.endAtUtc = DateTime.UtcNow.ToString("O");
            activeTrial.trialRecord.durationSeconds = GetElapsedTimeSeconds();
            activeTrial.trialRecord.success = endReason == TrialEndReason.ObjectiveReached;
            activeTrial.trialRecord.endReason = endReason.ToString();
            activeTrial.trialRecord.endPose = new PoseData
            {
                position = playerRoot != null ? playerRoot.position : Vector3.zero,
                rotation = playerRoot != null ? playerRoot.rotation : Quaternion.identity
            };

            RecordEvent("trial_end", message: $"Trial ended with reason '{endReason}'.");

            TrialSummaryRecord summaryRecord = BuildSummaryRecord();
            sessionManager.UpsertTrialRecord(activeTrial.trialRecord);
            sessionManager.WriteTrialSummary(activeTrial.fileSet.SummaryPath, summaryRecord);

            ActiveTrialContext completedTrial = activeTrial;
            TrialEnded?.Invoke(completedTrial, summaryRecord);
            activeTrial = null;
        }

        private TrialSummaryRecord BuildSummaryRecord()
        {
            float totalTimeSeconds = Mathf.Max(0f, activeTrial.trialRecord.durationSeconds);
            TrialSummaryRecord summaryRecord = new()
            {
                sessionId = activeTrial.sessionId,
                trialId = activeTrial.trialRecord.trialId,
                objectiveId = activeTrial.trialRecord.objectiveId,
                totalTimeSeconds = totalTimeSeconds,
                pathLengthMeters = activeTrial.summaryState.pathLengthMeters,
                targetSwitchCount = activeTrial.summaryState.targetSwitchCount,
                percentNoTargetHit = totalTimeSeconds <= Mathf.Epsilon
                    ? 0f
                    : activeTrial.summaryState.totalNoHitTimeSeconds / totalTimeSeconds,
                attentionSampleCount = activeTrial.summaryState.attentionSampleCount,
                visibilitySampleCount = activeTrial.summaryState.visibilitySampleCount,
                firstAttendedTargetId = activeTrial.summaryState.firstAttendedTargetId
            };

            foreach (string targetId in activeTrial.summaryState.attendedTargetIds)
            {
                summaryRecord.attendedTargetIds.Add(targetId);
            }

            summaryRecord.attendedTargetIds.Sort(StringComparer.Ordinal);

            foreach (string targetId in activeTrial.summaryState.visibleTargetIds)
            {
                summaryRecord.visibleTargetIds.Add(targetId);
            }

            summaryRecord.visibleTargetIds.Sort(StringComparer.Ordinal);

            foreach (KeyValuePair<string, float> pair in activeTrial.summaryState.dwellByTarget)
            {
                summaryRecord.dwellByTarget.Add(new DurationEntry
                {
                    key = pair.Key,
                    durationSeconds = pair.Value
                });
            }

            summaryRecord.dwellByTarget.Sort((left, right) => string.CompareOrdinal(left.key, right.key));

            foreach (KeyValuePair<string, float> pair in activeTrial.summaryState.dwellBySemanticLayer)
            {
                summaryRecord.dwellBySemanticLayer.Add(new DurationEntry
                {
                    key = pair.Key,
                    durationSeconds = pair.Value
                });
            }

            summaryRecord.dwellBySemanticLayer.Sort((left, right) => string.CompareOrdinal(left.key, right.key));

            foreach (KeyValuePair<string, float> pair in activeTrial.summaryState.firstSeenSigns)
            {
                summaryRecord.firstSeenSigns.Add(new FirstSeenEntry
                {
                    targetId = pair.Key,
                    timestampSeconds = pair.Value
                });
            }

            summaryRecord.firstSeenSigns.Sort((left, right) => string.CompareOrdinal(left.targetId, right.targetId));
            return summaryRecord;
        }

        private void RecordEvent(
            string eventType,
            string subjectId = "",
            string semanticLayer = "",
            string guidanceRole = "",
            string message = "",
            string payloadJson = "")
        {
            if (!IsTrialActive)
            {
                return;
            }

            TrialEventLine eventLine = new()
            {
                sessionId = activeTrial.sessionId,
                trialId = activeTrial.trialRecord.trialId,
                timestampSeconds = GetElapsedTimeSeconds(),
                eventType = eventType,
                subjectId = subjectId,
                semanticLayer = semanticLayer,
                guidanceRole = guidanceRole,
                message = message,
                payloadJson = payloadJson
            };

            JsonFileUtility.AppendJsonLine(activeTrial.fileSet.EventsPath, eventLine);
        }

        private void ResolveBindings()
        {
            if (playerRoot == null)
            {
                SimplePlayerMovement movement = FindAnyObjectByType<SimplePlayerMovement>();
                if (movement != null)
                {
                    playerRoot = movement.transform;
                }
            }

            if (attentionCamera == null)
            {
                attentionCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            }
        }

        private static void AddDuration(Dictionary<string, float> durations, string key, float deltaSeconds)
        {
            if (string.IsNullOrWhiteSpace(key) || deltaSeconds <= 0f)
            {
                return;
            }

            if (!durations.TryAdd(key, deltaSeconds))
            {
                durations[key] += deltaSeconds;
            }
        }
    }

    [Serializable]
    public class ActiveTrialContext
    {
        public string sessionId;
        public DonorTrialRecord trialRecord;
        public TrialFileSet fileSet;
        public double startRealtime;
        public float timeLimitSeconds;
        public TrialSummaryState summaryState = new();
        public Vector3 lastPathPosition;
        public string lastAttentionTargetId;
        public float lastAttentionTimestampSeconds;
    }

    [Serializable]
    public class TrialSummaryState
    {
        public float pathLengthMeters;
        public int targetSwitchCount;
        public float totalNoHitTimeSeconds;
        public int attentionSampleCount;
        public int visibilitySampleCount;
        public string firstAttendedTargetId;
        public readonly HashSet<string> attendedTargetIds = new();
        public readonly HashSet<string> visibleTargetIds = new();
        public readonly Dictionary<string, float> dwellByTarget = new();
        public readonly Dictionary<string, float> dwellBySemanticLayer = new();
        public readonly Dictionary<string, float> firstSeenSigns = new();
    }

    [Serializable]
    public class CollisionPayload
    {
        public Vector3 hitPoint;
    }
}
