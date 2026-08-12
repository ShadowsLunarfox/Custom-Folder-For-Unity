using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// File responsibility: provides the large management window for editing styles,
// controlling display layers, renaming folders, and managing imported icons.
internal sealed class CustomFolderStyleManagerWindow : EditorWindow
{
    private const string MenuPath = "Tools/Custom Folder/Style Manager";
    private const float FolderListWidth = 320f;
    private const float IconTileWidth = 86f;
    private const float IconTileHeight = 72f;
    private const float PreviewSize = 42f;
    private const float ImportedIconLibraryMinHeight = 150f;
    private const float ImportedIconLibraryMaxHeight = 480f;
    private const float ToolbarSearchMinWidth = 180f;
    private const float SelectAllButtonWidth = 78f;
    private const float ClearSelectionButtonWidth = 116f;
    private const string ImportedIconLibraryHeightKey = "CustomFolderStyleManager.ImportedIconLibraryHeight";

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
        "SettingsIcon",
        "Favorite",
        "FilterByType"
    };

    private readonly HashSet<string> selectedFolderGuids = new HashSet<string>(StringComparer.Ordinal);
    private readonly HashSet<string> selectedIconPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private CustomFolderStyleEntry[] styledEntries = Array.Empty<CustomFolderStyleEntry>();
    private Vector2 folderScroll;
    private Vector2 editorScroll;
    private Vector2 iconScroll;
    private string searchText = string.Empty;
    private string loadedSelectionSignature = string.Empty;
    private float importedIconLibraryHeight = 190f;
    private int lastSelectedIconIndex = -1;
    private bool resizingIconLibrary;
    private float resizeStartMouseY;
    private float resizeStartHeight;

    private bool applyColor;
    private bool applyBackgroundColor;
    private bool applyNameColor;
    private bool applyIcon;
    private bool showFolderColor = true;
    private bool showBackgroundColor = true;
    private bool showNameColor = true;
    private Color folderColor = new Color(0.20f, 0.48f, 0.95f, 1f);
    private Color backgroundColor = new Color(0.20f, 0.48f, 0.95f, 1f);
    private Color nameColor = Color.white;
    private string folderNameInput = string.Empty;
    private Texture2D selectedIcon;
    private string builtinIconName = string.Empty;
    private string importedIconPath = string.Empty;

    private static string L(string key)
    {
        return CustomFolderLocalization.Text(key);
    }

    [MenuItem(MenuPath)]
    private static void OpenWindow()
    {
        CustomFolderStyleManagerWindow window = GetWindow<CustomFolderStyleManagerWindow>();
        window.titleContent = new GUIContent(L("Custom Folder Manager"));
        window.minSize = new Vector2(900f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        // Keep the toolbar and icon-library actions readable after a domain reload.
        minSize = new Vector2(900f, 620f);
        importedIconLibraryHeight = EditorPrefs.GetFloat(ImportedIconLibraryHeightKey, importedIconLibraryHeight);
        importedIconLibraryHeight = Mathf.Clamp(importedIconLibraryHeight, ImportedIconLibraryMinHeight, ImportedIconLibraryMaxHeight);
        EditorApplication.projectChanged += RefreshEntries;
        RefreshEntries();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= RefreshEntries;
    }

    private void OnGUI()
    {
        // Keep the folder editor and the icon library in one reusable management view.
        SyncEditorState();
        DrawToolbar();
        DrawDisplaySettings();

        EditorGUILayout.BeginHorizontal();
        DrawFolderPanel();
        DrawEditorPanel();
        EditorGUILayout.EndHorizontal();

        DrawImportedIconLibrary();
    }

    private void DrawDisplaySettings()
    {
        // These switches are global display-layer controls, so they remain
        // available even when no styled folder is selected.
        using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField(L("Display Layers"), EditorStyles.boldLabel, GUILayout.Width(96f));
            DrawDisplayToggle("Folder Color", showFolderColor, 0);
            DrawDisplayToggle("Background Color", showBackgroundColor, 1);
            DrawDisplayToggle("Name Color", showNameColor, 2);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField(L("Language"), GUILayout.Width(58f));
            int newLanguageIndex = EditorGUILayout.Popup(
                CustomFolderLocalization.LanguageIndex,
                CustomFolderLocalization.GetLanguageOptions(),
                GUILayout.Width(136f));
            if (newLanguageIndex != CustomFolderLocalization.LanguageIndex)
            {
                CustomFolderLocalization.LanguageIndex = newLanguageIndex;
                titleContent = new GUIContent(L("Custom Folder Manager"));
                Repaint();
                EditorApplication.RepaintProjectWindow();
            }
            if (GUILayout.Button(L("Refresh"), GUILayout.Width(72f)))
            {
                RefreshWindowData();
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            if (GUILayout.Button(L("Refresh"), EditorStyles.toolbarButton, GUILayout.Width(60f)))
            {
                RefreshWindowData();
            }

            GUILayout.Label(L("Styled Folders"), EditorStyles.toolbarButton, GUILayout.Width(120f));
            searchText = GUILayout.TextField(
                searchText,
                GUI.skin.FindStyle("ToolbarSeachTextField") ?? EditorStyles.toolbarTextField,
                GUILayout.MinWidth(ToolbarSearchMinWidth),
                GUILayout.ExpandWidth(true));
            if (GUILayout.Button("x", EditorStyles.toolbarButton, GUILayout.Width(22f)))
            {
                searchText = string.Empty;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            GUILayout.Label(
                selectedFolderGuids.Count + " " + L("selected"),
                EditorStyles.toolbarButton,
                GUILayout.Width(100f));
        }
    }

    private void DrawFolderPanel()
    {
        using (new EditorGUILayout.VerticalScope(GUILayout.Width(FolderListWidth)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("Select All")))
                {
                    SelectAllVisibleFolders();
                }

                if (GUILayout.Button(L("Clear")))
                {
                    selectedFolderGuids.Clear();
                    loadedSelectionSignature = string.Empty;
                }
            }

            folderScroll = EditorGUILayout.BeginScrollView(folderScroll, GUI.skin.box);
            foreach (CustomFolderStyleEntry entry in styledEntries)
            {
                if (!MatchesSearch(entry))
                {
                    continue;
                }

                DrawFolderRow(entry);
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawFolderRow(CustomFolderStyleEntry entry)
    {
        bool isSelected = selectedFolderGuids.Contains(entry.Guid);
        Rect rowRect = GUILayoutUtility.GetRect(1f, 48f, GUILayout.ExpandWidth(true));
        if (isSelected)
        {
            EditorGUI.DrawRect(rowRect, new Color(0.24f, 0.48f, 0.90f, 0.30f));
        }

        if (GUI.Button(rowRect, GUIContent.none, GUIStyle.none))
        {
            // A normal click selects one folder; Ctrl/Cmd extends the selection.
            bool extendSelection = Event.current.control || Event.current.command;
            if (!extendSelection)
            {
                selectedFolderGuids.Clear();
            }

            if (isSelected && extendSelection)
            {
                selectedFolderGuids.Remove(entry.Guid);
            }
            else
            {
                selectedFolderGuids.Add(entry.Guid);
            }

            loadedSelectionSignature = string.Empty;
            Repaint();
        }

        Rect iconRect = new Rect(rowRect.x + 8f, rowRect.y + 3f, PreviewSize, PreviewSize);
        Texture2D icon = CustomFolderStyleDatabase.LoadIcon(entry);
        if (icon == null)
        {
            icon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
        }

        if (icon != null)
        {
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        }

        Rect labelRect = new Rect(iconRect.xMax + 8f, rowRect.y + 7f, rowRect.width - iconRect.xMax - 16f, 18f);
        GUI.Label(labelRect, entry.FolderName, EditorStyles.boldLabel);
        GUI.Label(new Rect(labelRect.x, labelRect.y + 18f, labelRect.width, 16f), entry.FolderPath, EditorStyles.miniLabel);

        float swatchX = rowRect.xMax - 26f;
        if (entry.HasBackgroundColor)
        {
            EditorGUI.DrawRect(new Rect(swatchX, rowRect.y + 8f, 16f, 8f), entry.BackgroundColor);
        }

        if (entry.HasColor)
        {
            EditorGUI.DrawRect(new Rect(swatchX, rowRect.y + 20f, 16f, 8f), entry.Color);
        }
    }

    private void DrawEditorPanel()
    {
        using (new EditorGUILayout.VerticalScope())
        {
            if (selectedFolderGuids.Count == 0)
            {
                EditorGUILayout.HelpBox(L("Select one or more styled folders."), MessageType.Info);
                return;
            }

            editorScroll = EditorGUILayout.BeginScrollView(editorScroll);
            EditorGUILayout.LabelField(L("Edit Selected Folders"), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(CustomFolderLocalization.Format("folder(s)", selectedFolderGuids.Count));
            EditorGUILayout.Space(4f);

            using (new EditorGUI.DisabledScope(selectedFolderGuids.Count != 1))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    folderNameInput = EditorGUILayout.TextField(L("Folder Name"), folderNameInput);
                    if (GUILayout.Button(L("Rename"), GUILayout.Width(72f)))
                    {
                        RenameSelectedFolder();
                    }
                }
            }

            EditorGUILayout.Space(4f);

            applyColor = EditorGUILayout.Toggle(L("Apply Folder Color"), applyColor);
            using (new EditorGUI.DisabledScope(!applyColor))
            {
                folderColor = EditorGUILayout.ColorField(new GUIContent(L("Folder Color")), folderColor, true, true, false);
                DrawColorPresets(ref folderColor);
            }

            applyBackgroundColor = EditorGUILayout.Toggle(L("Apply Background Color"), applyBackgroundColor);
            using (new EditorGUI.DisabledScope(!applyBackgroundColor))
            {
                backgroundColor = EditorGUILayout.ColorField(new GUIContent(L("Background Color")), backgroundColor, true, true, false);
                DrawColorPresets(ref backgroundColor);
            }

            applyNameColor = EditorGUILayout.Toggle(L("Apply Name Color"), applyNameColor);
            using (new EditorGUI.DisabledScope(!applyNameColor))
            {
                nameColor = EditorGUILayout.ColorField(new GUIContent(L("Name Color")), nameColor, true, true, false);
                DrawColorPresets(ref nameColor);
            }

            EditorGUILayout.Space(4f);
            applyIcon = EditorGUILayout.Toggle(L("Apply Icon"), applyIcon);
            using (new EditorGUI.DisabledScope(!applyIcon))
            {
                DrawIconEditor();
            }

            EditorGUILayout.Space(8f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("Apply Changes to Selected Folders"), GUILayout.Height(28f)))
                {
                    ApplyStyle(GetSelectedFolderGuids());
                }

                if (GUILayout.Button(L("Apply Changes to All Styled Folders"), GUILayout.Height(28f)))
                {
                    ApplyStyle(GetAllStyledFolderGuids());
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(L("Remove Styles from Selected Folders")))
                {
                    ClearStyles(GetSelectedFolderGuids(), "Remove Styles from Selected Folders");
                }

                if (GUILayout.Button(L("Remove Styles from All Styled Folders")))
                {
                    ClearStyles(GetAllStyledFolderGuids(), "Remove Styles from All Styled Folders");
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void DrawIconEditor()
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUI.BeginChangeCheck();
            Texture2D objectFieldIcon = (Texture2D)EditorGUILayout.ObjectField(L("Custom Icon"), selectedIcon, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                if (objectFieldIcon == null)
                {
                    selectedIcon = null;
                    importedIconPath = string.Empty;
                    builtinIconName = string.Empty;
                }
                else
                {
                    string assetPath = CustomFolderStyleDatabase.ImportTextureIconIntoProject(objectFieldIcon);
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        importedIconPath = assetPath;
                        builtinIconName = string.Empty;
                        selectedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                    }
                }
            }
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button(L("Import Image...")))
            {
                ImportSingleIcon();
            }

            if (GUILayout.Button(L("Clear Icon")))
            {
                selectedIcon = null;
                importedIconPath = string.Empty;
                builtinIconName = string.Empty;
            }
        }

        if (!string.IsNullOrEmpty(importedIconPath))
        {
            EditorGUILayout.LabelField(Path.GetFileName(importedIconPath), EditorStyles.miniLabel);
        }

        DrawUnityIconChoices();
    }

    private void DrawDisplayToggle(string label, bool currentValue, int displayChannel)
    {
        float controlWidth = displayChannel == 0
            ? 126f
            : displayChannel == 1
                ? 166f
                : 120f;
        bool newValue = EditorGUILayout.ToggleLeft(L(label), currentValue, GUILayout.Width(controlWidth));
        if (newValue == currentValue)
        {
            return;
        }

        if (displayChannel == 0)
        {
            showFolderColor = newValue;
            CustomFolderStyleDatabase.instance.SetGlobalFolderColorDisplay(newValue);
        }
        else if (displayChannel == 1)
        {
            showBackgroundColor = newValue;
            CustomFolderStyleDatabase.instance.SetGlobalBackgroundColorDisplay(newValue);
        }
        else
        {
            showNameColor = newValue;
            CustomFolderStyleDatabase.instance.SetGlobalNameColorDisplay(newValue);
        }

        EditorApplication.RepaintProjectWindow();
    }

    private void DrawUnityIconChoices()
    {
        EditorGUILayout.LabelField(L("Unity Icons"), EditorStyles.miniBoldLabel);
        int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - FolderListWidth - 32f) / IconTileWidth));
        int rows = Mathf.CeilToInt(UnityIconNames.Length / (float)columns);
        Rect contentRect = GUILayoutUtility.GetRect(1f, rows * IconTileHeight);

        for (int i = 0; i < UnityIconNames.Length; i++)
        {
            string iconName = UnityIconNames[i];
            Texture2D icon = EditorGUIUtility.IconContent(iconName).image as Texture2D;
            if (icon == null)
            {
                continue;
            }

            int row = i / columns;
            int column = i % columns;
            Rect tileRect = new Rect(contentRect.x + column * IconTileWidth, contentRect.y + row * IconTileHeight, IconTileWidth - 4f, IconTileHeight - 4f);
            DrawIconTile(tileRect, icon, iconName == builtinIconName, iconName);
            if (GUI.Button(tileRect, GUIContent.none, GUIStyle.none))
            {
                selectedIcon = icon;
                builtinIconName = iconName;
                importedIconPath = string.Empty;
                applyIcon = true;
            }
        }
    }

    private void DrawImportedIconLibrary()
    {
        // The library is backed by the project's managed icon folder, so every
        // thumbnail shown here remains available after Unity restarts.
        string[] iconPaths = CustomFolderStyleDatabase.GetImportedIconAssetPaths();
        Rect resizeHandle = GUILayoutUtility.GetRect(1f, 6f, GUILayout.ExpandWidth(true));
        HandleIconLibraryResize(resizeHandle);

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(importedIconLibraryHeight)))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L("Imported Icons"), EditorStyles.boldLabel);
                GUILayout.Label(CustomFolderLocalization.Format("icon(s)", iconPaths.Length), EditorStyles.miniLabel, GUILayout.Width(72f));
                using (new EditorGUI.DisabledScope(iconPaths.Length == 0))
                {
                    if (GUILayout.Button(L("Select All"), GUILayout.Width(SelectAllButtonWidth)))
                    {
                        SelectAllImportedIcons(iconPaths);
                    }

                    using (new EditorGUI.DisabledScope(selectedIconPaths.Count == 0))
                    {
                        if (GUILayout.Button(L("Clear Selection"), GUILayout.Width(ClearSelectionButtonWidth)))
                        {
                            ClearImportedIconSelection();
                        }
                    }
                }

                if (GUILayout.Button(L("Import Image..."), GUILayout.Width(100f)))
                {
                    ImportSingleIcon();
                }

                if (GUILayout.Button(L("Import Folder..."), GUILayout.Width(100f)))
                {
                    ImportIconFolder();
                }

                using (new EditorGUI.DisabledScope(selectedIconPaths.Count == 0))
                {
                    if (GUILayout.Button(L("Delete Selected"), GUILayout.Width(105f)))
                    {
                        DeleteSelectedIcons();
                    }
                }
            }

            iconScroll = EditorGUILayout.BeginScrollView(iconScroll, GUILayout.ExpandHeight(true));
            if (iconPaths.Length == 0)
            {
                EditorGUILayout.LabelField(L("No imported icons."), EditorStyles.miniLabel);
            }
            else
            {
                int columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 20f) / IconTileWidth));
                int rows = Mathf.CeilToInt(iconPaths.Length / (float)columns);
                Rect contentRect = GUILayoutUtility.GetRect(1f, rows * IconTileHeight);
                for (int i = 0; i < iconPaths.Length; i++)
                {
                    Texture2D icon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconPaths[i]);
                    if (icon == null)
                    {
                        continue;
                    }

                    int row = i / columns;
                    int column = i % columns;
                    Rect tileRect = new Rect(contentRect.x + column * IconTileWidth, contentRect.y + row * IconTileHeight, IconTileWidth - 4f, IconTileHeight - 4f);
                    bool isSelected = selectedIconPaths.Contains(iconPaths[i]);
                    DrawIconTile(tileRect, icon, isSelected, Path.GetFileNameWithoutExtension(iconPaths[i]));
                    if (GUI.Button(tileRect, GUIContent.none, GUIStyle.none))
                    {
                        ToggleIconSelection(iconPaths[i], i, iconPaths);
                    }
                }
            }

            EditorGUILayout.EndScrollView();
        }
    }

    private void HandleIconLibraryResize(Rect resizeHandle)
    {
        EditorGUIUtility.AddCursorRect(resizeHandle, MouseCursor.ResizeVertical);
        EditorGUI.DrawRect(resizeHandle, EditorGUIUtility.isProSkin
            ? new Color(0.18f, 0.18f, 0.18f, 1f)
            : new Color(0.72f, 0.72f, 0.72f, 1f));

        Event currentEvent = Event.current;
        if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0 && resizeHandle.Contains(currentEvent.mousePosition))
        {
            resizingIconLibrary = true;
            resizeStartMouseY = currentEvent.mousePosition.y;
            resizeStartHeight = importedIconLibraryHeight;
            currentEvent.Use();
        }
        else if (resizingIconLibrary && currentEvent.type == EventType.MouseDrag)
        {
            importedIconLibraryHeight = Mathf.Clamp(
                resizeStartHeight - (currentEvent.mousePosition.y - resizeStartMouseY),
                ImportedIconLibraryMinHeight,
                ImportedIconLibraryMaxHeight);
            EditorPrefs.SetFloat(ImportedIconLibraryHeightKey, importedIconLibraryHeight);
            Repaint();
            currentEvent.Use();
        }
        else if (resizingIconLibrary && currentEvent.type == EventType.MouseUp)
        {
            resizingIconLibrary = false;
            currentEvent.Use();
        }
    }

    private static void DrawIconTile(Rect tileRect, Texture2D icon, bool selected, string label)
    {
        if (selected)
        {
            EditorGUI.DrawRect(tileRect, new Color(0.24f, 0.48f, 0.90f, 0.35f));
        }

        Rect iconRect = new Rect(tileRect.x + (tileRect.width - PreviewSize) * 0.5f, tileRect.y + 3f, PreviewSize, PreviewSize);
        GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit, true);
        GUI.Label(new Rect(tileRect.x + 2f, iconRect.yMax + 2f, tileRect.width - 4f, 14f), label, EditorStyles.centeredGreyMiniLabel);
    }

    private void DrawColorPresets(ref Color targetColor)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            foreach (Color presetColor in PresetColors)
            {
                Rect swatchRect = GUILayoutUtility.GetRect(20f, 18f, GUILayout.Width(20f), GUILayout.Height(18f));
                EditorGUI.DrawRect(swatchRect, presetColor);
                if (GUI.Button(swatchRect, GUIContent.none, GUIStyle.none))
                {
                    targetColor = presetColor;
                }
            }
        }
    }

    private void SyncEditorState()
    {
        CustomFolderStyleDatabase database = CustomFolderStyleDatabase.instance;
        showFolderColor = database.ShowFolderColor;
        showBackgroundColor = database.ShowBackgroundColor;
        showNameColor = database.ShowNameColor;

        // Reload the editor controls only when the selected folder set changes.
        // This prevents repaint events from resetting values while editing.
        string signature = GetSelectionSignature();
        if (signature == loadedSelectionSignature)
        {
            return;
        }

        loadedSelectionSignature = signature;
        if (selectedFolderGuids.Count == 0)
        {
            return;
        }

        string[] selectedGuids = GetSelectedFolderGuids();
        string firstGuid = selectedGuids[0];
        folderNameInput = GetFolderName(firstGuid);
        CustomFolderStyleEntry style = CustomFolderStyleDatabase.instance.Get(firstGuid);
        if (style == null)
        {
            applyColor = true;
            applyBackgroundColor = true;
            applyNameColor = true;
            applyIcon = true;
            selectedIcon = null;
            importedIconPath = string.Empty;
            builtinIconName = string.Empty;
            return;
        }

        applyColor = style.HasColor;
        applyBackgroundColor = style.HasBackgroundColor;
        applyNameColor = style.HasNameColor;
        applyIcon = style.HasIcon;
        if (style.HasColor)
        {
            folderColor = style.Color;
        }

        if (style.HasBackgroundColor)
        {
            backgroundColor = style.BackgroundColor;
        }

        if (style.HasNameColor)
        {
            nameColor = style.NameColor;
        }

        builtinIconName = style.BuiltinIconName ?? string.Empty;
        importedIconPath = style.IconAssetPath ?? string.Empty;
        selectedIcon = CustomFolderStyleDatabase.LoadIcon(style);
    }

    private void RefreshEntries()
    {
        styledEntries = CustomFolderStyleDatabase.instance.GetStyledEntries();
        HashSet<string> validGuids = new HashSet<string>(StringComparer.Ordinal);
        foreach (CustomFolderStyleEntry entry in styledEntries)
        {
            validGuids.Add(entry.Guid);
        }

        selectedFolderGuids.RemoveWhere(guid => !validGuids.Contains(guid));
        loadedSelectionSignature = string.Empty;
        Repaint();
    }

    private void RefreshWindowData()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CustomFolderStyleDatabase.RefreshImportedIconCache();
        RefreshEntries();
        EditorApplication.RepaintProjectWindow();
    }

    private bool MatchesSearch(CustomFolderStyleEntry entry)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return true;
        }

        return entry.FolderName.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0
            || entry.FolderPath.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void SelectAllVisibleFolders()
    {
        foreach (CustomFolderStyleEntry entry in styledEntries)
        {
            if (MatchesSearch(entry))
            {
                selectedFolderGuids.Add(entry.Guid);
            }
        }

        loadedSelectionSignature = string.Empty;
    }

    private void ToggleIconSelection(string iconPath, int iconIndex, string[] iconPaths)
    {
        Event currentEvent = Event.current;
        bool extendSelection = currentEvent.control || currentEvent.command;
        bool rangeSelection = currentEvent.shift && lastSelectedIconIndex >= 0 && lastSelectedIconIndex < iconPaths.Length;
        if (rangeSelection)
        {
            if (!extendSelection)
            {
                selectedIconPaths.Clear();
            }

            int rangeStart = Mathf.Min(lastSelectedIconIndex, iconIndex);
            int rangeEnd = Mathf.Max(lastSelectedIconIndex, iconIndex);
            for (int i = rangeStart; i <= rangeEnd; i++)
            {
                selectedIconPaths.Add(iconPaths[i]);
            }

            SetSelectedIcon(iconPath);
        }
        else
        {
            if (!extendSelection)
            {
                selectedIconPaths.Clear();
                SetSelectedIcon(iconPath);
            }

            if (!selectedIconPaths.Add(iconPath) && extendSelection)
            {
                selectedIconPaths.Remove(iconPath);
            }
        }

        lastSelectedIconIndex = iconIndex;
        Repaint();
    }

    private void SelectAllImportedIcons(string[] iconPaths)
    {
        selectedIconPaths.Clear();
        foreach (string iconPath in iconPaths)
        {
            selectedIconPaths.Add(iconPath);
        }

        lastSelectedIconIndex = iconPaths.Length - 1;
        Repaint();
    }

    private void ClearImportedIconSelection()
    {
        selectedIconPaths.Clear();
        lastSelectedIconIndex = -1;
        Repaint();
    }

    private void SetSelectedIcon(string iconPath)
    {
        importedIconPath = iconPath ?? string.Empty;
        builtinIconName = string.Empty;
        selectedIcon = string.IsNullOrEmpty(importedIconPath)
            ? null
            : AssetDatabase.LoadAssetAtPath<Texture2D>(importedIconPath);
        applyIcon = selectedIcon != null;
    }

    private void ApplyStyle(string[] guids)
    {
        if (guids.Length == 0)
        {
            return;
        }

        CustomFolderStyleDatabase.instance.ApplyStyleToFolders(
            guids,
            applyColor,
            folderColor,
            applyBackgroundColor,
            backgroundColor,
            applyNameColor,
            nameColor,
            applyIcon,
            builtinIconName,
            importedIconPath,
            selectedIcon);
        EditorApplication.RepaintProjectWindow();
        RefreshEntries();
    }

    private void RenameSelectedFolder()
    {
        if (selectedFolderGuids.Count != 1)
        {
            return;
        }

        string guid = GetSelectedFolderGuids()[0];
        string folderPath = AssetDatabase.GUIDToAssetPath(guid);
        string newFolderName = folderNameInput.Trim();
        if (string.IsNullOrEmpty(folderPath))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), L("This folder could not be found."), L("OK"));
            return;
        }

        if (string.IsNullOrEmpty(newFolderName))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), L("Folder name cannot be empty."), L("OK"));
            return;
        }

        if (ContainsInvalidFileNameCharacter(newFolderName))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), L("Folder name contains invalid characters."), L("OK"));
            return;
        }

        if (newFolderName == GetFolderName(guid))
        {
            return;
        }

        string error = AssetDatabase.RenameAsset(folderPath, newFolderName);
        if (!string.IsNullOrEmpty(error))
        {
            EditorUtility.DisplayDialog(L("Rename Folder"), error, L("OK"));
            return;
        }

        AssetDatabase.SaveAssets();
        RefreshEntries();
        EditorApplication.RepaintProjectWindow();
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

        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            if (fileName.IndexOf(invalidCharacter) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearStyles(string[] guids, string message)
    {
        if (guids.Length == 0 || !EditorUtility.DisplayDialog(L("Custom Folder"), L(message), L("Clear"), L("Cancel")))
        {
            return;
        }

        CustomFolderStyleDatabase.instance.ClearMany(guids);
        selectedFolderGuids.RemoveWhere(guid => ContainsGuid(guids, guid));
        EditorApplication.RepaintProjectWindow();
        RefreshEntries();
    }

    private void ImportSingleIcon()
    {
        string sourcePath = EditorUtility.OpenFilePanelWithFilters(
            L("Import Folder Icon"),
            string.Empty,
            new[] { L("Image files"), "png,jpg,jpeg", L("All files"), "*" });
        if (string.IsNullOrEmpty(sourcePath))
        {
            return;
        }

        string assetPath = CustomFolderStyleDatabase.ImportIconIntoProject(sourcePath);
        if (!string.IsNullOrEmpty(assetPath))
        {
            SetSelectedIcon(assetPath);
            AssetDatabase.Refresh();
            Repaint();
        }
    }

    private void ImportIconFolder()
    {
        // Folder import provides a simple batch workflow because Unity's file
        // picker returns one file at a time.
        string folderPath = EditorUtility.OpenFolderPanel(L("Import Folder Icons"), string.Empty, string.Empty);
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            return;
        }

        string[] files = Directory.GetFiles(folderPath);
        int importedCount = 0;
        foreach (string file in files)
        {
            string extension = Path.GetExtension(file).ToLowerInvariant();
            if (extension != ".png" && extension != ".jpg" && extension != ".jpeg")
            {
                continue;
            }

            if (!string.IsNullOrEmpty(CustomFolderStyleDatabase.ImportIconIntoProject(file)))
            {
                importedCount++;
            }
        }

        AssetDatabase.Refresh();
        Repaint();
        EditorUtility.DisplayDialog(
            L("Import Folder Icons"),
            CustomFolderLocalization.Format("icon(s) imported.", importedCount),
            L("OK"));
    }

    private void DeleteSelectedIcons()
    {
        // The database also removes references from folder styles before the
        // managed icon assets are deleted.
        if (selectedIconPaths.Count == 0 || !EditorUtility.DisplayDialog(
            L("Delete Imported Icons"),
            L("Delete the selected imported icons?"),
            L("Delete"),
            L("Cancel")))
        {
            return;
        }

        string[] paths = new string[selectedIconPaths.Count];
        selectedIconPaths.CopyTo(paths);
        CustomFolderStyleDatabase.instance.DeleteImportedIcons(paths);
        selectedIconPaths.Clear();
        lastSelectedIconIndex = -1;
        if (ContainsAnyPath(paths, importedIconPath))
        {
            selectedIcon = null;
            importedIconPath = string.Empty;
            applyIcon = false;
        }

        RefreshEntries();
        EditorApplication.RepaintProjectWindow();
    }

    private string[] GetSelectedFolderGuids()
    {
        List<string> guids = new List<string>(selectedFolderGuids);
        guids.Sort(StringComparer.Ordinal);
        return guids.ToArray();
    }

    private string[] GetAllStyledFolderGuids()
    {
        string[] guids = new string[styledEntries.Length];
        for (int i = 0; i < styledEntries.Length; i++)
        {
            guids[i] = styledEntries[i].Guid;
        }

        return guids;
    }

    private string GetSelectionSignature()
    {
        return string.Join("|", GetSelectedFolderGuids());
    }

    private static bool ContainsGuid(string[] guids, string guid)
    {
        foreach (string currentGuid in guids)
        {
            if (currentGuid == guid)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAnyPath(string[] paths, string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        foreach (string currentPath in paths)
        {
            if (string.Equals(currentPath, path, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
