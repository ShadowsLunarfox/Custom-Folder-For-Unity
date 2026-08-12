using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// File responsibility: loads and saves the JSON style database, manages icon
// assets, and applies style or global display changes to folders.
internal sealed class CustomFolderStyleDatabase
{
    private const string DatabaseFilePath = "Assets/Editor/custom folder/CustomFolderStyles.json";
    private const string IconFolderAssetPath = "Assets/Editor/custom folder/icon";
    private const int ImportedIconSize = 64;
    private static CustomFolderStyleDatabase cachedInstance;
    private static string[] cachedImportedIconAssetPaths = Array.Empty<string>();
    private static bool importedIconPathsDirty = true;

    [SerializeField] private List<CustomFolderStyleEntry> entries = new List<CustomFolderStyleEntry>();
    [SerializeField] private bool displayStateSaved;
    [SerializeField] private bool showFolderColor = true;
    [SerializeField] private bool showBackgroundColor = true;
    [SerializeField] private bool showNameColor = true;
    [NonSerialized] private Dictionary<string, CustomFolderStyleEntry> entriesByGuid;
    [NonSerialized] private bool savePointPersistenceScheduled;

    public static CustomFolderStyleDatabase instance
    {
        get
        {
            if (cachedInstance == null)
            {
                cachedInstance = LoadOrCreate();
            }

            return cachedInstance;
        }
    }

    public CustomFolderStyleEntry Get(string guid)
    {
        EnsureEntries();
        CustomFolderStyleEntry entry = FindByGuid(guid);
        if (entry == null || !entry.HasAnyStyle)
        {
            return null;
        }

        if (RefreshSavePoint(entry, guid))
        {
            ScheduleSavePointPersistence();
        }

        return entry;
    }

    public bool ShowFolderColor => showFolderColor;
    public bool ShowBackgroundColor => showBackgroundColor;
    public bool ShowNameColor => showNameColor;

    public CustomFolderStyleEntry[] GetStyledEntries()
    {
        // Return only live folder styles so the manager does not expose deleted
        // or moved assets while keeping the saved folder reference up to date.
        EnsureEntries();
        List<CustomFolderStyleEntry> styledEntries = new List<CustomFolderStyleEntry>();
        bool changed = false;

        foreach (CustomFolderStyleEntry entry in entries)
        {
            if (entry == null || !entry.HasAnyStyle || string.IsNullOrEmpty(entry.Guid))
            {
                continue;
            }

            string folderPath = AssetDatabase.GUIDToAssetPath(entry.Guid);
            if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
            {
                continue;
            }

            if (entry.UpdateFolderSavePoint(entry.Guid, folderPath))
            {
                changed = true;
            }

            styledEntries.Add(entry);
        }

        if (changed)
        {
            ScheduleSavePointPersistence();
        }

        styledEntries.Sort((left, right) => string.Compare(left.FolderPath, right.FolderPath, StringComparison.OrdinalIgnoreCase));
        return styledEntries.ToArray();
    }

    public void SetColor(string guid, Color color)
    {
        CustomFolderStyleEntry entry = GetOrCreate(guid);
        entry.SetColor(color);
        SaveSettings();
    }

    public void SetIcon(string guid, Texture2D icon)
    {
        CustomFolderStyleEntry entry = GetOrCreate(guid);
        string iconAssetPath = ImportTextureIconIntoProject(icon);
        entry.SetIconFromProjectFile(iconAssetPath, icon);
        SaveSettings();
    }

    public void SetIconFromFile(string guid, string filePath, Texture2D previewIcon)
    {
        CustomFolderStyleEntry entry = GetOrCreate(guid);
        string iconAssetPath = ResolveIconAssetPath(filePath);
        entry.SetIconFromProjectFile(iconAssetPath, previewIcon);
        SaveSettings();
    }

    public void ClearIcon(string guid)
    {
        CustomFolderStyleEntry entry = FindByGuid(guid);
        if (entry == null)
        {
            return;
        }

        entry.ClearIcon();
        SaveSettings();
    }

    public void ClearColor(string guid)
    {
        CustomFolderStyleEntry entry = Get(guid);
        if (entry == null)
        {
            return;
        }

        entry.ClearColor();
        SaveSettings();
    }

    public void SetBackgroundColor(string guid, Color color)
    {
        CustomFolderStyleEntry entry = GetOrCreate(guid);
        entry.SetBackgroundColor(color);
        SaveSettings();
    }

    public void ClearBackgroundColor(string guid)
    {
        CustomFolderStyleEntry entry = Get(guid);
        if (entry == null)
        {
            return;
        }

        entry.ClearBackgroundColor();
        SaveSettings();
    }

    public void Clear(string guid)
    {
        EnsureEntries();
        entries.RemoveAll(entry => entry != null && entry.Guid == guid);
        InvalidateEntryIndex();
        SaveSettings();
    }

    public void ClearMany(string[] guids)
    {
        EnsureEntries();
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        entries.RemoveAll(entry => entry != null && ContainsGuid(guids, entry.Guid));
        InvalidateEntryIndex();
        SaveSettings();
    }

    public void SetColorAndBackgroundColor(string[] guids, Color color)
    {
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        foreach (string guid in guids)
        {
            CustomFolderStyleEntry entry = GetOrCreate(guid);
            entry.SetColor(color);
            entry.SetBackgroundColor(color);
        }

        SaveSettings();
    }

    public void ToggleColorDisplay(string[] guids)
    {
        ToggleDisplay(guids, 0);
    }

    public void ToggleBackgroundColorDisplay(string[] guids)
    {
        ToggleDisplay(guids, 1);
    }

    public void ToggleNameColorDisplay(string[] guids)
    {
        ToggleDisplay(guids, 2);
    }

    public void SetColorDisplay(string[] guids, bool visible)
    {
        SetDisplayChannel(guids, 0, visible);
    }

    public void SetBackgroundColorDisplay(string[] guids, bool visible)
    {
        SetDisplayChannel(guids, 1, visible);
    }

    public void SetNameColorDisplay(string[] guids, bool visible)
    {
        SetDisplayChannel(guids, 2, visible);
    }

    public void ToggleGlobalFolderColorDisplay()
    {
        showFolderColor = !showFolderColor;
        displayStateSaved = true;
        SaveSettings();
    }

    public void ToggleGlobalBackgroundColorDisplay()
    {
        showBackgroundColor = !showBackgroundColor;
        displayStateSaved = true;
        SaveSettings();
    }

    public void ToggleGlobalNameColorDisplay()
    {
        showNameColor = !showNameColor;
        displayStateSaved = true;
        SaveSettings();
    }

    public void SetGlobalFolderColorDisplay(bool visible)
    {
        showFolderColor = visible;
        displayStateSaved = true;
        SaveSettings();
    }

    public void SetGlobalBackgroundColorDisplay(bool visible)
    {
        showBackgroundColor = visible;
        displayStateSaved = true;
        SaveSettings();
    }

    public void SetGlobalNameColorDisplay(bool visible)
    {
        showNameColor = visible;
        displayStateSaved = true;
        SaveSettings();
    }

    public void SetDisplayOptions(string[] guids, bool showColor, bool showBackgroundColor, bool showNameColor)
    {
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        foreach (string guid in guids)
        {
            CustomFolderStyleEntry entry = FindByGuid(guid);
            if (entry == null)
            {
                continue;
            }

            entry.SetColorVisible(showColor);
            entry.SetBackgroundColorVisible(showBackgroundColor);
            entry.SetNameColorVisible(showNameColor);
        }

        SaveSettings();
    }

    public void ApplyStyleToFolders(
        string[] guids,
        bool applyColor,
        Color color,
        bool applyBackgroundColor,
        Color backgroundColor,
        bool applyNameColor,
        Color nameColor,
        bool applyIcon,
        string builtinIconName,
        string iconFilePath,
        Texture2D icon)
    {
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        string resolvedIconAssetPath = applyIcon && string.IsNullOrEmpty(builtinIconName)
            ? ResolveIconAssetPath(iconFilePath, icon)
            : string.Empty;

        foreach (string guid in guids)
        {
            CustomFolderStyleEntry entry = GetOrCreate(guid);

            if (applyColor)
            {
                entry.SetColor(color);
            }
            else
            {
                entry.ClearColor();
            }

            if (applyBackgroundColor)
            {
                entry.SetBackgroundColor(backgroundColor);
            }
            else
            {
                entry.ClearBackgroundColor();
            }

            if (applyNameColor)
            {
                entry.SetNameColor(nameColor);
            }
            else
            {
                entry.ClearNameColor();
            }

            if (applyIcon)
            {
                if (!string.IsNullOrEmpty(builtinIconName))
                {
                    entry.SetBuiltinIcon(builtinIconName);
                }
                else
                {
                    entry.SetIconFromProjectFile(resolvedIconAssetPath, icon);
                }
            }
            else
            {
                entry.ClearIcon();
            }
        }

        SaveSettings();
    }

    public static Texture2D LoadIcon(CustomFolderStyleEntry style)
    {
        return style == null ? null : style.LoadIcon();
    }

    public static string[] GetImportedIconAssetPaths()
    {
        if (!importedIconPathsDirty)
        {
            return (string[])cachedImportedIconAssetPaths.Clone();
        }

        if (!AssetDatabase.IsValidFolder(IconFolderAssetPath))
        {
            cachedImportedIconAssetPaths = Array.Empty<string>();
            importedIconPathsDirty = false;
            return Array.Empty<string>();
        }

        string[] iconGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { IconFolderAssetPath });
        List<string> iconPaths = new List<string>();
        foreach (string iconGuid in iconGuids)
        {
            string iconPath = AssetDatabase.GUIDToAssetPath(iconGuid);
            if (IsSupportedIconAssetPath(iconPath))
            {
                iconPaths.Add(iconPath);
            }
        }

        iconPaths.Sort(StringComparer.OrdinalIgnoreCase);
        cachedImportedIconAssetPaths = iconPaths.ToArray();
        importedIconPathsDirty = false;
        return (string[])cachedImportedIconAssetPaths.Clone();
    }

    public static void RefreshImportedIconCache()
    {
        InvalidateImportedIconCache();
    }

    public int DeleteImportedIcons(string[] iconAssetPaths)
    {
        // Clear style references first; otherwise a deleted icon path could stay
        // in the JSON database and be restored as a broken assignment later.
        if (iconAssetPaths == null || iconAssetPaths.Length == 0)
        {
            return 0;
        }

        HashSet<string> paths = new HashSet<string>(iconAssetPaths, StringComparer.OrdinalIgnoreCase);
        EnsureEntries();
        bool changed = false;
        foreach (CustomFolderStyleEntry entry in entries)
        {
            if (entry != null && paths.Contains(entry.IconAssetPath))
            {
                entry.ClearIcon();
                changed = true;
            }
        }

        int deletedCount = 0;
        foreach (string iconAssetPath in paths)
        {
            if (IsManagedIconAssetPath(iconAssetPath) && AssetDatabase.DeleteAsset(iconAssetPath))
            {
                deletedCount++;
            }
        }

        if (changed || deletedCount > 0)
        {
            InvalidateImportedIconCache();
            SaveSettings();
        }

        return deletedCount;
    }

    public static string ImportTextureIconIntoProject(Texture2D icon)
    {
        if (icon == null)
        {
            return string.Empty;
        }

        string iconAssetPath = AssetDatabase.GetAssetPath(icon);
        if (IsManagedIconAssetPath(iconAssetPath))
        {
            return iconAssetPath;
        }

        if (IsProjectAssetPath(iconAssetPath))
        {
            return ImportIconIntoProject(GetFullAssetPath(iconAssetPath));
        }

        return string.Empty;
    }

    private CustomFolderStyleEntry GetOrCreate(string guid)
    {
        EnsureEntries();
        CustomFolderStyleEntry entry = FindByGuid(guid);
        if (entry != null)
        {
            RefreshSavePoint(entry, guid);
            return entry;
        }

        string folderPath = AssetDatabase.GUIDToAssetPath(guid);
        entry = new CustomFolderStyleEntry(guid, folderPath);
        entries.Add(entry);
        EnsureEntryIndex();
        entriesByGuid[guid] = entry;
        return entry;
    }

    private CustomFolderStyleEntry FindByGuid(string guid)
    {
        EnsureEntries();
        if (string.IsNullOrEmpty(guid))
        {
            return null;
        }

        EnsureEntryIndex();
        entriesByGuid.TryGetValue(guid, out CustomFolderStyleEntry entry);
        return entry;
    }

    private void EnsureEntryIndex()
    {
        if (entriesByGuid != null)
        {
            return;
        }

        entriesByGuid = new Dictionary<string, CustomFolderStyleEntry>(StringComparer.Ordinal);
        foreach (CustomFolderStyleEntry entry in entries)
        {
            if (entry != null && !string.IsNullOrEmpty(entry.Guid))
            {
                entriesByGuid[entry.Guid] = entry;
            }
        }
    }

    private void InvalidateEntryIndex()
    {
        entriesByGuid = null;
    }

    private static bool RefreshSavePoint(CustomFolderStyleEntry entry, string guid)
    {
        if (entry == null || string.IsNullOrEmpty(guid))
        {
            return false;
        }

        return entry.UpdateFolderSavePoint(guid, AssetDatabase.GUIDToAssetPath(guid));
    }

    private void ScheduleSavePointPersistence()
    {
        if (savePointPersistenceScheduled)
        {
            return;
        }

        savePointPersistenceScheduled = true;
        EditorApplication.delayCall -= PersistSavePoints;
        EditorApplication.delayCall += PersistSavePoints;
    }

    private void PersistSavePoints()
    {
        savePointPersistenceScheduled = false;
        if (cachedInstance == this)
        {
            SaveSettings();
        }
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

    private void ToggleDisplay(string[] guids, int displayChannel)
    {
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        bool shouldShow = false;
        foreach (string guid in guids)
        {
            CustomFolderStyleEntry entry = FindByGuid(guid);
            if (entry == null)
            {
                continue;
            }

            bool isVisible = displayChannel == 0
                ? entry.ShowsColor
                : displayChannel == 1
                    ? entry.ShowsBackgroundColor
                    : entry.ShowsNameColor;
            if (!isVisible)
            {
                shouldShow = true;
                break;
            }
        }

        foreach (string guid in guids)
        {
            CustomFolderStyleEntry entry = FindByGuid(guid);
            if (entry == null)
            {
                continue;
            }

            if (displayChannel == 0)
            {
                entry.SetColorVisible(shouldShow);
            }
            else if (displayChannel == 1)
            {
                entry.SetBackgroundColorVisible(shouldShow);
            }
            else
            {
                entry.SetNameColorVisible(shouldShow);
            }
        }

        SaveSettings();
    }

    private void SetDisplayChannel(string[] guids, int displayChannel, bool visible)
    {
        if (guids == null || guids.Length == 0)
        {
            return;
        }

        foreach (string guid in guids)
        {
            CustomFolderStyleEntry entry = FindByGuid(guid);
            if (entry == null)
            {
                continue;
            }

            if (displayChannel == 0)
            {
                entry.SetColorVisible(visible);
            }
            else if (displayChannel == 1)
            {
                entry.SetBackgroundColorVisible(visible);
            }
            else
            {
                entry.SetNameColorVisible(visible);
            }
        }

        SaveSettings();
    }

    private bool RemoveEmptyEntries()
    {
        EnsureEntries();
        int removedCount = entries.RemoveAll(entry => entry == null || !entry.HasAnyStyle);
        if (removedCount > 0)
        {
            InvalidateEntryIndex();
        }

        return removedCount > 0;
    }

    private void SaveSettings()
    {
        EnsureEntries();
        RemoveEmptyEntries();

        string fullPath = GetFullDatabasePath();
        string directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string tempPath = fullPath + ".tmp";
        try
        {
            File.WriteAllText(
                tempPath,
                JsonUtility.ToJson(
                    new CustomFolderStyleFile(
                        entries,
                        showFolderColor,
                        showBackgroundColor,
                        showNameColor),
                    true));
            if (File.Exists(fullPath))
            {
                try
                {
                    File.Replace(tempPath, fullPath, null);
                }
                catch (Exception exception) when (
                    exception is IOException
                    || exception is PlatformNotSupportedException
                    || exception is NotSupportedException)
                {
                    // File.Replace is not available on every project filesystem.
                    // Fall back to an overwrite while retaining the temp copy until
                    // the destination has been written successfully.
                    File.Copy(tempPath, fullPath, true);
                    TryDeleteFile(tempPath);
                }
            }
            else
            {
                File.Move(tempPath, fullPath);
            }
        }
        catch (Exception exception)
        {
            TryDeleteFile(tempPath);
            Debug.LogWarning("Custom Folder styles could not be saved: " + exception.Message);
            return;
        }

        if (fullPath.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase))
        {
            AssetDatabase.ImportAsset(DatabaseFilePath);
        }
    }

    private void EnsureEntries()
    {
        if (entries == null)
        {
            entries = new List<CustomFolderStyleEntry>();
        }
    }

    private static CustomFolderStyleDatabase LoadOrCreate()
    {
        CustomFolderStyleDatabase database = new CustomFolderStyleDatabase();
        string fullPath = GetFullDatabasePath();
        if (File.Exists(fullPath))
        {
            try
            {
                string json = File.ReadAllText(fullPath);
                if (!string.IsNullOrWhiteSpace(json))
                {
                    CustomFolderStyleFile file = JsonUtility.FromJson<CustomFolderStyleFile>(json);
                    if (file != null && file.entries != null)
                    {
                        database.entries = file.entries;
                        database.displayStateSaved = file.displayStateSaved;
                        database.showFolderColor = file.showFolderColor;
                        database.showBackgroundColor = file.showBackgroundColor;
                        database.showNameColor = file.showNameColor;
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Custom Folder styles could not be loaded: " + exception.Message);
            }
        }

        database.EnsureEntries();
        bool needsSave = false;
        if (database.MigrateLegacyBackgroundColors())
        {
            needsSave = true;
        }

        if (database.MigrateDisplayVisibility())
        {
            needsSave = true;
        }

        if (database.MigrateGlobalDisplayVisibility())
        {
            needsSave = true;
        }

        if (database.MigrateExternalIconPaths())
        {
            needsSave = true;
        }

        if (database.RemoveEmptyEntries())
        {
            needsSave = true;
        }

        if (needsSave)
        {
            database.SaveSettings();
        }

        return database;
    }

    private bool MigrateDisplayVisibility()
    {
        EnsureEntries();
        bool changed = false;
        foreach (CustomFolderStyleEntry entry in entries)
        {
            if (entry != null && entry.MigrateDisplayVisibility())
            {
                changed = true;
            }
        }

        return changed;
    }

    private bool MigrateGlobalDisplayVisibility()
    {
        if (displayStateSaved)
        {
            return false;
        }

        // Older files did not have global switches, so all layers stay visible.
        showFolderColor = true;
        showBackgroundColor = true;
        showNameColor = true;
        displayStateSaved = true;
        return true;
    }

    private bool MigrateLegacyBackgroundColors()
    {
        EnsureEntries();
        bool changed = false;
        foreach (CustomFolderStyleEntry entry in entries)
        {
            if (entry != null && entry.MigrateLegacyBackgroundColor())
            {
                changed = true;
            }
        }

        return changed;
    }

    private bool MigrateExternalIconPaths()
    {
        EnsureEntries();
        bool changed = false;
        foreach (CustomFolderStyleEntry entry in entries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.IconAssetPath) || IsManagedIconAssetPath(entry.IconAssetPath))
            {
                continue;
            }

            string iconAssetPath = entry.IconAssetPath.Replace("\\", "/");
            if (IsProjectAssetPath(iconAssetPath) && IsSupportedIconAssetPath(iconAssetPath))
            {
                string fullIconPath = GetFullAssetPath(iconAssetPath);
                if (File.Exists(fullIconPath))
                {
                    string managedIconPath = ImportIconIntoProject(fullIconPath);
                    entry.SetIconFromProjectFile(managedIconPath, null);
                    changed = true;
                    continue;
                }
            }

            entry.ClearIcon();
            changed = true;
        }

        return changed;
    }

    private static string GetFullDatabasePath()
    {
        return Path.GetFullPath(Path.Combine(ProjectRoot, DatabaseFilePath));
    }

    private static string ProjectRoot
    {
        get
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }

    public static string GetFullAssetPath(string assetPath)
    {
        return Path.GetFullPath(Path.Combine(ProjectRoot, assetPath));
    }

    public static string ImportIconIntoProject(string sourceFilePath)
    {
        if (string.IsNullOrEmpty(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            return string.Empty;
        }

        EnsureIconFolder();

        byte[] iconPngBytes = CreateCroppedIconPng(sourceFilePath);
        if (iconPngBytes == null || iconPngBytes.Length == 0)
        {
            return string.Empty;
        }

        string safeFileName = MakeSafeFileName(Path.GetFileNameWithoutExtension(sourceFilePath)) + "_icon.png";
        string destinationAssetPath = AssetDatabase.GenerateUniqueAssetPath(IconFolderAssetPath + "/" + safeFileName);
        string destinationFullPath = GetFullAssetPath(destinationAssetPath);
        File.WriteAllBytes(destinationFullPath, iconPngBytes);
        AssetDatabase.ImportAsset(destinationAssetPath, ImportAssetOptions.ForceSynchronousImport);
        InvalidateImportedIconCache();

        return destinationAssetPath;
    }

    private static string ResolveIconAssetPath(string filePath)
    {
        return ResolveIconAssetPath(filePath, null);
    }

    private static string ResolveIconAssetPath(string filePath, Texture2D icon)
    {
        if (!string.IsNullOrEmpty(filePath))
        {
            string normalizedPath = filePath.Replace("\\", "/");
            if (IsManagedIconAssetPath(normalizedPath))
            {
                return normalizedPath;
            }

            if (IsProjectAssetPath(normalizedPath))
            {
                return ImportIconIntoProject(GetFullAssetPath(normalizedPath));
            }

            return ImportIconIntoProject(filePath);
        }

        return ImportTextureIconIntoProject(icon);
    }

    private static byte[] CreateCroppedIconPng(string sourceFilePath)
    {
        Texture2D sourceTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        sourceTexture.hideFlags = HideFlags.HideAndDontSave;

        try
        {
            if (!sourceTexture.LoadImage(File.ReadAllBytes(sourceFilePath)))
            {
                return null;
            }

            Texture2D iconTexture = CreateCenterCroppedTexture(sourceTexture, ImportedIconSize);
            byte[] pngBytes = iconTexture.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(iconTexture);
            return pngBytes;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(sourceTexture);
        }
    }

    private static Texture2D CreateCenterCroppedTexture(Texture2D sourceTexture, int outputSize)
    {
        Texture2D iconTexture = new Texture2D(outputSize, outputSize, TextureFormat.RGBA32, false);
        iconTexture.hideFlags = HideFlags.HideAndDontSave;

        float cropSize = Mathf.Min(sourceTexture.width, sourceTexture.height);
        float cropX = (sourceTexture.width - cropSize) * 0.5f;
        float cropY = (sourceTexture.height - cropSize) * 0.5f;

        for (int y = 0; y < outputSize; y++)
        {
            for (int x = 0; x < outputSize; x++)
            {
                float sourceX = cropX + ((x + 0.5f) / outputSize) * cropSize;
                float sourceY = cropY + ((y + 0.5f) / outputSize) * cropSize;
                Color pixel = sourceTexture.GetPixelBilinear(sourceX / sourceTexture.width, sourceY / sourceTexture.height);
                iconTexture.SetPixel(x, y, pixel);
            }
        }

        iconTexture.Apply(false, false);
        return iconTexture;
    }

    private static bool IsProjectAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string normalizedPath = path.Replace("\\", "/");
        return normalizedPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedIconAssetPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }

        string normalizedPath = path.Replace("\\", "/");
        return normalizedPath.StartsWith(IconFolderAssetPath + "/", StringComparison.OrdinalIgnoreCase)
            && IsSupportedIconAssetPath(normalizedPath);
    }

    private static void InvalidateImportedIconCache()
    {
        cachedImportedIconAssetPaths = Array.Empty<string>();
        importedIconPathsDirty = true;
    }

    private static void EnsureIconFolder()
    {
        if (AssetDatabase.IsValidFolder(IconFolderAssetPath))
        {
            return;
        }

        Directory.CreateDirectory(GetFullAssetPath(IconFolderAssetPath));
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
    }

    private static string MakeSafeFileName(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return "icon";
        }

        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidChar, '_');
        }

        return fileName.Length > 80 ? fileName.Substring(0, 80) : fileName;
    }

    private static bool IsSupportedIconAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
        {
            return false;
        }

        string extension = Path.GetExtension(assetPath).ToLowerInvariant();
        return extension == ".png" || extension == ".jpg" || extension == ".jpeg";
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception)
        {
        }
    }
}
