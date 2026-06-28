using UnityEngine;

/// <summary>
/// Thực thể block trên bàn cờ — lưu dữ liệu logic (loại tile, stack, ô hiện tại).
/// Luật merge nằm ở MergeRules; không xử lý hiển thị.
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

    /// <summary>Delegate sang MergeRules — giữ API cũ cho call site.</summary>
    public bool CanMergeWith(Block other)
        => MergeRules.CanMerge(MergeBlockState.From(this), MergeBlockState.From(other));

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

    /// <summary>Áp state sau MergeRules.Resolve — gọi từ MergeSystem.</summary>
    public void ApplyMergeState(int stackCount, int tier2MergeStage)
    {
        if (IsPendingDestroy) return;
        StackCount = stackCount;
        Tier2MergeStage = Mathf.Max(0, tier2MergeStage);
    }

    /// <summary>Đánh dấu block "2" sau 1+1 hoặc khi spawn stack 2.</summary>
    public void SetTier2MergeStage(int stage)
    {
        if (IsPendingDestroy) return;
        Tier2MergeStage = Mathf.Max(0, stage);
    }

    /// <summary>Gán ô lưới đang chứa block này (gọi từ Slot.SetBlock/Clear).</summary>
    internal void SetCurrentSlot(Slot slot)
    {
        if (CurrentSlot == slot) return;
        CurrentSlot = slot;
    }
}
