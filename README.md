# Custom Folder for Unity

A Unity Editor tool for customizing folders in the Project window. It lets you assign custom colors, backgrounds, name colors, and icons so large Unity projects are easier to browse, organize, and recognize.

This tool runs only inside the Unity Editor. It does not affect runtime scenes, Prefabs, or in-game UI.

## Features

- Set a custom folder icon color.
- Set an independent background color.
- Set an independent folder-name color.
- Use a custom image as a folder icon.
- Use Unity Editor built-in icons as folder icons.
- Import PNG, JPG, and JPEG images from your computer.
- Automatically store imported icons in the plugin `icon` folder.
- Import multiple images and manage them from the imported icon library.
- Select multiple imported icons with Shift-click.
- Select all or clear the current icon selection.
- Delete selected imported icons in batches.
- Edit multiple folders at the same time.
- Apply changes to selected folders or all styled folders.
- Remove styles from selected folders or all styled folders.
- Rename folders from the style editor.
- Use color presets and random folder/background colors.
- Toggle global display layers:
  - `Folder Color`: show or hide the folder-color layer.
  - `Background Color`: show or hide the background-color layer.
  - `Name Color`: show or hide the name-color layer.
- Open a quick Alt popup for editing selected folders.
- Manage all styled folders from a dedicated management window.
- Search, refresh, select all, and clear selections.
- Support English, Simplified Chinese, Traditional Chinese, Japanese, and Korean.
- Support `Auto (Unity/OS)` language detection.
- Save folder styles and global display settings to `CustomFolderStyles.json`.

## Installation

1. Close Unity, or make sure the project is not currently compiling scripts.
2. Copy `Assets/Editor/custom folder` into your Unity project's `Assets/Editor/` directory.
3. Open Unity and wait for the scripts to finish compiling.
4. Select one or more folders in the Project window.

Unity `2022.3.22f1` or later is recommended. The scripts have been compile-checked with Unity 2022.3.22f1 and Unity 6.0.0f1.

## Usage

### Alt Popup

1. Select one or more folders in the Unity Project window.
2. Move the mouse over a selected folder item.
3. Hold the `Alt` key.
4. Edit the folder color, background color, name color, or icon.
5. Use the color checkboxes to decide which color settings should be applied during this operation.
6. Custom icons do not have a separate checkbox. Selecting, importing, or removing an icon applies the icon automatically.
7. Click `OK` to save the changes.
8. Click `Cancel` to discard the current changes.
9. Click `Clear` to remove all styles from the selected folders.

### Project Window Context Menu

1. Select one or more folders in the Project window.
2. Right-click and open `Assets/Custom Folder`.
3. Choose one of the following actions:
   - `Edit Style...`: open the folder style popup.
   - `Clear Style`: remove styles from the selected folders.
   - `Set Color`: apply a preset color.

### Style Manager Window

Open the manager from the Unity main menu:

```text
Tools > Custom Folder > Style Manager
```

The manager window contains the following areas:

- Search bar: search styled folders.
- `Display Layers`: control the global folder-color, background-color, and name-color layers.
- `Language`: choose a language or use `Auto (Unity/OS)`.
- Folder list: browse and multi-select styled folders.
- Editor panel: edit folder names, colors, backgrounds, name colors, and icons.
- `Apply Changes to Selected Folders`: apply the current editor values to selected folders.
- `Apply Changes to All Styled Folders`: apply the current editor values to all styled folders.
- `Remove Styles from Selected Folders`: remove styles from selected folders.
- `Remove Styles from All Styled Folders`: remove styles from all styled folders.
- `Imported Icons`: browse, select, import, and delete managed icons.

### Global Display Layer Menu

The display layers can also be controlled from the Unity main menu:

```text
Tools > Custom Folder > Display
```

Unchecking an item immediately hides that drawing layer across the Project window. The saved folder color, background color, name color, and icon data are not deleted. Re-enable the item to show the layer again.

## Icon Import

There are two ways to import icons:

- Click `Import Image...` in the Alt popup or Style Manager and choose an image from your computer.
- Click `Import Folder...` in the Style Manager to batch-import images from a folder.

Imported images are processed to a Project-window-friendly icon size and stored in:

```text
Assets/Editor/custom folder/icon
```

When imported icons are deleted, the plugin also removes saved folder references to those icons so broken paths are not left behind.

## Data Storage

Folder styles are saved in:

```text
Assets/Editor/custom folder/CustomFolderStyles.json
```

The file stores:

- Folder GUID, name, and path save points.
- Folder colors.
- Background colors.
- Name colors.
- Unity built-in icon names or custom icon paths.
- Global display-layer states.

Add `CustomFolderStyles.json` and the `icon` folder to version control if the styles should be shared with a team.

## Screenshots

### Style Manager Window

<img src="Screenshot 2026-08-12 171345.png" >

### Alt Folder Style Popup

<img src="Screenshot 2026-08-12 171336.png" 

### Customized Folders in the Project Window

<img src="Screenshot 2026-08-12 171321.png" >
<img src="Screenshot 2026-08-12 172514.png" >

## File Structure

```text
Assets/
└── Editor/
    └── custom folder/
        ├── CustomFolderLocalization.cs
        ├── CustomFolderProjectView.cs
        ├── CustomFolderSelection.cs
        ├── CustomFolderStyleDatabase.cs
        ├── CustomFolderStyleEntry.cs
        ├── CustomFolderStyleFile.cs
        ├── CustomFolderStyleManagerWindow.cs
        ├── CustomFolderStylePopup.cs
        ├── CustomFolderStyles.json
        └── icon/
```

## Notes

- This is a Unity Editor extension and runs only in the Editor.
- Do not delete `CustomFolderStyles.json` unless you want to remove the saved folder styles.
- When a folder is moved or renamed, the plugin uses its Unity GUID to update the saved folder name and path.
- Click `Refresh` in the Style Manager if the Project window does not update immediately.
- The Alt popup may close temporarily while Unity recompiles scripts because of Unity's domain reload behavior.

## License

Add the repository's license information here.
