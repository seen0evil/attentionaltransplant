using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class PathHeatmapRenderer : MonoBehaviour
    {
        [SerializeField] private float cellSizeMeters = 0.75f;
        [SerializeField] private float groundYOffset = 0.035f;
        [SerializeField] private float maxSampleDeltaSeconds = 1f;
        [SerializeField] private int maxRenderedCells = 350;
        [SerializeField] private float minCellScale = 0.45f;
        [SerializeField] private float maxCellScale = 1.25f;
        [SerializeField] private float minColorIntensity = 1.1f;
        [SerializeField] private float maxColorIntensity = 5.5f;
        [SerializeField] private Color lowDwellColor = new(0.05f, 0.75f, 1f, 0.42f);
        [SerializeField] private Color highDwellColor = new(1f, 0.72f, 0.05f, 0.72f);

        private readonly List<GameObject> renderedCells = new();

        public PathHeatmapReport Render(DonorVisualizationDataSet dataSet)
        {
            Clear();

            if (dataSet.pathSamples.Count < 2)
            {
                return new PathHeatmapReport(dataSet.pathSamples.Count, 0, 0f);
            }

            Dictionary<Vector2Int, HeatCell> cells = BuildHeatCells(dataSet.pathSamples);
            if (cells.Count == 0)
            {
                return new PathHeatmapReport(dataSet.pathSamples.Count, 0, 0f);
            }

            List<HeatCell> sortedCells = new(cells.Values);
            sortedCells.Sort((left, right) => right.dwellSeconds.CompareTo(left.dwellSeconds));

            float maxCellDwell = Mathf.Max(sortedCells[0].dwellSeconds, Mathf.Epsilon);
            int renderCount = Mathf.Min(sortedCells.Count, maxRenderedCells);
            for (int index = 0; index < renderCount; index++)
            {
                RenderCell(sortedCells[index], maxCellDwell);
            }

            return new PathHeatmapReport(dataSet.pathSamples.Count, renderCount, maxCellDwell);
        }

        public void Clear()
        {
            for (int index = renderedCells.Count - 1; index >= 0; index--)
            {
                GameObject renderedCell = renderedCells[index];
                if (renderedCell == null)
                {
                    continue;
                }

                Destroy(renderedCell);
            }

            renderedCells.Clear();
        }

        private Dictionary<Vector2Int, HeatCell> BuildHeatCells(IReadOnlyList<DonorPathSample> samples)
        {
            Dictionary<Vector2Int, HeatCell> cells = new();

            for (int index = 1; index < samples.Count; index++)
            {
                DonorPathSample previous = samples[index - 1];
                DonorPathSample current = samples[index];
                float deltaSeconds = Mathf.Clamp(
                    current.timestampSeconds - previous.timestampSeconds,
                    0f,
                    maxSampleDeltaSeconds);

                if (deltaSeconds <= 0f)
                {
                    continue;
                }

                Vector2Int key = PositionToCell(previous.position);
                if (!cells.TryGetValue(key, out HeatCell cell))
                {
                    Vector3 center = new(
                        (key.x + 0.5f) * cellSizeMeters,
                        previous.position.y,
                        (key.y + 0.5f) * cellSizeMeters);

                    cell = new HeatCell
                    {
                        key = key,
                        center = center
                    };
                }

                cell.dwellSeconds += deltaSeconds;
                cells[key] = cell;
            }

            return cells;
        }

        private Vector2Int PositionToCell(Vector3 position)
        {
            return new Vector2Int(
                Mathf.FloorToInt(position.x / cellSizeMeters),
                Mathf.FloorToInt(position.z / cellSizeMeters));
        }

        private void RenderCell(HeatCell cell, float maxCellDwell)
        {
            float normalized = Mathf.Clamp01(cell.dwellSeconds / maxCellDwell);
            float scale = Mathf.Lerp(minCellScale, maxCellScale, normalized) * cellSizeMeters;
            Color color = Color.Lerp(lowDwellColor, highDwellColor, normalized);
            float intensity = Mathf.Lerp(minColorIntensity, maxColorIntensity, normalized);
            color = new Color(color.r * intensity, color.g * intensity, color.b * intensity, color.a);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = $"Path Heat {cell.key.x}_{cell.key.y}";
            quad.transform.SetParent(transform, false);
            quad.transform.position = ResolveGroundPosition(cell.center);
            quad.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
            quad.transform.localScale = new Vector3(scale, scale, 1f);

            if (quad.TryGetComponent(out Collider collider))
            {
                Destroy(collider);
            }

            Renderer cellRenderer = quad.GetComponent<Renderer>();
            cellRenderer.sharedMaterial = CreateCellMaterial(color);
            renderedCells.Add(quad);
        }

        private Vector3 ResolveGroundPosition(Vector3 center)
        {
            Vector3 rayOrigin = center + Vector3.up * 8f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 25f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * groundYOffset;
            }

            return center + Vector3.up * groundYOffset;
        }

        private static Material CreateCellMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            Material material = new(shader)
            {
                name = "Runtime Path Heat Material",
                renderQueue = (int)RenderQueue.Transparent
            };

            if (material.HasProperty("_Surface"))
            {
                material.SetFloat("_Surface", 1f);
            }

            if (material.HasProperty("_SrcBlend"))
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }

            if (material.HasProperty("_DstBlend"))
            {
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }

            if (material.HasProperty("_ZWrite"))
            {
                material.SetFloat("_ZWrite", 0f);
            }

            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return material;
        }

        private struct HeatCell
        {
            public Vector2Int key;
            public Vector3 center;
            public float dwellSeconds;
        }
    }
}
