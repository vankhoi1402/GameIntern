using UnityEngine;

/// <summary>Mục tiêu skill sau khi người chơi click (hoặc instant).</summary>
public struct SkillTarget
{
    public SkillTargetMode Mode;
    public int Row;
    public int Col;
    public Block Block;

    public static SkillTarget ForColumn(int col) => new SkillTarget
    {
        Mode = SkillTargetMode.PickColumn,
        Col = col
    };

    public static SkillTarget None => new SkillTarget { Mode = SkillTargetMode.None };
}
