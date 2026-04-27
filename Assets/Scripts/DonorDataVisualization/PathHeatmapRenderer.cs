using System.Collections.Generic;
using UnityEngine;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class PathHeatmapRenderer : MonoBehaviour
    {
        [SerializeField] private float trailGroundOffset = 0.08f;
        [SerializeField] private float trailWidthMeters = 0.18f;
        [SerializeField] private float minPointSpacingMeters = 0.18f;
        [SerializeField] private float maxSampleDeltaSeconds = 1f;
        [SerializeField] private float slowSpeedThresholdMetersPerSecond = 0.65f;
        [SerializeField] private int maxPauseMarkers = 60;
        [SerializeField] private Color trailColor = new(0.02f, 0.92f, 1f, 1f);
        [SerializeField] private Color pauseMarkerColor = new(1f, 0.72f, 0.04f, 1f);

        private readonly List<GameObject> spawnedPathObjects = new();
        private Material trailMaterial;
        private Material markerMaterial;

        public PathHeatmapReport Render(DonorVisualizationDataSet dataSet)
        {
            Clear();

            if (dataSet.pathSamples.Count < 2)
            {
                return new PathHeatmapReport(dataSet.pathSamples.Count, 0, 0f);
            }

            List<Vector3> trailPoints = BuildTrailPoints(dataSet.pathSamples);
            if (trailPoints.Count < 2)
            {
                return new PathHeatmapReport(dataSet.pathSamples.Count, 0, 0f);
            }

            CreateTrail(trailPoints);
            int markerCount = CreatePauseMarkers(dataSet.pathSamples);
            return new PathHeatmapReport(dataSet.pathSamples.Count, markerCount, 0f);
        }

        public void Clear()
        {
            for (int index = spawnedPathObjects.Count - 1; index >= 0; index--)
            {
                GameObject pathObject = spawnedPathObjects[index];
                if (pathObject != null)
                {
                    Destroy(pathObject);
                }
            }

            spawnedPathObjects.Clear();
        }

        private List<Vector3> BuildTrailPoints(IReadOnlyList<DonorPathSample> samples)
        {
            List<Vector3> points = new();
            Vector3 lastAccepted = Vector3.positiveInfinity;

            for (int index = 0; index < samples.Count; index++)
            {
                Vector3 resolved = ResolveGroundPosition(samples[index].position);
                if (points.Count > 0 && Vector3.Distance(lastAccepted, resolved) < minPointSpacingMeters)
                {
                    continue;
                }

                points.Add(resolved);
                lastAccepted = resolved;
            }

            if (points.Count == 1)
            {
                points.Add(ResolveGroundPosition(samples[^1].position));
            }

            return points;
        }

        private void CreateTrail(IReadOnlyList<Vector3> trailPoints)
        {
            GameObject trailObject = new("Player Path Trail");
            trailObject.transform.SetParent(transform, false);
            spawnedPathObjects.Add(trailObject);

            LineRenderer lineRenderer = trailObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = trailPoints.Count;
            lineRenderer.widthMultiplier = trailWidthMeters;
            lineRenderer.numCornerVertices = 6;
            lineRenderer.numCapVertices = 6;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.material = GetTrailMaterial();
            lineRenderer.startColor = trailColor;
            lineRenderer.endColor = trailColor;

            for (int index = 0; index < trailPoints.Count; index++)
            {
                lineRenderer.SetPosition(index, trailPoints[index]);
            }
        }

        private int CreatePauseMarkers(IReadOnlyList<DonorPathSample> samples)
        {
            List<PauseMarker> markers = new();
            for (int index = 1; index < samples.Count; index++)
            {
                DonorPathSample previous = samples[index - 1];
                DonorPathSample current = samples[index];
                float deltaSeconds = Mathf.Clamp(current.timestampSeconds - previous.timestampSeconds, 0f, maxSampleDeltaSeconds);
                if (deltaSeconds <= 0f)
                {
                    continue;
                }

                float speed = Vector3.Distance(previous.position, current.position) / deltaSeconds;
                if (speed > slowSpeedThresholdMetersPerSecond)
                {
                    continue;
                }

                markers.Add(new PauseMarker
                {
                    position = ResolveGroundPosition(previous.position),
                    weight = deltaSeconds * Mathf.Max(0.1f, slowSpeedThresholdMetersPerSecond - speed)
                });
            }

            markers.Sort((left, right) => right.weight.CompareTo(left.weight));

            int renderCount = Mathf.Min(maxPauseMarkers, markers.Count);
            for (int index = 0; index < renderCount; index++)
            {
                CreatePauseMarker(markers[index], index);
            }

            return renderCount;
        }

        private void CreatePauseMarker(PauseMarker marker, int index)
        {
            GameObject markerObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            markerObject.name = $"Path Pause Marker {index:00}";
            markerObject.transform.SetParent(transform, false);
            markerObject.transform.position = marker.position + Vector3.up * 0.04f;
            markerObject.transform.localScale = Vector3.one * Mathf.Clamp(0.18f + marker.weight * 2.2f, 0.18f, 0.5f);
            spawnedPathObjects.Add(markerObject);

            if (markerObject.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            Renderer markerRenderer = markerObject.GetComponent<Renderer>();
            markerRenderer.sharedMaterial = GetMarkerMaterial();
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
        }

        private Vector3 ResolveGroundPosition(Vector3 sourcePosition)
        {
            Vector3 rayOrigin = sourcePosition + Vector3.up * 8f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * trailGroundOffset;
            }

            return new Vector3(sourcePosition.x, sourcePosition.y + trailGroundOffset, sourcePosition.z);
        }

        private Material GetTrailMaterial()
        {
            if (trailMaterial == null)
            {
                trailMaterial = CreateUnlitMaterial("Runtime Path Trail Material", trailColor);
            }

            return trailMaterial;
        }

        private Material GetMarkerMaterial()
        {
            if (markerMaterial == null)
            {
                markerMaterial = CreateUnlitMaterial("Runtime Path Pause Material", pauseMarkerColor);
            }

            return markerMaterial;
        }

        private static Material CreateUnlitMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material material = new(shader)
            {
                name = materialName,
                renderQueue = 3100
            };

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private struct PauseMarker
        {
            public Vector3 position;
            public float weight;
        }
    }
}
