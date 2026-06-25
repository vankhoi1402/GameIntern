using System;
using UnityEditor;
using UnityEngine;

/// <summary>Tự validate khi level.csv được Unity reimport (save file).</summary>
public class LevelCsvAssetPostprocessor : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        BlockDatabase database = LevelCsvEditorPaths.LoadDefaultDatabase();
        if (database == null)
            return;

        foreach (string path in importedAssets)
        {
            string normalized = path.Replace('\\', '/');
            if (!normalized.EndsWith("/level.csv", StringComparison.OrdinalIgnoreCase))
                continue;

            if (!LevelCsvEditorPaths.TryResolveLevelId(path, out int levelId))
            {
                Debug.LogWarning($"[LevelCsv] Không đọc được LevelId từ path: {path}");
                continue;
            }

            TextAsset csv = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (csv == null)
                continue;

            LevelCsvValidator.LogReportFromCsv(levelId, csv.text, database);
        }
    }
}
