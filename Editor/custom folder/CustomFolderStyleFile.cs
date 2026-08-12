using System;
using System.Collections.Generic;
using UnityEngine;

// File responsibility: defines the root JSON container used by JsonUtility for
// folder styles and the persisted global display-layer switches.
[Serializable]
internal sealed class CustomFolderStyleFile
{
    // This wrapper keeps the JSON format compatible with Unity's JsonUtility.
    [SerializeField] public List<CustomFolderStyleEntry> entries = new List<CustomFolderStyleEntry>();
    [SerializeField] public bool displayStateSaved;
    [SerializeField] public bool showFolderColor = true;
    [SerializeField] public bool showBackgroundColor = true;
    [SerializeField] public bool showNameColor = true;

    public CustomFolderStyleFile()
    {
    }

    public CustomFolderStyleFile(
        List<CustomFolderStyleEntry> entries,
        bool showFolderColor,
        bool showBackgroundColor,
        bool showNameColor)
    {
        this.entries = entries;
        this.displayStateSaved = true;
        this.showFolderColor = showFolderColor;
        this.showBackgroundColor = showBackgroundColor;
        this.showNameColor = showNameColor;
    }
}
