using System;
using UnityEngine;

/// <summary>Danh sách level — mỗi entry trỏ tới level.csv và LevelData đã import.</summary>
[CreateAssetMenu(fileName = "LevelCatalog", menuName = "Grid Game/Level Catalog")]
public class LevelCatalog : ScriptableObject
{
    public LevelCatalogEntry[] Levels;

    public LevelCatalogEntry FindById(int levelId)
    {
        if (Levels == null) return default;
        foreach (var entry in Levels)
        {
            if (entry.LevelId == levelId)
                return entry;
        }
        return default;
    }

    public LevelCatalogEntry FindByIndex(int index)
    {
        if (Levels == null || index < 0 || index >= Levels.Length)
            return default;
        return Levels[index];
    }
}

[Serializable]
public struct LevelCatalogEntry
{
    public int LevelId;
    public TextAsset LevelCsv;
    public LevelData LevelAsset;
}
