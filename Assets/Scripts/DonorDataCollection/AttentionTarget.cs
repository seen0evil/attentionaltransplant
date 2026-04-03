using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [DisallowMultipleComponent]
    public class AttentionTarget : MonoBehaviour
    {
        private static readonly List<AttentionTarget> ActiveTargetBuffer = new();

        [SerializeField] private string targetId;
        [SerializeField] private AttentionSemanticLayer semanticLayer = AttentionSemanticLayer.Auto;
        [SerializeField] private GuidanceRole guidanceRole = GuidanceRole.Structural;
        [SerializeField] private string parentZoneId;
        [SerializeField] private bool includeChildrenInBounds = true;

        public static IReadOnlyList<AttentionTarget> ActiveTargets => ActiveTargetBuffer;

        public string ResolvedTargetId => string.IsNullOrWhiteSpace(targetId)
            ? SanitizeId($"{gameObject.scene.name}_{GetHierarchyPath(transform)}")
            : targetId.Trim();

        public string ParentZoneId => parentZoneId;
        public string ScenePath => GetHierarchyPath(transform);
        public GuidanceRole GuidanceRole => guidanceRole;
        public bool IsSignLike => ResolvedSemanticLayer == AttentionSemanticLayer.Signs || guidanceRole == GuidanceRole.DirectionalSign;

        public AttentionSemanticLayer ResolvedSemanticLayer
        {
            get
            {
                if (semanticLayer != AttentionSemanticLayer.Auto)
                {
                    return semanticLayer;
                }

                string unityLayerName = LayerMask.LayerToName(gameObject.layer);
                return unityLayerName switch
                {
                    "Environment" => AttentionSemanticLayer.Environment,
                    "Obstacle" => AttentionSemanticLayer.Obstacle,
                    "Signs" => AttentionSemanticLayer.Signs,
                    _ => AttentionSemanticLayer.Environment
                };
            }
        }

        private void Reset()
        {
            if (string.IsNullOrWhiteSpace(targetId))
            {
                targetId = SanitizeId(GetHierarchyPath(transform));
            }
        }

        private void OnEnable()
        {
            if (!ActiveTargetBuffer.Contains(this))
            {
                ActiveTargetBuffer.Add(this);
            }
        }

        private void OnDisable()
        {
            ActiveTargetBuffer.Remove(this);
        }

        public Bounds GetWorldBounds()
        {
            bool hasBounds = false;
            Bounds bounds = new(transform.position, Vector3.zero);

            Renderer[] renderers = includeChildrenInBounds
                ? GetComponentsInChildren<Renderer>(true)
                : GetComponents<Renderer>();

            foreach (Renderer renderer in renderers)
            {
                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            Collider[] colliders = includeChildrenInBounds
                ? GetComponentsInChildren<Collider>(true)
                : GetComponents<Collider>();

            foreach (Collider collider in colliders)
            {
                if (!hasBounds)
                {
                    bounds = collider.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(collider.bounds);
            }

            if (!hasBounds)
            {
                bounds = new Bounds(transform.position, Vector3.one * 0.25f);
            }

            return bounds;
        }

        private static string GetHierarchyPath(Transform current)
        {
            StringBuilder builder = new();
            Transform cursor = current;

            while (cursor != null)
            {
                if (builder.Length == 0)
                {
                    builder.Insert(0, cursor.name);
                }
                else
                {
                    builder.Insert(0, '/').Insert(0, cursor.name);
                }

                cursor = cursor.parent;
            }

            return builder.ToString();
        }

        private static string SanitizeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "attention_target";
            }

            StringBuilder builder = new();
            bool previousWasSeparator = false;

            foreach (char character in value.Trim().ToLowerInvariant())
            {
                if (char.IsLetterOrDigit(character))
                {
                    builder.Append(character);
                    previousWasSeparator = false;
                    continue;
                }

                if (previousWasSeparator)
                {
                    continue;
                }

                builder.Append('_');
                previousWasSeparator = true;
            }

            string sanitized = builder.ToString().Trim('_');
            return string.IsNullOrWhiteSpace(sanitized) ? "attention_target" : sanitized;
        }
    }
}
