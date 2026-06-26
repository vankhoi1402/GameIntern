using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Lệnh di chuyển một block do gravity — dùng cho cả logic và animation.</summary>
public struct GravityMoveCommand
{
    public Block Block;
    public int FromRow, FromCol;
    public int ToRow, ToCol;
    public int DropDistance;
}

/// <summary>
/// Tính toán và áp dụng trọng lực — block rơi xuống lấp chỗ trống theo từng cột.
/// </summary>
public class GravitySystem : MonoBehaviour
{
    private const float c_AnimationTimeoutSeconds = 5f;

    public static GravitySystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BoardManager boardManager;

    private bool m_IsRunning;
    private readonly List<Action> m_PendingCallbacks = new List<Action>();

    /// <summary>View (BlockAnimationManager) lắng nghe để chạy DOTween; phải gọi callback khi xong.</summary>
    public static event Action<List<GravityMoveCommand>, Action> OnGravityAnimationRequested;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>Dự đoán row sau gravity (sau khi ô trống đã clear) — dùng cho absorb bay đúng ô đích.</summary>
    public int PredictFinalRowAfterGravity(int row, int col)
    {
        if (boardManager == null)
            return row;

        int emptyRowsCount = 0;
        for (int r = 0; r < row; r++)
        {
            Slot slot = boardManager.GetSlot(r, col);
            if (slot != null && slot.IsEmpty)
                emptyRowsCount++;
        }

        return row - emptyRowsCount;
    }

    /// <summary>Compact block trong một cột — chỉ sửa logic, không animation (dùng trước skill recovery).</summary>
    public void ApplyGravitySilentForColumn(int col)
    {
        if (boardManager == null || col < 0 || col >= boardManager.Columns)
            return;

        List<GravityMoveCommand> commands = CollectGravityCommandsForColumn(col);
        foreach (GravityMoveCommand cmd in commands)
            boardManager.MoveBlock(cmd.FromRow, cmd.FromCol, cmd.ToRow, cmd.ToCol);
    }

    /// <summary>Điểm vào gravity — xếp hàng nếu đang chạy, tránh coroutine chồng nhau.</summary>
    public void RunGravity(Action onComplete = null)
    {
        if (onComplete != null)
            m_PendingCallbacks.Add(onComplete);

        if (m_IsRunning)
            return;

        StartCoroutine(GravityPipelineRoutine());
    }

    private IEnumerator GravityPipelineRoutine()
    {
        m_IsRunning = true;

        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ApplyingGravity);

        if (boardManager == null)
        {
            Debug.LogError("[GravitySystem] Chưa gán BoardManager!");
            FinishGravityPipeline();
            yield break;
        }

        while (true)
        {
            List<GravityMoveCommand> commands = CollectGravityCommands();
            if (commands.Count == 0)
                break;

            foreach (GravityMoveCommand cmd in commands)
                boardManager.MoveBlock(cmd.FromRow, cmd.FromCol, cmd.ToRow, cmd.ToCol);

            bool isAnimationComplete = false;
            if (OnGravityAnimationRequested == null)
            {
                isAnimationComplete = true;
            }
            else
            {
                OnGravityAnimationRequested.Invoke(commands, () => isAnimationComplete = true);
                yield return WaitUntilOrTimeout(() => isAnimationComplete, c_AnimationTimeoutSeconds, "gravity animation");
            }
        }

        FinishGravityPipeline();
    }

    private void FinishGravityPipeline()
    {
        m_IsRunning = false;
        MergeVisualContext.Clear();

        Action[] callbacks = m_PendingCallbacks.ToArray();
        m_PendingCallbacks.Clear();

        bool hasCallback = callbacks.Length > 0;
        foreach (Action callback in callbacks)
            callback?.Invoke();

        if (!hasCallback && m_PendingCallbacks.Count == 0 && BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);

        if (m_PendingCallbacks.Count > 0)
            RunGravity();
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
            Debug.LogWarning($"[GravitySystem] Timeout chờ {label} ({timeoutSeconds}s) — ép tiếp pipeline.");
    }

    private List<GravityMoveCommand> CollectGravityCommands()
    {
        var commands = new List<GravityMoveCommand>();

        for (int col = 0; col < boardManager.Columns; col++)
            commands.AddRange(CollectGravityCommandsForColumn(col));

        return commands;
    }

    private List<GravityMoveCommand> CollectGravityCommandsForColumn(int col)
    {
        var commands = new List<GravityMoveCommand>();
        int emptyRowsCount = 0;

        for (int row = 0; row < boardManager.Rows; row++)
        {
            Slot currentSlot = boardManager.GetSlot(row, col);
            if (currentSlot == null)
                continue;

            if (currentSlot.IsEmpty)
            {
                emptyRowsCount++;
            }
            else if (emptyRowsCount > 0)
            {
                commands.Add(new GravityMoveCommand
                {
                    Block = currentSlot.CurrentBlock,
                    FromRow = row,
                    FromCol = col,
                    ToRow = row - emptyRowsCount,
                    ToCol = col,
                    DropDistance = emptyRowsCount
                });
            }
        }

        return commands;
    }
}
