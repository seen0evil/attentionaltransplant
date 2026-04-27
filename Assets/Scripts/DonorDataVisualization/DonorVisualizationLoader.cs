using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using AttentionalTransplants.DonorDataCollection;
using UnityEngine;

namespace AttentionalTransplants.DonorDataVisualization
{
    public static class DonorVisualizationLoader
    {
        private const string DonorSessionsFolderName = "DonorSessions";
        private const string TrialManifestFileName = "trial_manifest.json";
        private const string SummaryFallbackPattern = "*_summary.json";
        private const string SamplesFallbackPattern = "*_samples.jsonl";

        public static bool TryLoadLatestLocalSession(out DonorVisualizationDataSet dataSet, out string message)
        {
            dataSet = null;

            string sessionsRoot = Path.Combine(Application.persistentDataPath, DonorSessionsFolderName);
            if (!Directory.Exists(sessionsRoot))
            {
                message = $"No donor sessions folder found at {sessionsRoot}.";
                return false;
            }

            string[] sessionFolders = Directory.GetDirectories(sessionsRoot, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(sessionFolders, CompareDirectoriesByNewestWriteTime);

            foreach (string sessionFolder in sessionFolders)
            {
                if (TryLoadSessionFolder(sessionFolder, out dataSet, out message))
                {
                    return true;
                }
            }

            message = "No local donor session contained a readable summary and samples file.";
            return false;
        }

        public static bool TryLoadExportJson(string exportJson, out DonorVisualizationDataSet dataSet, out string message)
        {
            dataSet = null;

            if (string.IsNullOrWhiteSpace(exportJson))
            {
                message = "The imported export JSON was empty.";
                return false;
            }

            DonorDataExportPayload payload;
            try
            {
                payload = JsonUtility.FromJson<DonorDataExportPayload>(exportJson);
            }
            catch (Exception exception)
            {
                message = $"Export JSON could not be parsed: {exception.Message}";
                return false;
            }

            if (payload == null || payload.files == null || payload.files.Count == 0)
            {
                message = "The import is not a donor export payload with embedded files.";
                return false;
            }

            Dictionary<string, string> filesByName = BuildExportFileLookup(payload);
            if (!filesByName.TryGetValue(TrialManifestFileName, out string manifestText))
            {
                message = $"The export is missing {TrialManifestFileName}.";
                return false;
            }

            if (!TryParseManifest(manifestText, out TrialManifestFile manifest, out message))
            {
                return false;
            }

            if (!TrySelectTrialFiles(
                    manifest,
                    fileName => filesByName.ContainsKey(fileName),
                    out DonorTrialRecord selectedTrial,
                    out string summaryFileName,
                    out string samplesFileName,
                    out message))
            {
                return false;
            }

            return TryBuildDataSet(
                manifest.sessionId,
                selectedTrial.trialId,
                payload.sessionFolderName,
                filesByName[summaryFileName],
                filesByName[samplesFileName],
                out dataSet,
                out message);
        }

        private static bool TryLoadSessionFolder(string sessionFolder, out DonorVisualizationDataSet dataSet, out string message)
        {
            dataSet = null;

            string manifestPath = Path.Combine(sessionFolder, TrialManifestFileName);
            if (File.Exists(manifestPath))
            {
                if (!TryParseManifest(File.ReadAllText(manifestPath), out TrialManifestFile manifest, out message))
                {
                    return false;
                }

                if (!TrySelectTrialFiles(
                        manifest,
                        fileName => File.Exists(Path.Combine(sessionFolder, fileName)),
                        out DonorTrialRecord selectedTrial,
                        out string summaryFileName,
                        out string samplesFileName,
                        out message))
                {
                    return false;
                }

                return TryBuildDataSet(
                    manifest.sessionId,
                    selectedTrial.trialId,
                    Path.GetFileName(sessionFolder),
                    File.ReadAllText(Path.Combine(sessionFolder, summaryFileName)),
                    File.ReadAllText(Path.Combine(sessionFolder, samplesFileName)),
                    out dataSet,
                    out message);
            }

            string[] summaryFiles = Directory.GetFiles(sessionFolder, SummaryFallbackPattern, SearchOption.TopDirectoryOnly);
            string[] samplesFiles = Directory.GetFiles(sessionFolder, SamplesFallbackPattern, SearchOption.TopDirectoryOnly);
            Array.Sort(summaryFiles, StringComparer.OrdinalIgnoreCase);
            Array.Sort(samplesFiles, StringComparer.OrdinalIgnoreCase);

            if (summaryFiles.Length == 0 || samplesFiles.Length == 0)
            {
                message = $"Session {Path.GetFileName(sessionFolder)} has no summary/samples pair.";
                return false;
            }

            string trialId = Path.GetFileName(summaryFiles[^1]).Replace("_summary.json", string.Empty);
            return TryBuildDataSet(
                Path.GetFileName(sessionFolder),
                trialId,
                Path.GetFileName(sessionFolder),
                File.ReadAllText(summaryFiles[^1]),
                File.ReadAllText(samplesFiles[^1]),
                out dataSet,
                out message);
        }

        private static bool TryBuildDataSet(
            string sessionId,
            string trialId,
            string sourceLabel,
            string summaryJson,
            string samplesJsonLines,
            out DonorVisualizationDataSet dataSet,
            out string message)
        {
            dataSet = new DonorVisualizationDataSet
            {
                sessionId = string.IsNullOrWhiteSpace(sessionId) ? "unknown_session" : sessionId,
                trialId = string.IsNullOrWhiteSpace(trialId) ? "unknown_trial" : trialId,
                sourceLabel = string.IsNullOrWhiteSpace(sourceLabel) ? "donor data" : sourceLabel
            };

            TrialSummaryRecord summary;
            try
            {
                summary = JsonUtility.FromJson<TrialSummaryRecord>(summaryJson);
            }
            catch (Exception exception)
            {
                message = $"Trial summary could not be parsed: {exception.Message}";
                return false;
            }

            if (summary?.dwellByTarget != null)
            {
                foreach (DurationEntry entry in summary.dwellByTarget)
                {
                    if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.durationSeconds <= 0f)
                    {
                        continue;
                    }

                    dataSet.dwellByTarget[entry.key.Trim()] = entry.durationSeconds;
                }
            }

            if (summary?.dwellByZoneTarget != null)
            {
                foreach (ZoneTargetDurationEntry entry in summary.dwellByZoneTarget)
                {
                    if (entry == null ||
                        string.IsNullOrWhiteSpace(entry.zoneId) ||
                        string.IsNullOrWhiteSpace(entry.targetId) ||
                        entry.durationSeconds <= 0f)
                    {
                        continue;
                    }

                    string zoneId = entry.zoneId.Trim();
                    string targetId = entry.targetId.Trim();
                    if (!dataSet.dwellByZoneTarget.TryGetValue(zoneId, out Dictionary<string, float> dwellByTargetForZone))
                    {
                        dwellByTargetForZone = new Dictionary<string, float>();
                        dataSet.dwellByZoneTarget[zoneId] = dwellByTargetForZone;
                    }

                    if (!dwellByTargetForZone.TryAdd(targetId, entry.durationSeconds))
                    {
                        dwellByTargetForZone[targetId] += entry.durationSeconds;
                    }
                }
            }

            ParseAttentionSamples(samplesJsonLines, dataSet.pathSamples);

            if (!dataSet.HasObjectDwell && !dataSet.HasPathSamples)
            {
                message = "The selected trial had no object dwell and too few attention samples to visualize.";
                return false;
            }

            message = $"Loaded {dataSet.sourceLabel}, trial {dataSet.trialId}.";
            return true;
        }

        private static void ParseAttentionSamples(string samplesJsonLines, List<DonorPathSample> output)
        {
            using StringReader reader = new(samplesJsonLines);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                RecordTypeProbe probe = JsonUtility.FromJson<RecordTypeProbe>(line);
                if (probe == null || !string.Equals(probe.recordType, "attention_sample", StringComparison.Ordinal))
                {
                    continue;
                }

                AttentionSampleLine sample = JsonUtility.FromJson<AttentionSampleLine>(line);
                if (sample == null)
                {
                    continue;
                }

                output.Add(new DonorPathSample(sample.timestampSeconds, sample.playerPosition));
            }
        }

        private static bool TryParseManifest(string manifestJson, out TrialManifestFile manifest, out string message)
        {
            try
            {
                manifest = JsonUtility.FromJson<TrialManifestFile>(manifestJson);
            }
            catch (Exception exception)
            {
                manifest = null;
                message = $"Trial manifest could not be parsed: {exception.Message}";
                return false;
            }

            if (manifest == null || manifest.trials == null || manifest.trials.Count == 0)
            {
                message = "The trial manifest does not contain any trials.";
                return false;
            }

            message = "Manifest parsed.";
            return true;
        }

        private static bool TrySelectTrialFiles(
            TrialManifestFile manifest,
            Predicate<string> fileExists,
            out DonorTrialRecord selectedTrial,
            out string summaryFileName,
            out string samplesFileName,
            out string message)
        {
            List<DonorTrialRecord> trials = new(manifest.trials);
            trials.Sort(CompareTrialsNewestFirst);

            foreach (DonorTrialRecord trial in trials)
            {
                if (trial == null ||
                    string.IsNullOrWhiteSpace(trial.summaryFile) ||
                    string.IsNullOrWhiteSpace(trial.samplesFile))
                {
                    continue;
                }

                if (!fileExists(trial.summaryFile) || !fileExists(trial.samplesFile))
                {
                    continue;
                }

                selectedTrial = trial;
                summaryFileName = trial.summaryFile;
                samplesFileName = trial.samplesFile;
                message = "Trial files selected.";
                return true;
            }

            selectedTrial = null;
            summaryFileName = string.Empty;
            samplesFileName = string.Empty;
            message = "No trial in the manifest had both summary and samples files.";
            return false;
        }

        private static Dictionary<string, string> BuildExportFileLookup(DonorDataExportPayload payload)
        {
            Dictionary<string, string> filesByName = new(StringComparer.OrdinalIgnoreCase);
            foreach (DonorDataExportFile file in payload.files)
            {
                if (file == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(file.fileName))
                {
                    filesByName[file.fileName] = file.text;
                }

                if (!string.IsNullOrWhiteSpace(file.relativePath))
                {
                    filesByName[Path.GetFileName(file.relativePath)] = file.text;
                }
            }

            return filesByName;
        }

        private static int CompareDirectoriesByNewestWriteTime(string left, string right)
        {
            int writeTimeComparison = DateTime.Compare(
                Directory.GetLastWriteTimeUtc(right),
                Directory.GetLastWriteTimeUtc(left));

            return writeTimeComparison != 0
                ? writeTimeComparison
                : string.Compare(right, left, StringComparison.OrdinalIgnoreCase);
        }

        private static int CompareTrialsNewestFirst(DonorTrialRecord left, DonorTrialRecord right)
        {
            DateTime leftTime = ParseTrialTime(left);
            DateTime rightTime = ParseTrialTime(right);
            int timeComparison = DateTime.Compare(rightTime, leftTime);
            return timeComparison != 0
                ? timeComparison
                : string.Compare(right?.trialId, left?.trialId, StringComparison.OrdinalIgnoreCase);
        }

        private static DateTime ParseTrialTime(DonorTrialRecord trial)
        {
            if (trial == null)
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParse(
                    string.IsNullOrWhiteSpace(trial.endAtUtc) ? trial.startAtUtc : trial.endAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal,
                    out DateTime parsed))
            {
                return parsed;
            }

            return DateTime.MinValue;
        }

        [Serializable]
        private class RecordTypeProbe
        {
            public string recordType = string.Empty;
        }
    }
}
