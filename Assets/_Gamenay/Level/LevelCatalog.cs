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

    public bool TryGetNextAfter(int levelId, out LevelCatalogEntry next)
    {
        next = default;
        if (Levels == null || Levels.Length == 0)
            return false;

        int index = -1;
        for (int i = 0; i < Levels.Length; i++)
        {
            if (Levels[i].LevelId == levelId)
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index + 1 >= Levels.Length)
            return false;

        next = Levels[index + 1];
        return next.LevelAsset != null;
    }
}

[Serializable]
public struct LevelCatalogEntry
{
    public int LevelId;
    public TextAsset LevelCsv;
    public LevelData LevelAsset;
}
