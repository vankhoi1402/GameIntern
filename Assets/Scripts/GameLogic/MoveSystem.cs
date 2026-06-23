using System;
using UnityEngine;

/// <summary>
/// Xử lý di chuyển block: kéo vào ô trống hoặc chuyển sang merge nếu cùng loại.
/// </summary>
public class MoveSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private MergeSystem mergeSystem;

    /// <summary>Phát khi thả vào ô không hợp lệ (khác loại, ô đã có block).</summary>
    public event Action<Slot, Slot> OnMoveRejected;

    /// <summary>
    /// Xử lý thả block từ sourceSlot sang targetSlot.
    /// Ô trống → di chuyển + gravity; cùng loại → merge; khác → reject.
    /// Trả true nếu move/merge được chấp nhận.
    /// </summary>
    public bool ProcessMove(Slot sourceSlot, Slot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || sourceSlot.IsEmpty) return false;

        if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput()) return false;

        Block sourceBlock = sourceSlot.CurrentBlock;
        Block targetBlock = targetSlot.CurrentBlock;

        if (targetSlot.CanPlace(sourceBlock))
        {
            ApplyMoveAndGravity(sourceSlot, targetSlot);
            return true;
        }

        if (targetBlock != null && sourceBlock.Type == targetBlock.Type)
            return mergeSystem.PreceptMerge(sourceBlock, targetBlock);

        OnMoveRejected?.Invoke(sourceSlot, targetSlot);
        return false;
    }

    /// <summary>Di chuyển block sang ô trống và kích hoạt gravity ngay.</summary>
    private void ApplyMoveAndGravity(Slot sourceSlot, Slot targetSlot)
    {
        BoardStateManager.Instance.ChangeState(BoardState.ProcessingMove);

        boardManager.MoveBlock(sourceSlot.Row, sourceSlot.Col, targetSlot.Row, targetSlot.Col);

        if (GravitySystem.Instance != null)
            GravitySystem.Instance.RunGravity();
        else
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
    }
}
