using System;
using UnityEngine;

/// <summary>
/// Xử lý logic merge: cộng stack, xóa block đạt max, kích hoạt gravity + refill.
/// </summary>
public class MergeSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private DesignRefillController designRefillController;

    /// <summary>Phát sau khi source đã cộng stack vào target (View xử lý animation nhập).</summary>
    public event Action<Block, Block> OnBlockStacked;

    private void Awake()
    {
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (designRefillController == null)
            designRefillController = GetComponent<DesignRefillController>();
        if (designRefillController == null)
            designRefillController = FindObjectOfType<DesignRefillController>();
    }

    /// <summary>Điểm vào merge — gọi khi kéo block cùng loại vào nhau. Trả false nếu merge không thực hiện được.</summary>
    public bool PreceptMerge(Block sourceBlock, Block targetBlock)
    {
        if (sourceBlock == null || targetBlock == null) return false;
        if (targetBlock.IsPendingDestroy || sourceBlock.IsPendingDestroy) return false;
        if (!sourceBlock.CanMergeWith(targetBlock)) return false;

        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);
        ProcessMerge(sourceBlock, targetBlock);
        return true;
    }

    /// <summary>
    /// Cộng stack source vào target, clear ô source, đổi nền/animation,
    /// nếu stack >= 3 thì nổ 3 block rơi khỏi màn → gravity → refill.
    /// </summary>
    private void ProcessMerge(Block sourceBlock, Block targetBlock)
    {
        sourceBlock.IsPendingDestroy = true;

        int sourceWeight = sourceBlock.StackCount;
        targetBlock.AddStack(sourceWeight);

        Slot sourceSlot = sourceBlock.CurrentSlot;
        if (sourceSlot != null)
            boardManager.ClearSlot(sourceSlot.Row, sourceSlot.Col);

        OnBlockStacked?.Invoke(sourceBlock, targetBlock);

        if (targetBlock.StackCount >= 3)
        {
            Slot targetSlot = targetBlock.CurrentSlot;
            if (targetSlot != null)
            {
                targetBlock.IsPendingDestroy = true;
                int row = targetSlot.Row;
                int col = targetSlot.Col;
                BlockData clearedData = targetBlock.Data;

                boardManager.ClearSlot(row, col);
                Destroy(targetBlock.gameObject);

                void AfterExplode()
                {
                    if (designRefillController != null)
                        designRefillController.OnT3Cleared(null);
                    else
                        RunGravityOrIdle();
                }

                if (spawnSystem != null && clearedData != null)
                    spawnSystem.SpawnBurstFallOff(row, col, clearedData, AfterExplode);
                else
                    AfterExplode();

                return;
            }

            RunGravityOrIdle();
            return;
        }

        RunGravityOrIdle();
    }

    /// <summary>Kích hoạt gravity hoặc trả state về Idle nếu không có GravitySystem.</summary>
    private static void RunGravityOrIdle()
    {
        if (GravitySystem.Instance != null)
            GravitySystem.Instance.RunGravity();
        else if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
    }
}
