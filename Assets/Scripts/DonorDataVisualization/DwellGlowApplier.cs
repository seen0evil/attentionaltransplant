using System;
using System.Collections.Generic;
using AttentionalTransplants.DonorDataCollection;
using UnityEngine;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class DwellGlowApplier : MonoBehaviour
    {
        [SerializeField] private Color lowDwellColor = new(0.1f, 0.8f, 1f, 1f);
        [SerializeField] private Color highDwellColor = new(1f, 0.85f, 0.1f, 1f);
        [SerializeField] private float minEmissionIntensity = 0.3f;
        [SerializeField] private float maxEmissionIntensity = 5f;

        public DwellGlowReport Apply(DonorVisualizationDataSet dataSet)
        {
            IReadOnlyList<AttentionTarget> activeTargets = AttentionTarget.ActiveTargets;
            Dictionary<string, AttentionTarget> targetsById = new(StringComparer.Ordinal);
            for (int index = 0; index < activeTargets.Count; index++)
            {
                AttentionTarget target = activeTargets[index];
                if (target != null)
                {
                    targetsById[target.ResolvedTargetId] = target;
                }
            }

            float maxDwellSeconds = 0f;
            foreach (float dwellSeconds in dataSet.dwellByTarget.Values)
            {
                maxDwellSeconds = Mathf.Max(maxDwellSeconds, dwellSeconds);
            }

            int glowingTargetCount = 0;
            foreach (AttentionTarget target in activeTargets)
            {
                if (target == null)
                {
                    continue;
                }

                dataSet.dwellByTarget.TryGetValue(target.ResolvedTargetId, out float dwellSeconds);
                if (dwellSeconds > 0f && maxDwellSeconds > 0f)
                {
                    glowingTargetCount++;
                }

                ApplyEmission(target, dwellSeconds, maxDwellSeconds);
            }

            int unmatchedDwellCount = 0;
            foreach (string targetId in dataSet.dwellByTarget.Keys)
            {
                if (!targetsById.ContainsKey(targetId))
                {
                    unmatchedDwellCount++;
                }
            }

            return new DwellGlowReport(activeTargets.Count, glowingTargetCount, unmatchedDwellCount);
        }

        private void ApplyEmission(AttentionTarget target, float dwellSeconds, float maxDwellSeconds)
        {
            float normalized = maxDwellSeconds <= Mathf.Epsilon
                ? 0f
                : Mathf.Clamp01(dwellSeconds / maxDwellSeconds);

            Color emissionColor = normalized <= 0f
                ? Color.black
                : Color.Lerp(lowDwellColor, highDwellColor, normalized) *
                  Mathf.Lerp(minEmissionIntensity, maxEmissionIntensity, normalized);

            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer targetRenderer in renderers)
            {
                Material[] materials = targetRenderer.materials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material material = materials[materialIndex];
                    if (material == null)
                    {
                        continue;
                    }

                    material.EnableKeyword("_EMISSION");
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

                    if (material.HasProperty("_EmissionColor"))
                    {
                        material.SetColor("_EmissionColor", emissionColor);
                    }

                    if (normalized > 0f && material.HasProperty("_BaseColor"))
                    {
                        Color baseColor = material.GetColor("_BaseColor");
                        Color warmedBase = Color.Lerp(baseColor, Color.Lerp(lowDwellColor, highDwellColor, normalized), 0.2f);
                        warmedBase.a = baseColor.a;
                        material.SetColor("_BaseColor", warmedBase);
                    }
                }
            }
        }
    }
}
