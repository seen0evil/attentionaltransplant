using System;
using System.IO;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AttentionalTransplants.DonorDataVisualization
{
    public class DwellVisualizationController : MonoBehaviour
    {
        public const string VisualizationSceneName = "Visualization";

        private const string RuntimeObjectName = "Dwell Visualization Runtime";

        [SerializeField] private DwellGlowApplier glowApplier;
        [SerializeField] private PathHeatmapRenderer pathHeatmapRenderer;

        private TMP_Text titleLabel;
        private TMP_Text statusLabel;
        private Button reloadButton;
        private Button importButton;

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
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            glowApplier = glowApplier != null ? glowApplier : gameObject.AddComponent<DwellGlowApplier>();
            pathHeatmapRenderer = pathHeatmapRenderer != null ? pathHeatmapRenderer : gameObject.AddComponent<PathHeatmapRenderer>();
            BuildInterface();
        }

        private void Start()
        {
            LoadLatestLocalSession();
        }

        public void ReceiveImportedExportJson(string exportJson)
        {
            if (DonorVisualizationLoader.TryLoadExportJson(exportJson, out DonorVisualizationDataSet dataSet, out string message))
            {
                ApplyDataSet(dataSet, message);
                return;
            }

            SetStatus(message);
        }

        private void LoadLatestLocalSession()
        {
            SetStatus("Loading latest local donor session...");
            if (DonorVisualizationLoader.TryLoadLatestLocalSession(out DonorVisualizationDataSet dataSet, out string message))
            {
                ApplyDataSet(dataSet, message);
                return;
            }

            SetStatus(message);
        }

        private void ApplyDataSet(DonorVisualizationDataSet dataSet, string loadMessage)
        {
            DwellGlowReport glowReport = glowApplier.Apply(dataSet);
            PathHeatmapReport pathReport = pathHeatmapRenderer.Render(dataSet);

            titleLabel.text = $"{dataSet.sessionId} / {dataSet.trialId}";
            SetStatus(
                $"{loadMessage}\n" +
                $"Objects glowing: {glowReport.glowingTargetCount}/{glowReport.sceneTargetCount}; unmatched data IDs: {glowReport.unmatchedDwellCount}.\n" +
                $"Path heat cells: {pathReport.renderedCellCount} from {pathReport.sampleCount} attention samples.");
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
                SetStatus($"Import failed: {exception.Message}");
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

            SetStatus("Import expects donor export JSON on the system clipboard in this build.");
#endif
        }

        private void BuildInterface()
        {
            EnsureEventSystem();

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObject = new("Visualization UI Canvas");
                canvas = canvasObject.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasObject.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                canvasObject.AddComponent<GraphicRaycaster>();
            }

            GameObject panelObject = new("Dwell Visualization Panel");
            RectTransform panelTransform = panelObject.AddComponent<RectTransform>();
            panelTransform.SetParent(canvas.transform, false);
            panelTransform.anchorMin = new Vector2(0f, 1f);
            panelTransform.anchorMax = new Vector2(0f, 1f);
            panelTransform.pivot = new Vector2(0f, 1f);
            panelTransform.anchoredPosition = new Vector2(18f, -18f);
            panelTransform.sizeDelta = new Vector2(560f, 190f);

            Image panelBackground = panelObject.AddComponent<Image>();
            panelBackground.color = new Color(0.04f, 0.06f, 0.09f, 0.84f);

            titleLabel = CreateText(panelTransform, "Title", new Vector2(20f, -14f), new Vector2(520f, 34f), 22f, FontStyles.Bold);
            titleLabel.text = "Dwell visualization";

            statusLabel = CreateText(panelTransform, "Status", new Vector2(20f, -54f), new Vector2(520f, 78f), 17f, FontStyles.Normal);
            statusLabel.text = "Ready.";

            reloadButton = CreateButton(panelTransform, "Reload Latest", new Vector2(20f, -144f), new Vector2(180f, 34f), "Reload Latest");
            reloadButton.onClick.AddListener(LoadLatestLocalSession);

            importButton = CreateButton(panelTransform, "Import Export", new Vector2(216f, -144f), new Vector2(180f, 34f), "Import Export");
            importButton.onClick.AddListener(HandleImportClicked);
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

        private static Button CreateButton(RectTransform parent, string name, Vector2 anchoredPosition, Vector2 size, string label)
        {
            GameObject buttonObject = new(name);
            RectTransform buttonTransform = buttonObject.AddComponent<RectTransform>();
            buttonTransform.SetParent(parent, false);
            buttonTransform.anchorMin = new Vector2(0f, 1f);
            buttonTransform.anchorMax = new Vector2(0f, 1f);
            buttonTransform.pivot = new Vector2(0f, 1f);
            buttonTransform.anchoredPosition = anchoredPosition;
            buttonTransform.sizeDelta = size;

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.13f, 0.42f, 0.72f, 0.96f);

            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = buttonImage;

            GameObject labelObject = new("Label");
            RectTransform labelTransform = labelObject.AddComponent<RectTransform>();
            labelTransform.SetParent(buttonTransform, false);
            labelTransform.anchorMin = Vector2.zero;
            labelTransform.anchorMax = Vector2.one;
            labelTransform.offsetMin = Vector2.zero;
            labelTransform.offsetMax = Vector2.zero;

            TMP_Text labelText = labelObject.AddComponent<TextMeshProUGUI>();
            labelText.font = TMP_Settings.defaultFontAsset;
            labelText.fontSize = 17f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.color = Color.white;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.text = label;

            return button;
        }

        private static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            eventSystemObject.AddComponent<StandaloneInputModule>();
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }
}
