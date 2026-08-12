using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// File responsibility: stores one folder's colors, icon references, save point,
// and cached texture data in a Unity-serializable model.
[Serializable]
internal sealed class CustomFolderStyleEntry
{
    // Serialized values are the durable source of truth for each folder style.
    [SerializeField] private string guid;
    [SerializeField] private string folderName;
    [SerializeField] private string folderPath;
    [SerializeField] private bool hasColor;
    [SerializeField] private Color color = Color.white;
    [SerializeField] private bool showColor;
    [SerializeField] private bool hasBackgroundColor;
    [SerializeField] private bool backgroundColorStateSaved;
    [SerializeField] private Color backgroundColor = Color.white;
    [SerializeField] private bool showBackgroundColor;
    [SerializeField] private bool hasNameColor;
    [SerializeField] private Color nameColor = Color.white;
    [SerializeField] private bool showNameColor;
    [SerializeField] private bool displayStateSaved;
    [SerializeField] private string builtinIconName;
    [SerializeField] private string iconAssetPath;
    [SerializeField] private string iconFileName;
    [SerializeField] private string iconImageBase64;

    [NonSerialized] private Texture2D cachedIcon;
    [NonSerialized] private string cachedBuiltinIconName;
    [NonSerialized] private string cachedIconAssetPath;
    [NonSerialized] private string cachedIconImageBase64;
    [NonSerialized] private bool cachedIconIsTransient;

    public CustomFolderStyleEntry()
    {
    }

    public CustomFolderStyleEntry(string guid, string folderPath)
    {
        backgroundColorStateSaved = true;
        displayStateSaved = true;
        UpdateFolderSavePoint(guid, folderPath);
    }

    public string Guid => guid;
    public string FolderName => folderName;
    public string FolderPath => folderPath;
    public bool HasColor => hasColor;
    public bool ShowsColor => hasColor && showColor;
    public Color Color => color;
    public bool HasBackgroundColor => hasBackgroundColor;
    public bool ShowsBackgroundColor => hasBackgroundColor && showBackgroundColor;
    public Color BackgroundColor => backgroundColor;
    public bool HasNameColor => hasNameColor;
    public bool ShowsNameColor => hasNameColor && showNameColor;
    public Color NameColor => nameColor;
    public string BuiltinIconName => builtinIconName;
    public string IconAssetPath => iconAssetPath;
    public bool HasIcon => !string.IsNullOrEmpty(builtinIconName) || !string.IsNullOrEmpty(iconAssetPath) || !string.IsNullOrEmpty(iconImageBase64);
    public bool HasAnyStyle => HasColor || HasBackgroundColor || HasNameColor || HasIcon;

    public void SetColor(Color newColor)
    {
        hasColor = true;
        showColor = true;
        color = newColor;
    }

    public void ClearColor()
    {
        hasColor = false;
        showColor = false;
        color = Color.white;
    }

    public void SetBackgroundColor(Color newColor)
    {
        backgroundColorStateSaved = true;
        hasBackgroundColor = true;
        showBackgroundColor = true;
        backgroundColor = newColor;
    }

    public void ClearBackgroundColor()
    {
        backgroundColorStateSaved = true;
        hasBackgroundColor = false;
        showBackgroundColor = false;
        backgroundColor = Color.white;
    }

    public void SetNameColor(Color newColor)
    {
        hasNameColor = true;
        showNameColor = true;
        nameColor = newColor;
    }

    public void ClearNameColor()
    {
        hasNameColor = false;
        showNameColor = false;
        nameColor = Color.white;
    }

    public void SetColorVisible(bool visible)
    {
        showColor = hasColor && visible;
    }

    public void SetBackgroundColorVisible(bool visible)
    {
        showBackgroundColor = hasBackgroundColor && visible;
    }

    public void SetNameColorVisible(bool visible)
    {
        showNameColor = hasNameColor && visible;
    }

    public bool MigrateDisplayVisibility()
    {
        if (displayStateSaved)
        {
            return false;
        }

        // Old files had no visibility flags, so preserve every existing style.
        showColor = hasColor;
        showBackgroundColor = hasBackgroundColor;
        showNameColor = hasNameColor;
        displayStateSaved = true;
        return true;
    }

    public bool MigrateLegacyBackgroundColor()
    {
        if (backgroundColorStateSaved)
        {
            return false;
        }

        backgroundColorStateSaved = true;
        if (!hasColor)
        {
            return true;
        }

        hasBackgroundColor = true;
        backgroundColor = color;
        return true;
    }

    public void SetIcon(Texture2D icon)
    {
        if (icon == null)
        {
            DestroyCachedTransientIcon();
            iconAssetPath = string.Empty;
            iconFileName = string.Empty;
            iconImageBase64 = string.Empty;
            builtinIconName = string.Empty;
            cachedIcon = null;
            cachedBuiltinIconName = string.Empty;
            cachedIconAssetPath = string.Empty;
            cachedIconImageBase64 = string.Empty;
            return;
        }

        iconAssetPath = AssetDatabase.GetAssetPath(icon);
        iconFileName = string.IsNullOrEmpty(iconAssetPath) ? icon.name : Path.GetFileName(iconAssetPath);

        builtinIconName = string.Empty;
        iconImageBase64 = string.Empty;
        DestroyCachedTransientIcon();
        cachedIcon = icon;
        cachedIconIsTransient = false;
        cachedBuiltinIconName = string.Empty;
        cachedIconAssetPath = iconAssetPath;
        cachedIconImageBase64 = string.Empty;
    }

    public void SetIconFromProjectFile(string projectIconAssetPath, Texture2D previewIcon)
    {
        if (string.IsNullOrEmpty(projectIconAssetPath))
        {
            SetIcon(null);
            return;
        }

        iconAssetPath = projectIconAssetPath;
        iconFileName = Path.GetFileName(projectIconAssetPath);
        builtinIconName = string.Empty;
        iconImageBase64 = string.Empty;
        DestroyCachedTransientIcon();
        cachedIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(projectIconAssetPath);
        if (cachedIcon == null)
        {
            cachedIcon = previewIcon;
        }

        cachedIconAssetPath = projectIconAssetPath;
        cachedIconImageBase64 = string.Empty;
        cachedIconIsTransient = false;
    }

    public void SetBuiltinIcon(string iconName)
    {
        if (string.IsNullOrEmpty(iconName))
        {
            SetIcon(null);
            return;
        }

        builtinIconName = iconName;
        iconAssetPath = string.Empty;
        iconFileName = iconName;
        iconImageBase64 = string.Empty;
        DestroyCachedTransientIcon();
        cachedIcon = LoadBuiltinIcon(iconName);
        cachedBuiltinIconName = iconName;
        cachedIconAssetPath = string.Empty;
        cachedIconImageBase64 = string.Empty;
        cachedIconIsTransient = false;
    }

    public void ClearIcon()
    {
        SetIcon(null);
    }

    public Texture2D LoadIcon()
    {
        // Resolve built-in icons, managed project icons, and legacy embedded data
        // in that order while keeping transient textures out of the asset database.
        if (!string.IsNullOrEmpty(builtinIconName))
        {
            if (cachedIcon != null && cachedBuiltinIconName == builtinIconName)
            {
                return cachedIcon;
            }

            cachedIcon = LoadBuiltinIcon(builtinIconName);
            cachedBuiltinIconName = builtinIconName;
            cachedIconAssetPath = string.Empty;
            cachedIconImageBase64 = string.Empty;
            if (cachedIcon != null)
            {
                return cachedIcon;
            }
        }

        if (!string.IsNullOrEmpty(iconAssetPath))
        {
            if (cachedIcon != null && cachedIconAssetPath == iconAssetPath)
            {
                return cachedIcon;
            }

            Texture2D projectIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(iconAssetPath);
            if (projectIcon != null)
            {
                DestroyCachedTransientIcon();
                cachedIcon = projectIcon;
                cachedIconAssetPath = iconAssetPath;
                cachedIconImageBase64 = string.Empty;
                cachedIconIsTransient = false;
                return cachedIcon;
            }
        }

        if (string.IsNullOrEmpty(iconImageBase64))
        {
            cachedIcon = null;
            cachedIconAssetPath = string.Empty;
            cachedIconImageBase64 = string.Empty;
            return null;
        }

        if (cachedIcon != null && cachedIconImageBase64 == iconImageBase64)
        {
            return cachedIcon;
        }

        try
        {
            byte[] imageBytes = Convert.FromBase64String(iconImageBase64);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            if (texture.LoadImage(imageBytes))
            {
                DestroyCachedTransientIcon();
                cachedIcon = texture;
                cachedIconAssetPath = string.Empty;
                cachedIconImageBase64 = iconImageBase64;
                cachedIconIsTransient = true;
                return cachedIcon;
            }

            UnityEngine.Object.DestroyImmediate(texture);
        }
        catch (Exception)
        {
            return null;
        }

        return null;
    }

    private void DestroyCachedTransientIcon()
    {
        if (cachedIconIsTransient && cachedIcon != null)
        {
            UnityEngine.Object.DestroyImmediate(cachedIcon);
        }

        cachedIcon = null;
        cachedIconIsTransient = false;
    }

    private static Texture2D LoadBuiltinIcon(string iconName)
    {
        GUIContent content = EditorGUIUtility.IconContent(iconName);
        return content == null ? null : content.image as Texture2D;
    }

    public bool UpdateFolderSavePoint(string newGuid, string newFolderPath)
    {
        // Save the latest folder path/name so renamed or moved folders can be
        // repaired without changing the style record's identity.
        string newFolderName = string.IsNullOrEmpty(newFolderPath) ? string.Empty : Path.GetFileName(newFolderPath);
        bool changed = guid != newGuid || folderPath != newFolderPath || folderName != newFolderName;
        guid = newGuid;
        folderPath = newFolderPath;
        folderName = newFolderName;
        return changed;
    }
}
