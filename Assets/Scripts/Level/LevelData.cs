using System;
using UnityEngine;

/// <summary>Asset level — mỗi cột là bin block từ đáy lên; VisibleRows hàng đầu hiện trên bàn.</summary>
[CreateAssetMenu(fileName = "LevelData", menuName = "Grid Game/Level Data")]
public class LevelData : ScriptableObject
{
    public int LevelId;
    public int VisibleRows = 7;
    public LevelGridRow[] Rows;

    [Header("Skills")]
    public SkillLoadoutEntry[] SkillLoadout;
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
