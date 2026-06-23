/// <summary>
/// Ô lưới thuần logic — lưu tọa độ (row, col) và block đang nằm trên ô.
/// Không phụ thuộc Unity GameObject.
/// </summary>
public class Slot
{
    public int Row { get; }
    public int Col { get; }
    public Block CurrentBlock { get; private set; }

    public bool IsEmpty => CurrentBlock == null;
    public bool HasBlock => CurrentBlock != null;

    /// <summary>Tạo ô tại vị trí row, col trên bàn cờ.</summary>
    public Slot(int row, int col)
    {
        Row = row;
        Col = col;
    }

    /// <summary>Kiểm tra block có được đặt vào ô này không (hiện tại: chỉ khi ô trống).</summary>
    public bool CanPlace(Block block)
    {
        return IsEmpty;
    }

    /// <summary>Đặt block vào ô và đồng bộ CurrentSlot trên block.</summary>
    public void SetBlock(Block block)
    {
        CurrentBlock = block;
        if (block != null)
            block.SetCurrentSlot(this);
    }

    /// <summary>Xóa block khỏi ô, ngắt liên kết CurrentSlot trên block.</summary>
    public void Clear()
    {
        if (CurrentBlock != null)
            CurrentBlock.SetCurrentSlot(null);
        CurrentBlock = null;
    }
}
