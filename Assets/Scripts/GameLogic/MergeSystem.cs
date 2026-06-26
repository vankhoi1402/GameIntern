using System;
using UnityEngine;

/// <summary>
/// Xử lý logic merge: cộng stack, xóa block đạt max, kích hoạt settlement.
/// </summary>
public class MergeSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private BoardSettlementService settlementService;

    /// <summary>Phát sau khi source đã cộng stack vào target (View xử lý animation nhập).</summary>
    public event Action<Block, Block> OnBlockStacked;

    private void Awake()
    {
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (settlementService == null)
            settlementService = FindObjectOfType<BoardSettlementService>();
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
    /// Nhánh 1+1 / 1+2: cộng stack; nhánh 2+2: tăng Tier2MergeStage (không cộng stack).
    /// Nổ khi stack >= 3 (cũ) hoặc Tier2MergeStage >= 3 (2+2+2).
    /// </summary>
    private void ProcessMerge(Block sourceBlock, Block targetBlock)
    {
        sourceBlock.IsPendingDestroy = true;

        bool tier2PairMerge = targetBlock.IsTier2PairMergeWith(sourceBlock);

        if (tier2PairMerge)
            targetBlock.IncrementTier2MergeStage();
        else
        {
            int sourceWeight = sourceBlock.StackCount;
            int targetWeightBefore = targetBlock.StackCount;
            targetBlock.AddStack(sourceWeight);

            if (targetBlock.StackCount == 2 && sourceWeight == 1 && targetWeightBefore == 1)
                targetBlock.SetTier2MergeStage(1);
        }

        Slot sourceSlot = sourceBlock.CurrentSlot;
        if (sourceSlot != null)
            boardManager.ClearSlot(sourceSlot.Row, sourceSlot.Col);

        OnBlockStacked?.Invoke(sourceBlock, targetBlock);

        bool shouldClear = targetBlock.StackCount >= 3
            || (tier2PairMerge && targetBlock.Tier2MergeStage >= 3);

        if (shouldClear)
        {
            TryClearTargetBlock(targetBlock);
            return;
        }

        RequestGravityOnly();
    }

    private void TryClearTargetBlock(Block targetBlock)
    {
        Slot targetSlot = targetBlock.CurrentSlot;
        if (targetSlot == null)
        {
            RequestGravityOnly();
            return;
        }

        targetBlock.IsPendingDestroy = true;
        int row = targetSlot.Row;
        int col = targetSlot.Col;
        BlockData clearedData = targetBlock.Data;

        boardManager.ClearSlot(row, col);
        Destroy(targetBlock.gameObject);

        void AfterExplode()
        {
            RequestFullSettlement();
        }

        if (spawnSystem != null && clearedData != null)
            spawnSystem.SpawnBurstFallOff(row, col, clearedData, AfterExplode);
        else
            AfterExplode();
    }

    private void RequestFullSettlement()
    {
        if (settlementService != null)
            settlementService.RunSettlement();
        else
            RequestGravityOnly();
    }

    private void RequestGravityOnly()
    {
        if (settlementService != null)
            settlementService.RunGravityOnly();
        else if (GravitySystem.Instance != null)
            GravitySystem.Instance.RunGravity();
        else if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
    }
}
