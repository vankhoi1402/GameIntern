using UnityEngine;

/// <summary>
/// Thực thể block trên bàn cờ — lưu dữ liệu logic (loại tile, stack, ô hiện tại).
/// Không xử lý hiển thị; phần visual do BlockView đảm nhiệm.
/// </summary>
public class Block : MonoBehaviour
{
    public BlockData Data { get; private set; }
    public string TypeKey => Data != null ? Data.TypeKey : string.Empty;
    public int TypeId => Data != null ? Data.BlockId : 0;

    /// <summary>Hai block cùng loại khi BlockId trùng nhau.</summary>
    public bool IsSameTypeAs(Block other)
    {
        if (Data == null || other == null || other.Data == null) return false;
        return Data.BlockId == other.Data.BlockId;
    }

    /// <summary>Merge hợp lệ: cùng loại và tổng stack ≤ 3 (cấm 2+2).</summary>
    public bool CanMergeWith(Block other)
    {
        if (!IsSameTypeAs(other)) return false;
        return StackCount + other.StackCount <= 3;
    }

    public Slot CurrentSlot { get; private set; }
    public int StackCount { get; private set; } = 1;

    /// <summary>True khi block sắp bị xóa — chặn tương tác kéo-thả.</summary>
    public bool IsPendingDestroy { get; set; } = false;

    /// <summary>Khởi tạo block với BlockData, stack mặc định 1.</summary>
    public void Init(BlockData data) => Init(data, 1);

    /// <summary>Khởi tạo block với BlockData và stack ban đầu (design: 1 hoặc 2).</summary>
    public void Init(BlockData data, int stack)
    {
        Data = data;
        StackCount = Mathf.Clamp(stack, 1, 2);
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
