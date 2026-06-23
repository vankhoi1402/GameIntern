using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Sau T3: gravity → refill mọi cột thiếu hàng cho đến khi full hoặc hết bin.</summary>
public class DesignRefillController : MonoBehaviour
{
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private ColumnPushSystem columnPushSystem;
    [SerializeField] private LevelManager levelManager;

    [SerializeField] private float riseDuration = 1f;

    private void Awake()
    {
        if (boardManager == null)
            boardManager = FindObjectOfType<BoardManager>();
        if (spawnSystem == null)
            spawnSystem = FindObjectOfType<SpawnSystem>();
        if (columnPushSystem == null)
            columnPushSystem = FindObjectOfType<ColumnPushSystem>();
        if (levelManager == null)
            levelManager = FindObjectOfType<LevelManager>();
    }

    public void OnT3Cleared(Action onComplete)
    {
        StartCoroutine(T3RefillRoutine(onComplete));
    }

    private IEnumerator T3RefillRoutine(Action onComplete)
    {
        bool gravityDone = false;
        if (GravitySystem.Instance != null)
            GravitySystem.Instance.RunGravity(() => gravityDone = true);
        else
            gravityDone = true;

        yield return new WaitUntil(() => gravityDone);

        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ApplyingRefill);

        while (AnyColumnNeedsRefill())
        {
            List<ColumnRefillPacket> wave = CollectRefillWave();
            if (wave.Count == 0)
                break;

            bool animDone = false;
            columnPushSystem.PlayRefillWaveAnimation(wave, () => animDone = true);
            PlayRiseAnimations(wave);

            yield return new WaitUntil(() => animDone);
        }

        FinishPipeline(onComplete);
    }

    private bool AnyColumnNeedsRefill()
    {
        if (columnPushSystem == null || boardManager == null)
            return false;

        for (int col = 0; col < boardManager.Columns; col++)
        {
            if (columnPushSystem.ColumnNeedsRefill(col))
                return true;
        }

        return false;
    }

    private List<ColumnRefillPacket> CollectRefillWave()
    {
        var wave = new List<ColumnRefillPacket>();
        if (levelManager == null || spawnSystem == null || columnPushSystem == null || boardManager == null)
            return wave;

        for (int col = 0; col < boardManager.Columns; col++)
        {
            if (!columnPushSystem.ColumnNeedsRefill(col))
                continue;

            LevelBinBlock? binBlock = levelManager.TryDequeueBin(col);
            if (!binBlock.HasValue)
                continue;

            if (!columnPushSystem.TryShiftColumnUp(col, out List<ColumnPushCommand> commands))
                continue;

            Block newBlock = spawnSystem.SpawnBlockAtWithoutPlace(binBlock.Value.BlockType, binBlock.Value.Stack);
            if (newBlock == null)
                continue;

            boardManager.PlaceBlock(newBlock, 0, col);
            wave.Add(new ColumnRefillPacket
            {
                Col = col,
                PushCommands = commands,
                NewBlock = newBlock
            });
        }

        return wave;
    }

    private void PlayRiseAnimations(IReadOnlyList<ColumnRefillPacket> wave)
    {
        if (boardManager?.Layout == null)
            return;

        float cellHeight = boardManager.Layout.CellHeight;
        foreach (ColumnRefillPacket packet in wave)
        {
            if (packet.NewBlock == null)
                continue;

            if (!packet.NewBlock.TryGetComponent<BlockView>(out var riseView))
                continue;

            Vector3 target = boardManager.Layout.GetWorldPosition(0, packet.Col);
            riseView.PlayRiseFromBelow(target, cellHeight, riseDuration, null);
        }
    }

    private static void FinishPipeline(Action onComplete)
    {
        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
        onComplete?.Invoke();
    }
}
