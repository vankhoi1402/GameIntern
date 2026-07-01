using System;
using UnityEngine;

/// <summary>Asset level — CSV row 0..N là depth cột; ranh giới bàn/bin do BoardManager.rows lúc chơi.</summary>
[CreateAssetMenu(fileName = "LevelData", menuName = "Grid Game/Level Data")]
public class LevelData : ScriptableObject
{
    public int LevelId;
    [Tooltip("Legacy — runtime dùng BoardManager.rows, không dùng field này.")]
    public int VisibleRows;
    public LevelGridRow[] Rows;

    [Header("Rules")]
    public int TimeLimitSeconds;
}

[Serializable]
public struct LevelGridRow
{
    public int Row;
    public string[] ColBlockTypes;
}

/// <summary>Block lấy từ bin cột khi refill sau T3.</summary>
[Serializable]
public struct LevelBinBlock
{
    public string BlockType;
    public int Stack;
}
