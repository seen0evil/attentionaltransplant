using System.Collections.Generic;
using UnityEngine;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class DonorVisualizationDataSet
    {
        public string sessionId;
        public string trialId;
        public string sourceLabel;
        public readonly Dictionary<string, float> dwellByTarget = new();
        public readonly Dictionary<string, Dictionary<string, float>> dwellByZoneTarget = new();
        public readonly List<DonorPathSample> pathSamples = new();

        public bool HasObjectDwell => dwellByTarget.Count > 0;
        public bool HasZoneObjectDwell => dwellByZoneTarget.Count > 0;
        public bool HasPathSamples => pathSamples.Count > 1;

        public Dictionary<string, float> BuildDwellByTargetForZone(string zoneId)
        {
            Dictionary<string, float> filtered = new();
            if (string.IsNullOrWhiteSpace(zoneId) ||
                !dwellByZoneTarget.TryGetValue(zoneId.Trim(), out Dictionary<string, float> dwellByTargetForZone))
            {
                return filtered;
            }

            foreach (KeyValuePair<string, float> pair in dwellByTargetForZone)
            {
                if (!string.IsNullOrWhiteSpace(pair.Key) && pair.Value > 0f)
                {
                    filtered[pair.Key] = pair.Value;
                }
            }

            return filtered;
        }
    }

    public readonly struct DonorPathSample
    {
        public DonorPathSample(float timestampSeconds, Vector3 position)
        {
            this.timestampSeconds = timestampSeconds;
            this.position = position;
        }

        public readonly float timestampSeconds;
        public readonly Vector3 position;
    }

    public readonly struct DwellGlowReport
    {
        public DwellGlowReport(int sceneTargetCount, int glowingTargetCount, int unmatchedDwellCount)
        {
            this.sceneTargetCount = sceneTargetCount;
            this.glowingTargetCount = glowingTargetCount;
            this.unmatchedDwellCount = unmatchedDwellCount;
        }

        public readonly int sceneTargetCount;
        public readonly int glowingTargetCount;
        public readonly int unmatchedDwellCount;
    }

    public readonly struct PathHeatmapReport
    {
        public PathHeatmapReport(int sampleCount, int renderedCellCount, float maxCellDwellSeconds)
        {
            this.sampleCount = sampleCount;
            this.renderedCellCount = renderedCellCount;
            this.maxCellDwellSeconds = maxCellDwellSeconds;
        }

        public readonly int sampleCount;
        public readonly int renderedCellCount;
        public readonly float maxCellDwellSeconds;
    }
}
