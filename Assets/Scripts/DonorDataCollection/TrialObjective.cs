using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttentionalTransplants.DonorDataCollection
{
    [RequireComponent(typeof(Collider))]
    public class TrialObjective : MonoBehaviour
    {
        [SerializeField] private string objectiveId;
        [SerializeField] private bool loadSceneOnComplete = true;
        [SerializeField] private string sceneToLoadOnComplete = "End";

        private void Reset()
        {
            objectiveId = string.IsNullOrWhiteSpace(objectiveId) ? gameObject.name : objectiveId;

            if (TryGetComponent(out Collider collider))
            {
                collider.isTrigger = true;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.GetComponentInParent<DonorParticipantMarker>() == null)
            {
                return;
            }

            TrialManager.Instance?.CompleteCurrentTrial(GetResolvedObjectiveId());

            if (!loadSceneOnComplete || string.IsNullOrWhiteSpace(sceneToLoadOnComplete))
            {
                return;
            }

            if (Application.CanStreamedLevelBeLoaded(sceneToLoadOnComplete))
            {
                SceneManager.LoadScene(sceneToLoadOnComplete, LoadSceneMode.Single);
                return;
            }

            Debug.LogError($"TrialObjective could not load scene '{sceneToLoadOnComplete}'. Add it to Build Settings or correct the scene name.", this);
        }

        private string GetResolvedObjectiveId()
        {
            return string.IsNullOrWhiteSpace(objectiveId) ? gameObject.name : objectiveId.Trim();
        }
    }
}
