using System;
using UnityEngine;

/// <summary>
/// Xử lý kéo thả — chỉ cho merge hợp lệ; ô trống / merge cấm → reject (snap về ô gốc).
/// </summary>
public class MoveSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MergeSystem mergeSystem;

    /// <summary>Phát khi thả không hợp lệ (ô trống, khác loại, 2+2+1, ...).</summary>
    public event Action<Slot, Slot> OnMoveRejected;

    /// <summary>
    /// Xử lý thả block từ sourceSlot sang targetSlot.
    /// Chỉ merge cùng loại: stack 1 (≤3, 1+2 khi stage==1) hoặc 2+2 (stage đến 3 mới nổ).
    /// </summary>
    public bool ProcessMove(Slot sourceSlot, Slot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null || sourceSlot.IsEmpty) return false;

        if (BoardStateManager.Instance != null && !BoardStateManager.Instance.CanAcceptInput()) return false;

        if (InputManager.Instance != null && InputManager.Instance.IsInputLocked)
            return false;

        Block sourceBlock = sourceSlot.CurrentBlock;
        Block targetBlock = targetSlot.CurrentBlock;

        if (targetBlock != null && MergeRules.CanMerge(
                MergeBlockState.From(sourceBlock),
                MergeBlockState.From(targetBlock)))
            return mergeSystem.PreceptMerge(sourceBlock, targetBlock);

        OnMoveRejected?.Invoke(sourceSlot, targetSlot);
        return false;
    }
}
