/// <summary>Block đang merge — BlockAnimationManager dùng để không DOKill tween nhập khi gravity chạy song song.</summary>
public static class MergeVisualContext
{
    public static Block MergeSource { get; private set; }
    public static Block MergeTarget { get; private set; }

    public static void Begin(Block source, Block target)
    {
        MergeSource = source;
        MergeTarget = target;
    }

    public static void Clear()
    {
        MergeSource = null;
        MergeTarget = null;
    }

    public static bool IsMergeTarget(Block block) => MergeTarget != null && MergeTarget == block;
}
