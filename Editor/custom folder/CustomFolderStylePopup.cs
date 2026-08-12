using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// File responsibility: provides the Alt-triggered popup for staged folder style
// edits, color presets, icon selection, renaming, and OK/Cancel workflows.
internal sealed class CustomFolderStylePopup : EditorWindow
{
    // Popup state is kept separate from the persistent database until OK is pressed.
    private static PendingImportedIcon pendingImportedIcon;
    private static CustomFolderStylePopup activePopup;
    private static CustomFolderStylePopup importPanelOwner;
    private static bool importPanelScheduled;
    private static readonly Color[] PresetColors =
    {
        new Color(0.93f, 0.18f, 0.22f, 1f),
        new Color(1.00f, 0.50f, 0.12f, 1f),
        new Color(1.00f, 0.82f, 0.18f, 1f),
        new Color(0.22f, 0.72f, 0.35f, 1f),
        new Color(0.10f, 0.72f, 0.82f, 1f),
        new Color(0.20f, 0.48f, 0.95f, 1f),
        new Color(0.58f, 0.35f, 0.92f, 1f),
        new Color(0.94f, 0.30f, 0.66f, 1f),
        new Color(0.55f, 0.58f, 0.62f, 1f),
        Color.white
    };
    private static readonly string[] UnityIconNames =
    {
        "Folder Icon",
        "Prefab Icon",
        "SceneAsset Icon",
        "GameObject Icon",
        "cs Script Icon",
        "ScriptableObject Icon",
        "Material Icon",
        "Texture Icon",
        "Sprite Icon",
        "Shader Icon",
        "AudioSource Icon",
        "AnimationClip Icon",
        "Animator Icon",
        "Camera Icon",
        "Light Icon",
        "Font Icon",
        "TextAsset Icon",
        "AssemblyDefinitionAsset Icon",
        "SettingsIcon",
        "Favorite",
        "FilterByType",
        "BuildSettings.Editor.Small"
    };

    private string[] folderGuids = Array.Empty<string>();
    private string folderNameInput = string.Empty;
    private Color folderColor = new Color(0.20f, 0.48f, 0.95f, 1f);
    private Color backgroundColor = new Color(0.20f, 0.48f, 0.95f, 1f);
    private Color nameColor = Color.white;
    private Texture2D folderIcon;
    private string builtinIconName;
    private string importedIconPath;
    private string importedIconName;
    private Vector2 mainScroll;
    private Vector2 importedIconScroll;
    private Vector2 unityIconScroll;
    private bool applyColor = true;
    private bool applyBackgroundColor = true;
    private bool applyNameColor = true;
    private bool applyIcon = true;
    private bool watchingExternalFocus;
    private double ignoreLostFocusUntil;
    private string[] scheduledImportFolderGuids = Array.Empty<string>();
    [NonSerialized] private Dictionary<string, Texture2D> importedIconTextureCache;
    [NonSerialized] private Dictionary<string, Texture2D> unityIconTextureCache;

    private static string L(string key)
    {
        return CustomFolderLocalization.Text(key);
    }

    public static void OpenForSelection(Rect activatorRect)
    {
        OpenForSelection(activatorRect, CustomFolderSelection.GetSelectedFolderGuids());
    }

    public static void OpenForSelection(Rect activatorRect, string[] targetFolderGuids)
    {
        OpenForScreenSelection(GUIUtility.GUIToScreenRect(activatorRect), targetFolderGuids);
    }

    public static void OpenForScreenSelection(Rect activatorScreenRect, string[] targetFolderGuids)
    {
        // This entry point accepts an already converted screen rectangle. It is
        // used when the caller captured the exact icon from a Project window.
        if (activePopup != null)
        {
            activePopup.Close();
        }

        targetFolderGuids = CloneFolderGuids(targetFolderGuids);
        if (targetFolderGuids.Length == 0)
        {
            return;
        }

        Vector2 popupSize = GetPopupSize();
        CustomFolderStylePopup popup = CreateInstance<CustomFolderStylePopup>();
        popup.position = GetPopupPosition(activatorScreenRect, popupSize);
        popup.LoadSelection(targetFolderGuids);
        popup.ApplyPendingImportedIcon();
        popup.ShowPopup();
        popup.Focus();
        activePopup = popup;
    }

    private static Vector2 GetPopupSize()
    {
        return new Vector2(390f, 600f);
    }

    private static Rect GetPopupPosition(Rect activatorScreenRect, Vector2 popupSize)
    {
        // Keep the popup beside the icon: prefer the left side and fall back to
        // the right only when the left side cannot fit inside the editor window.
        const float spacing = 8f;
        Rect mainWindowRect = EditorGUIUtility.GetMainWindowPosition();

        if (mainWindowRect.width <= 0f || mainWindowRect.height <= 0f)
        {
            mainWindowRect = new Rect(0f, 0f, Screen.currentResolution.width, Screen.currentResolution.height);
        }

        float leftX = activatorScreenRect.xMin - popupSize.x - spacing;
        float rightX = activatorScreenRect.xMax + spacing;
        float minX = mainWindowRect.xMin + spacing;
        float maxX = mainWindowRect.xMax - popupSize.x - spacing;
        bool leftFits = leftX >= minX && leftX <= maxX;
        bool rightFits = rightX >= minX && rightX <= maxX;
        float x;

        if (leftFits && rightFits)
        {
            x = leftX;
        }
        else if (rightFits)
        {
            x = rightX;
        }
        else if (leftFits)
        {
            x = leftX;
        }
        else
        {
            x = Mathf.Clamp(activatorScreenRect.center.x - popupSize.x * 0.5f, minX, maxX);
        }

        float minY = mainWindowRect.yMin + spacing;
        float maxY = mainWindowRect.yMax - popupSize.y - spacing;
        float y = activatorScreenRect.yMin;
        if (y > maxY)
        {
            y = activatorScreenRect.yMax - popupSize.y;
        }

        y = Mathf.Clamp(y, minY, maxY);
        return new Rect(x, y, popupSize.x, popupSize.y);
    }

    private static string[] CloneFolderGuids(string[] sourceFolderGuids)
    {
        return sourceFolderGuids == null ? Array.Empty<string>() : (string[])sourceFolderGuids.Clone();
    }

    private void OnDisable()
    {
        // Do not discard an imported icon while Unity's external file picker owns
        // focus; the delayed import flow will restore it when the picker closes.
        EditorApplication.delayCall -= CloseAfterLostFocusIfSafe;
        if (!importPanelScheduled)
        {
            pendingImportedIcon = null;
        }

        if (importPanelOwner == this)
        {
            EditorApplication.delayCall -= RunScheduledImportIconFromComputer;
            importPanelOwner = null;
            importPanelScheduled = false;
        }

        StopWatchingExternalFocus();

        if (activePopup == this)
        {
            activePopup = null;
        }
    }

    private void OnLostFocus()
    {
        EditorApplication.delayCall -= CloseAfterLostFocusIfSafe;
        EditorApplication.delayCall += CloseAfterLostFocusIfSafe;
    }

    private void CloseAfterLostFocusIfSafe()
    {
        if (this == null || activePopup != this)
        {
            return;
        }

        if (ShouldIgnoreLostFocus())
        {
            return;
        }

        if (ShouldKeepOpenForFocusedWindow(EditorWindow.focusedWindow))
        {
            StartWatchingExternalFocus();
            return;
        }

        Close();
    }

    private void StartWatchingExternalFocus()
    {
        if (watchingExternalFocus)
        {
            return;
        }

        watchingExternalFocus = true;
        EditorApplication.update += CloseWhenExternalToolFinishes;
    }

    private void StopWatchingExternalFocus()
    {
        if (!watchingExternalFocus)
        {
            return;
        }

        watchingExternalFocus = false;
        EditorApplication.update -= CloseWhenExternalToolFinishes;
    }

    private void CloseWhenExternalToolFinishes()
    {
        if (this == null || activePopup != this)
        {
            StopWatchingExternalFocus();
            return;
        }

        if (ShouldIgnoreLostFocus())
        {
            return;
        }

        EditorWindow focusedWindow = EditorWindow.focusedWindow;
        if (focusedWindow == this)
        {
            StopWatchingExternalFocus();
            return;
        }

        if (ShouldKeepOpenForFocusedWindow(focusedWindow))
        {
            return;
        }

        StopWatchingExternalFocus();
        Close();
    }

    private bool ShouldIgnoreLostFocus()
    {
        return importPanelScheduled || EditorApplication.timeSinceStartup < ignoreLostFocusUntil;
    }

    private static bool ShouldKeepOpenForFocusedWindow(EditorWindow focusedWindow)
    {
        if (focusedWindow == null)
        {
            return false;
        }

        if (focusedWindow is CustomFolderStylePopup)
        {
            return true;
        }

        Type focusedType = focusedWindow.GetType();
        string focusedTypeName = focusedType.Name;
        string focusedFullName = focusedType.FullName ?? string.Empty;
        return focusedTypeName.IndexOf("ColorPicker", StringComparison.OrdinalIgnoreCase) >= 0
            || focusedFullName.IndexOf("ColorPicker", StringComparison.OrdinalIgnoreCase) >= 0
            || focusedTypeName.IndexOf("ObjectSelector", StringComparison.OrdinalIgnoreCase) >= 0
            || focusedFullName.IndexOf("ObjectSelector", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void OnGUI()
    {
        // The content scrolls independently from the fixed action buttons below.
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape)
        {
            pendingImportedIcon = null;
            Close();
            Event.current.Use();
            return;
        }

        Rect rect = new Rect(0f, 0f, position.width, position.height);
        Rect contentRect = new Rect(10f, 8f, rect.width - 20f, rect.height - 56f);
        Rect buttonRect = new Rect(10f, rect.height - 40f, rect.width - 20f, 32f);
        GUILayout.BeginArea(contentRect);

        if (folderGuids.Length == 0)
        {
            EditorGUILayout.HelpBox(L("Select one or more folders in the Project window."), MessageType.Info);
            if (GUILayout.Button(L("Refresh Selection")))
            {
                LoadSelection();
            }

            GUILayout.EndArea();
            return;
        }

        mainScroll = EditorGUILayout.BeginScrollView(mainScroll);

        EditorGUILayout.LabelField(L("Selected Folders"), folderGuids.Length.ToString());
        EditorGUILayout.Space(6f);

        using (new EditorGUI.DisabledScope(folderGuids.Length != 1))
        {
            folderNameInput = EditorGUILayout.TextField(L("Folder Name"), folderNameInput);
        }

        EditorGUILayout.Space(6f);

        applyColor = EditorGUILayout.ToggleLeft(L("Apply Folder Color"), applyColor);
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!applyColor))
        {
            folderColor = EditorGUILayout.ColorField(new GUIContent(L("Folder Color")), folderColor, true, true, false);
            if (EditorGUI.EndChangeCheck())
            {
                applyColor = true;
            }
            DrawColorPresets(ref folderColor, ref applyColor);
        }

        EditorGUILayout.Space(6f);
        applyBackgroundColor = EditorGUILayout.ToggleLeft(L("Apply Background Color"), applyBackgroundColor);
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!applyBackgroundColor))
        {
            backgroundColor = EditorGUILayout.ColorField(new GUIContent(L("Background Color")), backgroundColor, true, true, false);
            if (EditorGUI.EndChangeCheck())
            {
                applyBackgroundColor = true;
            }
            DrawColorPresets(ref backgroundColor, ref applyBackgroundColor);
        }

        if (CanUseRandomColors())
        {
            if (GUILayout.Button(L("Randomize Folder and Background Colors"), GUILayout.Height(24f)))
            {
                RandomizeColors();
            }
        }

        EditorGUILayout.Space(6f);
        applyNameColor = EditorGUILayout.ToggleLeft(L("Apply Name Color"), applyNameColor);
        EditorGUI.BeginChangeCheck();
        using (new EditorGUI.DisabledScope(!applyNameColor))
        {
            nameColor = EditorGUILayout.ColorField(new GUIContent(L("Name Color")), nameColor, true, true, false);
            if (EditorGUI.EndChangeCheck())
            {
                applyNameColor = true;
            }
            DrawColorPresets(ref nameColor, ref applyNameColor);
        }

        EditorGUILayout.Space(6f);
        EditorGUI.BeginChangeCheck();
        Texture2D selectedIcon = (Texture2D)EditorGUILayout.ObjectField(L("Custom Icon"), folderIcon, typeof(Texture2D), false);
        if (EditorGUI.EndChangeCheck())
        {
            SetIconFromObjectField(selectedIcon);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(L("Import Image..."), GUILayout.Height(24f)))
            {
                ScheduleImportIconFromComputer();
            }

            using (new EditorGUI.DisabledScope(folderIcon == null))
            {
                if (GUILayout.Button(L("Remove"), GUILayout.Width(80f), GUILayout.Height(24f)))
                {
                    folderIcon = null;
                    builtinIconName = string.Empty;
                    importedIconPath = string.Empty;
                    importedIconName = string.Empty;
                    applyIcon = true;
                }
            }
        }

        if (!string.IsNullOrEmpty(importedIconName))
        {
            EditorGUILayout.LabelField(L("Selected Icon"), importedIconName);
        }

        DrawImportedIconPicker();
        DrawUnityIconPicker();

        EditorGUILayout.EndScrollView();
        GUILayout.EndArea();

        GUILayout.BeginArea(buttonRect);
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(L("OK"), GUILayout.Height(28f)))
            {
                if (ApplyStyle())
                {
                    pendingImportedIcon = null;
                    Close();
                }
            }

            if (GUILayout.Button(L("Clear"), GUILayout.Height(28f)))
            {
                ClearStyle();
                pendingImportedIcon = null;
                Close();
            }

            if (GUILayout.Button(L("Cancel"), GUILayout.Height(28f)))
            {
                pendingImportedIcon = null;
                Close();
            }
        }
        GUILayout.EndArea();
    }

    private static void DrawColorPresets(ref Color targetColor, ref bool applyTarget)
    {
        EditorGUILayout.LabelField(L("Presets"));
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (Color presetColor in PresetColors)
            {
                Rect swatchRect = GUILayoutUtility.GetRect(22f, 22f, GUILayout.Width(22f), GUILayout.Height(22f));
                EditorGUI.DrawRect(swatchRect, presetColor);
                Handles.color = EditorGUIUtility.isProSkin ? new Color(0f, 0f, 0f, 0.45f) : new Color(0f, 0f, 0f, 0.25f);
                Handles.DrawAAPolyLine(
                    1f,
                    new Vector3(swatchRect.x, swatchRect.y),
                    new Vector3(swatchRect.xMax, swatchRect.y),
                    new Vector3(swatchRect.xMax, swatchRect.yMax),
                    new Vector3(swatchRect.x, swatchRect.yMax),
                    new Vector3(swatchRect.x, swatchRect.y));

                if (GUI.Button(swatchRect, GUIContent.none, GUIStyle.none))
                {
                    targetColor = presetColor;
                    applyTarget = true;
                }
            }
        }
    }

    private void LoadSelection()
    {
        LoadSelection(CustomFolderSelection.GetSelectedFolderGuids());
    }

    private void LoadSelection(string[] selectedFolderGuids)
    {
        folderGuids = CloneFolderGuids(selectedFolderGuids);

        if (folderGuids.Length == 0)
        {
            return;
        }

        folderNameInput = folderGuids.Length == 1 ? GetFolderName(folderGuids[0]) : string.Empty;
        CustomFolderStyleEntry style = CustomFolderStyleDatabase.instance.Get(folderGuids[0]);
        if (style == null)
        {
            applyColor = true;
            applyBackgroundColor = true;
            applyNameColor = true;
            applyIcon = true;
            folderIcon = null;
            builtinIconName = string.Empty;
            importedIconPath = string.Empty;
            importedIconName = string.Empty;
            return;
        }

        applyColor = style.HasColor;
        if (applyColor)
        {
            folderColor = style.Color;
        }

        applyBackgroundColor = style.HasBackgroundColor;
        if (applyBackgroundColor)
        {
            backgroundColor = style.BackgroundColor;
        }

        applyNameColor = style.HasNameColor;
        if (applyNameColor)
        {
            nameColor = style.NameColor;
        }

        applyIcon = style.HasIcon;
        if (applyIcon)
        {
            folderIcon = CustomFolderStyleDatabase.LoadIcon(style);
            builtinIconName = style.BuiltinIconName;
            importedIconPath = style.IconAssetPath;
            importedIconName = !string.IsNullOrEmpty(importedIconPath)
                ? Path.GetFileName(importedIconPath)
                : builtinIconName;
        }
        else
        {
            folderIcon = null;
            builtinIconName = string.Empty;
            importedIconPath = string.Empty;
            importedIconName = string.Empty;
        }
    }

    private bool CanUseRandomColors()
    {
        // Random colors are an explicit popup action and are available for both
        // new folders and folders that already have a saved style.
        return folderGuids.Length > 0;
    }

    private void RandomizeColors()
    {
        // Random colors are intentionally created only from the Alt popup and
        // become persistent only when the user confirms with OK.
        folderColor = CreateRandomColor();
        backgroundColor = CreateRandomColor();
        applyColor = true;
        applyBackgroundColor = true;
    }

    private static Color CreateRandomColor()
    {
        return Color.HSVToRGB(
            UnityEngine.Random.value,
            Mathf.Lerp(0.45f, 0.80f, UnityEngine.Random.value),
            Mathf.Lerp(0.70f, 0.98f, UnityEngine.Random.value));
    }

    private bool ApplyStyle()
    {
        if (!ApplyFolderRename())
        {
            return false;
        }

        CustomFolderStyleDatabase.instance.ApplyStyleToFolders(
            folderGuids,
            applyColor,
            folderColor,
            applyBackgroundColor,
            backgroundColor,
            applyNameColor,
            nameColor,
            applyIcon,
            builtinIconName,
            importedIconPath,
            folderIcon);

        EditorApplication.RepaintProjectWindow();
        return true;
    }

    private bool ApplyFolderRename()
    {
        if (folderGuids.Length != 1)
        {
            return true;
        }

        string newFolderName = folderNameInput.Trim();
        string currentFolderName = GetFolderName(folderGuids[0]);
        if (newFolderName == currentFolderName)
        {
            return true;
        }

        if (string.IsNullOrEmpty(newFolderName))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), L("Folder name cannot be empty."), L("OK"));
            return false;
        }

        if (ContainsInvalidFileNameCharacter(newFolderName))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), L("Folder name contains invalid characters."), L("OK"));
            return false;
        }

        string folderPath = AssetDatabase.GUIDToAssetPath(folderGuids[0]);
        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), L("This folder could not be found."), L("OK"));
            return false;
        }

        string error = AssetDatabase.RenameAsset(folderPath, newFolderName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), error, L("OK"));
            return false;
        }

        folderNameInput = newFolderName;
        AssetDatabase.SaveAssets();
        return true;
    }

    private static string GetFolderName(string guid)
    {
        string folderPath = AssetDatabase.GUIDToAssetPath(guid);
        return string.IsNullOrEmpty(folderPath) ? string.Empty : Path.GetFileName(folderPath);
    }

    private static bool ContainsInvalidFileNameCharacter(string fileName)
    {
        if (fileName.IndexOf('/') >= 0 || fileName.IndexOf('\\') >= 0)
        {
            return true;
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            if (fileName.IndexOf(invalidChar) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearStyle()
    {
        CustomFolderStyleDatabase.instance.ClearMany(folderGuids);

        folderIcon = null;
        builtinIconName = string.Empty;
        importedIconPath = string.Empty;
        importedIconName = string.Empty;
        EditorApplication.RepaintProjectWindow();
    }

    private void ScheduleImportIconFromComputer()
    {
        // Delay the native file picker until the current GUI event has finished,
        // preventing the popup from closing or reopening during focus changes.
        if (importPanelScheduled)
        {
            return;
        }

        importPanelScheduled = true;
        importPanelOwner = this;
        ignoreLostFocusUntil = EditorApplication.timeSinceStartup + 2d;
        scheduledImportFolderGuids = CloneFolderGuids(folderGuids);
        EditorApplication.delayCall -= RunScheduledImportIconFromComputer;
        EditorApplication.delayCall += RunScheduledImportIconFromComputer;
    }

    private void RunScheduledImportIconFromComputer()
    {
        if (importPanelOwner != this)
        {
            return;
        }

        importPanelOwner = null;
        importPanelScheduled = false;
        string[] importFolderGuids = CloneFolderGuids(scheduledImportFolderGuids);
        scheduledImportFolderGuids = Array.Empty<string>();

        if (this == null || activePopup != this)
        {
            return;
        }

        ImportIconFromComputer(importFolderGuids);
    }

    private void ImportIconFromComputer(string[] importFolderGuids)
    {
        ignoreLostFocusUntil = EditorApplication.timeSinceStartup + 2d;
        string path = EditorUtility.OpenFilePanelWithFilters(
            L("Import Folder Icon"),
            string.Empty,
            new[] { L("Image files"), "png,jpg,jpeg", L("All files"), "*" });
        ignoreLostFocusUntil = EditorApplication.timeSinceStartup + 2d;

        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        string projectIconAssetPath = CustomFolderStyleDatabase.ImportIconIntoProject(path);
        Texture2D projectTexture = string.IsNullOrEmpty(projectIconAssetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(projectIconAssetPath);
        if (projectTexture == null)
        {
            EditorUtility.DisplayDialog(L("Import Folder Icon"), L("This image could not be loaded. Please choose a PNG or JPG file."), L("OK"));
            return;
        }

        folderIcon = projectTexture;
        builtinIconName = string.Empty;
        importedIconPath = projectIconAssetPath;
        importedIconName = Path.GetFileName(projectIconAssetPath);
        applyIcon = true;
        pendingImportedIcon = new PendingImportedIcon(importFolderGuids, importedIconPath, importedIconName, folderIcon);

        if (activePopup == this)
        {
            Repaint();
            return;
        }

        CustomFolderProjectView.RequestPopupOpenOnNextRepaint(importFolderGuids);
    }

    private void DrawImportedIconPicker()
    {
        string[] iconPaths = CustomFolderStyleDatabase.GetImportedIconAssetPaths();
        if (iconPaths.Length == 0)
        {
            return;
        }

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(L("Imported Icons"));

        const float iconSize = 42f;
        const float gap = 4f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 36f) / (iconSize + gap)));
        int rows = Mathf.CeilToInt(iconPaths.Length / (float)columns);
        float contentHeight = rows * (iconSize + gap);

        importedIconScroll = EditorGUILayout.BeginScrollView(importedIconScroll, GUILayout.Height(102f));
        Rect contentRect = GUILayoutUtility.GetRect(1f, contentHeight);

        for (int i = 0; i < iconPaths.Length; i++)
        {
            string iconPath = iconPaths[i];
            Texture2D icon = GetImportedIconTexture(iconPath);
            if (icon == null)
            {
                continue;
            }

            int row = i / columns;
            int column = i % columns;
            Rect buttonRect = new Rect(
                contentRect.x + column * (iconSize + gap),
                contentRect.y + row * (iconSize + gap),
                iconSize,
                iconSize);

            if (iconPath == importedIconPath)
            {
                EditorGUI.DrawRect(buttonRect, new Color(0.24f, 0.48f, 0.90f, 0.30f));
            }

            if (GUI.Button(buttonRect, new GUIContent(icon, Path.GetFileName(iconPath))))
            {
                folderIcon = icon;
                builtinIconName = string.Empty;
                importedIconPath = iconPath;
                importedIconName = Path.GetFileName(iconPath);
                applyIcon = true;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void DrawUnityIconPicker()
    {
        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(L("Unity Icons"));

        const float iconSize = 42f;
        const float gap = 4f;
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 36f) / (iconSize + gap)));
        int rows = Mathf.CeilToInt(UnityIconNames.Length / (float)columns);
        float contentHeight = rows * (iconSize + gap);

        unityIconScroll = EditorGUILayout.BeginScrollView(unityIconScroll, GUILayout.Height(102f));
        Rect contentRect = GUILayoutUtility.GetRect(1f, contentHeight);

        for (int i = 0; i < UnityIconNames.Length; i++)
        {
            string iconName = UnityIconNames[i];
            Texture2D icon = GetUnityIconTexture(iconName);
            if (icon == null)
            {
                continue;
            }

            int row = i / columns;
            int column = i % columns;
            Rect buttonRect = new Rect(
                contentRect.x + column * (iconSize + gap),
                contentRect.y + row * (iconSize + gap),
                iconSize,
                iconSize);

            if (iconName == builtinIconName)
            {
                EditorGUI.DrawRect(buttonRect, new Color(0.24f, 0.48f, 0.90f, 0.30f));
            }

            if (GUI.Button(buttonRect, new GUIContent(icon, iconName)))
            {
                folderIcon = icon;
                builtinIconName = iconName;
                importedIconPath = string.Empty;
                importedIconName = iconName;
                applyIcon = true;
            }
        }

        EditorGUILayout.EndScrollView();
    }

    private void SetIconFromObjectField(Texture2D selectedIcon)
    {
        if (selectedIcon == null)
        {
            folderIcon = null;
            builtinIconName = string.Empty;
            importedIconPath = string.Empty;
            importedIconName = string.Empty;
            applyIcon = true;
            return;
        }

        string projectIconAssetPath = CustomFolderStyleDatabase.ImportTextureIconIntoProject(selectedIcon);
        Texture2D projectTexture = string.IsNullOrEmpty(projectIconAssetPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(projectIconAssetPath);
        if (projectTexture == null)
        {
            folderIcon = null;
            builtinIconName = string.Empty;
            importedIconPath = string.Empty;
            importedIconName = string.Empty;
            EditorUtility.DisplayDialog(L("Import Folder Icon"), L("Please choose a PNG or JPG texture from this project, or use Import Image."), L("OK"));
            return;
        }

        folderIcon = projectTexture;
        builtinIconName = string.Empty;
        importedIconPath = projectIconAssetPath;
        importedIconName = Path.GetFileName(projectIconAssetPath);
        applyIcon = true;
    }

    private Texture2D GetImportedIconTexture(string iconPath)
    {
        if (importedIconTextureCache == null)
        {
            importedIconTextureCache = new Dictionary<string, Texture2D>();
        }

        if (importedIconTextureCache.TryGetValue(iconPath, out Texture2D cachedIcon) && cachedIcon != null)
        {
            return cachedIcon;
        }

        Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPath);
        importedIconTextureCache[iconPath] = icon;
        return icon;
    }

    private Texture2D GetUnityIconTexture(string iconName)
    {
        if (unityIconTextureCache == null)
        {
            unityIconTextureCache = new Dictionary<string, Texture2D>();
        }

        if (unityIconTextureCache.TryGetValue(iconName, out Texture2D cachedIcon) && cachedIcon != null)
        {
            return cachedIcon;
        }

        GUIContent content = EditorGUIUtility.IconContent(iconName);
        Texture2D icon = content == null ? null : content.image as Texture2D;
        unityIconTextureCache[iconName] = icon;
        return icon;
    }

    private void ApplyPendingImportedIcon()
    {
        if (pendingImportedIcon == null || !pendingImportedIcon.Matches(folderGuids))
        {
            return;
        }

        folderIcon = pendingImportedIcon.Texture;
        builtinIconName = string.Empty;
        importedIconPath = pendingImportedIcon.Path;
        importedIconName = pendingImportedIcon.Name;
        applyIcon = true;
    }

    private sealed class PendingImportedIcon
    {
        // Carries an imported icon across the temporary popup/file-picker handoff.
        private readonly string[] folderGuids;

        public PendingImportedIcon(string[] folderGuids, string path, string name, Texture2D texture)
        {
            this.folderGuids = folderGuids == null ? Array.Empty<string>() : (string[])folderGuids.Clone();
            Path = path;
            Name = name;
            Texture = texture;
        }

        public string Path { get; }
        public string Name { get; }
        public Texture2D Texture { get; }

        public bool Matches(string[] otherFolderGuids)
        {
            if (otherFolderGuids == null || otherFolderGuids.Length != folderGuids.Length)
            {
                return false;
            }

            for (int i = 0; i < folderGuids.Length; i++)
            {
                if (folderGuids[i] != otherFolderGuids[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}

