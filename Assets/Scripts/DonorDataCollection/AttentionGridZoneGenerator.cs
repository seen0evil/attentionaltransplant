using System;
using System.Collections.Generic;
using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [DisallowMultipleComponent]
    public class AttentionGridZoneGenerator : MonoBehaviour
    {
        private const string GeneratedZoneTag = "Generated Attention Zone";

        private static readonly List<AttentionGridZoneGenerator> ActiveGenerators = new();

        [SerializeField] private int columns = 5;
        [SerializeField] private int rows = 5;
        [SerializeField] private Vector3 center = new(-20f, 3.5f, -15f);
        [SerializeField] private Vector2 size = new(60f, 60f);
        [SerializeField] private float height = 8f;
        [SerializeField] private string zoneIdPrefix = "grid";

        public int Columns => Mathf.Max(1, columns);
        public int Rows => Mathf.Max(1, rows);
        public Vector3 Center => center;
        public Vector2 Size => new(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
        public float Height => Mathf.Max(0.01f, height);
        public string ZoneIdPrefix => string.IsNullOrWhiteSpace(zoneIdPrefix) ? "grid" : zoneIdPrefix.Trim();

        private void OnEnable()
        {
            if (!ActiveGenerators.Contains(this))
            {
                ActiveGenerators.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveGenerators.Remove(this);
        }

        private void Start()
        {
            GenerateZones();
        }

        public static bool TryGetZoneIdForWorldPositionGlobal(Vector3 worldPosition, out string zoneId)
        {
            for (int index = 0; index < ActiveGenerators.Count; index++)
            {
                AttentionGridZoneGenerator generator = ActiveGenerators[index];
                if (generator != null && generator.TryGetZoneIdForWorldPosition(worldPosition, out zoneId))
                {
                    return true;
                }
            }

            zoneId = string.Empty;
            return false;
        }

        public void GenerateZones()
        {
            ClearGeneratedZones();

            int resolvedRows = Rows;
            int resolvedColumns = Columns;
            Vector2 resolvedSize = Size;
            float resolvedHeight = Height;
            float cellWidth = resolvedSize.x / resolvedColumns;
            float cellDepth = resolvedSize.y / resolvedRows;
            Vector3 min = GetMinCorner();

            for (int row = 0; row < resolvedRows; row++)
            {
                for (int column = 0; column < resolvedColumns; column++)
                {
                    string zoneId = GetZoneId(row, column);
                    GameObject zoneObject = new($"{GeneratedZoneTag} {zoneId}");
                    zoneObject.transform.SetParent(transform, false);
                    zoneObject.transform.position = new Vector3(
                        min.x + cellWidth * (column + 0.5f),
                        center.y,
                        min.z + cellDepth * (row + 0.5f));

                    BoxCollider collider = zoneObject.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    collider.size = new Vector3(cellWidth, resolvedHeight, cellDepth);

                    AttentionZone zone = zoneObject.AddComponent<AttentionZone>();
                    zone.Initialize(zoneId);
                }
            }
        }

        public bool TryGetZoneIdForWorldPosition(Vector3 worldPosition, out string zoneId)
        {
            if (!ContainsWorldPosition(worldPosition))
            {
                zoneId = string.Empty;
                return false;
            }

            Vector3 min = GetMinCorner();
            Vector2 resolvedSize = Size;
            int column = Mathf.Clamp(
                Mathf.FloorToInt((worldPosition.x - min.x) / resolvedSize.x * Columns),
                0,
                Columns - 1);
            int row = Mathf.Clamp(
                Mathf.FloorToInt((worldPosition.z - min.z) / resolvedSize.y * Rows),
                0,
                Rows - 1);

            zoneId = GetZoneId(row, column);
            return true;
        }

        public string GetZoneId(int row, int column)
        {
            int clampedRow = Mathf.Clamp(row, 0, Rows - 1);
            int clampedColumn = Mathf.Clamp(column, 0, Columns - 1);
            return $"{ZoneIdPrefix}_r{clampedRow:00}_c{clampedColumn:00}";
        }

        public void Configure(int columns, int rows, Vector3 center, Vector2 size, float height, string zoneIdPrefix = "grid")
        {
            this.columns = Mathf.Max(1, columns);
            this.rows = Mathf.Max(1, rows);
            this.center = center;
            this.size = new Vector2(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y));
            this.height = Mathf.Max(0.01f, height);
            this.zoneIdPrefix = string.IsNullOrWhiteSpace(zoneIdPrefix) ? "grid" : zoneIdPrefix.Trim();
        }

        private bool ContainsWorldPosition(Vector3 worldPosition)
        {
            Vector3 min = GetMinCorner();
            Vector3 max = GetMaxCorner();
            return worldPosition.x >= min.x &&
                   worldPosition.x <= max.x &&
                   worldPosition.y >= min.y &&
                   worldPosition.y <= max.y &&
                   worldPosition.z >= min.z &&
                   worldPosition.z <= max.z;
        }

        private Vector3 GetMinCorner()
        {
            Vector2 resolvedSize = Size;
            return new Vector3(
                center.x - resolvedSize.x * 0.5f,
                center.y - Height * 0.5f,
                center.z - resolvedSize.y * 0.5f);
        }

        private Vector3 GetMaxCorner()
        {
            Vector2 resolvedSize = Size;
            return new Vector3(
                center.x + resolvedSize.x * 0.5f,
                center.y + Height * 0.5f,
                center.z + resolvedSize.y * 0.5f);
        }

        private void ClearGeneratedZones()
        {
            for (int index = transform.childCount - 1; index >= 0; index--)
            {
                Transform child = transform.GetChild(index);
                if (!child.name.StartsWith(GeneratedZoneTag, StringComparison.Ordinal))
                {
                    continue;
                }

                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.05f, 0.85f, 1f, 0.35f);
            Gizmos.DrawWireCube(center, new Vector3(Size.x, Height, Size.y));
        }
    }
}
