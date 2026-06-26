using System;
using UnityEngine;

/// <summary>
/// Xử lý hiệu ứng View khi merge — cập nhật nền stack, source hút vào target.
/// Gravity chạy song song từ MergeSystem (không chờ impact xong).
/// </summary>
public class BlockMergeViewHandler : MonoBehaviour
{
    [SerializeField] private MergeSystem mergeSystem;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;

    private void Awake()
    {
        if (mergeSystem == null)
            mergeSystem = FindObjectOfType<MergeSystem>();
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
    }

    /// <summary>Lắng nghe OnBlockStacked từ MergeSystem.</summary>
    private void OnEnable()
    {
        if (mergeSystem != null)
            mergeSystem.OnBlockStacked += HandleStacked;
    }

    /// <summary>Hủy đăng ký event merge.</summary>
    private void OnDisable()
    {
        if (mergeSystem != null)
            mergeSystem.OnBlockStacked -= HandleStacked;
    }

    /// <summary>
    /// Cập nhật nền target theo stack mới, cho source PlayAbsorbInto rồi impact trên target.
    /// </summary>
    private void HandleStacked(Block source, Block target)
    {
        if (target == null)
        {
            if (source != null) Destroy(source.gameObject);
            MergeVisualContext.Clear();
            return;
        }

        target.TryGetComponent<BlockView>(out BlockView targetView);
        if (targetView != null)
        {
            targetView.SetBackgroundConfig(stackBackgroundConfig);
            targetView.UpdateTier2StageVisual(target.StackCount, target.Tier2MergeStage);
        }

        if (source == null)
        {
            FinishTargetStackVisual(target, targetView);
            return;
        }

        Vector3 absorbTarget = ResolveAbsorbTargetWorldPos(target);

        if (source.TryGetComponent<BlockView>(out BlockView sourceView))
        {
            sourceView.PlayAbsorbInto(absorbTarget, () =>
            {
                if (source != null)
                    Destroy(source.gameObject);
                FinishTargetStackVisual(target, targetView);
            });
        }
        else
        {
            Destroy(source.gameObject);
            FinishTargetStackVisual(target, targetView);
        }
    }

    /// <summary>Ô đích sau gravity — tránh source bay vào chỗ cũ khi merge cùng cột.</summary>
    private Vector3 ResolveAbsorbTargetWorldPos(Block target)
    {
        if (target?.CurrentSlot == null || boardManager?.Layout == null)
            return target != null ? target.transform.position : Vector3.zero;

        Slot slot = target.CurrentSlot;
        int finalRow = slot.Row;

        if (GravitySystem.Instance != null)
            finalRow = GravitySystem.Instance.PredictFinalRowAfterGravity(slot.Row, slot.Col);

        return boardManager.Layout.GetWorldPosition(finalRow, slot.Col);
    }

    /// <summary>Impact cosmetic — gravity đã chạy song song từ MergeSystem.</summary>
    private void FinishTargetStackVisual(Block target, BlockView targetView)
    {
        if (target == null)
            return;

        if (mergeSystem != null && mergeSystem.IsPendingTier2TripleClear(target))
        {
            if (targetView != null)
                targetView.PlayTier2ExplosionWindUp(() => mergeSystem.CommitPendingTier2TripleClear());
            else
                mergeSystem.CommitPendingTier2TripleClear();
            return;
        }

        bool impactHandled = false;
        void AfterImpact()
        {
            if (impactHandled) return;
            impactHandled = true;

            if (BoardStateManager.Instance != null &&
                BoardStateManager.Instance.CurrentState == BoardState.ApplyingGravity)
                return;

            SnapTargetToCurrentSlot(target, targetView);
        }

        if (targetView == null)
        {
            AfterImpact();
            return;
        }

        targetView.PlayMergeImpact(AfterImpact);
    }

    private void SnapTargetToCurrentSlot(Block target, BlockView targetView)
    {
        if (target == null || targetView == null || target.CurrentSlot == null)
            return;

        if (boardManager?.Layout == null)
            return;

        Slot slot = target.CurrentSlot;
        Vector3 gridPos = boardManager.Layout.GetWorldPosition(slot.Row, slot.Col);
        targetView.SnapToGridCell(gridPos, slot.Row);
    }
}
