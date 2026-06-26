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

    /// <summary>
    /// Số "đơn vị 2" đã góp trên ô này khi StackCount == 2 (1+1 hoặc spawn → 1; mỗi 2+2 +1; đủ 3 thì nổ).
    /// </summary>
    public int Tier2MergeStage { get; private set; }

    /// <summary>
    /// Merge hợp lệ: nhánh stack 1 (≤3, 1+2 khi stage==1) hoặc nhánh 2+2 (chỉ stack 2 với stack 2).
    /// </summary>
    public bool CanMergeWith(Block other)
    {
        if (!IsSameTypeAs(other)) return false;

        int totalStack = StackCount + other.StackCount;

        if (StackCount == 1 && other.StackCount == 1)
            return totalStack <= 3;

        if (StackCount == 2 && other.StackCount == 2)
            return true;

        if (totalStack != 3)
            return false;

        Block tier2Block = StackCount == 2 ? this : other;
        return tier2Block.Tier2MergeStage == 1;
    }

    public bool IsTier2PairMergeWith(Block other)
        => IsSameTypeAs(other) && StackCount == 2 && other.StackCount == 2;

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
        Tier2MergeStage = StackCount == 2 ? 1 : 0;
        IsPendingDestroy = false;
    }

    /// <summary>Cộng thêm stack khi merge nhánh 1+1 / 1+2 (không dùng cho 2+2).</summary>
    public void AddStack(int amount = 1)
    {
        if (IsPendingDestroy) return;
        StackCount += amount;
    }

    /// <summary>Đánh dấu block "2" sau 1+1 hoặc khi spawn stack 2.</summary>
    public void SetTier2MergeStage(int stage)
    {
        if (IsPendingDestroy) return;
        Tier2MergeStage = Mathf.Max(0, stage);
    }

    /// <summary>Mỗi lần ghép thêm một block stack 2 vào ô đích (nhánh 2+2).</summary>
    public void IncrementTier2MergeStage()
    {
        if (IsPendingDestroy) return;
        Tier2MergeStage++;
    }

    /// <summary>Gán ô lưới đang chứa block này (gọi từ Slot.SetBlock/Clear).</summary>
    internal void SetCurrentSlot(Slot slot)
    {
        if (CurrentSlot == slot) return;
        CurrentSlot = slot;
    }
}
