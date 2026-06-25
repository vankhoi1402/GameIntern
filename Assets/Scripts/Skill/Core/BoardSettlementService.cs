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
    [SerializeField] private float riseDuration = 0.15f;

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
        RunSettlement(includeRefill: true, onComplete);
    }

    /// <summary>Chỉ gravity — merge nhỏ (stack &lt; 3).</summary>
    public void RunGravityOnly(Action onComplete = null)
    {
        RunSettlement(includeRefill: false, onComplete);
    }

    public void RunSettlement(bool includeRefill, Action onComplete = null)
    {
        if (m_IsRunning)
        {
            Debug.LogWarning("[BoardSettlement] Pipeline đang chạy — bỏ qua lệnh trùng.");
            onComplete?.Invoke();
            return;
        }

        StartCoroutine(SettlementRoutine(includeRefill, onComplete));
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

        yield return SettlementRoutine(includeRefill: true, onComplete);
    }

    private IEnumerator SettlementRoutine(bool includeRefill, Action onComplete)
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

            if (includeRefill)
            {
                if (BoardStateManager.Instance != null)
                    BoardStateManager.Instance.ChangeState(BoardState.ApplyingRefill);

                while (AnyColumnNeedsRefill())
                {
                    List<ColumnRefillPacket> wave = CollectRefillWave();
                    if (wave.Count == 0)
                        break;

                    bool animDone = false;
                    BoardPresentationEvents.RequestRefillWaveAnimation(wave, () => animDone = true);
                    PlayRiseAnimations(wave);

                    yield return WaitUntilOrTimeout(() => animDone, c_PipelineTimeoutSeconds, "refill wave");
                }
            }
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
}
