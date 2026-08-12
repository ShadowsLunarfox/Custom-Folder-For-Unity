using System.Collections.Generic;
using UnityEditor;

// File responsibility: resolves the currently selected Project window folders
// and provides shared selection and folder-validation helpers.
internal static class CustomFolderSelection
{
    // Centralizes Project window folder selection checks used by menus and overlays.
    public static string[] GetSelectedFolderGuids()
    {
        List<string> folderGuids = new List<string>();
        foreach (string guid in Selection.assetGUIDs)
        {
            if (IsFolderGuid(guid))
            {
                folderGuids.Add(guid);
            }
        }

        return folderGuids.ToArray();
    }

    public static bool IsFolderGuid(string guid)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        return !string.IsNullOrEmpty(path) && AssetDatabase.IsValidFolder(path);
    }

    public static bool IsSelectedFolderGuid(string guid)
    {
        string folderPath = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            return false;
        }

        // The active object is updated during the Project Browser click before
        // Selection.assetGUIDs is refreshed. Use it first for immediate repaint.
        UnityEngine.Object activeObject = Selection.activeObject;
        if (activeObject != null && AssetDatabase.GetAssetPath(activeObject) == folderPath)
        {
            return true;
        }

        foreach (string selectedGuid in Selection.assetGUIDs)
        {
            if (selectedGuid == guid)
            {
                return true;
            }
        }

        return false;
    }
}
