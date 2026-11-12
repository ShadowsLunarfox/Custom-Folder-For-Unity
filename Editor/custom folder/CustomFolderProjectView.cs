using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// File responsibility: draws custom folder overlays in the Project window and
// routes folder clicks, Alt shortcuts, menus, and popup positioning.
[InitializeOnLoad]
internal static class CustomFolderProjectView
{
    // Draws custom folder visuals and bridges Project window input to the popup.
    private const string MenuRoot = "Assets/Custom Folder";
    private const double AltOpenCooldown = 0.35d;
    private const float GridBackgroundHorizontalPadding = 8f;
    private static readonly Texture2D FolderIcon = EditorGUIUtility.IconContent("Folder Icon").image as Texture2D;
    private static GUIStyle nameIconLabelStyle;
    private static GUIStyle nameListLabelStyle;
    private static GUIStyle listSelectionStyle;
    private static GUIStyle gridSelectionStyle;
    private static double lastAltOpenTime;
    private static bool pendingPopupOpen;
    private static string[] pendingPopupFolderGuids;
    private static string lastInteractedFolderGuid;
    private static Rect lastInteractedFolderIconScreenRect;
    private static bool hasInteractedFolderAnchor;
    private static string rightClickedFolderGuid;
    private static string rightClickRenameOriginalPath;
    private static double rightClickTime;
    private static bool rightClickRenameCommandReceived;
    private static bool rightClickRenameActive;
    private static readonly HashSet<string> immediateSelectedGuids = new HashSet<string>(StringComparer.Ordinal);
    private static bool hasImmediateSelectionHint;

    static CustomFolderProjectView()
    {
        // Unity invokes this callback for every visible Project window item.
        EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
        Selection.selectionChanged += RepaintAfterSelectionChanged;
    }

    private static void RepaintAfterSelectionChanged()
    {
        immediateSelectedGuids.Clear();
        foreach (string selectedGuid in Selection.assetGUIDs)
        {
            immediateSelectedGuids.Add(selectedGuid);
        }

        string activeGuid = GetActiveSelectionGuid();
        if (!string.IsNullOrEmpty(activeGuid))
        {
            immediateSelectedGuids.Add(activeGuid);
        }

        hasImmediateSelectionHint = false;
        EditorApplication.RepaintProjectWindow();
    }

    private static string GetActiveSelectionGuid()
    {
        UnityEngine.Object activeObject = Selection.activeObject;
        if (activeObject == null)
        {
            return null;
        }

        string assetPath = AssetDatabase.GetAssetPath(activeObject);
        return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.AssetPathToGUID(assetPath);
    }

    [MenuItem(MenuRoot + "/Edit Style...", true)]
    [MenuItem(MenuRoot + "/Clear Style", true)]
    [MenuItem(MenuRoot + "/Set Color/Red", true)]
    [MenuItem(MenuRoot + "/Set Color/Orange", true)]
    [MenuItem(MenuRoot + "/Set Color/Yellow", true)]
    [MenuItem(MenuRoot + "/Set Color/Green", true)]
    [MenuItem(MenuRoot + "/Set Color/Cyan", true)]
    [MenuItem(MenuRoot + "/Set Color/Blue", true)]
    [MenuItem(MenuRoot + "/Set Color/Purple", true)]
    [MenuItem(MenuRoot + "/Set Color/Pink", true)]
    [MenuItem(MenuRoot + "/Set Color/Gray", true)]
    private static bool ValidateFolderSelection()
    {
        return CustomFolderSelection.GetSelectedFolderGuids().Length > 0;
    }

    [MenuItem(MenuRoot + "/Edit Style...")]
    private static void OpenStyleWindow()
    {
        RequestPopupOpenOnNextRepaint();
    }

    public static void RequestPopupOpenOnNextRepaint()
    {
        RequestPopupOpenOnNextRepaint(CustomFolderSelection.GetSelectedFolderGuids());
    }

    public static void RequestPopupOpenOnNextRepaint(string[] folderGuids)
    {
        pendingPopupOpen = true;
        pendingPopupFolderGuids = folderGuids == null ? null : (string[])folderGuids.Clone();
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem(MenuRoot + "/Clear Style")]
    private static void ClearSelectedStyles()
    {
        CustomFolderStyleDatabase.instance.ClearMany(CustomFolderSelection.GetSelectedFolderGuids());

        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem(MenuRoot + "/Set Color/Red")]
    private static void SetRed()
    {
        SetSelectedColor(new Color(0.93f, 0.18f, 0.22f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Orange")]
    private static void SetOrange()
    {
        SetSelectedColor(new Color(1.00f, 0.50f, 0.12f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Yellow")]
    private static void SetYellow()
    {
        SetSelectedColor(new Color(1.00f, 0.82f, 0.18f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Green")]
    private static void SetGreen()
    {
        SetSelectedColor(new Color(0.22f, 0.72f, 0.35f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Cyan")]
    private static void SetCyan()
    {
        SetSelectedColor(new Color(0.10f, 0.72f, 0.82f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Blue")]
    private static void SetBlue()
    {
        SetSelectedColor(new Color(0.20f, 0.48f, 0.95f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Purple")]
    private static void SetPurple()
    {
        SetSelectedColor(new Color(0.58f, 0.35f, 0.92f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Pink")]
    private static void SetPink()
    {
        SetSelectedColor(new Color(0.94f, 0.30f, 0.66f, 1f));
    }

    [MenuItem(MenuRoot + "/Set Color/Gray")]
    private static void SetGray()
    {
        SetSelectedColor(new Color(0.55f, 0.58f, 0.62f, 1f));
    }

    private const string FolderColorDisplayMenu = "Tools/Custom Folder/Display/Folder Color";
    private const string BackgroundColorDisplayMenu = "Tools/Custom Folder/Display/Background Color";
    private const string NameColorDisplayMenu = "Tools/Custom Folder/Display/Name Color";

    [MenuItem(FolderColorDisplayMenu, true)]
    [MenuItem(BackgroundColorDisplayMenu, true)]
    [MenuItem(NameColorDisplayMenu, true)]
    private static bool ValidateDisplayToggle()
    {
        CustomFolderStyleDatabase database = CustomFolderStyleDatabase.instance;
        Menu.SetChecked(FolderColorDisplayMenu, database.ShowFolderColor);
        Menu.SetChecked(BackgroundColorDisplayMenu, database.ShowBackgroundColor);
        Menu.SetChecked(NameColorDisplayMenu, database.ShowNameColor);
        return true;
    }

    [MenuItem(FolderColorDisplayMenu)]
    private static void ToggleFolderColorDisplay()
    {
        CustomFolderStyleDatabase.instance.ToggleGlobalFolderColorDisplay();
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem(BackgroundColorDisplayMenu)]
    private static void ToggleBackgroundColorDisplay()
    {
        CustomFolderStyleDatabase.instance.ToggleGlobalBackgroundColorDisplay();
        EditorApplication.RepaintProjectWindow();
    }

    [MenuItem(NameColorDisplayMenu)]
    private static void ToggleNameColorDisplay()
    {
        CustomFolderStyleDatabase.instance.ToggleGlobalNameColorDisplay();
        EditorApplication.RepaintProjectWindow();
    }

    private static void SetSelectedColor(Color color)
    {
        CustomFolderStyleDatabase.instance.SetColorAndBackgroundColor(CustomFolderSelection.GetSelectedFolderGuids(), color);

        EditorApplication.RepaintProjectWindow();
    }

    private static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
    {
        // Input is handled before repaint drawing so clicks and Alt shortcuts
        // remain responsive even while Unity is updating its native highlight.
        TrackRightClickRename(guid, selectionRect);
        HandleImmediateSelectionHint(guid, selectionRect);
        HandlePendingPopup(guid, selectionRect);
        HandleAltShortcut(guid, selectionRect);

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        // Let Unity's native rename field remain visible while the user edits a
        // folder name. The custom overlay returns on the next repaint after OK
        // or Cancel closes the native text field.
        if (IsFolderBeingRenamed(guid))
        {
            return;
        }

        if (!CustomFolderSelection.IsFolderGuid(guid))
        {
            return;
        }

        bool iconMode = selectionRect.height > EditorGUIUtility.singleLineHeight + 4f;
        bool isSelected = hasImmediateSelectionHint
            ? immediateSelectedGuids.Contains(guid)
            : CustomFolderSelection.IsSelectedFolderGuid(guid);
        CustomFolderStyleEntry style = CustomFolderStyleDatabase.instance.Get(guid);
        if (style == null)
        {
            return;
        }

        CustomFolderStyleDatabase database = CustomFolderStyleDatabase.instance;
        bool showFolderColor = database.ShowFolderColor && style.HasColor;
        bool showBackgroundColor = database.ShowBackgroundColor && style.HasBackgroundColor;
        bool showNameColor = database.ShowNameColor && style.HasNameColor;
        bool hasFocus = EditorWindow.focusedWindow != null
            && EditorWindow.focusedWindow.GetType().FullName == "UnityEditor.ProjectBrowser";

        if (showBackgroundColor)
        {
            DrawBackground(selectionRect, style.BackgroundColor, iconMode);
        }

        Texture2D icon = CustomFolderStyleDatabase.LoadIcon(style);
        bool hasCustomIcon = icon != null || (showFolderColor && FolderIcon != null);
        if (hasCustomIcon)
        {
            DrawIconCover(selectionRect, iconMode, showBackgroundColor, style.BackgroundColor, isSelected, hasFocus);
        }

        if (icon != null)
        {
            DrawIcon(selectionRect, icon, Color.white, iconMode, isSelected);
        }
        else if (showFolderColor && FolderIcon != null)
        {
            Color iconTint = style.Color;
            iconTint.a = 1f;
            DrawIcon(selectionRect, FolderIcon, iconTint, iconMode, isSelected);
        }

        if (showBackgroundColor)
        {
            DrawBackgroundTopStrip(selectionRect, style.BackgroundColor, iconMode);
        }

        if (showNameColor)
        {
            DrawName(selectionRect, guid, style.NameColor, iconMode, showBackgroundColor, style.BackgroundColor, isSelected, hasFocus);
        }
    }

    private static bool IsFolderBeingRenamed(string guid)
    {
        if (string.IsNullOrEmpty(rightClickedFolderGuid) || rightClickedFolderGuid != guid)
        {
            return false;
        }

        if (!rightClickRenameActive && !rightClickRenameCommandReceived && !EditorGUIUtility.editingTextField)
        {
            return false;
        }

        if (!rightClickRenameActive)
        {
            rightClickRenameActive = true;
        }

        if (!string.Equals(AssetDatabase.GUIDToAssetPath(guid), rightClickRenameOriginalPath, StringComparison.Ordinal))
        {
            ClearRightClickRenameState();
            return false;
        }

        return true;
    }

    private static void TrackRightClickRename(string guid, Rect selectionRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null)
        {
            return;
        }

        bool isRightClick = (currentEvent.type == EventType.MouseDown && currentEvent.button == 1)
            || currentEvent.type == EventType.ContextClick;
        if (isRightClick && selectionRect.Contains(currentEvent.mousePosition) && CustomFolderSelection.IsFolderGuid(guid))
        {
            rightClickedFolderGuid = guid;
            rightClickRenameOriginalPath = AssetDatabase.GUIDToAssetPath(guid);
            rightClickTime = EditorApplication.timeSinceStartup;
            rightClickRenameCommandReceived = false;
            rightClickRenameActive = false;
            return;
        }

        if (currentEvent.type == EventType.ExecuteCommand
            && currentEvent.commandName == "Rename"
            && guid == rightClickedFolderGuid)
        {
            rightClickRenameCommandReceived = true;
            rightClickRenameActive = true;
        }

        if (rightClickRenameActive && guid == rightClickedFolderGuid)
        {
            if (currentEvent.type == EventType.KeyDown
                && (currentEvent.keyCode == KeyCode.Return
                    || currentEvent.keyCode == KeyCode.KeypadEnter
                    || currentEvent.keyCode == KeyCode.Escape))
            {
                ClearRightClickRenameState();
                return;
            }

            if (currentEvent.type == EventType.MouseDown
                && currentEvent.button == 0
                && !selectionRect.Contains(currentEvent.mousePosition))
            {
                ClearRightClickRenameState();
                return;
            }
        }

        if (currentEvent.type == EventType.Repaint
            && !rightClickRenameActive
            && !rightClickRenameCommandReceived
            && EditorApplication.timeSinceStartup - rightClickTime > 2d)
        {
            ClearRightClickRenameState();
        }
    }

    private static void ClearRightClickRenameState()
    {
        rightClickedFolderGuid = null;
        rightClickRenameOriginalPath = null;
        rightClickRenameCommandReceived = false;
        rightClickRenameActive = false;
    }

    private static void HandleImmediateSelectionHint(string guid, Rect selectionRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.type != EventType.MouseDown || currentEvent.button != 0)
        {
            return;
        }

        if (currentEvent.alt || !selectionRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        RememberFolderAnchor(guid, selectionRect);

        bool usesActionKey = currentEvent.control || currentEvent.command;
        bool extendsSelection = usesActionKey || currentEvent.shift;
        if (!extendsSelection)
        {
            immediateSelectedGuids.Clear();
        }

        if (extendsSelection && usesActionKey && immediateSelectedGuids.Contains(guid))
        {
            immediateSelectedGuids.Remove(guid);
        }
        else
        {
            immediateSelectedGuids.Add(guid);
        }

        hasImmediateSelectionHint = true;
    }

    private static void DrawBackground(Rect selectionRect, Color color, bool iconMode)
    {
        Color tint = color;
        tint.a = EditorGUIUtility.isProSkin ? 0.22f : 0.16f;

        Rect backgroundRect = GetBackgroundRect(selectionRect, iconMode);

        EditorGUI.DrawRect(backgroundRect, tint);
    }

    private static void DrawBackgroundTopStrip(Rect selectionRect, Color color, bool iconMode)
    {
        Color strip = color;
        strip.a = EditorGUIUtility.isProSkin ? 0.78f : 0.70f;
        Rect backgroundRect = GetBackgroundRect(selectionRect, iconMode);
        Rect stripRect = iconMode
            ? new Rect(backgroundRect.x, backgroundRect.y, backgroundRect.width, 3f)
            : new Rect(selectionRect.x - 2f, selectionRect.y, 3f, selectionRect.height);
        EditorGUI.DrawRect(stripRect, strip);
    }

    private static Rect GetBackgroundRect(Rect selectionRect, bool iconMode)
    {
        if (!iconMode)
        {
            return new Rect(selectionRect.x - 2f, selectionRect.y, selectionRect.width + 4f, selectionRect.height);
        }

        return new Rect(
            selectionRect.x - GridBackgroundHorizontalPadding,
            selectionRect.y + 1f,
            selectionRect.width + GridBackgroundHorizontalPadding * 2f,
            Mathf.Max(18f, selectionRect.height + 10f));
    }

    private static void DrawIcon(Rect selectionRect, Texture2D icon, Color tint, bool iconMode, bool isSelected)
    {
        if (iconMode && isSelected)
        {
            // Unity tints selected grid icons; selection backgrounds belong to
            // their labels, not to a rectangular block behind the image.
            tint *= new Color(0.85f, 0.9f, 1f, 1f);
        }

        GUI.DrawTexture(GetIconRect(selectionRect, iconMode), icon, ScaleMode.ScaleToFit, true, 0f, tint, 0f, 0f);
    }

    private static void DrawIconCover(Rect selectionRect, bool iconMode, bool hasBackgroundColor, Color backgroundColor, bool isSelected, bool hasFocus)
    {
        Rect iconRect = GetIconRect(selectionRect, iconMode);
        DrawCoverBackground(iconRect, hasBackgroundColor, backgroundColor, isSelected && !iconMode, iconMode, hasFocus);
    }

    private static void DrawCoverBackground(Rect rect, bool hasBackgroundColor, Color backgroundColor, bool isSelected, bool iconMode, bool hasFocus)
    {
        // Selection styles can be translucent. Always erase the native icon or
        // text first so it cannot show through transparent replacement pixels.
        EditorGUI.DrawRect(rect, GetProjectViewBackgroundColor());

        if (isSelected)
        {
            GUIStyle selectionStyle = iconMode ? GetGridSelectionStyle() : GetListSelectionStyle();
            selectionStyle.Draw(rect, GUIContent.none, false, false, true, hasFocus);
            return;
        }

        if (hasBackgroundColor)
        {
            Color tint = backgroundColor;
            tint.a = EditorGUIUtility.isProSkin ? 0.22f : 0.16f;
            EditorGUI.DrawRect(rect, tint);
        }
    }

    private static Rect GetIconRect(Rect selectionRect, bool iconMode)
    {
        // Match Unity's list/grid icon bounds so covers and popup anchors use the
        // same visual rectangle as the folder icon itself.
        if (iconMode)
        {
            // ObjectListArea reserves only the native grid label height. An
            // inset cover leaves the original folder visible around its edges.
            return new Rect(
                selectionRect.x,
                selectionRect.y,
                selectionRect.width,
                Mathf.Max(1f, selectionRect.height - GetNameLabelStyle(true).fixedHeight));
        }

        return new Rect(selectionRect.x + GetNameLabelStyle(false).margin.left, selectionRect.y, 16f, selectionRect.height);
    }

    private static void DrawName(Rect selectionRect, string guid, Color color, bool iconMode, bool hasBackgroundColor, Color backgroundColor, bool isSelected, bool hasFocus)
    {
        string folderPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(folderPath))
        {
            return;
        }

        GUIStyle labelStyle = GetNameLabelStyle(iconMode);
        labelStyle.normal.textColor = color;

        string labelText = Path.GetFileName(folderPath);
        GUIContent labelContent = new GUIContent(labelText);
        Rect labelRect = GetNameRect(selectionRect, labelStyle, labelContent, iconMode);
        Rect coverRect = GetNameCoverRect(selectionRect, labelRect, labelStyle, labelContent, iconMode);
        DrawCoverBackground(coverRect, hasBackgroundColor, backgroundColor, isSelected, iconMode, hasFocus);

        TextClipping oldClipping = labelStyle.clipping;
        TextAnchor oldAlignment = labelStyle.alignment;
#if UNITY_6000_0_OR_NEWER
        labelStyle.clipping = iconMode ? TextClipping.Ellipsis : TextClipping.Clip;
#else
        // Older editors crop the string before drawing; they do not expose
        // TextClipping.Ellipsis.
        labelStyle.clipping = TextClipping.Clip;
        if (iconMode)
        {
            labelContent.text = GetEllipsizedLabel(labelText, labelStyle, labelRect.width - labelStyle.padding.horizontal);
        }
#endif
        if (iconMode)
        {
            labelStyle.alignment = TextAnchor.MiddleCenter;
        }

        labelStyle.Draw(labelRect, labelContent, false, false, false, false);
        labelStyle.clipping = oldClipping;
        labelStyle.alignment = oldAlignment;
    }

#if !UNITY_6000_0_OR_NEWER
    private static string GetEllipsizedLabel(string text, GUIStyle style, float width)
    {
        GUIContent content = new GUIContent(text);
        if (style.CalcSize(content).x <= width)
        {
            return text;
        }

        int low = 0;
        int high = text.Length;
        while (low < high)
        {
            int length = (low + high + 1) / 2;
            content.text = text.Substring(0, length) + "\u2026";
            if (style.CalcSize(content).x <= width)
            {
                low = length;
            }
            else
            {
                high = length - 1;
            }
        }

        if (low > 0 && char.IsHighSurrogate(text[low - 1]))
        {
            low--;
        }

        return text.Substring(0, low) + "\u2026";
    }
#endif

    private static Rect GetNameRect(Rect selectionRect, GUIStyle labelStyle, GUIContent labelContent, bool iconMode)
    {
        if (iconMode)
        {
            // Match Unity's constrained measurement, including its label
            // padding, so long names erase and redraw the same ellipsis area.
            Vector2 size = labelStyle.CalcSize(labelContent);
            size.x = Mathf.Min(size.x, selectionRect.width);
            size.y = Mathf.Min(size.y, selectionRect.height);
            size.x += labelStyle.padding.horizontal;

            return new Rect(
                selectionRect.x + (selectionRect.width - size.x) * 0.5f,
                selectionRect.yMax + 1f - labelStyle.fixedHeight,
                size.x,
                size.y);
        }

        return new Rect(
            selectionRect.x + labelStyle.margin.left,
            selectionRect.y,
            Mathf.Max(1f, selectionRect.width - labelStyle.margin.left),
            selectionRect.height);
    }

    private static Rect GetNameCoverRect(Rect selectionRect, Rect labelRect, GUIStyle labelStyle, GUIContent labelContent, bool iconMode)
    {
        if (iconMode)
        {
            return labelRect;
        }

        Vector2 textSize = labelStyle.CalcSize(labelContent);
        float textWidth = textSize.x + labelStyle.padding.horizontal;
        float textStart = labelRect.x + labelStyle.padding.left;

        return new Rect(
            textStart - 1f,
            labelRect.y,
            Mathf.Min(Mathf.Max(1f, textWidth + 2f), Mathf.Max(1f, labelRect.width - labelStyle.padding.left + 2f)),
            labelRect.height);
    }

    private static GUIStyle GetListSelectionStyle()
    {
        if (listSelectionStyle == null)
        {
            listSelectionStyle = new GUIStyle("OL ResultLabel");
        }

        return listSelectionStyle;
    }

    private static GUIStyle GetGridSelectionStyle()
    {
        if (gridSelectionStyle == null)
        {
            gridSelectionStyle = new GUIStyle("ProjectBrowserGridLabel");
        }

        return gridSelectionStyle;
    }

    private static GUIStyle GetNameLabelStyle(bool iconMode)
    {
        if (iconMode)
        {
            if (nameIconLabelStyle == null)
            {
                // This is the same style ObjectListArea uses for Project Browser grid names.
                nameIconLabelStyle = new GUIStyle("ProjectBrowserGridLabel");
            }

            return nameIconLabelStyle;
        }

        if (nameListLabelStyle == null)
        {
            // This is the same style used by ObjectListArea.DrawIconAndLabel.
            nameListLabelStyle = new GUIStyle("OL ResultLabel");
            nameListLabelStyle.padding.left = 18;
        }

        return nameListLabelStyle;
    }

    private static Color GetProjectViewBackgroundColor()
    {
        return EditorGUIUtility.isProSkin
            ? new Color(0.22f, 0.22f, 0.22f, 1f)
            : new Color(0.76f, 0.76f, 0.76f, 1f);
    }

    private static void HandlePendingPopup(string guid, Rect selectionRect)
    {
        // A delayed menu command can be repainted by more than one Project view;
        // prefer the screen anchor captured from the user's actual click.
        if (!pendingPopupOpen || Event.current.type != EventType.Repaint)
        {
            return;
        }

        if (!IsPendingPopupTargetGuid(guid))
        {
            return;
        }

        if (HasPendingInteractedFolderAnchor() && guid != lastInteractedFolderGuid)
        {
            return;
        }

        string[] popupFolderGuids = pendingPopupFolderGuids == null ? null : (string[])pendingPopupFolderGuids.Clone();
        bool useScreenAnchor = HasPendingInteractedFolderAnchor();
        Rect activatorRect = useScreenAnchor
            ? lastInteractedFolderIconScreenRect
            : GetFolderIconRect(selectionRect);
        pendingPopupOpen = false;
        pendingPopupFolderGuids = null;
        hasInteractedFolderAnchor = false;
        if (useScreenAnchor)
        {
            CustomFolderStylePopup.OpenForScreenSelection(activatorRect, popupFolderGuids);
        }
        else
        {
            CustomFolderStylePopup.OpenForSelection(activatorRect, popupFolderGuids);
        }
    }

    private static bool IsPendingPopupTargetGuid(string guid)
    {
        if (!CustomFolderSelection.IsFolderGuid(guid))
        {
            return false;
        }

        if (pendingPopupFolderGuids == null || pendingPopupFolderGuids.Length == 0)
        {
            return CustomFolderSelection.IsSelectedFolderGuid(guid);
        }

        foreach (string pendingGuid in pendingPopupFolderGuids)
        {
            if (pendingGuid == guid)
            {
                return true;
            }
        }

        return false;
    }

    private static void HandleAltShortcut(string guid, Rect selectionRect)
    {
        Event currentEvent = Event.current;
        if (currentEvent == null || currentEvent.type != EventType.KeyDown)
        {
            return;
        }

        if (currentEvent.keyCode != KeyCode.LeftAlt && currentEvent.keyCode != KeyCode.RightAlt)
        {
            return;
        }

        if (!selectionRect.Contains(currentEvent.mousePosition))
        {
            return;
        }

        if (!CustomFolderSelection.IsSelectedFolderGuid(guid))
        {
            return;
        }

        double now = EditorApplication.timeSinceStartup;
        if (now - lastAltOpenTime < AltOpenCooldown)
        {
            return;
        }

        lastAltOpenTime = now;
        RememberFolderAnchor(guid, selectionRect);
        CustomFolderStylePopup.OpenForScreenSelection(
            lastInteractedFolderIconScreenRect,
            CustomFolderSelection.GetSelectedFolderGuids());
        currentEvent.Use();
    }

    private static void RememberFolderAnchor(string guid, Rect selectionRect)
    {
        if (!CustomFolderSelection.IsFolderGuid(guid))
        {
            return;
        }

        lastInteractedFolderGuid = guid;
        lastInteractedFolderIconScreenRect = GUIUtility.GUIToScreenRect(GetFolderIconRect(selectionRect));
        hasInteractedFolderAnchor = true;
    }

    private static bool HasPendingInteractedFolderAnchor()
    {
        if (!hasInteractedFolderAnchor || string.IsNullOrEmpty(lastInteractedFolderGuid))
        {
            return false;
        }

        if (pendingPopupFolderGuids == null || pendingPopupFolderGuids.Length == 0)
        {
            return CustomFolderSelection.IsSelectedFolderGuid(lastInteractedFolderGuid);
        }

        foreach (string pendingGuid in pendingPopupFolderGuids)
        {
            if (pendingGuid == lastInteractedFolderGuid)
            {
                return true;
            }
        }

        return false;
    }

    private static Rect GetFolderIconRect(Rect selectionRect)
    {
        bool iconMode = selectionRect.height > EditorGUIUtility.singleLineHeight + 4f;
        return GetIconRect(selectionRect, iconMode);
    }
}
