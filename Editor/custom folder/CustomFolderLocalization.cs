using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;
using UnityEngine;

// File responsibility: stores supported language labels, detects the automatic
// language, persists the user's choice, and translates plugin UI text.
internal enum CustomFolderLanguage
{
    Auto = 0,
    English = 1,
    ChineseSimplified = 2,
    ChineseTraditional = 3,
    Japanese = 4,
    Korean = 5
}

internal static class CustomFolderLocalization
{
    private const string LanguagePreferenceKey = "CustomFolder.Localization.Language";

    private static readonly string[] LanguageOptions =
    {
        "Auto (Unity/OS)",
        "English",
        "简体中文",
        "繁體中文",
        "日本語",
        "한국어"
    };

    private static readonly Dictionary<string, string[]> Translations = CreateTranslations();

    public static string[] GetLanguageOptions()
    {
        return (string[])LanguageOptions.Clone();
    }

    public static int LanguageIndex
    {
        get
        {
            int value = EditorPrefs.GetInt(LanguagePreferenceKey, (int)CustomFolderLanguage.Auto);
            return Mathf.Clamp(value, 0, LanguageOptions.Length - 1);
        }
        set
        {
            EditorPrefs.SetInt(LanguagePreferenceKey, Mathf.Clamp(value, 0, LanguageOptions.Length - 1));
        }
    }

    public static CustomFolderLanguage CurrentLanguage
    {
        get
        {
            CustomFolderLanguage selected = (CustomFolderLanguage)LanguageIndex;
            return selected == CustomFolderLanguage.Auto ? DetectLanguage() : selected;
        }
    }

    public static string Text(string key)
    {
        if (string.IsNullOrEmpty(key) || !Translations.TryGetValue(key, out string[] values))
        {
            return key;
        }

        int languageIndex = Mathf.Clamp((int)CurrentLanguage - 1, 0, values.Length - 1);
        return values[languageIndex];
    }

    public static string Format(string key, params object[] arguments)
    {
        return string.Format(CultureInfo.InvariantCulture, Text(key), arguments);
    }

    private static CustomFolderLanguage DetectLanguage()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
                return CustomFolderLanguage.ChineseSimplified;
            case SystemLanguage.ChineseTraditional:
                return CustomFolderLanguage.ChineseTraditional;
            case SystemLanguage.Japanese:
                return CustomFolderLanguage.Japanese;
            case SystemLanguage.Korean:
                return CustomFolderLanguage.Korean;
        }

        string cultureName = CultureInfo.CurrentUICulture.Name;
        if (cultureName.StartsWith("zh-Hant", StringComparison.OrdinalIgnoreCase)
            || cultureName.StartsWith("zh-TW", StringComparison.OrdinalIgnoreCase)
            || cultureName.StartsWith("zh-HK", StringComparison.OrdinalIgnoreCase))
        {
            return CustomFolderLanguage.ChineseTraditional;
        }

        if (cultureName.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
        {
            return CustomFolderLanguage.ChineseSimplified;
        }

        if (cultureName.StartsWith("ja", StringComparison.OrdinalIgnoreCase))
        {
            return CustomFolderLanguage.Japanese;
        }

        if (cultureName.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            return CustomFolderLanguage.Korean;
        }

        return CustomFolderLanguage.English;
    }

    private static Dictionary<string, string[]> CreateTranslations()
    {
        Dictionary<string, string[]> translations = new Dictionary<string, string[]>(StringComparer.Ordinal);

        Add(translations, "Language", "Language", "语言", "語言", "言語", "언어");
        Add(translations, "Image files", "Image files", "图片文件", "圖片檔案", "画像ファイル", "이미지 파일");
        Add(translations, "All files", "All files", "所有文件", "所有檔案", "すべてのファイル", "모든 파일");
        Add(translations, "Custom Folder Manager", "Custom Folder Manager", "自定义文件夹管理器", "自訂資料夾管理器", "カスタムフォルダーマネージャー", "사용자 지정 폴더 관리자");
        Add(translations, "Custom Folder", "Custom Folder", "自定义文件夹", "自訂資料夾", "カスタムフォルダー", "사용자 지정 폴더");
        Add(translations, "Display Layers", "Display Layers", "显示图层", "顯示圖層", "表示レイヤー", "표시 레이어");
        Add(translations, "Folder Color", "Folder Color", "文件夹颜色", "資料夾顏色", "フォルダー色", "폴더 색상");
        Add(translations, "Background Color", "Background Color", "背景颜色", "背景顏色", "背景色", "배경 색상");
        Add(translations, "Name Color", "Name Color", "名称颜色", "名稱顏色", "名前の色", "이름 색상");
        Add(translations, "Refresh", "Refresh", "刷新", "重新整理", "更新", "새로 고침");
        Add(translations, "Styled Folders", "Styled Folders", "已设置样式的文件夹", "已設定樣式的資料夾", "スタイル付きフォルダー", "스타일 폴더");
        Add(translations, "selected", "selected", "已选择", "已選取", "選択済み", "선택됨");
        Add(translations, "Select All", "Select All", "全选", "全選", "すべて選択", "모두 선택");
        Add(translations, "Clear", "Clear", "清除", "清除", "クリア", "지우기");
        Add(translations, "Select one or more styled folders.", "Select one or more styled folders.", "请选择一个或多个已设置样式的文件夹。", "請選取一個或多個已設定樣式的資料夾。", "スタイル付きフォルダーを1つ以上選択してください。", "스타일 폴더를 하나 이상 선택하세요.");
        Add(translations, "Edit Selected Folders", "Edit Selected Folders", "编辑选中的文件夹", "編輯選取的資料夾", "選択したフォルダーを編集", "선택한 폴더 편집");
        Add(translations, "folder(s)", "{0} folder(s)", "{0} 个文件夹", "{0} 個資料夾", "{0} 個のフォルダー", "폴더 {0}개");
        Add(translations, "Folder Name", "Folder Name", "文件夹名称", "資料夾名稱", "フォルダー名", "폴더 이름");
        Add(translations, "Rename", "Rename", "重命名", "重新命名", "名前を変更", "이름 바꾸기");
        Add(translations, "Apply Folder Color", "Apply Folder Color", "应用文件夹颜色", "套用資料夾顏色", "フォルダー色を適用", "폴더 색상 적용");
        Add(translations, "Apply Background Color", "Apply Background Color", "应用背景颜色", "套用背景顏色", "背景色を適用", "배경 색상 적용");
        Add(translations, "Apply Name Color", "Apply Name Color", "应用名称颜色", "套用名稱顏色", "名前の色を適用", "이름 색상 적용");
        Add(translations, "Apply Icon", "Apply Icon", "应用图标", "套用圖示", "アイコンを適用", "아이콘 적용");
        Add(translations, "Apply Changes to Selected Folders", "Apply Changes to Selected Folders", "应用更改到选中的文件夹", "套用變更至選取的資料夾", "選択したフォルダーに変更を適用", "선택한 폴더에 변경 사항 적용");
        Add(translations, "Apply Changes to All Styled Folders", "Apply Changes to All Styled Folders", "应用更改到所有已设置样式的文件夹", "套用變更至所有已設定樣式的資料夾", "すべてのスタイル付きフォルダーに変更を適用", "모든 스타일 폴더에 변경 사항 적용");
        Add(translations, "Remove Styles from Selected Folders", "Remove Styles from Selected Folders", "移除选中文件夹的样式", "移除選取資料夾的樣式", "選択したフォルダーのスタイルを削除", "선택한 폴더의 스타일 제거");
        Add(translations, "Remove Styles from All Styled Folders", "Remove Styles from All Styled Folders", "移除所有已设置样式文件夹的样式", "移除所有已設定樣式資料夾的樣式", "すべてのスタイル付きフォルダーのスタイルを削除", "모든 스타일 폴더의 스타일 제거");
        Add(translations, "Custom Icon", "Custom Icon", "自定义图标", "自訂圖示", "カスタムアイコン", "사용자 지정 아이콘");
        Add(translations, "Import Image...", "Import Image...", "导入图片...", "匯入圖片...", "画像をインポート...", "이미지 가져오기...");
        Add(translations, "Import Folder...", "Import Folder...", "导入文件夹...", "匯入資料夾...", "フォルダーをインポート...", "폴더 가져오기...");
        Add(translations, "Clear Icon", "Clear Icon", "清除图标", "清除圖示", "アイコンをクリア", "아이콘 지우기");
        Add(translations, "Remove", "Remove", "移除", "移除", "削除", "제거");
        Add(translations, "Unity Icons", "Unity Icons", "Unity 图标", "Unity 圖示", "Unity アイコン", "Unity 아이콘");
        Add(translations, "Imported Icons", "Imported Icons", "已导入图标", "已匯入圖示", "インポート済みアイコン", "가져온 아이콘");
        Add(translations, "icon(s)", "{0} icon(s)", "{0} 个图标", "{0} 個圖示", "{0} 個のアイコン", "아이콘 {0}개");
        Add(translations, "Clear Selection", "Clear Selection", "清除选择", "清除選取", "選択をクリア", "선택 지우기");
        Add(translations, "Delete Selected", "Delete Selected", "删除选中项", "刪除選取項目", "選択項目を削除", "선택 항목 삭제");
        Add(translations, "No imported icons.", "No imported icons.", "没有已导入的图标。", "沒有已匯入的圖示。", "インポート済みアイコンはありません。", "가져온 아이콘이 없습니다.");
        Add(translations, "Selected Folders", "Selected Folders", "选中的文件夹", "選取的資料夾", "選択したフォルダー", "선택한 폴더");
        Add(translations, "Refresh Selection", "Refresh Selection", "刷新选择", "重新整理選取", "選択を更新", "선택 새로 고침");
        Add(translations, "Randomize Folder and Background Colors", "Randomize Folder and Background Colors", "随机设置文件夹和背景颜色", "隨機設定資料夾和背景顏色", "フォルダーと背景の色をランダム化", "폴더 및 배경 색상 무작위 지정");
        Add(translations, "Selected Icon", "Selected Icon", "已选图标", "已選圖示", "選択したアイコン", "선택한 아이콘");
        Add(translations, "Presets", "Presets", "预设", "預設", "プリセット", "프리셋");
        Add(translations, "OK", "OK", "确定", "確定", "OK", "확인");
        Add(translations, "Cancel", "Cancel", "取消", "取消", "キャンセル", "취소");
        Add(translations, "Rename Folder", "Rename Folder", "重命名文件夹", "重新命名資料夾", "フォルダー名を変更", "폴더 이름 바꾸기");
        Add(translations, "This folder could not be found.", "This folder could not be found.", "找不到此文件夹。", "找不到此資料夾。", "このフォルダーが見つかりません。", "이 폴더를 찾을 수 없습니다.");
        Add(translations, "Folder name cannot be empty.", "Folder name cannot be empty.", "文件夹名称不能为空。", "資料夾名稱不可為空白。", "フォルダー名を空にすることはできません。", "폴더 이름은 비워 둘 수 없습니다.");
        Add(translations, "Folder name contains invalid characters.", "Folder name contains invalid characters.", "文件夹名称包含无效字符。", "資料夾名稱包含無效字元。", "フォルダー名に無効な文字が含まれています。", "폴더 이름에 잘못된 문자가 포함되어 있습니다.");
        Add(translations, "Import Folder Icon", "Import Folder Icon", "导入文件夹图标", "匯入資料夾圖示", "フォルダーアイコンをインポート", "폴더 아이콘 가져오기");
        Add(translations, "This image could not be loaded. Please choose a PNG or JPG file.", "This image could not be loaded. Please choose a PNG or JPG file.", "无法加载此图片，请选择 PNG 或 JPG 文件。", "無法載入此圖片，請選擇 PNG 或 JPG 檔案。", "画像を読み込めません。PNG または JPG ファイルを選択してください。", "이미지를 불러올 수 없습니다. PNG 또는 JPG 파일을 선택하세요.");
        Add(translations, "Please choose a PNG or JPG texture from this project, or use Import Image.", "Please choose a PNG or JPG texture from this project, or use Import Image.", "请选择项目中的 PNG 或 JPG 纹理，或使用“导入图片”。", "請選擇專案中的 PNG 或 JPG 紋理，或使用「匯入圖片」。", "プロジェクト内の PNG または JPG テクスチャを選択するか、「画像をインポート」を使用してください。", "프로젝트의 PNG 또는 JPG 텍스처를 선택하거나 '이미지 가져오기'를 사용하세요.");
        Add(translations, "Import Folder Icons", "Import Folder Icons", "导入文件夹图标", "匯入資料夾圖示", "フォルダーアイコンをインポート", "폴더 아이콘 가져오기");
        Add(translations, "Delete Imported Icons", "Delete Imported Icons", "删除已导入图标", "刪除已匯入圖示", "インポート済みアイコンを削除", "가져온 아이콘 삭제");
        Add(translations, "Delete the selected imported icons?", "Delete the selected imported icons?", "要删除选中的已导入图标吗？", "要刪除選取的已匯入圖示嗎？", "選択したインポート済みアイコンを削除しますか？", "선택한 가져온 아이콘을 삭제하시겠습니까?");
        Add(translations, "Delete", "Delete", "删除", "刪除", "削除", "삭제");
        Add(translations, "icon(s) imported.", "{0} icon(s) imported.", "已导入 {0} 个图标。", "已匯入 {0} 個圖示。", "{0} 個のアイコンをインポートしました。", "아이콘 {0}개를 가져왔습니다.");

        return translations;
    }

    private static void Add(Dictionary<string, string[]> translations, string key, params string[] values)
    {
        translations[key] = values;
    }
}
