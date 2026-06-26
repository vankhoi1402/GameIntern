/// <summary>
/// Neighbor 2+2+2 đã clear — payload spawn lại qua refill (row 0) sau gravity.
/// </summary>
public readonly struct Tier2NeighborRestore
{
    public readonly string TypeKey;
    public readonly int Stack;
    public readonly int Tier2MergeStage;
    public readonly int Col;

    public Tier2NeighborRestore(Block block, int col)
    {
        TypeKey = block != null ? block.TypeKey : string.Empty;
        Stack = block != null ? block.StackCount : 1;
        Tier2MergeStage = block != null ? block.Tier2MergeStage : 0;
        Col = col;
    }
}
