using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Kiểm tra level — log lúc import CSV và khi Play load level.</summary>
public static class LevelCsvValidator
{
    private const int c_MergeGroupSize = 3;

    public static void LogReport(int levelId, LevelGridRow[] rows, BlockDatabase database)
    {
        if (rows == null || rows.Length == 0)
        {
            Debug.LogWarning($"[LevelCsv] Level {levelId} — CSV rỗng hoặc không parse được dòng nào.");
            return;
        }

        if (database == null)
        {
            Debug.LogError($"[LevelCsv] Level {levelId} — chưa gán BlockDatabase, không validate được.");
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
                if (!LevelCellParser.TryParseCell(cell, out string typeKey, out _))
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
            sb.AppendLine($"[LevelCsv] Level {levelId} — KHÔNG HỢP LỆ");

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
        summary.AppendLine($"[LevelCsv] Level {levelId} — OK | {countByType.Count} loại | tổng {totalBlocks} block");

        foreach (KeyValuePair<string, int> pair in countByType.OrderBy(p => p.Key))
            summary.AppendLine($"  - loại '{pair.Key}': {pair.Value} block");

        Debug.Log(summary.ToString().TrimEnd());
    }

    /// <summary>Parse CSV rồi log — dùng khi chỉ validate, không bake asset.</summary>
    public static void LogReportFromCsv(int levelId, string csvText, BlockDatabase database)
    {
        LevelGridRow[] rows = LevelCsvParser.ParseBinCsv(csvText);
        LogReport(levelId, rows, database);
    }
}
