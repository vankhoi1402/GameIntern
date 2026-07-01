/// <summary>
/// Ô lưới thuần logic — lưu tọa độ (row, col) và block đang nằm trên ô.
/// </summary>
public class Slot
{
    public int Row { get; }
    public int Col { get; }
    public BlockManager CurrentBlock { get; private set; }

    public bool IsEmpty => CurrentBlock == null;
    public bool HasBlock => CurrentBlock != null;

    public Slot(int row, int col)
    {
        Row = row;
        Col = col;
    }

    public bool CanPlace(BlockManager block) => IsEmpty;

    public void SetBlock(BlockManager block)
    {
        CurrentBlock = block;
        if (block != null)
            block.SetCurrentSlot(this);
    }

    public void Clear()
    {
        if (CurrentBlock != null)
            CurrentBlock.SetCurrentSlot(null);
        CurrentBlock = null;
    }
}
