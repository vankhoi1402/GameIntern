using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Gravity + refill bin — cửa vào duy nhất sau T3 / skill.</summary>
public class BoardSettlementService : MonoBehaviour
{
    private const float c_PipelineTimeoutSeconds = 8f;

    [SerializeField] private BoardManager boardManager;
    [SerializeField] private SpawnSystem spawnSystem;
    [SerializeField] private ColumnPushSystem columnPushSystem;
    [SerializeField] private LevelManager levelManager;

    [Header("Refill Rise")]
    [SerializeField] private float riseDuration = 0.16f;

    private bool m_IsRunning;

    public bool IsRunning => m_IsRunning;

    /// <summary>Phát khi pipeline kết thúc (Idle) — hook win/lose sau này.</summary>
    public event Action<bool> OnSettlementCompleted;

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

    /// <summary>Gravity + refill bin (T3, skill).</summary>
    public void RunSettlement(Action onComplete = null)
    {
        RunSettlement(includeRefill: true, neighborRestores: null, onComplete);
    }

    /// <summary>2+2+2 — gravity rồi refill (neighbor restore + bin).</summary>
    public void RunSettlement(IReadOnlyList<Tier2NeighborRestore> neighborRestores, Action onComplete = null)
    {
        RunSettlement(includeRefill: true, neighborRestores, onComplete);
    }

    /// <summary>Chỉ gravity — merge nhỏ (stack &lt; 3).</summary>
    public void RunGravityOnly(Action onComplete = null)
    {
        RunSettlement(includeRefill: false, neighborRestores: null, onComplete);
    }

    public void RunSettlement(bool includeRefill, Action onComplete = null)
    {
        RunSettlement(includeRefill, neighborRestores: null, onComplete);
    }

    public void RunSettlement(
        bool includeRefill,
        IReadOnlyList<Tier2NeighborRestore> neighborRestores,
        Action onComplete = null)
    {
        if (m_IsRunning)
        {
            Debug.LogWarning("[BoardSettlement] Pipeline đang chạy — bỏ qua lệnh trùng.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(SettlementRoutine(includeRefill, neighborRestores, onComplete));
    }

    /// <summary>Alias cũ — skill / code legacy.</summary>
    public void RunGravityThenRefill(Action onComplete = null) => RunSettlement(onComplete);

    public IEnumerator RunGravityThenRefillRoutine(Action onComplete = null)
    {
        if (m_IsRunning)
        {
            Debug.LogWarning("[BoardSettlement] Pipeline đang chạy — bỏ qua lệnh trùng.");
            onComplete?.Invoke();
            yield break;
        }

        yield return SettlementRoutine(includeRefill: true, neighborRestores: null, onComplete);
    }

    private IEnumerator SettlementRoutine(
        bool includeRefill,
        IReadOnlyList<Tier2NeighborRestore> neighborRestores,
        Action onComplete)
    {
        m_IsRunning = true;

        try
        {
            bool gravityDone = false;
            if (GravitySystem.Instance != null)
                GravitySystem.Instance.RunGravity(() => gravityDone = true);
            else
                gravityDone = true;

            yield return WaitUntilOrTimeout(() => gravityDone, c_PipelineTimeoutSeconds, "gravity");

            Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues = BuildNeighborQueues(neighborRestores);
            bool hasNeighborRefill = HasPendingNeighborRestore(neighborQueues);

            if (includeRefill || hasNeighborRefill)
                yield return RefillPhaseRoutine(includeRefill, neighborQueues);
        }
        finally
        {
            m_IsRunning = false;
            if (BoardStateManager.Instance != null)
                BoardStateManager.Instance.ChangeState(BoardState.Idle);

            OnSettlementCompleted?.Invoke(includeRefill);
            onComplete?.Invoke();
        }
    }

    /// <summary>Refill bin + neighbor restore — cùng pipeline push + rise từ row 0.</summary>
    private IEnumerator RefillPhaseRoutine(
        bool includeBin,
        Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ApplyingRefill);

        while (true)
        {
            List<ColumnRefillPacket> wave = CollectFillWave(includeBin, neighborQueues);
            if (wave.Count == 0)
                break;

            var batch = new AnimationCompletionBatch();
            batch.AddOutstanding(1);
            BoardPresentationEvents.RequestRefillWaveAnimation(wave, batch.NotifyOneDone);
            PlayRefillRiseAnimations(wave, batch);

            yield return batch.WaitRoutine(c_PipelineTimeoutSeconds, "refill wave");
        }
    }

    private static Dictionary<int, Queue<Tier2NeighborRestore>> BuildNeighborQueues(
        IReadOnlyList<Tier2NeighborRestore> neighborRestores)
    {
        var queues = new Dictionary<int, Queue<Tier2NeighborRestore>>();
        if (neighborRestores == null)
            return queues;

        foreach (Tier2NeighborRestore restore in neighborRestores)
        {
            if (string.IsNullOrEmpty(restore.TypeKey))
                continue;

            if (!queues.TryGetValue(restore.Col, out Queue<Tier2NeighborRestore> queue))
            {
                queue = new Queue<Tier2NeighborRestore>();
                queues[restore.Col] = queue;
            }

            queue.Enqueue(restore);
        }

        return queues;
    }

    private static bool HasPendingNeighborRestore(Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        foreach (Queue<Tier2NeighborRestore> queue in neighborQueues.Values)
        {
            if (queue.Count > 0)
                return true;
        }

        return false;
    }

    private List<ColumnRefillPacket> CollectFillWave(
        bool includeBin,
        Dictionary<int, Queue<Tier2NeighborRestore>> neighborQueues)
    {
        var wave = new List<ColumnRefillPacket>();
        if (boardManager == null || columnPushSystem == null || spawnSystem == null)
            return wave;

        for (int col = 0; col < boardManager.Columns; col++)
        {
            if (!columnPushSystem.ColumnNeedsRefill(col))
                continue;

            bool hasNeighbor = neighborQueues.TryGetValue(col, out Queue<Tier2NeighborRestore> queue)
                && queue.Count > 0;

            Tier2NeighborRestore? restore = null;
            if (hasNeighbor)
                restore = queue.Dequeue();

            LevelBinBlock? binBlock = null;
            if (!restore.HasValue && includeBin && levelManager != null)
                binBlock = levelManager.TryDequeueBin(col);

            if (!restore.HasValue && !binBlock.HasValue)
                continue;

            if (!columnPushSystem.TryShiftColumnUp(col, out List<ColumnPushCommand> commands))
            {
                if (restore.HasValue)
                    queue.Enqueue(restore.Value);
                continue;
            }

            Block newBlock = restore.HasValue
                ? SpawnRestoredNeighbor(restore.Value)
                : spawnSystem.SpawnBlockAtWithoutPlace(binBlock.Value.BlockType, binBlock.Value.Stack);

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

    private Block SpawnRestoredNeighbor(Tier2NeighborRestore restore)
    {
        Block block = spawnSystem.SpawnBlockAtWithoutPlace(restore.TypeKey, restore.Stack);
        if (block == null)
            return null;

        if (restore.Stack == 2 && restore.Tier2MergeStage > 0)
        {
            block.SetTier2MergeStage(restore.Tier2MergeStage);
            if (block.TryGetComponent<BlockView>(out BlockView view))
                view.UpdateTier2StageVisual(block.StackCount, block.Tier2MergeStage);
        }

        return block;
    }

    private void PlayRefillRiseAnimations(IReadOnlyList<ColumnRefillPacket> wave, AnimationCompletionBatch batch)
    {
        if (boardManager?.Layout == null)
            return;

        float cellHeight = boardManager.Layout.CellHeight;
        foreach (ColumnRefillPacket packet in wave)
        {
            if (packet.NewBlock == null)
                continue;

            if (!packet.NewBlock.TryGetComponent<BlockView>(out BlockView riseView))
                continue;

            batch.AddOutstanding(1);
            Vector3 target = boardManager.Layout.GetWorldPosition(0, packet.Col);
            riseView.PlayRiseFromBelow(target, cellHeight, riseDuration, 0, 0f, batch.NotifyOneDone);
        }
    }

    private static IEnumerator WaitUntilOrTimeout(Func<bool> condition, float timeoutSeconds, string label)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeoutSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!condition())
            Debug.LogWarning($"[BoardSettlement] Timeout chờ {label} ({timeoutSeconds}s).");
    }

    private sealed class AnimationCompletionBatch
    {
        private int m_Remaining;

        public void AddOutstanding(int count) => m_Remaining += count;

        public void NotifyOneDone() => m_Remaining--;

        public IEnumerator WaitRoutine(float timeoutSeconds, string label)
        {
            if (m_Remaining <= 0)
                yield break;

            float elapsed = 0f;
            while (m_Remaining > 0 && elapsed < timeoutSeconds)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (m_Remaining > 0)
                Debug.LogWarning($"[BoardSettlement] Timeout chờ {label} ({timeoutSeconds}s) — còn {m_Remaining} anim.");
        }
    }
}
