using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [DefaultExecutionOrder(-100)]
    public class AttentionRecorder : MonoBehaviour
    {
        [SerializeField] private Camera attentionCamera;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private float maxRayDistance = 250f;

        private TrialManager trialManager;
        private int sampleIndex;
        private double nextSampleRealtime;
        private Vector3 previousPlayerPosition;
        private float previousSampleTimestampSeconds;
        private string activeSignTargetId;
        private float activeSignStartTimestampSeconds;

        private void Start()
        {
            trialManager = TrialManager.Instance;
            ResolveBindings();

            if (trialManager != null)
            {
                trialManager.TrialStarted += HandleTrialStarted;
                trialManager.TrialEnded += HandleTrialEnded;
            }
        }

        private void OnDestroy()
        {
            if (trialManager == null)
            {
                return;
            }

            trialManager.TrialStarted -= HandleTrialStarted;
            trialManager.TrialEnded -= HandleTrialEnded;
        }

        private void Update()
        {
            if (trialManager == null || !trialManager.IsTrialActive)
            {
                return;
            }

            ResolveBindings();

            if (attentionCamera == null || playerRoot == null)
            {
                return;
            }

            double currentRealtime = Time.realtimeSinceStartupAsDouble;
            while (currentRealtime >= nextSampleRealtime)
            {
                CaptureSample();
                nextSampleRealtime += 1d / SessionManager.Instance.AttentionSampleRateHz;
            }
        }

        private void HandleTrialStarted(ActiveTrialContext context)
        {
            sampleIndex = 0;
            previousPlayerPosition = playerRoot != null ? playerRoot.position : Vector3.zero;
            previousSampleTimestampSeconds = 0f;
            nextSampleRealtime = Time.realtimeSinceStartupAsDouble;
            activeSignTargetId = string.Empty;
            activeSignStartTimestampSeconds = 0f;
        }

        private void HandleTrialEnded(ActiveTrialContext context, TrialSummaryRecord summary)
        {
            FlushSignDwell(context.trialRecord.durationSeconds);
        }

        private void CaptureSample()
        {
            float timestampSeconds = trialManager.GetElapsedTimeSeconds();
            Vector3 currentPlayerPosition = playerRoot.position;
            float deltaTime = Mathf.Max(timestampSeconds - previousSampleTimestampSeconds, 0.0001f);
            float speed = Vector3.Distance(previousPlayerPosition, currentPlayerPosition) / deltaTime;
            AttentionGridZoneGenerator.TryGetZoneIdForWorldPositionGlobal(currentPlayerPosition, out string playerZoneId);

            AttentionTarget hitTarget = null;
            RaycastHit raycastHit;
            string hitUnityLayer = string.Empty;
            bool hadRelevantHit = false;
            Vector3 hitPoint = Vector3.zero;
            float hitDistance = 0f;

            Ray centerRay = attentionCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(centerRay, out raycastHit, maxRayDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                hitPoint = raycastHit.point;
                hitDistance = raycastHit.distance;
                hitUnityLayer = LayerMask.LayerToName(raycastHit.collider.gameObject.layer);
                hitTarget = raycastHit.collider.GetComponentInParent<AttentionTarget>();
                hadRelevantHit = hitTarget != null;
            }

            AttentionSampleLine sampleLine = new()
            {
                sessionId = SessionManager.Instance.SessionId,
                trialId = trialManager.CurrentTrialId,
                sampleIndex = sampleIndex++,
                timestampSeconds = timestampSeconds,
                playerPosition = currentPlayerPosition,
                playerRotation = playerRoot.rotation,
                cameraPosition = attentionCamera.transform.position,
                cameraRotation = attentionCamera.transform.rotation,
                cameraForward = attentionCamera.transform.forward,
                speedMetersPerSecond = speed,
                playerZoneId = playerZoneId,
                hadRelevantHit = hadRelevantHit,
                hitTargetId = hadRelevantHit ? hitTarget.ResolvedTargetId : null,
                hitSemanticLayer = hadRelevantHit ? hitTarget.ResolvedSemanticLayer.ToString() : string.Empty,
                hitGuidanceRole = hadRelevantHit ? hitTarget.GuidanceRole.ToString() : string.Empty,
                hitUnityLayer = hitUnityLayer,
                hitPoint = hitPoint,
                hitDistance = hitDistance
            };

            trialManager.RecordAttentionSample(sampleLine);
            UpdateSignDwell(hitTarget, timestampSeconds);

            previousPlayerPosition = currentPlayerPosition;
            previousSampleTimestampSeconds = timestampSeconds;
        }

        private void UpdateSignDwell(AttentionTarget currentTarget, float timestampSeconds)
        {
            string currentSignId = currentTarget != null && currentTarget.IsSignLike
                ? currentTarget.ResolvedTargetId
                : string.Empty;

            if (string.Equals(activeSignTargetId, currentSignId))
            {
                return;
            }

            FlushSignDwell(timestampSeconds);

            if (string.IsNullOrEmpty(currentSignId))
            {
                return;
            }

            activeSignTargetId = currentSignId;
            activeSignStartTimestampSeconds = timestampSeconds;
            trialManager.RecordCustomEvent(
                "sign_dwell_start",
                subjectId: currentTarget.ResolvedTargetId,
                message: $"Started dwelling on sign '{currentTarget.ResolvedTargetId}'.");
        }

        private void FlushSignDwell(float timestampSeconds)
        {
            if (string.IsNullOrEmpty(activeSignTargetId))
            {
                return;
            }

            SignDwellPayload payload = new()
            {
                durationSeconds = Mathf.Max(0f, timestampSeconds - activeSignStartTimestampSeconds)
            };

            trialManager.RecordCustomEvent(
                "sign_dwell_end",
                subjectId: activeSignTargetId,
                message: $"Ended sign dwell for '{activeSignTargetId}'.",
                payloadJson: JsonUtility.ToJson(payload));

            activeSignTargetId = string.Empty;
            activeSignStartTimestampSeconds = 0f;
        }

        private void ResolveBindings()
        {
            if (playerRoot == null && trialManager != null)
            {
                playerRoot = trialManager.PlayerRoot;
            }

            if (attentionCamera == null && trialManager != null)
            {
                attentionCamera = trialManager.AttentionCamera;
            }
        }
    }

    [System.Serializable]
    public class SignDwellPayload
    {
        public float durationSeconds;
    }
}
