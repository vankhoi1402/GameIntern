using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Quét folder Level_XXX/, rebuild catalog và import LevelData.</summary>
public static class LevelCatalogRebuilder
{
    private static readonly Regex s_FolderPattern = new Regex(@"Level_(\d+)$", RegexOptions.Compiled);

    [MenuItem("Grid Game/Rebuild Catalog From Level Folders")]
    public static void RebuildAndImport()
    {
        string levelsFolder = LevelCsvEditorPaths.LevelsFolder;
        LevelCatalog catalog = LevelCsvEditorPaths.LoadDefaultCatalog();
        BlockDatabase database = LevelCsvEditorPaths.LoadDefaultDatabase();

        if (catalog == null)
        {
            Debug.LogError("[LevelCatalogRebuilder] Không tìm thấy LevelCatalog.asset.");
            return;
        }

        if (database == null)
        {
            Debug.LogError("[LevelCatalogRebuilder] Không tìm thấy BlockDatabase.");
            return;
        }

        if (!Directory.Exists(levelsFolder))
        {
            Debug.LogError($"[LevelCatalogRebuilder] Thư mục không tồn tại: {levelsFolder}");
            return;
        }

        var entries = new List<LevelCatalogEntry>();

        foreach (string dir in Directory.GetDirectories(levelsFolder))
        {
            string folderName = Path.GetFileName(dir.Replace('\\', '/'));
            Match match = s_FolderPattern.Match(folderName);
            if (!match.Success || !int.TryParse(match.Groups[1].Value, out int levelId))
                continue;

            string csvPath = $"{dir.Replace('\\', '/')}/level.csv";
            TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            if (csv == null)
            {
                Debug.LogWarning($"[LevelCatalogRebuilder] Level {levelId} thiếu level.csv tại {csvPath}");
                continue;
            }

            LevelData asset = ImportLevel(levelId, csv, database);
            entries.Add(new LevelCatalogEntry
            {
                LevelId = levelId,
                LevelCsv = csv,
                LevelAsset = asset
            });
        }

        entries.Sort((a, b) => a.LevelId.CompareTo(b.LevelId));
        catalog.Levels = entries.ToArray();

        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[LevelCatalogRebuilder] Đã rebuild catalog — {entries.Count} level.");
    }

    public static void RebuildAndImportBatch()
    {
        RebuildAndImport();
    }

    private static LevelData ImportLevel(int levelId, TextAsset csv, BlockDatabase database)
    {
        LevelData data = LevelCsvParser.ParseLevelFromCsv(
            levelId,
            csv.text,
            database,
            LevelCsvParser.DefaultVisibleRows);

        LevelCsvValidator.LogReport(levelId, data.Rows, database);

        string assetPath = LevelCsvImporter.GetLevelAssetPathFromCsv(csv, levelId);
        LevelCsvParser.SaveLevelAsset(data, assetPath);
        return AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
    }
}
