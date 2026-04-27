using UnityEngine;
using UnityEngine.SceneManagement;

namespace AttentionalTransplants.DonorDataCollection
{
    public static class DonorRuntimeBootstrap
    {
        private const string VisualizationSceneName = "Visualization";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureParticipantAndCameraHooks();
            EnsureRuntimeManagers(scene);
        }

        private static void EnsureParticipantAndCameraHooks()
        {
            SimplePlayerMovement playerMovement = Object.FindAnyObjectByType<SimplePlayerMovement>();
            Camera mainCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();

            if (playerMovement != null)
            {
                if (playerMovement.GetComponent<DonorParticipantMarker>() == null)
                {
                    playerMovement.gameObject.AddComponent<DonorParticipantMarker>();
                }

                if (playerMovement.GetComponent<PlayerCollisionReporter>() == null)
                {
                    playerMovement.gameObject.AddComponent<PlayerCollisionReporter>();
                }
            }

            if (mainCamera != null && playerMovement != null)
            {
                PlayerCameraLook cameraLook = mainCamera.GetComponent<PlayerCameraLook>();
                if (cameraLook == null)
                {
                    cameraLook = mainCamera.gameObject.AddComponent<PlayerCameraLook>();
                }

                cameraLook.BindToPlayer(playerMovement.transform);
            }
        }

        private static void EnsureRuntimeManagers(Scene scene)
        {
            if (scene.IsValid() && scene.name == VisualizationSceneName)
            {
                return;
            }

            if (Object.FindAnyObjectByType<SessionManager>() != null)
            {
                return;
            }

            GameObject runtimeObject = new("Donor Session Runtime");
            runtimeObject.AddComponent<SessionManager>();
            runtimeObject.AddComponent<TrialManager>();
            runtimeObject.AddComponent<AttentionRecorder>();
            runtimeObject.AddComponent<VisibilityRecorder>();
        }
    }
}
