using System;
using UnityEngine;

/// <summary>
/// Trạng thái bàn cờ — dùng để khóa input khi đang xử lý merge/gravity.
/// </summary>
public enum BoardState
{
    Idle,
    ProcessingMove,
    ResolvingMerges,
    ApplyingGravity,
    ApplyingRefill
}

/// <summary>
/// Singleton quản lý state machine bàn cờ. Chỉ cho input khi Idle.
/// </summary>
public class BoardStateManager : MonoBehaviour
{
    public static BoardStateManager Instance { get; private set; }

    [SerializeField] private BoardState currentState = BoardState.Idle;

    [Header("Debug")]
    [SerializeField] private bool logStateChanges;

    public BoardState CurrentState => currentState;

    public event Action<BoardState> OnStateChanged;

    /// <summary>Đăng ký singleton khi Awake.</summary>
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>Chuyển trạng thái bàn cờ và phát event.</summary>
    public void ChangeState(BoardState newState)
    {
        if (currentState == newState) return;
        if (logStateChanges)
            Debug.Log($"[BoardState] {currentState} → {newState}");
        currentState = newState;
        OnStateChanged?.Invoke(currentState);
    }

    /// <summary>Khôi phục Idle khi pipeline lỗi — dùng từ timeout fallback.</summary>
    public void ForceIdle(string reason = null)
    {
        if (!string.IsNullOrEmpty(reason))
            Debug.LogWarning($"[BoardState] Force Idle: {reason}");
        ChangeState(BoardState.Idle);
    }

    /// <summary>True khi người chơi được phép kéo-thả block.</summary>
    public bool CanAcceptInput() => currentState == BoardState.Idle;
}
