using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Apply kết quả merge lên board + kích hoạt settlement/VFX. Luật nằm ở MergeRules.
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

        MergeResult result = MergeRules.Resolve(
            MergeBlockState.From(sourceBlock),
            MergeBlockState.From(targetBlock));

        if (!result.IsValid) return false;

        ApplyMergeResult(sourceBlock, targetBlock, result);
        return true;
    }

    private void ApplyMergeResult(Block sourceBlock, Block targetBlock, MergeResult result)
    {
        sourceBlock.IsPendingDestroy = true;
        targetBlock.ApplyMergeState(result.TargetStackCount, result.TargetTier2Stage);

        Slot sourceSlot = sourceBlock.CurrentSlot;
        if (sourceSlot != null)
            boardManager.ClearSlot(sourceSlot.Row, sourceSlot.Col);

        if (result.NeedsWindUp)
        {
            m_PendingTier2TripleClearTarget = targetBlock;
            if (BoardStateManager.Instance != null)
                BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);
        }

        MergeVisualContext.Begin(sourceBlock, targetBlock);
        OnBlockStacked?.Invoke(sourceBlock, targetBlock);

        if (!result.ShouldClear)
        {
            RequestGravityOnly();
            return;
        }

        if (!result.NeedsWindUp && BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);

        if (result.NeedsWindUp)
            return;

        TryClearTargetBlock(targetBlock, isTier2TripleClear: false);
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
