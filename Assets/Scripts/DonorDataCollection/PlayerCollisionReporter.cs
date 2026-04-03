using UnityEngine;

namespace AttentionalTransplants.DonorDataCollection
{
    [RequireComponent(typeof(CharacterController))]
    public class PlayerCollisionReporter : MonoBehaviour
    {
        [SerializeField] private float repeatCollisionCooldownSeconds = 0.25f;

        private float lastCollisionTimestamp = -999f;
        private string lastCollisionTargetId = string.Empty;

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            TrialManager trialManager = TrialManager.Instance;
            if (trialManager == null || !trialManager.IsTrialActive)
            {
                return;
            }

            AttentionTarget target = hit.collider.GetComponentInParent<AttentionTarget>();
            bool isObstacleLayer = hit.collider.gameObject.layer == LayerMask.NameToLayer("Obstacle");
            bool isObstacleTarget = target != null && target.ResolvedSemanticLayer == AttentionSemanticLayer.Obstacle;

            if (!isObstacleLayer && !isObstacleTarget)
            {
                return;
            }

            string currentTargetId = target != null ? target.ResolvedTargetId : hit.collider.name;
            if (Time.time - lastCollisionTimestamp < repeatCollisionCooldownSeconds &&
                string.Equals(currentTargetId, lastCollisionTargetId))
            {
                return;
            }

            lastCollisionTimestamp = Time.time;
            lastCollisionTargetId = currentTargetId;
            trialManager.RecordObstacleCollision(target, hit.point);
        }
    }
}
