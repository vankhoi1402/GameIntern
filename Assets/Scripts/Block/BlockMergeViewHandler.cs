using System;
using UnityEngine;

/// <summary>
/// Xử lý hiệu ứng View khi merge — cập nhật nền stack, source hút vào target.
/// </summary>
public class BlockMergeViewHandler : MonoBehaviour
{
    [SerializeField] private MergeSystem mergeSystem;
    [SerializeField] private StackBackgroundConfig stackBackgroundConfig;

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

        Vector3 absorbTarget = target.transform.position;

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

    /// <summary>Phát hiệu ứng punch scale trên target sau khi absorb xong.</summary>
    private void FinishTargetStackVisual(Block target, BlockView targetView)
    {
        if (target == null || targetView == null) return;
        targetView.PlayMergeImpact();
    }
}
