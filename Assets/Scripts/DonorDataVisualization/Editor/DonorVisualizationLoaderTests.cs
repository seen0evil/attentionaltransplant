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
    }
}
