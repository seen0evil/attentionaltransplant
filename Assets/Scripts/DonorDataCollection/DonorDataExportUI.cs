using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AttentionalTransplants.DonorDataCollection
{
    public class DonorDataExportUI : MonoBehaviour
    {
        private const string EndSceneName = "End";
        private const string DonorSessionsFolderName = "DonorSessions";
        private const string ExportMimeType = "application/json";

        [SerializeField] private Button exportButton;
        [SerializeField] private TMP_Text buttonLabel;
        [SerializeField] private TMP_Text statusLabel;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void DownloadDonorDataFile(string fileName, string content, string mimeType);
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
            if (!string.Equals(scene.name, EndSceneName, StringComparison.Ordinal))
            {
                return;
            }

            Canvas canvas = FindAnyObjectByType<Canvas>();
            if (canvas == null || canvas.GetComponentInChildren<DonorDataExportUI>(true) != null)
            {
                return;
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            GameObject root = new("Donor Data Export UI");
            RectTransform rootTransform = root.AddComponent<RectTransform>();
            rootTransform.SetParent(canvas.transform, false);
            rootTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rootTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rootTransform.pivot = new Vector2(0.5f, 0.5f);
            rootTransform.anchoredPosition = new Vector2(0f, -150f);
            rootTransform.sizeDelta = new Vector2(520f, 170f);

            root.AddComponent<DonorDataExportUI>();
        }

        private void Awake()
        {
            if (exportButton == null || buttonLabel == null || statusLabel == null)
            {
                BuildInterface();
            }

            exportButton.onClick.RemoveListener(HandleExportClicked);
            exportButton.onClick.AddListener(HandleExportClicked);
            SetStatus("Ready to export the latest donor session.");
        }

        private void BuildInterface()
        {
            RectTransform rootTransform = GetComponent<RectTransform>();
            if (rootTransform == null)
            {
                rootTransform = gameObject.AddComponent<RectTransform>();
            }

            Image panelBackground = gameObject.GetComponent<Image>();
            if (panelBackground == null)
            {
                panelBackground = gameObject.AddComponent<Image>();
            }

            panelBackground.color = new Color(0.07f, 0.11f, 0.18f, 0.88f);

            GameObject buttonObject = new("Export Button");
            RectTransform buttonTransform = buttonObject.AddComponent<RectTransform>();
            buttonTransform.SetParent(rootTransform, false);
            buttonTransform.anchorMin = new Vector2(0.5f, 1f);
            buttonTransform.anchorMax = new Vector2(0.5f, 1f);
            buttonTransform.pivot = new Vector2(0.5f, 1f);
            buttonTransform.anchoredPosition = new Vector2(0f, -22f);
            buttonTransform.sizeDelta = new Vector2(300f, 56f);

            Image buttonImage = buttonObject.AddComponent<Image>();
            buttonImage.color = new Color(0.23f, 0.53f, 0.92f, 1f);

            exportButton = buttonObject.AddComponent<Button>();
            ColorBlock colors = exportButton.colors;
            colors.normalColor = buttonImage.color;
            colors.highlightedColor = new Color(0.29f, 0.61f, 0.96f, 1f);
            colors.pressedColor = new Color(0.17f, 0.42f, 0.74f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 0.6f);
            exportButton.colors = colors;
            exportButton.targetGraphic = buttonImage;

            GameObject buttonTextObject = new("Label");
            RectTransform buttonTextTransform = buttonTextObject.AddComponent<RectTransform>();
            buttonTextTransform.SetParent(buttonTransform, false);
            buttonTextTransform.anchorMin = Vector2.zero;
            buttonTextTransform.anchorMax = Vector2.one;
            buttonTextTransform.offsetMin = Vector2.zero;
            buttonTextTransform.offsetMax = Vector2.zero;

            buttonLabel = buttonTextObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(buttonLabel, "Export Donor Data", 28, FontStyles.Bold);
            buttonLabel.alignment = TextAlignmentOptions.Center;

            GameObject statusObject = new("Status Text");
            RectTransform statusTransform = statusObject.AddComponent<RectTransform>();
            statusTransform.SetParent(rootTransform, false);
            statusTransform.anchorMin = new Vector2(0.5f, 0f);
            statusTransform.anchorMax = new Vector2(0.5f, 0f);
            statusTransform.pivot = new Vector2(0.5f, 0f);
            statusTransform.anchoredPosition = new Vector2(0f, 18f);
            statusTransform.sizeDelta = new Vector2(470f, 72f);

            statusLabel = statusObject.AddComponent<TextMeshProUGUI>();
            ConfigureText(statusLabel, string.Empty, 22, FontStyles.Normal);
            statusLabel.alignment = TextAlignmentOptions.Center;
        }

        private void HandleExportClicked()
        {
            if (!TryBuildExportPayload(out string fileName, out string exportJson, out string message))
            {
                SetStatus(message);
                return;
            }

            try
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                DownloadDonorDataFile(fileName, exportJson, ExportMimeType);
                SetStatus($"Downloaded {fileName} from the latest donor session.");
#else
                string exportPath = Path.Combine(Application.persistentDataPath, fileName);
                File.WriteAllText(exportPath, exportJson);
                SetStatus($"Saved export to {exportPath}");
#endif
            }
            catch (Exception exception)
            {
                SetStatus($"Export failed: {exception.Message}");
            }
        }

        private bool TryBuildExportPayload(out string fileName, out string exportJson, out string message)
        {
            fileName = string.Empty;
            exportJson = string.Empty;

            string sessionFolderPath = ResolveSessionFolderPath();
            if (string.IsNullOrWhiteSpace(sessionFolderPath) || !Directory.Exists(sessionFolderPath))
            {
                message = "No donor session folder was found to export.";
                return false;
            }

            string[] filePaths = Directory.GetFiles(sessionFolderPath, "*", SearchOption.TopDirectoryOnly);
            Array.Sort(filePaths, StringComparer.OrdinalIgnoreCase);

            if (filePaths.Length == 0)
            {
                message = "The donor session folder exists, but it does not contain any files yet.";
                return false;
            }

            DonorDataExportPayload payload = new()
            {
                exportFormat = "attentionaltransplant_donor_export_v1",
                exportedAtUtc = DateTime.UtcNow.ToString("O"),
                sessionFolderName = Path.GetFileName(sessionFolderPath),
                sourcePersistentDataPath = Application.persistentDataPath
            };

            foreach (string currentFilePath in filePaths)
            {
                payload.files.Add(new DonorDataExportFile
                {
                    fileName = Path.GetFileName(currentFilePath),
                    relativePath = Path.GetFileName(currentFilePath),
                    text = File.ReadAllText(currentFilePath)
                });
            }

            fileName = $"{payload.sessionFolderName}_export.json";
            exportJson = JsonUtility.ToJson(payload, true);
            message = "Export prepared.";
            return true;
        }

        private static string ResolveSessionFolderPath()
        {
            if (SessionManager.Instance != null && !string.IsNullOrWhiteSpace(SessionManager.Instance.SessionFolderPath))
            {
                return SessionManager.Instance.SessionFolderPath;
            }

            string sessionsRoot = Path.Combine(Application.persistentDataPath, DonorSessionsFolderName);
            if (!Directory.Exists(sessionsRoot))
            {
                return string.Empty;
            }

            string[] sessionDirectories = Directory.GetDirectories(sessionsRoot, "*", SearchOption.TopDirectoryOnly);
            if (sessionDirectories.Length == 0)
            {
                return string.Empty;
            }

            Array.Sort(sessionDirectories, StringComparer.OrdinalIgnoreCase);
            return sessionDirectories[^1];
        }

        private static void ConfigureText(TMP_Text textComponent, string text, float fontSize, FontStyles fontStyle)
        {
            textComponent.text = text;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.color = Color.white;
            textComponent.enableWordWrapping = true;
            textComponent.font = TMP_Settings.defaultFontAsset;
        }

        private void SetStatus(string message)
        {
            if (statusLabel != null)
            {
                statusLabel.text = message;
            }
        }
    }

    [Serializable]
    public class DonorDataExportPayload
    {
        public string exportFormat;
        public string exportedAtUtc;
        public string sessionFolderName;
        public string sourcePersistentDataPath;
        public List<DonorDataExportFile> files = new();
    }

    [Serializable]
    public class DonorDataExportFile
    {
        public string fileName;
        public string relativePath;
        public string text;
    }
}
