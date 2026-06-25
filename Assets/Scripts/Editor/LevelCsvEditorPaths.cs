using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>Đường dẫn mặc định + helper validate level.csv trong Editor.</summary>
public static class LevelCsvEditorPaths
{
    public const string LevelsFolder = "Assets/Data/Levels";
    public const string CatalogAssetPath = LevelsFolder + "/LevelCatalog.asset";
    public const string BlockDatabasePath = "Assets/Data/Block/BlockDatabase.asset";

    private static readonly Regex s_LevelFolderPattern = new Regex(@"Level_(\d+)", RegexOptions.Compiled);

    public static LevelCatalog LoadDefaultCatalog()
        => AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogAssetPath);

    public static BlockDatabase LoadDefaultDatabase()
        => AssetDatabase.LoadAssetAtPath<BlockDatabase>(BlockDatabasePath);

    public static bool TryResolveLevelId(string csvAssetPath, out int levelId)
    {
        levelId = 0;
        if (string.IsNullOrEmpty(csvAssetPath))
            return false;

        string dirName = Path.GetFileName(Path.GetDirectoryName(csvAssetPath.Replace('\\', '/')));
        if (string.IsNullOrEmpty(dirName))
            return false;

        Match match = s_LevelFolderPattern.Match(dirName);
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out levelId))
            return false;

        return true;
    }

    public static void ValidateAllInCatalog(LevelCatalog catalog, BlockDatabase database)
    {
        if (catalog == null || catalog.Levels == null || catalog.Levels.Length == 0)
        {
            Debug.LogError("[LevelCsv] Chưa có LevelCatalog hoặc catalog trống.");
            return;
        }

        if (database == null)
        {
            Debug.LogError($"[LevelCsv] Không tìm thấy BlockDatabase tại {BlockDatabasePath}.");
            return;
        }

        foreach (LevelCatalogEntry entry in catalog.Levels)
        {
            if (entry.LevelCsv == null)
            {
                Debug.LogWarning($"[LevelCsv] Level {entry.LevelId} thiếu level.csv trong catalog.");
                continue;
            }

            LevelCsvValidator.LogReportFromCsv(entry.LevelId, entry.LevelCsv.text, database);
        }
    }
}
