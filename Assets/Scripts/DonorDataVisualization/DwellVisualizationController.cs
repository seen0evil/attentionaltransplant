using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using AttentionalTransplants.DonorDataCollection;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class DwellVisualizationController : MonoBehaviour
    {
        public const string VisualizationSceneName = "Visualization";

        private const string RuntimeObjectName = "Dwell Visualization Runtime";
        private const float ToastVisibleSeconds = 4.5f;

        [SerializeField] private DwellHighlightApplier highlightApplier;
        [SerializeField] private PathHeatmapRenderer pathHeatmapRenderer;

        private TMP_Text titleLabel;
        private TMP_Text statusLabel;
        private TMP_Text shortcutLabel;
        private CanvasGroup panelGroup;
        private DonorVisualizationDataSet currentDataSet;
        private DwellVisualizationMode visualizationMode = DwellVisualizationMode.FullTrial;
        private string activeZoneId = string.Empty;
        private bool diagnosticsVisible;
        private float hideToastAt;

        public DwellVisualizationMode CurrentMode => visualizationMode;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RequestDonorDataUpload(string targetObjectName, string targetMethodName);
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!string.Equals(scene.name, VisualizationSceneName, StringComparison.Ordinal))
            {
                return;
            }

            if (FindAnyObjectByType<DwellVisualizationController>() != null)
            {
                return;
            }

            GameObject runtimeObject = new(RuntimeObjectName);
            runtimeObject.AddComponent<DwellVisualizationController>();
        }

        private void Awake()
        {
            highlightApplier = highlightApplier != null ? highlightApplier : gameObject.AddComponent<DwellHighlightApplier>();
            pathHeatmapRenderer = pathHeatmapRenderer != null ? pathHeatmapRenderer : gameObject.AddComponent<PathHeatmapRenderer>();
            BuildInterface();
        }

        private void Start()
        {
            LoadLatestLocalSession();
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
            {
                ToggleDiagnostics();
            }

            if (Input.GetKeyDown(KeyCode.R))
            {
                LoadLatestLocalSession();
            }

            if (Input.GetKeyDown(KeyCode.I))
            {
                HandleImportClicked();
            }

            if (Input.GetKeyDown(KeyCode.Z))
            {
                ToggleVisualizationMode();
            }

            if (visualizationMode == DwellVisualizationMode.CurrentZone)
            {
                RefreshCurrentZoneHighlightsIfNeeded();
            }

            if (!diagnosticsVisible && panelGroup != null && panelGroup.alpha > 0f && Time.unscaledTime >= hideToastAt)
            {
                SetPanelVisible(false);
            }
        }

        public void ReceiveImportedExportJson(string exportJson)
        {
            if (DonorVisualizationLoader.TryLoadExportJson(exportJson, out DonorVisualizationDataSet dataSet, out string message))
            {
                ApplyDataSet(dataSet, message);
                return;
            }

            ShowStatus(message);
        }

        private void LoadLatestLocalSession()
        {
            ShowStatus("Loading latest local donor session...");
            if (DonorVisualizationLoader.TryLoadLatestLocalSession(out DonorVisualizationDataSet dataSet, out string message))
            {
                ApplyDataSet(dataSet, message);
                return;
            }

            ShowStatus(message);
        }

        private void ApplyDataSet(DonorVisualizationDataSet dataSet, string loadMessage)
        {
            currentDataSet = dataSet;
            visualizationMode = DwellVisualizationMode.FullTrial;
            activeZoneId = string.Empty;

            DwellGlowReport glowReport = ApplyFullTrialHighlights();
            PathHeatmapReport pathReport = pathHeatmapRenderer.Render(dataSet);

            titleLabel.text = $"{dataSet.sessionId} / {dataSet.trialId}";
            ShowStatus(
                $"{loadMessage}\n" +
                $"Highlights: {glowReport.glowingTargetCount}/{glowReport.sceneTargetCount}; unmatched IDs: {glowReport.unmatchedDwellCount}.\n" +
                $"Path: {pathReport.sampleCount} samples, {pathReport.renderedCellCount} slow-point markers.");
        }

        public void ToggleVisualizationMode()
        {
            visualizationMode = visualizationMode == DwellVisualizationMode.FullTrial
                ? DwellVisualizationMode.CurrentZone
                : DwellVisualizationMode.FullTrial;
            activeZoneId = string.Empty;

            if (currentDataSet == null)
            {
                ShowStatus($"Mode: {GetModeLabel()}. Load donor data before highlights can be shown.");
                return;
            }

            if (visualizationMode == DwellVisualizationMode.FullTrial)
            {
                DwellGlowReport glowReport = ApplyFullTrialHighlights();
                ShowStatus(
                    $"Mode: {GetModeLabel()}.\n" +
                    $"Highlights: {glowReport.glowingTargetCount}/{glowReport.sceneTargetCount}; unmatched IDs: {glowReport.unmatchedDwellCount}.");
                return;
            }

            ApplyCurrentZoneHighlights(forceStatus: true);
        }

        private DwellGlowReport ApplyFullTrialHighlights()
        {
            return currentDataSet == null
                ? highlightApplier.Apply(new Dictionary<string, float>())
                : highlightApplier.Apply(currentDataSet);
        }

        private void RefreshCurrentZoneHighlightsIfNeeded()
        {
            if (currentDataSet == null)
            {
                return;
            }

            string resolvedZoneId = ResolveCurrentViewerZoneId();
            if (string.Equals(activeZoneId, resolvedZoneId, StringComparison.Ordinal))
            {
                return;
            }

            ApplyCurrentZoneHighlights(forceStatus: true);
        }

        private void ApplyCurrentZoneHighlights(bool forceStatus)
        {
            if (currentDataSet == null)
            {
                return;
            }

            string zoneId = ResolveCurrentViewerZoneId();
            activeZoneId = zoneId;

            if (string.IsNullOrWhiteSpace(zoneId))
            {
                highlightApplier.Clear();
                if (forceStatus)
                {
                    ShowStatus("Mode: Current zone.\nNo current grid zone resolved; highlights cleared.");
                }

                return;
            }

            Dictionary<string, float> dwellByTargetForZone = currentDataSet.BuildDwellByTargetForZone(zoneId);
            if (dwellByTargetForZone.Count == 0)
            {
                highlightApplier.Clear();
                if (forceStatus)
                {
                    ShowStatus($"Mode: Current zone ({zoneId}).\nNo donor dwell data for this zone; highlights cleared.");
                }

                return;
            }

            DwellGlowReport glowReport = highlightApplier.Apply(dwellByTargetForZone);
            if (forceStatus)
            {
                ShowStatus(
                    $"Mode: Current zone ({zoneId}).\n" +
                    $"Highlights: {glowReport.glowingTargetCount}/{glowReport.sceneTargetCount}; unmatched IDs: {glowReport.unmatchedDwellCount}.");
            }
        }

        private static string ResolveCurrentViewerZoneId()
        {
            SimplePlayerMovement playerMovement = FindAnyObjectByType<SimplePlayerMovement>();
            if (playerMovement != null &&
                AttentionGridZoneGenerator.TryGetZoneIdForWorldPositionGlobal(playerMovement.transform.position, out string playerZoneId))
            {
                return playerZoneId;
            }

            Camera mainCamera = Camera.main != null ? Camera.main : FindAnyObjectByType<Camera>();
            if (mainCamera != null &&
                AttentionGridZoneGenerator.TryGetZoneIdForWorldPositionGlobal(mainCamera.transform.position, out string cameraZoneId))
            {
                return cameraZoneId;
            }

            return string.Empty;
        }

        private void HandleImportClicked()
        {
#if UNITY_EDITOR
            string path = UnityEditor.EditorUtility.OpenFilePanel("Import donor export JSON", string.Empty, "json");
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            try
            {
                ReceiveImportedExportJson(File.ReadAllText(path));
            }
            catch (Exception exception)
            {
                ShowStatus($"Import failed: {exception.Message}");
            }
#elif UNITY_WEBGL && !UNITY_EDITOR
            RequestDonorDataUpload(RuntimeObjectName, nameof(ReceiveImportedExportJson));
#else
            string clipboard = GUIUtility.systemCopyBuffer;
            if (DonorVisualizationLoader.TryLoadExportJson(clipboard, out DonorVisualizationDataSet dataSet, out string message))
            {
                ApplyDataSet(dataSet, message);
                return;
            }

            ShowStatus("Import expects donor export JSON on the system clipboard in this build.");
#endif
        }

        private void BuildInterface()
        {
            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new("Visualization UI Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            GameObject panelObject = new("Dwell Visualization HUD");
            RectTransform panelTransform = panelObject.AddComponent<RectTransform>();
            panelTransform.SetParent(canvas.transform, false);
            panelTransform.anchorMin = new Vector2(0f, 1f);
            panelTransform.anchorMax = new Vector2(0f, 1f);
            panelTransform.pivot = new Vector2(0f, 1f);
            panelTransform.anchoredPosition = new Vector2(14f, -14f);
            panelTransform.sizeDelta = new Vector2(540f, 112f);

            Image panelBackground = panelObject.AddComponent<Image>();
            panelBackground.color = new Color(0.03f, 0.045f, 0.065f, 0.66f);

            panelGroup = panelObject.AddComponent<CanvasGroup>();
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;

            titleLabel = CreateText(panelTransform, "Title", new Vector2(12f, -9f), new Vector2(516f, 24f), 15f, FontStyles.Bold);
            titleLabel.text = "Dwell visualization";

            statusLabel = CreateText(panelTransform, "Status", new Vector2(12f, -34f), new Vector2(516f, 52f), 11.5f, FontStyles.Normal);
            statusLabel.text = "Ready.";

            shortcutLabel = CreateText(panelTransform, "Shortcuts", new Vector2(12f, -88f), new Vector2(516f, 18f), 10.5f, FontStyles.Italic);
            shortcutLabel.text = "H diagnostics  |  R reload latest  |  I import export  |  Z zone mode";

            SetPanelVisible(false);
        }

        private static TMP_Text CreateText(
            RectTransform parent,
            string name,
            Vector2 anchoredPosition,
            Vector2 size,
            float fontSize,
            FontStyles fontStyle)
        {
            GameObject textObject = new(name);
            RectTransform textTransform = textObject.AddComponent<RectTransform>();
            textTransform.SetParent(parent, false);
            textTransform.anchorMin = new Vector2(0f, 1f);
            textTransform.anchorMax = new Vector2(0f, 1f);
            textTransform.pivot = new Vector2(0f, 1f);
            textTransform.anchoredPosition = anchoredPosition;
            textTransform.sizeDelta = size;

            TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
            text.font = TMP_Settings.defaultFontAsset;
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = Color.white;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            return text;
        }

        private void ToggleDiagnostics()
        {
            diagnosticsVisible = !diagnosticsVisible;
            SetPanelVisible(diagnosticsVisible);

            if (diagnosticsVisible)
            {
                hideToastAt = float.PositiveInfinity;
            }
        }

        private void ShowStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }

            SetPanelVisible(true);
            if (!diagnosticsVisible)
            {
                hideToastAt = Time.unscaledTime + ToastVisibleSeconds;
            }
        }

        private void SetPanelVisible(bool isVisible)
        {
            if (panelGroup == null)
            {
                return;
            }

            panelGroup.alpha = isVisible ? 1f : 0f;
            panelGroup.interactable = false;
            panelGroup.blocksRaycasts = false;
        }

        private string GetModeLabel()
        {
            return visualizationMode == DwellVisualizationMode.CurrentZone ? "Current zone" : "Full trial";
        }
    }

    public enum DwellVisualizationMode
    {
        FullTrial,
        CurrentZone
    }
}
