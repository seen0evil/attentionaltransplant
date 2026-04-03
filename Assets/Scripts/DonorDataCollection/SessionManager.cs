using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttentionalTransplants.DonorDataCollection
{
    [DefaultExecutionOrder(-200)]
    public class SessionManager : MonoBehaviour
    {
        [SerializeField] private string participantCode = "anonymous";
        [SerializeField] private string sessionLabel = "donor";
        [SerializeField] private string outputFolderName = "DonorSessions";
        [SerializeField] private int attentionSampleRateHz = 20;
        [SerializeField] private int visibilitySampleRateHz = 4;

        public static SessionManager Instance { get; private set; }

        public string SessionId { get; private set; }
        public string SessionFolderPath { get; private set; }
        public int AttentionSampleRateHz => Mathf.Max(1, attentionSampleRateHz);
        public int VisibilitySampleRateHz => Mathf.Max(1, visibilitySampleRateHz);

        private readonly TrialManifestFile manifest = new();
        private string sessionMetaPath;
        private string objectCatalogPath;
        private string trialManifestPath;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            SessionId = $"{sessionLabel}_{DateTime.UtcNow:yyyyMMddTHHmmss}_{Guid.NewGuid().ToString("N")[..6]}";
            SessionFolderPath = Path.Combine(Application.persistentDataPath, outputFolderName, SessionId);
            Directory.CreateDirectory(SessionFolderPath);

            sessionMetaPath = Path.Combine(SessionFolderPath, "session_meta.json");
            objectCatalogPath = Path.Combine(SessionFolderPath, "object_catalog.json");
            trialManifestPath = Path.Combine(SessionFolderPath, "trial_manifest.json");

            manifest.sessionId = SessionId;

            WriteSessionMeta();
            WriteObjectCatalog();
            WriteTrialManifest();

            Debug.Log($"Donor session data will be written to: {SessionFolderPath}", this);
        }

        public TrialFileSet CreateTrialFiles(string trialId)
        {
            string safeTrialId = string.IsNullOrWhiteSpace(trialId) ? "trial" : trialId.Trim();
            return new TrialFileSet
            {
                SamplesPath = Path.Combine(SessionFolderPath, $"{safeTrialId}_samples.jsonl"),
                EventsPath = Path.Combine(SessionFolderPath, $"{safeTrialId}_events.jsonl"),
                SummaryPath = Path.Combine(SessionFolderPath, $"{safeTrialId}_summary.json")
            };
        }

        public void UpsertTrialRecord(DonorTrialRecord trialRecord)
        {
            int existingIndex = manifest.trials.FindIndex(record => record.trialId == trialRecord.trialId);
            if (existingIndex >= 0)
            {
                manifest.trials[existingIndex] = trialRecord;
            }
            else
            {
                manifest.trials.Add(trialRecord);
            }

            WriteTrialManifest();
        }

        public void WriteTrialSummary(string path, TrialSummaryRecord summaryRecord)
        {
            JsonFileUtility.WriteJson(path, summaryRecord);
        }

        public void WriteObjectCatalog()
        {
            IReadOnlyList<AttentionTarget> targets = AttentionTarget.ActiveTargets;
            ObjectCatalogFile catalog = new()
            {
                sessionId = SessionId,
                generatedAtUtc = DateTime.UtcNow.ToString("O")
            };

            List<AttentionTarget> sortedTargets = new(targets);
            sortedTargets.Sort((left, right) => string.CompareOrdinal(left.ResolvedTargetId, right.ResolvedTargetId));

            foreach (AttentionTarget target in sortedTargets)
            {
                Bounds bounds = target.GetWorldBounds();
                catalog.objects.Add(new ObjectCatalogRecord
                {
                    targetId = target.ResolvedTargetId,
                    objectName = target.gameObject.name,
                    semanticLayer = target.ResolvedSemanticLayer.ToString(),
                    guidanceRole = target.GuidanceRole.ToString(),
                    parentZoneId = target.ParentZoneId,
                    unityLayer = LayerMask.LayerToName(target.gameObject.layer),
                    scenePath = target.ScenePath,
                    position = target.transform.position,
                    boundsCenter = bounds.center,
                    boundsSize = bounds.size
                });
            }

            JsonFileUtility.WriteJson(objectCatalogPath, catalog);
        }

        private void WriteSessionMeta()
        {
            Scene activeScene = SceneManager.GetActiveScene();
            DonorSessionRecord sessionRecord = new()
            {
                sessionId = SessionId,
                participantCode = participantCode,
                sessionLabel = sessionLabel,
                createdAtUtc = DateTime.UtcNow.ToString("O"),
                applicationVersion = string.IsNullOrWhiteSpace(Application.version) ? "0.0.0" : Application.version,
                unityVersion = Application.unityVersion,
                sceneName = activeScene.name,
                scenePath = activeScene.path,
                inputProfile = "keyboard_mouse_first_person",
                persistentDataPath = SessionFolderPath,
                attentionSampleRateHz = AttentionSampleRateHz,
                visibilitySampleRateHz = VisibilitySampleRateHz
            };

            JsonFileUtility.WriteJson(sessionMetaPath, sessionRecord);
        }

        private void WriteTrialManifest()
        {
            manifest.generatedAtUtc = DateTime.UtcNow.ToString("O");
            JsonFileUtility.WriteJson(trialManifestPath, manifest);
        }
    }

    public struct TrialFileSet
    {
        public string SamplesPath;
        public string EventsPath;
        public string SummaryPath;
    }
}
