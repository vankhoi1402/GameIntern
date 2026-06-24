using System.Collections.Generic;
using UnityEngine;

/// <summary>Dữ liệu tạm giữa validate → presentation → resolve (mỗi lần cast skill).</summary>
public class SkillEffectPayload
{
    public readonly List<Vector2Int> CellsToClear = new List<Vector2Int>();
    public readonly List<Block> BlocksToClear = new List<Block>();

    public void Clear()
    {
        CellsToClear.Clear();
        BlocksToClear.Clear();
    }
}
