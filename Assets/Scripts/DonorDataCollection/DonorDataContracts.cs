using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [Serializable]
    public class DonorSessionRecord
    {
        public int schemaVersion = 1;
        public string sessionId;
        public string participantCode;
        public string sessionLabel;
        public string createdAtUtc;
        public string applicationVersion;
        public string unityVersion;
        public string sceneName;
        public string scenePath;
        public string inputProfile;
        public string persistentDataPath;
        public int attentionSampleRateHz;
        public int visibilitySampleRateHz;
    }

    [Serializable]
    public class ObjectCatalogFile
    {
        public string sessionId;
        public string generatedAtUtc;
        public List<ObjectCatalogRecord> objects = new();
    }

    [Serializable]
    public class ObjectCatalogRecord
    {
        public string targetId;
        public string objectName;
        public string semanticLayer;
        public string guidanceRole;
        public string parentZoneId;
        public string unityLayer;
        public string scenePath;
        public Vector3 position;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
    }

    [Serializable]
    public class TrialManifestFile
    {
        public string sessionId;
        public string generatedAtUtc;
        public List<DonorTrialRecord> trials = new();
    }

    [Serializable]
    public class PoseData
    {
        public Vector3 position;
        public Quaternion rotation;
    }

    [Serializable]
    public class DonorTrialRecord
    {
        public string trialId;
        public string objectiveId;
        public string startAtUtc;
        public string endAtUtc;
        public float durationSeconds;
        public bool success;
        public string endReason;
        public PoseData spawnPose;
        public PoseData endPose;
        public string samplesFile;
        public string eventsFile;
        public string summaryFile;
    }

    [Serializable]
    public class AttentionSampleLine
    {
        public string recordType = "attention_sample";
        public string sessionId;
        public string trialId;
        public int sampleIndex;
        public float timestampSeconds;
        public Vector3 playerPosition;
        public Quaternion playerRotation;
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;
        public Vector3 cameraForward;
        public float speedMetersPerSecond;
        public bool hadRelevantHit;
        public string hitTargetId;
        public string hitSemanticLayer;
        public string hitGuidanceRole;
        public string hitUnityLayer;
        public Vector3 hitPoint;
        public float hitDistance;
    }

    [Serializable]
    public class VisibilitySampleLine
    {
        public string recordType = "visibility_sample";
        public string sessionId;
        public string trialId;
        public int sampleIndex;
        public float timestampSeconds;
        public List<string> visibleTargetIds = new();
        public List<string> centralConeTargetIds = new();
    }

    [Serializable]
    public class TrialEventLine
    {
        public string recordType = "trial_event";
        public string sessionId;
        public string trialId;
        public float timestampSeconds;
        public string eventType;
        public string subjectId;
        public string semanticLayer;
        public string guidanceRole;
        public string message;
        public string payloadJson;
    }

    [Serializable]
    public class DurationEntry
    {
        public string key;
        public float durationSeconds;
    }

    [Serializable]
    public class FirstSeenEntry
    {
        public string targetId;
        public float timestampSeconds;
    }

    [Serializable]
    public class TrialSummaryRecord
    {
        public string sessionId;
        public string trialId;
        public string objectiveId;
        public float totalTimeSeconds;
        public float pathLengthMeters;
        public int targetSwitchCount;
        public float percentNoTargetHit;
        public int attentionSampleCount;
        public int visibilitySampleCount;
        public string firstAttendedTargetId;
        public List<string> attendedTargetIds = new();
        public List<string> visibleTargetIds = new();
        public List<DurationEntry> dwellByTarget = new();
        public List<DurationEntry> dwellBySemanticLayer = new();
        public List<FirstSeenEntry> firstSeenSigns = new();
    }
}
