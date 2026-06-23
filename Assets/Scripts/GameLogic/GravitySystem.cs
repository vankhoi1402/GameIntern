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
    public static GravitySystem Instance { get; private set; }

    [Header("References")]
    [SerializeField] private BoardManager boardManager;

    /// <summary>View (BlockAnimationManager) lắng nghe để chạy DOTween; phải gọi callback khi xong.</summary>
    public static event Action<List<GravityMoveCommand>, Action> OnGravityAnimationRequested;

    /// <summary>Đăng ký singleton.</summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>Điểm vào gravity — đổi state và chạy pipeline cascade.</summary>
    public void RunGravity(Action onComplete = null)
    {
        if (BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.ApplyingGravity);
        StartCoroutine(GravityPipelineRoutine(onComplete));
    }

    /// <summary>
    /// Lặp: thu thập lệnh rơi → cập nhật lưới → đợi animation → lặp lại đến khi hết chỗ trống.
    /// </summary>
    private IEnumerator GravityPipelineRoutine(Action onComplete)
    {
        while (true)
        {
            List<GravityMoveCommand> commands = CollectGravityCommands();

            if (commands.Count == 0) break;

            foreach (var cmd in commands)
                boardManager.MoveBlock(cmd.FromRow, cmd.FromCol, cmd.ToRow, cmd.ToCol);

            bool isAnimationComplete = false;
            if (OnGravityAnimationRequested == null)
            {
                isAnimationComplete = true;
            }
            else
            {
                OnGravityAnimationRequested.Invoke(commands, () => isAnimationComplete = true);
                yield return new WaitUntil(() => isAnimationComplete);
            }
        }

        onComplete?.Invoke();

        if (onComplete == null && BoardStateManager.Instance != null)
            BoardStateManager.Instance.ChangeState(BoardState.Idle);
    }

    /// <summary>
    /// Quét từng cột từ dưới lên — đếm ô trống và tạo lệnh dịch block xuống.
    /// </summary>
    private List<GravityMoveCommand> CollectGravityCommands()
    {
        List<GravityMoveCommand> commands = new List<GravityMoveCommand>();

        for (int col = 0; col < boardManager.Columns; col++)
        {
            int emptyRowsCount = 0;

            for (int row = 0; row < boardManager.Rows; row++)
            {
                Slot currentSlot = boardManager.GetSlot(row, col);
                if (currentSlot == null) continue;

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
        }
        return commands;
    }
}
