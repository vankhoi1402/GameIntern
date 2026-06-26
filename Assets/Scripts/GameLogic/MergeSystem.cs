using System;
using System.Collections.Generic;
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
    [SerializeField] private Tier2TripleClearVfxHandler tier2TripleClearVfx;

    /// <summary>Phát sau khi source đã cộng stack vào target (View xử lý animation nhập).</summary>
    public event Action<Block, Block> OnBlockStacked;

    private Block m_PendingTier2TripleClearTarget;
    private bool m_PendingGravityAfterSettlement;

    /// <summary>Target 2+2+2 chờ wind-up trước khi clear — BlockMergeViewHandler gọi Commit.</summary>
    public bool IsPendingTier2TripleClear(Block target)
        => m_PendingTier2TripleClearTarget != null && m_PendingTier2TripleClearTarget == target;

    /// <summary>Sau wind-up: clear ô + chạy VFX 2+2+2.</summary>
    public void CommitPendingTier2TripleClear()
    {
        if (m_PendingTier2TripleClearTarget == null)
            return;

        Block target = m_PendingTier2TripleClearTarget;
        m_PendingTier2TripleClearTarget = null;

        if (target == null)
        {
            RequestFullSettlement();
            return;
        }

        TryClearTargetBlock(target, isTier2TripleClear: true);
    }

    private void Awake()
    {
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (settlementService == null)
            settlementService = FindObjectOfType<BoardSettlementService>();
        if (tier2TripleClearVfx == null)
            tier2TripleClearVfx = FindObjectOfType<Tier2TripleClearVfxHandler>();
        if (tier2TripleClearVfx == null && spawnSystem != null)
            tier2TripleClearVfx = spawnSystem.gameObject.AddComponent<Tier2TripleClearVfxHandler>();
    }

    private void OnDestroy()
    {
        if (settlementService != null)
            settlementService.OnSettlementCompleted -= HandlePendingGravityAfterSettlement;
    }

    /// <summary>Điểm vào merge — gọi khi kéo block cùng loại vào nhau. Trả false nếu merge không thực hiện được.</summary>
    public bool PreceptMerge(Block sourceBlock, Block targetBlock)
    {
        if (sourceBlock == null || targetBlock == null) return false;
        if (targetBlock.IsPendingDestroy || sourceBlock.IsPendingDestroy) return false;
        if (!sourceBlock.CanMergeWith(targetBlock)) return false;

        ProcessMerge(sourceBlock, targetBlock);
        return true;
    }

    /// <summary>
    /// Nhánh 1+1 / 1+2: cộng stack; nhánh 2+2: gộp Tier2MergeStage hai block lên ô đích.
    /// Nổ khi stack >= 3 (cũ) hoặc Tier2MergeStage >= 3 (2+2+2).
    /// </summary>
    private void ProcessMerge(Block sourceBlock, Block targetBlock)
    {
        sourceBlock.IsPendingDestroy = true;

        bool tier2PairMerge = targetBlock.IsTier2PairMergeWith(sourceBlock);

        if (tier2PairMerge)
        {
            int combinedStage = targetBlock.Tier2MergeStage + sourceBlock.Tier2MergeStage;
            targetBlock.SetTier2MergeStage(combinedStage);
        }
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

        bool shouldClear = targetBlock.StackCount >= 3
            || (tier2PairMerge && targetBlock.Tier2MergeStage >= 3);
        bool isTier2TripleClear = tier2PairMerge && targetBlock.Tier2MergeStage >= 3;

        if (isTier2TripleClear)
        {
            m_PendingTier2TripleClearTarget = targetBlock;
            if (BoardStateManager.Instance != null)
                BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);
        }

        MergeVisualContext.Begin(sourceBlock, targetBlock);
        OnBlockStacked?.Invoke(sourceBlock, targetBlock);

        if (shouldClear)
        {
            if (!isTier2TripleClear && BoardStateManager.Instance != null)
                BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);

            if (isTier2TripleClear)
                return;

            TryClearTargetBlock(targetBlock, isTier2TripleClear: false);
            return;
        }

        RequestGravityOnly();
    }

    private void TryClearTargetBlock(Block targetBlock, bool isTier2TripleClear)
    {
        Slot targetSlot = targetBlock.CurrentSlot;
        if (targetSlot == null)
        {
            if (targetBlock != null && isTier2TripleClear)
                Destroy(targetBlock.gameObject);

            if (isTier2TripleClear)
                RequestFullSettlement();
            else
                RequestGravityOnly();
            return;
        }

        targetBlock.IsPendingDestroy = true;
        int row = targetSlot.Row;
        int col = targetSlot.Col;
        BlockData clearedData = targetBlock.Data;

        boardManager.ClearSlot(row, col);
        Destroy(targetBlock.gameObject);

        void AfterTier2Explode(IReadOnlyList<Tier2NeighborRestore> neighborRestores)
        {
            RequestFullSettlement(neighborRestores);
        }

        void AfterNormalExplode()
        {
            RequestFullSettlement();
        }

        if (isTier2TripleClear && tier2TripleClearVfx != null)
            tier2TripleClearVfx.Play(row, col, clearedData, AfterTier2Explode);
        else if (spawnSystem != null && clearedData != null)
            spawnSystem.SpawnBurstFallOff(row, col, clearedData, AfterNormalExplode);
        else
            AfterNormalExplode();
    }

    private void RequestFullSettlement(IReadOnlyList<Tier2NeighborRestore> neighborRestores = null)
    {
        MergeVisualContext.Clear();

        if (settlementService != null)
        {
            if (neighborRestores != null && neighborRestores.Count > 0)
                settlementService.RunSettlement(neighborRestores);
            else
                settlementService.RunSettlement();
        }
        else
        {
            RequestGravityOnly();
        }
    }

    private void RequestGravityOnly()
    {
        if (settlementService != null)
        {
            if (settlementService.IsRunning)
            {
                if (!m_PendingGravityAfterSettlement)
                {
                    m_PendingGravityAfterSettlement = true;
                    settlementService.OnSettlementCompleted += HandlePendingGravityAfterSettlement;
                }

                return;
            }

            settlementService.RunGravityOnly();
            return;
        }

        MergeVisualContext.Clear();

        if (GravitySystem.Instance != null)
            GravitySystem.Instance.RunGravity();
        else if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
    }

    private void HandlePendingGravityAfterSettlement(bool _)
    {
        if (settlementService != null)
            settlementService.OnSettlementCompleted -= HandlePendingGravityAfterSettlement;

        m_PendingGravityAfterSettlement = false;
        RequestGravityOnly();
    }
}
