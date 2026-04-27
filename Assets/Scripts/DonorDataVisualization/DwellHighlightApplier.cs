using System;
using System.Collections.Generic;
using AttentionalTransplants.DonorDataCollection;
using UnityEngine;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class DwellHighlightApplier : MonoBehaviour
    {
        [SerializeField] private Color lowDwellColor = new(0.05f, 0.9f, 1f, 1f);
        [SerializeField] private Color highDwellColor = new(1f, 0.76f, 0.08f, 1f);
        [SerializeField] private float minRingWidth = 0.035f;
        [SerializeField] private float maxRingWidth = 0.14f;
        [SerializeField] private int ringSegments = 72;

        private readonly List<GameObject> spawnedHighlights = new();
        private Material highlightMaterial;

        public DwellGlowReport Apply(DonorVisualizationDataSet dataSet)
        {
            return Apply(dataSet.dwellByTarget);
        }

        public DwellGlowReport Apply(IReadOnlyDictionary<string, float> dwellByTarget)
        {
            Clear();

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
            foreach (float dwellSeconds in dwellByTarget.Values)
            {
                maxDwellSeconds = Mathf.Max(maxDwellSeconds, dwellSeconds);
            }

            int highlightedTargetCount = 0;
            foreach (AttentionTarget target in activeTargets)
            {
                if (target == null ||
                    !dwellByTarget.TryGetValue(target.ResolvedTargetId, out float dwellSeconds) ||
                    dwellSeconds <= 0f ||
                    maxDwellSeconds <= Mathf.Epsilon)
                {
                    continue;
                }

                highlightedTargetCount++;
                CreateHighlight(target, Mathf.Clamp01(dwellSeconds / maxDwellSeconds));
            }

            int unmatchedDwellCount = 0;
            foreach (string targetId in dwellByTarget.Keys)
            {
                if (!targetsById.ContainsKey(targetId))
                {
                    unmatchedDwellCount++;
                }
            }

            return new DwellGlowReport(activeTargets.Count, highlightedTargetCount, unmatchedDwellCount);
        }

        public void Clear()
        {
            for (int index = spawnedHighlights.Count - 1; index >= 0; index--)
            {
                GameObject highlight = spawnedHighlights[index];
                if (highlight != null)
                {
                    Destroy(highlight);
                }
            }

            spawnedHighlights.Clear();
        }

        private void CreateHighlight(AttentionTarget target, float normalizedDwell)
        {
            Bounds bounds = target.GetWorldBounds();
            Color color = Color.Lerp(lowDwellColor, highDwellColor, normalizedDwell);
            float radius = Mathf.Max(bounds.extents.x, bounds.extents.z, 0.25f) * Mathf.Lerp(1.22f, 1.55f, normalizedDwell);
            float baseY = bounds.min.y + 0.08f;
            float ringWidth = Mathf.Lerp(minRingWidth, maxRingWidth, normalizedDwell);

            GameObject root = new($"Dwell Highlight {target.ResolvedTargetId}");
            root.transform.SetParent(transform, false);
            spawnedHighlights.Add(root);

            CreateRing(root.transform, bounds.center, baseY, radius, ringWidth, color, "Base Ring");
            CreateRing(root.transform, bounds.center, bounds.center.y, radius * 0.86f, ringWidth * 0.65f, color, "Mid Halo");
            CreateParticles(root.transform, bounds.center, radius, bounds.size.y, color, normalizedDwell);
        }

        private void CreateRing(Transform parent, Vector3 center, float y, float radius, float width, Color color, string name)
        {
            GameObject ringObject = new(name);
            ringObject.transform.SetParent(parent, false);

            LineRenderer lineRenderer = ringObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            lineRenderer.positionCount = Mathf.Max(12, ringSegments);
            lineRenderer.widthMultiplier = width;
            lineRenderer.numCornerVertices = 4;
            lineRenderer.numCapVertices = 4;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.material = GetHighlightMaterial();
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            for (int index = 0; index < lineRenderer.positionCount; index++)
            {
                float angle = index / (float)lineRenderer.positionCount * Mathf.PI * 2f;
                Vector3 position = new(
                    center.x + Mathf.Cos(angle) * radius,
                    y,
                    center.z + Mathf.Sin(angle) * radius);
                lineRenderer.SetPosition(index, position);
            }
        }

        private void CreateParticles(Transform parent, Vector3 center, float radius, float height, Color color, float normalizedDwell)
        {
            GameObject particleObject = new("Dwell Particles");
            particleObject.transform.SetParent(parent, false);
            particleObject.transform.position = center;

            ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.startLifetime = Mathf.Lerp(1.1f, 2.4f, normalizedDwell);
            main.startSpeed = Mathf.Lerp(0.08f, 0.28f, normalizedDwell);
            main.startSize = Mathf.Lerp(0.045f, 0.11f, normalizedDwell);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 180;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = Mathf.Lerp(6f, 28f, normalizedDwell);

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.2f, radius * 0.82f);
            shape.position = Vector3.up * Mathf.Max(0.25f, height * 0.15f);

            ParticleSystemRenderer renderer = particleObject.GetComponent<ParticleSystemRenderer>();
            renderer.material = GetHighlightMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private Material GetHighlightMaterial()
        {
            if (highlightMaterial != null)
            {
                return highlightMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            highlightMaterial = new Material(shader)
            {
                name = "Runtime Dwell Highlight Material",
                renderQueue = 3100
            };

            return highlightMaterial;
        }
    }
}
