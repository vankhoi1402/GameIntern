/// <summary>Loại clear sau merge.</summary>
public enum MergeClearKind
{
    None,
    NormalBurst,
    Tier2Triple
}

/// <summary>Snapshot state — dùng trong MergeRules, không phụ thuộc MonoBehaviour.</summary>
public readonly struct MergeBlockState
{
    public int TypeId { get; }
    public int StackCount { get; }
    public int Tier2MergeStage { get; }

    public MergeBlockState(int typeId, int stackCount, int tier2MergeStage)
    {
        TypeId = typeId;
        StackCount = stackCount;
        Tier2MergeStage = tier2MergeStage;
    }

    public static MergeBlockState From(Block block)
    {
        if (block == null)
            return new MergeBlockState(0, 0, 0);

        return new MergeBlockState(block.TypeId, block.StackCount, block.Tier2MergeStage);
    }
}

/// <summary>Kết quả resolve — MergeSystem apply lên board.</summary>
public readonly struct MergeResult
{
    public bool IsValid { get; }
    public int TargetStackCount { get; }
    public int TargetTier2Stage { get; }
    public MergeClearKind ClearKind { get; }

    public bool ShouldClear => ClearKind != MergeClearKind.None;
    public bool NeedsWindUp => ClearKind == MergeClearKind.Tier2Triple;

    public static MergeResult Invalid => new MergeResult(false, 0, 0, MergeClearKind.None);

    public MergeResult(bool isValid, int targetStackCount, int targetTier2Stage, MergeClearKind clearKind)
    {
        IsValid = isValid;
        TargetStackCount = targetStackCount;
        TargetTier2Stage = targetTier2Stage;
        ClearKind = clearKind;
    }
}

/// <summary>Luật merge thuần — CanMerge + Resolve. Không sửa board, không VFX.</summary>
public static class MergeRules
{
    /// <summary>Merge hợp lệ: 1+1 (≤3), 1+2 (stage==1), 2+2.</summary>
    public static bool CanMerge(MergeBlockState a, MergeBlockState b)
    {
        if (a.TypeId == 0 || b.TypeId == 0 || a.TypeId != b.TypeId)
            return false;

        int totalStack = a.StackCount + b.StackCount;

        if (a.StackCount == 1 && b.StackCount == 1)
            return totalStack <= 3;

        if (a.StackCount == 2 && b.StackCount == 2)
            return true;

        if (totalStack != 3)
            return false;

        MergeBlockState tier2Block = a.StackCount == 2 ? a : b;
        return tier2Block.Tier2MergeStage == 1;
    }

    /// <summary>Source kéo vào target — target là ô đích.</summary>
    public static MergeResult Resolve(MergeBlockState source, MergeBlockState target)
    {
        if (!CanMerge(source, target))
            return MergeResult.Invalid;

        bool isTier2Pair = source.StackCount == 2 && target.StackCount == 2;

        if (isTier2Pair)
        {
            int combinedStage = target.Tier2MergeStage + source.Tier2MergeStage;
            MergeClearKind clearKind = combinedStage >= 3
                ? MergeClearKind.Tier2Triple
                : MergeClearKind.None;

            return new MergeResult(true, 2, combinedStage, clearKind);
        }

        int newStack = target.StackCount + source.StackCount;
        int newStage = target.Tier2MergeStage;

        if (newStack == 2 && source.StackCount == 1 && target.StackCount == 1)
            newStage = 1;

        MergeClearKind stackClear = newStack >= 3
            ? MergeClearKind.NormalBurst
            : MergeClearKind.None;

        return new MergeResult(true, newStack, newStage, stackClear);
    }
}
