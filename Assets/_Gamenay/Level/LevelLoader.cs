using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>
/// Đọc, parse và validate dữ liệu level — không chứa gameplay.
/// </summary>
public class LevelLoader
{
    #region Constants

    public const int GridColumnCount = 5;
    private const int c_MergeGroupSize = 3;

    #endregion

    #region Load

    /// <summary>Lấy LevelData từ catalog entry — ưu tiên asset, fallback CSV.</summary>
    public LevelData LoadFromEntry(LevelCatalogEntry entry, BlockDatabase database)
    {
        if (entry.LevelAsset != null)
            return entry.LevelAsset;

        if (entry.LevelCsv == null || string.IsNullOrWhiteSpace(entry.LevelCsv.text))
            return null;

        return ParseLevelFromCsv(entry.LevelId, entry.LevelCsv.text);
    }

    /// <summary>Parse CSV thành LevelData — toàn bộ depth; bàn/bin tách theo BoardManager.rows lúc chơi.</summary>
    public LevelData ParseLevelFromCsv(int levelId, string csvText)
    {
        ParseBinCsv(csvText, out int timeLimitSeconds, out LevelGridRow[] rows);

        LevelData level = ScriptableObject.CreateInstance<LevelData>();
        level.LevelId = levelId;
        level.VisibleRows = 0;
        level.TimeLimitSeconds = timeLimitSeconds;
        level.Rows = rows;
        return level;
    }

    /// <summary>Parse toàn bộ bin rows từ CSV.</summary>
    public LevelGridRow[] ParseBinCsv(string csvText)
    {
        ParseBinCsv(csvText, out _, out LevelGridRow[] rows);
        return rows;
    }

    #endregion

    #region Parse CSV

    private void ParseBinCsv(string csvText, out int timeLimitSeconds, out LevelGridRow[] rows)
    {
        timeLimitSeconds = 0;
        var rowList = new List<LevelGridRow>();

        if (string.IsNullOrWhiteSpace(csvText))
        {
            rows = rowList.ToArray();
            return;
        }

        string[] lines = csvText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        int startLine = lines.Length > 0 && IsHeader(lines[0]) ? 1 : 0;

        for (int i = startLine; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line) || line.StartsWith("#"))
                continue;

            string[] cols = SplitCsvLine(line);
            if (cols.Length < 1)
                continue;

            if (!int.TryParse(cols[0].Trim(), out int row))
                continue;

            if (cols.Length < GridColumnCount + 1)
                Debug.LogWarning($"[LevelLoader] Dòng row {row} thiếu cột — pad ô trống: {line}");

            var blockTypes = new string[GridColumnCount];
            for (int c = 0; c < GridColumnCount; c++)
            {
                string type = c + 1 < cols.Length ? cols[c + 1].Trim() : string.Empty;
                blockTypes[c] = string.IsNullOrEmpty(type) ? string.Empty : type;
            }

            if (row == 0 && cols.Length > GridColumnCount + 1 &&
                int.TryParse(cols[GridColumnCount + 1].Trim(), out int parsedTime))
            {
                timeLimitSeconds = Mathf.Max(0, parsedTime);
            }

            rowList.Add(new LevelGridRow { Row = row, ColBlockTypes = blockTypes });
        }

        rows = rowList.ToArray();
    }

    private static bool IsHeader(string line)
    {
        string lower = line.ToLowerInvariant();
        return lower.StartsWith("row,") || lower.StartsWith("row\t") ||
               lower.Contains("col0") || lower.Contains("col1");
    }

    private static string[] SplitCsvLine(string line)
        => line.Contains('\t') ? line.Split('\t') : line.Split(',');

    #endregion

    #region Parse Cell

    /// <summary>Parse ô: "1", "1:2". Ô trống → false.</summary>
    public bool TryParseCell(string cell, out string typeKey, out int stack)
    {
        typeKey = null;
        stack = 1;

        if (string.IsNullOrWhiteSpace(cell))
            return false;

        string trimmed = cell.Trim();
        int colon = trimmed.IndexOf(':');

        if (colon < 0)
        {
            typeKey = trimmed;
            return true;
        }

        typeKey = trimmed.Substring(0, colon).Trim();
        if (string.IsNullOrEmpty(typeKey))
            return false;

        if (!int.TryParse(trimmed.Substring(colon + 1).Trim(), out stack))
            stack = 1;

        stack = Mathf.Clamp(stack, 1, 2);
        return true;
    }

    #endregion

    #region Validate

    /// <summary>Validate level data và log kết quả.</summary>
    public void ValidateAndLog(LevelData level, BlockDatabase database)
    {
        if (level == null)
            return;

        LogReport(level.LevelId, level.Rows, database);
    }

    /// <summary>Validate trực tiếp từ CSV text.</summary>
    public void ValidateCsvAndLog(int levelId, string csvText, BlockDatabase database)
    {
        LevelGridRow[] rows = ParseBinCsv(csvText);
        LogReport(levelId, rows, database);
    }

    private void LogReport(int levelId, LevelGridRow[] rows, BlockDatabase database)
    {
        if (rows == null || rows.Length == 0)
        {
            Debug.LogWarning($"[LevelLoader] Level {levelId} — CSV rỗng hoặc không parse được dòng nào.");
            return;
        }

        if (database == null)
        {
            Debug.LogError($"[LevelLoader] Level {levelId} — chưa gán BlockDatabase, không validate được.");
            return;
        }

        database.BuildLookupTable();

        var countByType = new Dictionary<string, int>();
        var missingSamples = new List<string>();

        foreach (LevelGridRow gridRow in rows)
        {
            if (gridRow.ColBlockTypes == null)
                continue;

            for (int col = 0; col < gridRow.ColBlockTypes.Length; col++)
            {
                string cell = gridRow.ColBlockTypes[col];
                if (!TryParseCell(cell, out string typeKey, out _))
                    continue;

                if (database.GetBlockData(typeKey) == null)
                    missingSamples.Add($"  - loại '{typeKey}' @ row {gridRow.Row} col {col}");

                if (!countByType.ContainsKey(typeKey))
                    countByType[typeKey] = 0;
                countByType[typeKey]++;
            }
        }

        var notDivisibleByThree = new List<string>();
        foreach (KeyValuePair<string, int> pair in countByType.OrderBy(p => p.Key))
        {
            int remainder = pair.Value % c_MergeGroupSize;
            if (remainder != 0)
                notDivisibleByThree.Add($"  - loại '{pair.Key}': {pair.Value} block (chia 3 dư {remainder})");
        }

        if (missingSamples.Count > 0 || notDivisibleByThree.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[LevelLoader] Level {levelId} — KHÔNG HỢP LỆ");

            if (missingSamples.Count > 0)
            {
                sb.AppendLine("Không có trong BlockDatabase:");
                sb.AppendLine(string.Join("\n", missingSamples));
            }

            if (notDivisibleByThree.Count > 0)
            {
                sb.AppendLine("Số block cùng loại không chia hết cho 3:");
                sb.AppendLine(string.Join("\n", notDivisibleByThree));
            }

            Debug.LogError(sb.ToString().TrimEnd());
            return;
        }

        int totalBlocks = countByType.Values.Sum();
        var summary = new StringBuilder();
        summary.AppendLine($"[LevelLoader] Level {levelId} — OK | {countByType.Count} loại | tổng {totalBlocks} block");

        foreach (KeyValuePair<string, int> pair in countByType.OrderBy(p => p.Key))
            summary.AppendLine($"  - loại '{pair.Key}': {pair.Value} block");

        Debug.Log(summary.ToString().TrimEnd());
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>Lưu LevelData asset — giữ tương thích pipeline Editor hiện có.</summary>
    public static void SaveLevelAsset(LevelData level, string assetPath)
    {
        string dir = Path.GetDirectoryName(assetPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        LevelData existing = UnityEditor.AssetDatabase.LoadAssetAtPath<LevelData>(assetPath);
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
    }
#endif
}