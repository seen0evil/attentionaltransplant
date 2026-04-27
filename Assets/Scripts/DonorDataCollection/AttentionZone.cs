using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [RequireComponent(typeof(Collider))]
    public class AttentionZone : MonoBehaviour
    {
        [SerializeField] private string zoneId;

        public string ResolvedZoneId => GetResolvedZoneId();

        public void Initialize(string zoneId)
        {
            this.zoneId = string.IsNullOrWhiteSpace(zoneId) ? gameObject.name : zoneId.Trim();

            if (TryGetComponent(out Collider collider))
            {
                collider.isTrigger = true;
            }
        }

        private void Reset()
        {
            Initialize(zoneId);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsParticipant(other))
            {
                return;
            }

            TrialManager.Instance?.RecordZoneEvent(GetResolvedZoneId(), true);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsParticipant(other))
            {
                return;
            }

            TrialManager.Instance?.RecordZoneEvent(GetResolvedZoneId(), false);
        }

        private string GetResolvedZoneId()
        {
            return string.IsNullOrWhiteSpace(zoneId) ? gameObject.name : zoneId.Trim();
        }

        private static bool IsParticipant(Collider other)
        {
            return other.GetComponentInParent<DonorParticipantMarker>() != null;
        }
    }
}
