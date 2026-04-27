using AttentionalTransplants.DonorDataCollection;
using AttentionalTransplants.DonorDataVisualization;
using NUnit.Framework;
using UnityEngine;

namespace AttentionalTransplants.DonorDataVisualizationTests
{
    public class DonorVisualizationLoaderTests
    {
        [Test]
        public void TryLoadExportJsonReadsDwellAndAttentionPathSamples()
        {
            DonorDataExportPayload payload = new()
            {
                exportFormat = "attentionaltransplant_donor_export_v1",
                sessionFolderName = "donor_20260426"
            };

            TrialManifestFile manifest = new()
            {
                sessionId = "session_001"
            };
            manifest.trials.Add(new DonorTrialRecord
            {
                trialId = "trial_001",
                startAtUtc = "2026-04-26T10:00:00Z",
                endAtUtc = "2026-04-26T10:01:00Z",
                samplesFile = "trial_001_samples.jsonl",
                summaryFile = "trial_001_summary.json"
            });

            TrialSummaryRecord summary = new()
            {
                sessionId = "session_001",
                trialId = "trial_001"
            };
            summary.dwellByTarget.Add(new DurationEntry
            {
                key = "sign",
                durationSeconds = 2.5f
            });

            string samples =
                JsonUtility.ToJson(new AttentionSampleLine
                {
                    timestampSeconds = 0f,
                    playerPosition = new Vector3(1f, 2f, 3f)
                }) + "\n" +
                JsonUtility.ToJson(new VisibilitySampleLine
                {
                    timestampSeconds = 0.25f
                }) + "\n" +
                JsonUtility.ToJson(new AttentionSampleLine
                {
                    timestampSeconds = 0.5f,
                    playerPosition = new Vector3(2f, 2f, 4f)
                });

            payload.files.Add(new DonorDataExportFile
            {
                fileName = "trial_manifest.json",
                relativePath = "trial_manifest.json",
                text = JsonUtility.ToJson(manifest)
            });
            payload.files.Add(new DonorDataExportFile
            {
                fileName = "trial_001_summary.json",
                relativePath = "trial_001_summary.json",
                text = JsonUtility.ToJson(summary)
            });
            payload.files.Add(new DonorDataExportFile
            {
                fileName = "trial_001_samples.jsonl",
                relativePath = "trial_001_samples.jsonl",
                text = samples
            });

            bool loaded = DonorVisualizationLoader.TryLoadExportJson(
                JsonUtility.ToJson(payload),
                out DonorVisualizationDataSet dataSet,
                out string message);

            Assert.IsTrue(loaded, message);
            Assert.AreEqual("session_001", dataSet.sessionId);
            Assert.AreEqual("trial_001", dataSet.trialId);
            Assert.AreEqual(2.5f, dataSet.dwellByTarget["sign"]);
            Assert.AreEqual(2, dataSet.pathSamples.Count);
            Assert.AreEqual(new Vector3(2f, 2f, 4f), dataSet.pathSamples[1].position);
        }

        [Test]
        public void TryLoadExportJsonReadsZoneDwellAndIgnoresInvalidEntries()
        {
            TrialSummaryRecord summary = new()
            {
                sessionId = "session_001",
                trialId = "trial_001"
            };
            summary.dwellByTarget.Add(new DurationEntry
            {
                key = "sign",
                durationSeconds = 3f
            });
            summary.dwellByZoneTarget.Add(new ZoneTargetDurationEntry
            {
                zoneId = "grid_r02_c03",
                targetId = "sign",
                durationSeconds = 1.25f
            });
            summary.dwellByZoneTarget.Add(new ZoneTargetDurationEntry
            {
                zoneId = "grid_r02_c03",
                targetId = "obstacle",
                durationSeconds = 0.75f
            });
            summary.dwellByZoneTarget.Add(new ZoneTargetDurationEntry
            {
                zoneId = "",
                targetId = "ignored",
                durationSeconds = 4f
            });
            summary.dwellByZoneTarget.Add(new ZoneTargetDurationEntry
            {
                zoneId = "grid_r03_c03",
                targetId = "ignored",
                durationSeconds = -1f
            });

            bool loaded = DonorVisualizationLoader.TryLoadExportJson(
                BuildExportJson(summary),
                out DonorVisualizationDataSet dataSet,
                out string message);

            Assert.IsTrue(loaded, message);
            Assert.IsTrue(dataSet.HasZoneObjectDwell);
            Assert.AreEqual(2, dataSet.dwellByZoneTarget["grid_r02_c03"].Count);
            Assert.AreEqual(1.25f, dataSet.dwellByZoneTarget["grid_r02_c03"]["sign"]);
            Assert.AreEqual(0.75f, dataSet.dwellByZoneTarget["grid_r02_c03"]["obstacle"]);
            Assert.IsFalse(dataSet.dwellByZoneTarget.ContainsKey("grid_r03_c03"));
        }

        [Test]
        public void TryLoadExportJsonAcceptsOlderSummaryWithoutZoneDwell()
        {
            string summaryJson =
                "{\"sessionId\":\"session_001\",\"trialId\":\"trial_001\",\"dwellByTarget\":[{\"key\":\"sign\",\"durationSeconds\":2.5}]}";

            bool loaded = DonorVisualizationLoader.TryLoadExportJson(
                BuildExportJson(summaryJson),
                out DonorVisualizationDataSet dataSet,
                out string message);

            Assert.IsTrue(loaded, message);
            Assert.AreEqual(2.5f, dataSet.dwellByTarget["sign"]);
            Assert.IsFalse(dataSet.HasZoneObjectDwell);
        }

        [Test]
        public void BuildDwellByTargetForZoneFiltersWithoutChangingFullDwell()
        {
            DonorVisualizationDataSet dataSet = new();
            dataSet.dwellByTarget["sign"] = 5f;
            dataSet.dwellByTarget["obstacle"] = 3f;
            dataSet.dwellByZoneTarget["grid_r02_c03"] = new()
            {
                ["sign"] = 1.5f
            };

            System.Collections.Generic.Dictionary<string, float> filtered = dataSet.BuildDwellByTargetForZone("grid_r02_c03");
            System.Collections.Generic.Dictionary<string, float> unknown = dataSet.BuildDwellByTargetForZone("grid_r00_c00");

            Assert.AreEqual(1, filtered.Count);
            Assert.AreEqual(1.5f, filtered["sign"]);
            Assert.AreEqual(0, unknown.Count);
            Assert.AreEqual(2, dataSet.dwellByTarget.Count);
            Assert.AreEqual(5f, dataSet.dwellByTarget["sign"]);
        }

        [Test]
        public void TryLoadExportJsonRejectsEmptyPayload()
        {
            bool loaded = DonorVisualizationLoader.TryLoadExportJson(
                string.Empty,
                out DonorVisualizationDataSet dataSet,
                out string message);

            Assert.IsFalse(loaded);
            Assert.IsNull(dataSet);
            Assert.IsNotEmpty(message);
        }

        private static string BuildExportJson(TrialSummaryRecord summary)
        {
            return BuildExportJson(JsonUtility.ToJson(summary));
        }

        private static string BuildExportJson(string summaryJson)
        {
            DonorDataExportPayload payload = new()
            {
                exportFormat = "attentionaltransplant_donor_export_v1",
                sessionFolderName = "donor_20260426"
            };

            TrialManifestFile manifest = new()
            {
                sessionId = "session_001"
            };
            manifest.trials.Add(new DonorTrialRecord
            {
                trialId = "trial_001",
                startAtUtc = "2026-04-26T10:00:00Z",
                endAtUtc = "2026-04-26T10:01:00Z",
                samplesFile = "trial_001_samples.jsonl",
                summaryFile = "trial_001_summary.json"
            });

            string samples =
                JsonUtility.ToJson(new AttentionSampleLine
                {
                    timestampSeconds = 0f,
                    playerPosition = new Vector3(1f, 2f, 3f)
                }) + "\n" +
                JsonUtility.ToJson(new AttentionSampleLine
                {
                    timestampSeconds = 0.5f,
                    playerPosition = new Vector3(2f, 2f, 4f)
                });

            payload.files.Add(new DonorDataExportFile
            {
                fileName = "trial_manifest.json",
                relativePath = "trial_manifest.json",
                text = JsonUtility.ToJson(manifest)
            });
            payload.files.Add(new DonorDataExportFile
            {
                fileName = "trial_001_summary.json",
                relativePath = "trial_001_summary.json",
                text = summaryJson
            });
            payload.files.Add(new DonorDataExportFile
            {
                fileName = "trial_001_samples.jsonl",
                relativePath = "trial_001_samples.jsonl",
                text = samples
            });

            return JsonUtility.ToJson(payload);
        }
    }
}
