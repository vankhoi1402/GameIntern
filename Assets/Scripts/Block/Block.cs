using UnityEngine;

/// <summary>
/// Thực thể block trên bàn cờ — lưu dữ liệu logic (loại tile, stack, ô hiện tại).
/// Không xử lý hiển thị; phần visual do BlockView đảm nhiệm.
/// </summary>
public class Block : MonoBehaviour
{
    public BlockData Data { get; private set; }
    public BlockType Type => Data != null ? Data.BlockType : BlockType.None;

    public Slot CurrentSlot { get; private set; }
    public int StackCount { get; private set; } = 1;

    /// <summary>True khi block sắp bị xóa — chặn tương tác kéo-thả.</summary>
    public bool IsPendingDestroy { get; set; } = false;

    /// <summary>Khởi tạo block với BlockData, reset stack về 1.</summary>
    public void Init(BlockData data)
    {
        Data = data;
        StackCount = 1;
        IsPendingDestroy = false;
    }

    /// <summary>Cộng thêm stack khi merge (ví dụ: kéo block stack=2 vào stack=1 → thành 3).</summary>
    public void AddStack(int amount = 1)
    {
        if (IsPendingDestroy) return;
        StackCount += amount;
    }

    /// <summary>Gán ô lưới đang chứa block này (gọi từ Slot.SetBlock/Clear).</summary>
    internal void SetCurrentSlot(Slot slot)
    {
        if (CurrentSlot == slot) return;
        CurrentSlot = slot;
    }
}
