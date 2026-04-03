using System.Collections.Generic;
using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [DefaultExecutionOrder(-90)]
    public class VisibilityRecorder : MonoBehaviour
    {
        [SerializeField] private Camera attentionCamera;
        [SerializeField] private float centralConeRadiusViewport = 0.18f;

        private readonly List<AttentionTarget> visibleTargetsBuffer = new();
        private readonly Plane[] frustumPlanes = new Plane[6];
        private TrialManager trialManager;
        private int sampleIndex;
        private double nextSampleRealtime;

        private void Start()
        {
            trialManager = TrialManager.Instance;
            ResolveBindings();

            if (trialManager != null)
            {
                trialManager.TrialStarted += HandleTrialStarted;
            }
        }

        private void OnDestroy()
        {
            if (trialManager != null)
            {
                trialManager.TrialStarted -= HandleTrialStarted;
            }
        }

        private void Update()
        {
            if (trialManager == null || !trialManager.IsTrialActive)
            {
                return;
            }

            ResolveBindings();
            if (attentionCamera == null)
            {
                return;
            }

            double currentRealtime = Time.realtimeSinceStartupAsDouble;
            while (currentRealtime >= nextSampleRealtime)
            {
                CaptureVisibilitySample();
                nextSampleRealtime += 1d / SessionManager.Instance.VisibilitySampleRateHz;
            }
        }

        private void HandleTrialStarted(ActiveTrialContext context)
        {
            sampleIndex = 0;
            nextSampleRealtime = Time.realtimeSinceStartupAsDouble;
        }

        private void CaptureVisibilitySample()
        {
            GeometryUtility.CalculateFrustumPlanes(attentionCamera, frustumPlanes);

            visibleTargetsBuffer.Clear();
            List<string> visibleTargetIds = new();
            List<string> centralConeIds = new();

            IReadOnlyList<AttentionTarget> targets = AttentionTarget.ActiveTargets;
            for (int index = 0; index < targets.Count; index++)
            {
                AttentionTarget target = targets[index];
                Bounds bounds = target.GetWorldBounds();
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                {
                    continue;
                }

                Vector3 viewportPoint = attentionCamera.WorldToViewportPoint(bounds.center);
                if (viewportPoint.z <= 0f)
                {
                    continue;
                }

                visibleTargetsBuffer.Add(target);
                visibleTargetIds.Add(target.ResolvedTargetId);

                Vector2 centeredViewport = new(viewportPoint.x - 0.5f, viewportPoint.y - 0.5f);
                if (centeredViewport.magnitude <= centralConeRadiusViewport)
                {
                    centralConeIds.Add(target.ResolvedTargetId);
                }
            }

            VisibilitySampleLine sampleLine = new()
            {
                sessionId = SessionManager.Instance.SessionId,
                trialId = trialManager.CurrentTrialId,
                sampleIndex = sampleIndex++,
                timestampSeconds = trialManager.GetElapsedTimeSeconds(),
                visibleTargetIds = visibleTargetIds,
                centralConeTargetIds = centralConeIds
            };

            trialManager.RecordVisibilitySample(sampleLine, visibleTargetsBuffer);
        }

        private void ResolveBindings()
        {
            if (attentionCamera == null && trialManager != null)
            {
                attentionCamera = trialManager.AttentionCamera;
            }
        }
    }
}
