using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Parse level.csv — mỗi cột là bin từ đáy lên.
/// row 0..VisibleRows-1 = bàn ban đầu; row VisibleRows+ = block chờ đẩy sau T3.
/// </summary>
public static class LevelCsvParser
{
    public const int GridColumnCount = 5;
    public const int DefaultVisibleRows = 7;

    public static LevelData ParseLevelFromCsv(int levelId, string csvText, BlockDatabase database, int visibleRows = DefaultVisibleRows)
    {
        var level = ScriptableObject.CreateInstance<LevelData>();
        level.LevelId = levelId;
        level.VisibleRows = visibleRows;
        level.Rows = ParseBinCsv(csvText, out int timeLimitSeconds);
        level.TimeLimitSeconds = timeLimitSeconds;
        return level;
    }

    public static LevelGridRow[] ParseBinCsv(string csvText)
        => ParseBinCsv(csvText, out _);

    public static LevelGridRow[] ParseBinCsv(string csvText, out int timeLimitSeconds)
    {
        timeLimitSeconds = 0;
        var rows = new List<LevelGridRow>();
        if (string.IsNullOrWhiteSpace(csvText))
            return rows.ToArray();

        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int startLine = 0;
        if (lines.Length > 0 && IsHeader(lines[0]))
            startLine = 1;

        for (int i = startLine; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            string[] cols = SplitCsvLine(line);
            if (cols.Length < GridColumnCount + 1)
            {
                Debug.LogWarning($"[LevelCsv] Bỏ qua dòng thiếu cột: {line}");
                continue;
            }

            if (!int.TryParse(cols[0].Trim(), out int row))
                continue;

            var blockTypes = new string[GridColumnCount];
            for (int c = 0; c < GridColumnCount; c++)
            {
                string type = cols[c + 1].Trim();
                blockTypes[c] = string.IsNullOrEmpty(type) ? string.Empty : type;
            }

            if (row == 0 && cols.Length > GridColumnCount + 1 &&
                int.TryParse(cols[GridColumnCount + 1].Trim(), out int parsedTime))
            {
                timeLimitSeconds = Mathf.Max(0, parsedTime);
            }

            rows.Add(new LevelGridRow { Row = row, ColBlockTypes = blockTypes });
        }

        return rows.ToArray();
    }

    private static bool IsHeader(string line)
    {
        string lower = line.ToLowerInvariant();
        return lower.StartsWith("row,") || lower.Contains("col0") || lower.Contains("col1");
    }

    private static string[] SplitCsvLine(string line) => line.Split(',');

    public static void SaveLevelAsset(LevelData level, string assetPath)
    {
#if UNITY_EDITOR
        string dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
        if (existing != null)
        {
            existing.LevelId = level.LevelId;
            existing.VisibleRows = level.VisibleRows;
            existing.TimeLimitSeconds = level.TimeLimitSeconds;
            existing.Rows = level.Rows;
            UnityEditor.EditorUtility.SetDirty(existing);
            UnityEngine.Object.DestroyImmediate(level);
        }
        else
        {
            UnityEditor.AssetDatabase.CreateAsset(level, assetPath);
        }

        UnityEditor.AssetDatabase.SaveAssets();
#endif
    }
}
