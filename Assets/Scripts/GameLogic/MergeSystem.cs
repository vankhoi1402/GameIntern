using System;
using UnityEngine;

/// <summary>
/// Xử lý logic merge: cộng stack, xóa block đạt max, kích hoạt gravity.
/// </summary>
public class MergeSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private BlockRemoveViewHandler removeViewHandler;
    [SerializeField] private SpawnSystem spawnSystem;

    /// <summary>Phát sau khi source đã cộng stack vào target (View xử lý animation nhập).</summary>
    public event Action<Block, Block> OnBlockStacked;

    /// <summary>Phát khi target đạt stack >= 3 (View xử lý rơi ra ngoài màn hình).</summary>
    public event Action<Block, int, int, Action> OnBlockMaxedOut;

    private void Awake()
    {
        if (removeViewHandler == null)
            removeViewHandler = GetComponent<BlockRemoveViewHandler>();
        if (spawnSystem == null)
            spawnSystem = GetComponent<SpawnSystem>();
    }

    /// <summary>Điểm vào merge — gọi khi kéo block cùng loại vào nhau. Trả false nếu merge không thực hiện được.</summary>
    public bool PreceptMerge(Block sourceBlock, Block targetBlock)
    {
        if (sourceBlock == null || targetBlock == null) return false;
        if (targetBlock.IsPendingDestroy || sourceBlock.IsPendingDestroy) return false;

        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ResolvingMerges);
        ProcessMerge(sourceBlock, targetBlock);
        return true;
    }

    /// <summary>
    /// Cộng stack source vào target, clear ô source, đổi nền/animation,
    /// nếu stack >= 3 thì clear target, sinh 3 block cùng loại rơi khỏi màn hình rồi gravity.
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

                if (spawnSystem != null)
                {
                    Destroy(targetBlock.gameObject);
                    spawnSystem.SpawnBurstFallOff(row, col, clearedData, RunGravityOrIdle);
                    return;
                }

                if (removeViewHandler != null)
                    removeViewHandler.PlayFallOff(targetBlock, RunGravityOrIdle);
                else if (OnBlockMaxedOut != null)
                    OnBlockMaxedOut.Invoke(targetBlock, row, col, RunGravityOrIdle);
                else
                {
                    Destroy(targetBlock.gameObject);
                    RunGravityOrIdle();
                }
                return;
            }
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
